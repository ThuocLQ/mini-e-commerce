using BasketService.Domain.Baskets;

namespace BasketService.Application.Abstractions;

public interface IBasketRepository
{
    Task<ShoppingCart> GetBasketAsync(string userId, CancellationToken cancellationToken = default);
    Task<ShoppingCart?> TryUpdateBasketAsync(ShoppingCart cart, long expectedVersion, CancellationToken cancellationToken = default);
    Task<bool> DeleteBasketAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> TryDeleteBasketAsync(string userId, long expectedVersion, CancellationToken cancellationToken = default);
}
