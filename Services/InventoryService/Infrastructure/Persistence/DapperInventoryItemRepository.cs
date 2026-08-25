using Dapper;
using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Inventory;
using InventoryService.Application.Abstractions;
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

    private sealed record InventoryItemRow(string ProductId, int StockQuantity, int ReservedQuantity, DateTime UpdatedAtUtc);
}
