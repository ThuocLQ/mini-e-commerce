using BasketService.Application.Catalog;
using MediatR;

namespace BasketService.Application.Baskets.PreviewBasketItem;

public sealed record PreviewBasketItemCommand(
    string ProductId,
    int Quantity,
    CatalogCommunicationMode Mode) : IRequest<PreviewBasketItemResult?>;
