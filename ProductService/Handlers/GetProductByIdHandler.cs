using MediatR;
using ProductService.Models;

namespace ProductService.Handlers;

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly ProductStore _store;
    
    public GetProductByIdHandler(ProductStore store)
    {
        _store = store;
    }
     
    public Task<Product?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = _store.Products.FirstOrDefault(p => p.Id == request.id);
        
        return Task.FromResult(product);
    }
}