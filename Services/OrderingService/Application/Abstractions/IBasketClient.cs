using OrderingService.Application.Baskets;

namespace OrderingService.Application.Abstractions;

public interface IBasketClient
{
    Task<BasketDto?> GetBasketAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task ClearBasketAsync(Guid customerId, CancellationToken cancellationToken = default);
}