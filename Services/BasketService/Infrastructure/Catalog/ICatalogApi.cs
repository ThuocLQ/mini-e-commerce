using Refit;

namespace BasketService.Infrastructure.Catalog;

public interface ICatalogApi
{
    [Get("/products/{id}")]
    Task<CatalogProductResponse?> GetProductByIdAsync(string id, CancellationToken cancellationToken = default);
}
