namespace BasketService.API.Contracts;

public sealed class CatalogProductValidateErrors
{
    public bool Valid { get; set; } = false;
    public string Message { get; set; } = string.Empty;
}
