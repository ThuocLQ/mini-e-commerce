namespace BasketService.Application.Baskets;

public sealed record CompareCatalogCommunicationResult(
    string ProductId,
    CatalogCommunicationResult Rest,
    CatalogCommunicationResult Grpc);

public sealed record CatalogCommunicationResult(
    bool Success,
    long ElapsedMs);
