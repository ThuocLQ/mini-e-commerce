using BasketService.DTOs;
using Refit;

namespace BasketService.Clients;

public interface ICatalogApi
{
    [Get("/products/{id}")]
    Task<CatalogProductResponse?> GetProductByIdAsync(string id);
}