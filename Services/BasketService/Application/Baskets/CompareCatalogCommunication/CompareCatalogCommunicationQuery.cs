using MediatR;

namespace BasketService.Application.Baskets.CompareCatalogCommunication;

public sealed record CompareCatalogCommunicationQuery(string ProductId) : IRequest<CompareCatalogCommunicationResult>;
