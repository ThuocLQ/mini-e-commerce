using BasketService.Application.Catalog;
using MediatR;

namespace BasketService.Application.Baskets.ValidateCatalogProduct;

public sealed record ValidateCatalogProductQuery(
    string ProductId,
    CatalogCommunicationMode Mode) : IRequest<CatalogProductValidateResult>;
