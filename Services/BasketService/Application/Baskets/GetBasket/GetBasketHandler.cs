using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.GetBasket;

public sealed class GetBasketHandler : IRequestHandler<GetBasketQuery, BasketDto>
{
    private readonly IBasketRepository _repository;

    public GetBasketHandler(IBasketRepository repository)
    {
        _repository = repository;
    }

    public async Task<BasketDto> Handle(GetBasketQuery request, CancellationToken cancellationToken)
    {
        var basket = await _repository.GetBasketAsync(request.UserId, cancellationToken);

        return BasketDto.FromDomain(basket);
    }
}
