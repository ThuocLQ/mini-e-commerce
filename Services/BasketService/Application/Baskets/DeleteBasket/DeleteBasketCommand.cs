using MediatR;

namespace BasketService.Application.Baskets.DeleteBasket;

public sealed record DeleteBasketCommand(string UserId) : IRequest<bool>;
