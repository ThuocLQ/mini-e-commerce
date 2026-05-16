using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.ClearBasket;

public sealed class ClearBasketHandler : IRequestHandler<ClearBasketCommand, BasketDto>
{
    private readonly IBasketRepository _repository;

    public ClearBasketHandler(IBasketRepository repository)
    {
        _repository = repository;
    }

    public async Task<BasketDto> Handle(ClearBasketCommand request, CancellationToken cancellationToken)
    {
        var basket = await _repository.GetBasketAsync(request.UserId, cancellationToken);
        basket.Clear();

        var updatedBasket = await _repository.UpdateBasketAsync(basket, cancellationToken);

        return BasketDto.FromDomain(updatedBasket);
    }
}
