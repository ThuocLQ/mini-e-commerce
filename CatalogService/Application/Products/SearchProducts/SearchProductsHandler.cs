using CatalogService.Application.Abstractions;
using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.SearchProducts;

public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<ProductDto>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<ProductDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.SearchAsync(request.Keyword, cancellationToken);

        return products.Select(ProductDto.FromDomain).ToList();
    }
}
