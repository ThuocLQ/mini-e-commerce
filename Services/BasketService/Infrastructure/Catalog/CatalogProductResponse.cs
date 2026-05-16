namespace BasketService.Infrastructure.Catalog;

public sealed class CatalogProductResponse
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public decimal Price { get; set; }
}
