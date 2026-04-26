using MediatR;
using ProductService.Commands;
using ProductService.Models;

namespace ProductService.Handlers;

public class DeleteProductHandler : IRequestHandler<DeleteProductCommand, bool>
{
    private readonly ProductStore _store;
    
    public DeleteProductHandler(ProductStore store)
    {
        _store = store;
    }
    
    public Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = _store.Products.FirstOrDefault(p => p.Id == request.id);
        
        if (product is null)        
            return Task.FromResult(false);
        
        _store.Products.Remove(product);
        
        return Task.FromResult(true);
    }
}