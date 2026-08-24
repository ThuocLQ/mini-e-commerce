using System.Data;
using CatalogService.Application.Abstractions;
using CatalogService.Domain.Outbox;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Contracts.Events.Inventory;
using Dapper;
using System.Text.Json;

namespace CatalogService.Infrastructure.Persistence;

public sealed class DapperInventoryReservationRepository : IInventoryReservationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ICatalogOutboxRepository _outboxRepository;

    public DapperInventoryReservationRepository(IDbConnectionFactory connectionFactory, ICatalogOutboxRepository outboxRepository)
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
            SELECT Id, StockQuantity, ReservedQuantity
            FROM Products
            WHERE Id = ANY(@ProductIds)
            ORDER BY Id
            FOR UPDATE
            """, new { ProductIds = productIds }, transaction, cancellationToken: cancellationToken))).ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);

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
                UPDATE Products SET ReservedQuantity = ReservedQuantity + @Quantity WHERE Id = @ProductId;
                """, new { OrderId = orderId, item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));
        }

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

        var status = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Status FROM InventoryReservations WHERE OrderId = @OrderId FOR UPDATE", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        if (status is null)
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

        if (status != "Reserved")
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Inventory reservation for order {orderId:D} is already {status}.");
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
                ? "UPDATE Products SET StockQuantity = StockQuantity - @Quantity, ReservedQuantity = ReservedQuantity - @Quantity WHERE Id = @ProductId"
                : "UPDATE Products SET ReservedQuantity = ReservedQuantity - @Quantity WHERE Id = @ProductId",
                new { item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE InventoryReservations SET Status = @TargetStatus, UpdatedAtUtc = @Now WHERE OrderId = @OrderId",
            new { OrderId = orderId, TargetStatus = targetStatus, Now = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));

        IntegrationEvent outcome = targetStatus == "Committed"
            ? new InventoryCommittedIntegrationEvent { OrderId = orderId, CorrelationId = CorrelationContext.CorrelationId, CausationId = messageId?.ToString("D") }
            : new InventoryReleasedIntegrationEvent { OrderId = orderId, Reason = messageId is null ? "ReservationExpired" : "PaymentFlow", CorrelationId = CorrelationContext.CorrelationId, CausationId = messageId?.ToString("D") };

        await _outboxRepository.AddAsync(new CatalogOutboxMessage
        {
            Id = outcome.EventId,
            OccurredAtUtc = outcome.OccurredAtUtc,
            Type = outcome.GetType().FullName!,
            Content = JsonSerializer.Serialize(outcome, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CorrelationId = outcome.CorrelationId,
            CausationId = outcome.CausationId,
            NextAttemptAtUtc = outcome.OccurredAtUtc
        }, transaction, cancellationToken);
        transaction.Commit();
    }

    private sealed record StockRow(string Id, int StockQuantity, int ReservedQuantity);
}
