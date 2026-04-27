namespace CatalogService.Models;

public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string?Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}