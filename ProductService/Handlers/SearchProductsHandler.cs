using MediatR;
using ProductService.Models;
using ProductService.Queries;
using ProductService.Repositories;

namespace ProductService.Handlers;

public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<Product?>>
{
    private readonly IProductRepository _productRepository;

    public SearchProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<Product?>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.SearchAsync(request.keyword);
    }
}