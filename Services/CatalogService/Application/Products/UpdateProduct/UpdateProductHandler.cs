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
        var existing = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var sku = string.IsNullOrWhiteSpace(request.Sku) ? existing.Sku : request.Sku.Trim();
        if (!string.Equals(existing.Sku, sku, StringComparison.Ordinal) && !existing.Sku.StartsWith("LEGACY-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Product SKU is immutable after it has been assigned.");
        }

        var product = new Product(
            existing.Id,
            request.Name,
            request.Description ?? string.Empty,
            request.Price,
            existing.StockQuantity,
            existing.IsActive,
            request.Category,
            request.ImageUrl,
            sku,
            request.Brand);

        var updated = await _productRepository.UpdateAsync(product, cancellationToken);
        return updated is null ? null : ProductMapper.ToDto(updated);
    }
}