namespace CatalogService.Application.Abstractions;

public interface IInventoryReservationRepository
{
    Task<InventoryReservationResult> ReserveAsync(
        Guid orderId,
        IReadOnlyList<InventoryReservationItem> items,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(Guid orderId, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task CommitAsync(Guid orderId, Guid? messageId = null, CancellationToken cancellationToken = default);
    Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed record InventoryReservationItem(string ProductId, int Quantity);

public sealed record InventoryReservationResult(bool Succeeded, string? FailureReason = null);
