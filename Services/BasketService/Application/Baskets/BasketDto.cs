using BasketService.Domain.Baskets;

namespace BasketService.Application.Baskets;

public sealed record BasketDto(
    string UserId,
    IReadOnlyList<BasketItemDto> Items,
    decimal TotalPrice)
{
    public static BasketDto FromDomain(ShoppingCart basket)
    {
        return new BasketDto(
            basket.UserId,
            basket.Items.Select(item => new BasketItemDto(
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.Price)).ToList(),
            basket.TotalPrice);
    }
}

public sealed record BasketItemDto(
    string ProductId,
    string? ProductName,
    int Quantity,
    decimal Price);
