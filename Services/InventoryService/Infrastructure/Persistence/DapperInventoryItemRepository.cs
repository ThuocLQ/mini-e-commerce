using Dapper;
using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Inventory;
using InventoryService.Application.Abstractions;
using InventoryService.Application.Inventory.GetInventoryItems;
using InventoryService.Application.Inventory.ReceiveInventoryStock;
using InventoryService.Domain.Outbox;
using InventoryService.Infrastructure.Persistence.Outbox;

namespace InventoryService.Infrastructure.Persistence;

public sealed class DapperInventoryItemRepository : IInventoryItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IInventoryOutboxRepository _outboxRepository;

    public DapperInventoryItemRepository(
        IDbConnectionFactory connectionFactory,
        IInventoryOutboxRepository outboxRepository)
    {
        _connectionFactory = connectionFactory;
        _outboxRepository = outboxRepository;
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<InventoryItemSnapshotRow>(new CommandDefinition("""
            SELECT ProductId, StockQuantity, ReservedQuantity, StockQuantity - ReservedQuantity AS AvailableQuantity, UpdatedAtUtc
            FROM InventoryItems
            ORDER BY UpdatedAtUtc DESC, ProductId;
            """, cancellationToken: cancellationToken));

        return rows.Select(row => new InventoryItemDto(
            row.ProductId,
            row.StockQuantity,
            row.ReservedQuantity,
            row.AvailableQuantity,
            row.UpdatedAtUtc)).ToList();
    }

    public async Task UpsertStockAsync(string productId, int stockQuantity, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var item = await connection.QuerySingleOrDefaultAsync<InventoryItemRow>(new CommandDefinition("""
            INSERT INTO InventoryItems (ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc)
            VALUES (@ProductId, @StockQuantity, 0, CURRENT_TIMESTAMP)
            ON CONFLICT (ProductId) DO UPDATE
            SET StockQuantity = EXCLUDED.StockQuantity,
                UpdatedAtUtc = CURRENT_TIMESTAMP
            WHERE InventoryItems.ReservedQuantity <= EXCLUDED.StockQuantity
            RETURNING ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc;
            """, new { ProductId = productId, StockQuantity = stockQuantity }, transaction, cancellationToken: cancellationToken));

        if (item is null)
        {
            transaction.Rollback();
            throw new InvalidOperationException("Stock cannot be lowered below the active reserved quantity.");
        }

        var availabilityEvent = new InventoryAvailabilityChangedIntegrationEvent
        {
            ProductId = item.ProductId,
            StockQuantity = item.StockQuantity,
            ReservedQuantity = item.ReservedQuantity,
            AvailableQuantity = item.StockQuantity - item.ReservedQuantity,
            InventoryUpdatedAtUtc = item.UpdatedAtUtc,
            CorrelationId = CorrelationContext.CorrelationId
        };

        await _outboxRepository.AddAsync(new InventoryOutboxMessage
        {
            Id = availabilityEvent.EventId,
            OccurredAtUtc = availabilityEvent.OccurredAtUtc,
            Type = availabilityEvent.GetType().FullName!,
            Content = JsonSerializer.Serialize(availabilityEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CorrelationId = availabilityEvent.CorrelationId,
            NextAttemptAtUtc = availabilityEvent.OccurredAtUtc
        }, transaction, cancellationToken);

        transaction.Commit();
    }

    public async Task<bool> ReceiveStockAsync(Guid receiptId, Guid sourcePurchaseOrderId, IReadOnlyList<InventoryStockReceiptItem> items, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var insertedReceiptId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("""
            INSERT INTO InventoryStockReceipts (ReceiptId, SourcePurchaseOrderId, ReceivedAtUtc)
            VALUES (@ReceiptId, @SourcePurchaseOrderId, CURRENT_TIMESTAMP)
            ON CONFLICT (ReceiptId) DO NOTHING
            RETURNING ReceiptId;
            """, new { ReceiptId = receiptId, SourcePurchaseOrderId = sourcePurchaseOrderId }, transaction, cancellationToken: cancellationToken));

        if (insertedReceiptId is null)
        {
            transaction.Commit();
            return false;
        }

        foreach (var item in items)
        {
            var updatedItem = await connection.QuerySingleOrDefaultAsync<InventoryItemRow>(new CommandDefinition("""
                UPDATE InventoryItems
                SET StockQuantity = StockQuantity + @Quantity,
                    UpdatedAtUtc = CURRENT_TIMESTAMP
                WHERE ProductId = @ProductId
                RETURNING ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc;
                """, new { item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));

            if (updatedItem is null)
            {
                transaction.Rollback();
                throw new InvalidOperationException($"Inventory item '{item.ProductId}' was not found.");
            }

            var availabilityEvent = new InventoryAvailabilityChangedIntegrationEvent
            {
                ProductId = updatedItem.ProductId,
                StockQuantity = updatedItem.StockQuantity,
                ReservedQuantity = updatedItem.ReservedQuantity,
                AvailableQuantity = updatedItem.StockQuantity - updatedItem.ReservedQuantity,
                InventoryUpdatedAtUtc = updatedItem.UpdatedAtUtc,
                CorrelationId = CorrelationContext.CorrelationId,
                CausationId = receiptId.ToString("D")
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

        transaction.Commit();
        return true;
    }
    private sealed record InventoryItemRow(string ProductId, int StockQuantity, int ReservedQuantity, DateTime UpdatedAtUtc);
    private sealed record InventoryItemSnapshotRow(string ProductId, int StockQuantity, int ReservedQuantity, int AvailableQuantity, DateTime UpdatedAtUtc);
}
