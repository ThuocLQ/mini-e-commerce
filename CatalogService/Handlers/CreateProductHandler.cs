using CatalogService.Commands;
using CatalogService.Models;
using CatalogService.Repositories;
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
        return await _productRepository.CreateAsync(request.name, request.price);
    }
}