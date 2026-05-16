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

        if (!basket.RemoveItem(request.ProductId))
        {
            return false;
        }

        await _repository.UpdateBasketAsync(basket, cancellationToken);

        return true;
    }
}
