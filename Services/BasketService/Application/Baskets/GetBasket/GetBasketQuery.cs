using MediatR;

namespace BasketService.Application.Baskets.GetBasket;

public sealed record GetBasketQuery(string UserId) : IRequest<BasketDto>;
