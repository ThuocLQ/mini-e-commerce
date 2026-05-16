namespace CatalogService.API.Contracts;

public sealed record CreateProductRequest(string Name, decimal Price, string? Description = null);
