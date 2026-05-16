namespace BasketService.Application.Catalog;

public sealed class CatalogUnavailableException : Exception
{
    public CatalogUnavailableException()
        : base("CatalogService is unavailable. Please try again later.")
    {
    }

    public CatalogUnavailableException(Exception innerException)
        : base("CatalogService is unavailable. Please try again later.", innerException)
    {
    }
}
