using MediatR;

namespace BasketService.Application.Baskets.CheckoutBasket;

public sealed record CheckoutBasketCommand(string UserId, long ExpectedVersion) : IRequest<bool>;
