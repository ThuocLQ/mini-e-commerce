namespace OrderingService.Application.Catalog;

public sealed class CatalogUnavailableException : Exception
{
    public CatalogUnavailableException(Exception innerException)
        : base("CatalogService is unavailable. Checkout cannot confirm current product pricing.", innerException)
    {
    }
}
