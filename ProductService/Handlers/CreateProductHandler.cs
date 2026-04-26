using MediatR;
using ProductService.Commands;
using ProductService.Models;

namespace ProductService.Handlers;

public class CreateProductHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly ProductStore _store;
    
    public CreateProductHandler(ProductStore store)
    {
        _store = store;
    }
    
    public Task<Product> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name
        };
        
        _store.Products.Add(product);
        
        return Task.FromResult(product);
    }
}