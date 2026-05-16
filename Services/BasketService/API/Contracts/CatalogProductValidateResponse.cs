namespace BasketService.API.Contracts;

public sealed class CatalogProductValidateResponse
{
    public bool Valid { get; set; } = true;
    public string ProductId { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
}
