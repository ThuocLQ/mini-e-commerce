using BasketService.Application.Catalog;

namespace BasketService.Application.Abstractions;

public interface ICatalogProductClient
{
    Task<CatalogProduct?> GetProductByIdAsync(
        string productId,
        CatalogCommunicationMode mode,
        CancellationToken cancellationToken = default);

    Task<CatalogCallMeasurement> MeasureGetProductByIdAsync(
        string productId,
        CatalogCommunicationMode mode,
        CancellationToken cancellationToken = default);
}
