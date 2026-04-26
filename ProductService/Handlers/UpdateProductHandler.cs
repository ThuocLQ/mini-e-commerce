using MediatR;
using ProductService.Commands;
using ProductService.Models;

namespace ProductService.Handlers;

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Product?>
{
    private readonly ProductStore _store;
    
    public UpdateProductHandler(ProductStore store)
    {
        _store = store;
    }
    
    public Task<Product?> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = _store.Products.FirstOrDefault(p => p.Id == request.id);
        
        if (product is null)        
            return Task.FromResult<Product?>(null);
        
        product.Name = request.name;
        
        return Task.FromResult<Product?>(product);
    }
}