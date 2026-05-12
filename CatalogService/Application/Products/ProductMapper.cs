using CatalogService.Domain.Products;

namespace CatalogService.Application.Products;

public static class ProductMapper
{
    public static ProductDto ToDto(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price);
    }
}
