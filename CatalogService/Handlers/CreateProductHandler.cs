using CatalogService.Application.Abstractions;
using CatalogService.Commands;
using CatalogService.Models;
using MediatR;

namespace CatalogService.Handlers;

public class CreateProductHandler : IRequestHandler<CreateProductCommand, Product>
{
    private readonly IProductRepository _productRepository;
    
    public CreateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        return await _productRepository.CreateAsync(request.Name, request.Price, cancellationToken);
    }
}
