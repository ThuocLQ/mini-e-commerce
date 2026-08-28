using System.Data;
using InventoryService.Application.Abstractions;
using InventoryService.Domain.Outbox;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Contracts.Events.Inventory;
using Dapper;
using System.Text.Json;

namespace InventoryService.Infrastructure.Persistence;

public sealed class DapperInventoryReservationRepository : IInventoryReservationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IInventoryOutboxRepository _outboxRepository;

    public DapperInventoryReservationRepository(IDbConnectionFactory connectionFactory, IInventoryOutboxRepository outboxRepository)
    {
        _connectionFactory = connectionFactory;
        _outboxRepository = outboxRepository;
    }

    public async Task<InventoryReservationResult> ReserveAsync(Guid orderId, IReadOnlyList<InventoryReservationItem> items, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var existingStatus = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Status FROM InventoryReservations WHERE OrderId = @OrderId FOR UPDATE", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        if (existingStatus is not null)
        {
            transaction.Commit();
            return existingStatus == "Reserved"
                ? new InventoryReservationResult(true)
                : new InventoryReservationResult(false, "Inventory reservation is no longer active.");
        }

        var productIds = items.Select(item => item.ProductId).ToArray();
        var products = (await connection.QueryAsync<StockRow>(new CommandDefinition("""
            SELECT ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc
            FROM InventoryItems
            WHERE ProductId = ANY(@ProductIds)
            ORDER BY ProductId
            FOR UPDATE
            """, new { ProductIds = productIds }, transaction, cancellationToken: cancellationToken))).ToDictionary(row => row.ProductId, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                transaction.Rollback();
                return new InventoryReservationResult(false, $"Product '{item.ProductId}' was not found.");
            }

            if (product.StockQuantity - product.ReservedQuantity < item.Quantity)
            {
                transaction.Rollback();
                return new InventoryReservationResult(false, $"Insufficient inventory for product '{item.ProductId}'.");
            }
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO InventoryReservations (OrderId, Status, ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@OrderId, 'Reserved', @ExpiresAtUtc, @Now, @Now)
            """, new { OrderId = orderId, ExpiresAtUtc = expiresAtUtc, Now = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));

        foreach (var item in items)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO InventoryReservationItems (OrderId, ProductId, Quantity)
                VALUES (@OrderId, @ProductId, @Quantity);
                UPDATE InventoryItems SET ReservedQuantity = ReservedQuantity + @Quantity, UpdatedAtUtc = CURRENT_TIMESTAMP WHERE ProductId = @ProductId;
                """, new { OrderId = orderId, item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));
        }

        await AddAvailabilityChangedEventsAsync(
            connection,
            transaction,
            items.Select(item => item.ProductId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            causationId: null,
            cancellationToken);

        transaction.Commit();
        return new InventoryReservationResult(true);
    }

    public Task ReleaseAsync(Guid orderId, Guid? messageId = null, CancellationToken cancellationToken = default) =>
        ChangeReservationAsync(orderId, "Released", false, messageId, cancellationToken);

    public Task CommitAsync(Guid orderId, Guid? messageId = null, CancellationToken cancellationToken = default) =>
        ChangeReservationAsync(orderId, "Committed", true, messageId, cancellationToken);

    public async Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var orderIds = (await connection.QueryAsync<Guid>(new CommandDefinition("""
            SELECT OrderId FROM InventoryReservations
            WHERE Status = 'Reserved' AND ExpiresAtUtc <= @Now
            ORDER BY ExpiresAtUtc
            LIMIT 100
            """, new { Now = DateTime.UtcNow }, cancellationToken: cancellationToken))).ToList();

        foreach (var orderId in orderIds)
        {
            await ReleaseAsync(orderId, cancellationToken: cancellationToken);
        }

        return orderIds.Count;
    }

    private async Task ChangeReservationAsync(
        Guid orderId,
        string targetStatus,
        bool deductStock,
        Guid? messageId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var reservation = await connection.QuerySingleOrDefaultAsync<ReservationState>(new CommandDefinition(
            "SELECT Status, ExpiresAtUtc FROM InventoryReservations WHERE OrderId = @OrderId FOR UPDATE", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        if (reservation is null)
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Inventory reservation for order {orderId:D} does not exist yet.");
        }

        if (messageId is not null)
        {
            var alreadyProcessed = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT EventId FROM InventoryCommandReceipts WHERE EventId = @EventId;",
                new { EventId = messageId }, transaction, cancellationToken: cancellationToken));
            if (alreadyProcessed is not null)
            {
                transaction.Commit();
                return;
            }
        }

        if (reservation.Status != "Reserved")
        {
            if ((targetStatus == "Released" && reservation.Status == "Released") ||
                (targetStatus == "Committed" && reservation.Status == "Released"))
            {
                if (messageId is not null)
                {
                    // A release outcome has already been durably published. A late commit command
                    // must be acknowledged to stop redelivery, but it must not mutate stock or emit
                    // a competing committed outcome.
                    await connection.ExecuteAsync(new CommandDefinition("""
                        INSERT INTO InventoryCommandReceipts (EventId, CommandType, ReceivedAtUtc)
                        VALUES (@EventId, @CommandType, @ReceivedAtUtc)
                        ON CONFLICT (EventId) DO NOTHING;
                        """, new { EventId = messageId, CommandType = targetStatus, ReceivedAtUtc = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));
                }

                transaction.Commit();
                return;
            }

            transaction.Rollback();
            throw new InvalidOperationException($"Inventory reservation for order {orderId:D} is already {reservation.Status}.");
        }

        var reservationExpired = deductStock && reservation.ExpiresAtUtc <= DateTime.UtcNow;
        if (reservationExpired)
        {
            // An expired hold must never be committed just because the expiry worker has not run yet.
            targetStatus = "Released";
            deductStock = false;
        }

        if (messageId is not null)
        {
            var receiptEventId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
                INSERT INTO InventoryCommandReceipts (EventId, CommandType, ReceivedAtUtc)
                VALUES (@EventId, @CommandType, @ReceivedAtUtc)
                ON CONFLICT (EventId) DO NOTHING
                RETURNING EventId;
                """, new
            {
                EventId = messageId,
                CommandType = targetStatus,
                ReceivedAtUtc = DateTime.UtcNow
            }, transaction, cancellationToken: cancellationToken));

            if (receiptEventId is null)
            {
                transaction.Commit();
                return;
            }
        }

        var items = (await connection.QueryAsync<InventoryReservationItem>(new CommandDefinition(
            "SELECT ProductId, Quantity FROM InventoryReservationItems WHERE OrderId = @OrderId", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken))).ToList();
        foreach (var item in items)
        {
            await connection.ExecuteAsync(new CommandDefinition(deductStock
                ? "UPDATE InventoryItems SET StockQuantity = StockQuantity - @Quantity, ReservedQuantity = ReservedQuantity - @Quantity, UpdatedAtUtc = CURRENT_TIMESTAMP WHERE ProductId = @ProductId"
                : "UPDATE InventoryItems SET ReservedQuantity = ReservedQuantity - @Quantity, UpdatedAtUtc = CURRENT_TIMESTAMP WHERE ProductId = @ProductId",
                new { item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE InventoryReservations SET Status = @TargetStatus, UpdatedAtUtc = @Now WHERE OrderId = @OrderId",
            new { OrderId = orderId, TargetStatus = targetStatus, Now = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));

        await AddAvailabilityChangedEventsAsync(
            connection,
            transaction,
            items.Select(item => item.ProductId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            messageId,
            cancellationToken);

        IntegrationEvent outcome = targetStatus == "Committed"
            ? new InventoryCommittedIntegrationEvent { OrderId = orderId, CorrelationId = CorrelationContext.CorrelationId, CausationId = messageId?.ToString("D") }
            : new InventoryReleasedIntegrationEvent
            {
                OrderId = orderId,
                Reason = reservationExpired || messageId is null ? "ReservationExpired" : "PaymentFlow",
                CorrelationId = CorrelationContext.CorrelationId,
                CausationId = messageId?.ToString("D")
            };

        await _outboxRepository.AddAsync(new InventoryOutboxMessage
        {
            Id = outcome.EventId,
            OccurredAtUtc = outcome.OccurredAtUtc,
            Type = outcome.GetType().FullName!,
            // Serialize the concrete event. Serializing through IntegrationEvent would discard
            // event-specific fields such as OrderId before the outbox publisher sees them.
            Content = JsonSerializer.Serialize(outcome, outcome.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CorrelationId = outcome.CorrelationId,
            CausationId = outcome.CausationId,
            NextAttemptAtUtc = outcome.OccurredAtUtc
        }, transaction, cancellationToken);
        transaction.Commit();
    }

    private async Task AddAvailabilityChangedEventsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        IReadOnlyCollection<string> productIds,
        Guid? causationId,
        CancellationToken cancellationToken)
    {
        var items = await connection.QueryAsync<StockRow>(new CommandDefinition("""
            SELECT ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc
            FROM InventoryItems
            WHERE ProductId = ANY(@ProductIds)
            ORDER BY ProductId;
            """, new { ProductIds = productIds.ToArray() }, transaction, cancellationToken: cancellationToken));

        foreach (var item in items)
        {
            var availabilityEvent = new InventoryAvailabilityChangedIntegrationEvent
            {
                ProductId = item.ProductId,
                StockQuantity = item.StockQuantity,
                ReservedQuantity = item.ReservedQuantity,
                AvailableQuantity = item.StockQuantity - item.ReservedQuantity,
                InventoryUpdatedAtUtc = item.UpdatedAtUtc,
                CorrelationId = CorrelationContext.CorrelationId,
                CausationId = causationId?.ToString("D")
            };

            await _outboxRepository.AddAsync(new InventoryOutboxMessage
            {
                Id = availabilityEvent.EventId,
                OccurredAtUtc = availabilityEvent.OccurredAtUtc,
                Type = availabilityEvent.GetType().FullName!,
                Content = JsonSerializer.Serialize(availabilityEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                CorrelationId = availabilityEvent.CorrelationId,
                CausationId = availabilityEvent.CausationId,
                NextAttemptAtUtc = availabilityEvent.OccurredAtUtc
            }, transaction, cancellationToken);
        }
    }

    private sealed record StockRow(string ProductId, int StockQuantity, int ReservedQuantity, DateTime UpdatedAtUtc);
    private sealed record ReservationState(string Status, DateTime ExpiresAtUtc);
}

