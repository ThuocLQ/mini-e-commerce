namespace BasketService.DTOs;

public class CatalogProductValidateErrors
{
    public bool Valid { get; set; } = false;
    public string Message { get; set; } = string.Empty;
}