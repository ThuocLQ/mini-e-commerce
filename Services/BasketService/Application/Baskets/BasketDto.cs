using BasketService.Domain.Baskets;

namespace BasketService.Application.Baskets;

public sealed record BasketDto(
    string UserId,
    Guid BasketId,
    IReadOnlyList<BasketItemDto> Items,
    decimal TotalPrice,
    long Version)
{
    public static BasketDto FromDomain(ShoppingCart basket)
    {
        return new BasketDto(
            basket.UserId,
            basket.BasketId,
            basket.Items.Select(item => new BasketItemDto(
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.Price)).ToList(),
            basket.TotalPrice,
            basket.Version);
    }
}

public sealed record BasketItemDto(
    string ProductId,
    string? ProductName,
    int Quantity,
    decimal Price);
