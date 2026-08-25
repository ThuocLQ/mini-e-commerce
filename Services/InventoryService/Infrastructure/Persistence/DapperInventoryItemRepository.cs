using Dapper;
using InventoryService.Application.Abstractions;

namespace InventoryService.Infrastructure.Persistence;

public sealed class DapperInventoryItemRepository(IDbConnectionFactory connectionFactory) : IInventoryItemRepository
{
    public async Task UpsertStockAsync(string productId, int stockQuantity, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO InventoryItems (ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc)
            VALUES (@ProductId, @StockQuantity, 0, CURRENT_TIMESTAMP)
            ON CONFLICT (ProductId) DO UPDATE
            SET StockQuantity = EXCLUDED.StockQuantity,
                UpdatedAtUtc = CURRENT_TIMESTAMP
            WHERE InventoryItems.ReservedQuantity <= EXCLUDED.StockQuantity;
            """, new { ProductId = productId, StockQuantity = stockQuantity }, cancellationToken: cancellationToken));

        if (affected == 0)
        {
            throw new InvalidOperationException("Stock cannot be lowered below the active reserved quantity.");
        }
    }
}
