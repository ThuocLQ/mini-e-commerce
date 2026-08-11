namespace CatalogService.API.Contracts;

public sealed record CreateProductRequest(string Name, decimal Price, string? Description = null, int StockQuantity = 0);
