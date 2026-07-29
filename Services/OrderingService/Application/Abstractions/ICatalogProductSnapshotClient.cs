namespace OrderingService.Application.Abstractions;

public interface ICatalogProductSnapshotClient
{
    Task<CatalogProductSnapshot?> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}

public sealed record CatalogProductSnapshot(
    Guid ProductId,
    string Name,
    decimal Price);
