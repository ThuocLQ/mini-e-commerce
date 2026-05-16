namespace BasketService.Application.Catalog;

public sealed record CatalogProduct(
    string Id,
    string? Name,
    decimal Price,
    string? Description = null);
