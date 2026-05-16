using MediatR;

namespace BasketService.Application.Baskets.ClearBasket;

public sealed record ClearBasketCommand(string UserId) : IRequest<BasketDto>;
