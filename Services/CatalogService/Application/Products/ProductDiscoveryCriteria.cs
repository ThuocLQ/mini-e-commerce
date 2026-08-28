using CatalogService.Domain.Products;

namespace CatalogService.Application.Products;

public sealed record ProductDiscoveryCriteria(
    string? Keyword,
    string? Category,
    ProductDiscoverySort Sort,
    int PageSize,
    string? Cursor);

public sealed record ProductDiscoveryResult(
    IReadOnlyList<Product> Products,
    string? NextCursor);