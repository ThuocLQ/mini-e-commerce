using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.UpdateBasketItemQuantity;

public sealed class UpdateBasketItemQuantityHandler : IRequestHandler<UpdateBasketItemQuantityCommand, BasketDto?>
{
    private readonly IBasketRepository _repository;

    public UpdateBasketItemQuantityHandler(IBasketRepository repository)
    {
        _repository = repository;
    }

    public async Task<BasketDto?> Handle(UpdateBasketItemQuantityCommand request, CancellationToken cancellationToken)
    {
        var basket = await _repository.GetBasketAsync(request.UserId, cancellationToken);
        var expectedVersion = basket.Version;

        if (!basket.UpdateItemQuantity(request.ProductId, request.Quantity))
        {
            return null;
        }

        var updatedBasket = await _repository.TryUpdateBasketAsync(basket, expectedVersion, cancellationToken)
                            ?? throw new BasketConcurrencyException();

        return BasketDto.FromDomain(updatedBasket);
    }
}
