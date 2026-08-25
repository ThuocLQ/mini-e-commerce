namespace InventoryService.Application.Abstractions;

public interface IInventoryItemRepository
{
    Task UpsertStockAsync(string productId, int stockQuantity, CancellationToken cancellationToken = default);
}
