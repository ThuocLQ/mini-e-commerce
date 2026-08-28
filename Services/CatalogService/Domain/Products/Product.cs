namespace CatalogService.Domain.Products;

public sealed class Product
{
    public string Id { get; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; }
    public bool IsActive { get; }
    public string? Category { get; private set; }
    public string? ImageUrl { get; private set; }
    public string Sku { get; }
    public string? Brand { get; private set; }

    public Product(
        string id,
        string name,
        string description,
        decimal price,
        int stockQuantity = 0,
        bool isActive = true,
        string? category = null,
        string? imageUrl = null,
        string? sku = null,
        string? brand = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Product id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Product price cannot be negative.");
        }

        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock quantity cannot be negative.");
        }

        Id = id;
        Name = name;
        Description = description;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = isActive;
        Category = NormalizeCategory(category);
        ImageUrl = NormalizeImageUrl(imageUrl);
        Sku = NormalizeSku(sku, id);
        Brand = NormalizeBrand(brand);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        Name = name;
    }

    public void ChangeDescription(string description)
    {
        Description = description;
    }

    public void ChangePrice(decimal newPrice)
    {
        if (newPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPrice), "Product price cannot be negative.");
        }

        Price = newPrice;
    }

    private static string? NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category) ? null : category.Trim();
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        return string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
    }

    private static string NormalizeSku(string? sku, string id)
    {
        var normalized = string.IsNullOrWhiteSpace(sku) ? $"LEGACY-{id}" : sku.Trim();

        if (normalized.Length > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(sku), "Product SKU must not exceed 64 characters.");
        }

        return normalized;
    }

    private static string? NormalizeBrand(string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return null;
        }

        var normalized = brand.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(brand), "Product brand must not exceed 100 characters.");
        }

        return normalized;
    }
}