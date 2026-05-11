using CatalogService.Application.Abstractions;
using CatalogService.Application.Products;
using CatalogService.Domain.Products;
using MediatR;

namespace CatalogService.Application.Products.CreateProduct;

public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;

    public CreateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(
            Guid.NewGuid().ToString(),
            request.Name,
            string.Empty,
            request.Price);

        var created = await _productRepository.CreateAsync(product, cancellationToken);

        return ProductDto.FromDomain(created);
    }
}
