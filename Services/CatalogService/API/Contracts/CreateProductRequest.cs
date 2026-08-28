namespace CatalogService.API.Contracts;

public sealed record CreateProductRequest(
    string Name,
    decimal Price,
    string? Description = null,
    int StockQuantity = 0,
    string? Category = null,
    string? ImageUrl = null,
    string? Sku = null,
    string? Brand = null);