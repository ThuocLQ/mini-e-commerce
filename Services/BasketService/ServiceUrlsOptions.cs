namespace BasketService;

public sealed class ServiceUrlsOptions
{
    public const string SectionName = "ServiceUrls";
    
    public string CatalogHttp { get; set; } = string.Empty;
    public string CatalogGrpc { get; set; } = string.Empty;
    
}