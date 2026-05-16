using BasketService.Domain.Baskets;

namespace BasketService.Application.Abstractions;

public interface IBasketRepository
{
    Task<ShoppingCart> GetBasketAsync(string userId, CancellationToken cancellationToken = default);
    Task<ShoppingCart> UpdateBasketAsync(ShoppingCart cart, CancellationToken cancellationToken = default);
    Task<bool> DeleteBasketAsync(string userId, CancellationToken cancellationToken = default);
}
