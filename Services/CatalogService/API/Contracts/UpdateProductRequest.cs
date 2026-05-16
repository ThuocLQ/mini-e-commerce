namespace CatalogService.API.Contracts;

public sealed record UpdateProductRequest(string Name, decimal Price, string? Description = null);
