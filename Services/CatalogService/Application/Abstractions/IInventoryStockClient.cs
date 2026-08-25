namespace CatalogService.Application.Abstractions;

public interface IInventoryStockClient
{
    Task SetStockAsync(string productId, int stockQuantity, CancellationToken cancellationToken = default);
}

public sealed class InventoryUnavailableException(Exception innerException)
    : Exception("InventoryService is unavailable.", innerException);
