using CatalogService.Application.Abstractions;
using CatalogService.Commands;
using CatalogService.Models;
using MediatR;

namespace CatalogService.Handlers;

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Product?>
{
    private readonly IProductRepository _productRepository;
    
    public UpdateProductHandler(IProductRepository  productRepository )
    {
        _productRepository = productRepository;
    }
    
    public async Task<Product?> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        return await _productRepository.UpdateAsync(request.Id, request.Name, request.Price, cancellationToken);
    }
}
