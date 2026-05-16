using CatalogService.Application.Abstractions;
using CatalogService.Application.Products;
using CatalogService.Domain.Products;
using MediatR;

namespace CatalogService.Application.Products.UpdateProduct;

public sealed class UpdateProductHandler : IRequestHandler<UpdateProductCommand, ProductDto?>
{
    private readonly IProductRepository _productRepository;

    public UpdateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductDto?> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(
            request.Id,
            request.Name,
            request.Description ?? string.Empty,
            request.Price);

        var updated = await _productRepository.UpdateAsync(product, cancellationToken);

        return updated is null ? null : ProductMapper.ToDto(updated);
    }
}
