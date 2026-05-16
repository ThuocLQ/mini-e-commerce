using CatalogService.Domain.Products;

namespace CatalogService.Application.Products;

public sealed record ProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price);
