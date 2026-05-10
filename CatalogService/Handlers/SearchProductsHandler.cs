using CatalogService.Application.Abstractions;
using CatalogService.Models;
using CatalogService.Queries;
using MediatR;

namespace CatalogService.Handlers;

public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<Product>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<Product>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.SearchAsync(request.Keyword, cancellationToken);
    }
}
