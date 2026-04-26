using MediatR;
using ProductService.Commands;
using ProductService.Models;
using ProductService.Repositories;

namespace ProductService.Handlers;

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Product?>
{
    private readonly IProductRepository _productRepository;
    
    public UpdateProductHandler(IProductRepository  productRepository )
    {
        _productRepository = productRepository;
    }
    
    public async Task<Product?> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        return await _productRepository.UpdateAsync(request.id, request.name,  request.price);
    }
}