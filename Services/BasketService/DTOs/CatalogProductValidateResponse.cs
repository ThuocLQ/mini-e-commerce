namespace BasketService.DTOs;

public class CatalogProductValidateResponse
{
    public bool Valid { get; set; } = true;
    public string ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
}