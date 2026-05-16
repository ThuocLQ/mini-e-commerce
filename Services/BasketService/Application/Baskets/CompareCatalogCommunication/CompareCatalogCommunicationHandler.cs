using BasketService.Application.Abstractions;
using BasketService.Application.Catalog;
using MediatR;

namespace BasketService.Application.Baskets.CompareCatalogCommunication;

public sealed class CompareCatalogCommunicationHandler
    : IRequestHandler<CompareCatalogCommunicationQuery, CompareCatalogCommunicationResult>
{
    private readonly ICatalogProductClient _catalogProductClient;

    public CompareCatalogCommunicationHandler(ICatalogProductClient catalogProductClient)
    {
        _catalogProductClient = catalogProductClient;
    }

    public async Task<CompareCatalogCommunicationResult> Handle(
        CompareCatalogCommunicationQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            throw new ArgumentException("ProductId is required.");
        }

        var rest = await _catalogProductClient.MeasureGetProductByIdAsync(
            request.ProductId,
            CatalogCommunicationMode.Rest,
            cancellationToken);

        var grpc = await _catalogProductClient.MeasureGetProductByIdAsync(
            request.ProductId,
            CatalogCommunicationMode.Grpc,
            cancellationToken);

        return new CompareCatalogCommunicationResult(
            request.ProductId,
            new CatalogCommunicationResult(rest.Success, rest.ElapsedMs),
            new CatalogCommunicationResult(grpc.Success, grpc.ElapsedMs));
    }
}
