using CatalogService.Application.Abstractions;
using MediatR;

namespace CatalogService.Application.Products.DiscoverProducts;

public sealed class DiscoverProductsHandler : IRequestHandler<DiscoverProductsQuery, ProductDiscoveryPageDto>
{
    private readonly IProductRepository _productRepository;

    public DiscoverProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductDiscoveryPageDto> Handle(DiscoverProductsQuery request, CancellationToken cancellationToken)
    {
        var result = await _productRepository.DiscoverAsync(
            new ProductDiscoveryCriteria(
                Normalize(request.Keyword),
                Normalize(request.Category),
                request.Sort,
                request.PageSize,
                request.Cursor),
            cancellationToken);

        return new ProductDiscoveryPageDto(
            result.Products.Select(ProductMapper.ToDto).ToList(),
            result.NextCursor,
            request.PageSize,
            request.Sort.ToApiValue());
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}