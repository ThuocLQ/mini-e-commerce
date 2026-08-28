using System.Text;
using System.Text.Json;
using CatalogService.Domain.Products;

namespace CatalogService.Application.Products;

public sealed record ProductDiscoveryCursorValue(
    ProductDiscoverySort Sort,
    string Id,
    string? Name,
    decimal? Price);

public static class ProductDiscoveryCursor
{
    public static string Encode(Product product, ProductDiscoverySort sort)
    {
        var payload = new ProductDiscoveryCursorValue(
            sort,
            product.Id,
            sort is ProductDiscoverySort.NameAscending or ProductDiscoverySort.NameDescending ? product.Name : null,
            sort is ProductDiscoverySort.PriceAscending or ProductDiscoverySort.PriceDescending ? product.Price : null);

        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool IsValidFor(string cursor, ProductDiscoverySort sort)
    {
        try
        {
            _ = Decode(cursor, sort);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static ProductDiscoveryCursorValue Decode(string cursor, ProductDiscoverySort expectedSort)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            throw new ArgumentException("Product discovery cursor is required.", nameof(cursor));
        }

        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            var payload = JsonSerializer.Deserialize<ProductDiscoveryCursorValue>(Encoding.UTF8.GetString(Convert.FromBase64String(normalized)));

            if (payload is null || payload.Sort != expectedSort || string.IsNullOrWhiteSpace(payload.Id))
            {
                throw new ArgumentException("Product discovery cursor does not match the requested sort.", nameof(cursor));
            }

            if ((expectedSort is ProductDiscoverySort.NameAscending or ProductDiscoverySort.NameDescending) && string.IsNullOrWhiteSpace(payload.Name))
            {
                throw new ArgumentException("Product discovery cursor is missing the product name.", nameof(cursor));
            }

            if ((expectedSort is ProductDiscoverySort.PriceAscending or ProductDiscoverySort.PriceDescending) && !payload.Price.HasValue)
            {
                throw new ArgumentException("Product discovery cursor is missing the product price.", nameof(cursor));
            }

            return payload;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException("Product discovery cursor is invalid.", nameof(cursor), exception);
        }
    }
}