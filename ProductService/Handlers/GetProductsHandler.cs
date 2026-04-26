using MediatR;
using ProductService.Models;

namespace ProductService.Handlers;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, List<Product>>
{
    private readonly ProductStore  _store;

    public GetProductsHandler(ProductStore store)
    {
        _store = store;
    }

    public Task<List<Product>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_store.Products);
    }
}