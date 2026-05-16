using MediatR;

namespace BasketService.Application.Baskets.UpdateBasketItemQuantity;

public sealed record UpdateBasketItemQuantityCommand(
    string UserId,
    string ProductId,
    int Quantity) : IRequest<BasketDto?>;
