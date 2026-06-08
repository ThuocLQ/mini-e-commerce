namespace CatalogService.Application.Products;

public sealed record ProductQueryCriteria(
    string? SearchTerm = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null);
