namespace CatalogService.API.Contracts;

public sealed record UpdateProductRequest(
    string Name,
    decimal Price,
    string? Description = null,
    string? Category = null,
    string? ImageUrl = null,
    string? Brand = null,
    string? Sku = null);