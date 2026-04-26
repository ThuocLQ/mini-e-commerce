using MediatR;
using ProductService.Commands;
using ProductService.Models;
using ProductService.Repositories;

namespace ProductService.Handlers;

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