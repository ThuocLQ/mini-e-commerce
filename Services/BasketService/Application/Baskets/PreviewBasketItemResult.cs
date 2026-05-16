namespace BasketService.Application.Baskets;

public sealed record PreviewBasketItemResult(
    string ProductId,
    string? ProductName,
    int Quantity,
    decimal UnitPrice,
    string? Description)
{
    public decimal TotalPrice => UnitPrice * Quantity;
}
