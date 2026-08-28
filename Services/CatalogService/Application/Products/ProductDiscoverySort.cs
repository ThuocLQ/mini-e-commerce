namespace CatalogService.Application.Products;

public enum ProductDiscoverySort
{
    NameAscending,
    NameDescending,
    PriceAscending,
    PriceDescending
}

public static class ProductDiscoverySortExtensions
{
    public static bool TryParse(string? value, out ProductDiscoverySort sort)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null:
            case "":
            case "name_asc":
                sort = ProductDiscoverySort.NameAscending;
                return true;
            case "name_desc":
                sort = ProductDiscoverySort.NameDescending;
                return true;
            case "price_asc":
                sort = ProductDiscoverySort.PriceAscending;
                return true;
            case "price_desc":
                sort = ProductDiscoverySort.PriceDescending;
                return true;
            default:
                sort = default;
                return false;
        }
    }

    public static string ToApiValue(this ProductDiscoverySort sort) => sort switch
    {
        ProductDiscoverySort.NameAscending => "name_asc",
        ProductDiscoverySort.NameDescending => "name_desc",
        ProductDiscoverySort.PriceAscending => "price_asc",
        ProductDiscoverySort.PriceDescending => "price_desc",
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unsupported product discovery sort.")
    };
}