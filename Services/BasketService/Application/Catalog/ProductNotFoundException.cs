namespace BasketService.Application.Catalog;

public sealed class ProductNotFoundException : Exception
{
    public ProductNotFoundException()
        : base("Product not found.")
    {
    }
}
