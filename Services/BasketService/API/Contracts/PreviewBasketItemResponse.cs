namespace BasketService.API.Contracts;

public sealed class PreviewBasketItemResponse
{
    public string ProductId { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => UnitPrice * Quantity;
    public string? Description { get; set; }
}
