using MediatR;

namespace CatalogService.Application.Products.DiscoverProducts;

public sealed record DiscoverProductsQuery(
    string? Keyword,
    string? Category,
    ProductDiscoverySort Sort,
    int PageSize,
    string? Cursor) : IRequest<ProductDiscoveryPageDto>;