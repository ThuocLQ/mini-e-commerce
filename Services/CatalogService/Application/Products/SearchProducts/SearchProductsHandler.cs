using CatalogService.Application.Abstractions;
using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.SearchProducts;

public sealed class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<ProductDto>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<ProductDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var criteria = new ProductQueryCriteria(SearchTerm: request.Keyword);
        var products = await _productRepository.SearchAsync(criteria, cancellationToken);

        return products.Select(ProductMapper.ToDto).ToList();
    }
}
