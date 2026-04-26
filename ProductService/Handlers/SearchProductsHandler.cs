using MediatR;
using ProductService.Models;
using ProductService.Queries;

namespace ProductService.Handlers;

public class SearchProductsHandler : IRequestHandler<SearchProductsQuery, List<Product?>>
{
    private readonly ProductStore _store;
    
    public SearchProductsHandler(ProductStore store)
    {
        _store = store;
    }
    
    public Task<List<Product?>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.keyword))
            return Task.FromResult(_store.Products);
        return Task.FromResult(_store.Products
            .Where(x => x.Name.Contains(request.keyword, StringComparison.OrdinalIgnoreCase))
            .ToList());
    }
}