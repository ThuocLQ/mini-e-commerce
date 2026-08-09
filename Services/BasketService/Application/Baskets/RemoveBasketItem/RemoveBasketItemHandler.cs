using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.RemoveBasketItem;

public sealed class RemoveBasketItemHandler : IRequestHandler<RemoveBasketItemCommand, bool>
{
    private readonly IBasketRepository _repository;

    public RemoveBasketItemHandler(IBasketRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(RemoveBasketItemCommand request, CancellationToken cancellationToken)
    {
        var basket = await _repository.GetBasketAsync(request.UserId, cancellationToken);
        var expectedVersion = basket.Version;

        if (!basket.RemoveItem(request.ProductId))
        {
            return false;
        }

        if (await _repository.TryUpdateBasketAsync(basket, expectedVersion, cancellationToken) is null)
        {
            throw new BasketConcurrencyException();
        }

        return true;
    }
}
