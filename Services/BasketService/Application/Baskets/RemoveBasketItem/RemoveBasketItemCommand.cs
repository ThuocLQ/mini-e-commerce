using MediatR;

namespace BasketService.Application.Baskets.RemoveBasketItem;

public sealed record RemoveBasketItemCommand(
    string UserId,
    string ProductId) : IRequest<bool>;
