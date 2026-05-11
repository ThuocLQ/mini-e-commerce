using CatalogService.Domain.Products;

namespace CatalogService.Application.Products;

public sealed record ProductDto(
    string Id,
    string Name,
    string Description,
    decimal Price)
{
    public static ProductDto FromDomain(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price);
    }
}
