using BasketService.Application.Catalog;
using MediatR;

namespace BasketService.Application.Baskets.AddBasketItem;

public sealed record AddBasketItemCommand(
    string UserId,
    string ProductId,
    int Quantity,
    CatalogCommunicationMode Mode) : IRequest<BasketDto>;
