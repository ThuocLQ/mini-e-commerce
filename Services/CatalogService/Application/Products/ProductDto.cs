namespace CatalogService.Application.Products;

public sealed record ProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string? Category = null,
    string? ImageUrl = null,
    string? Sku = null,
    string? Brand = null);