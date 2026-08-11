namespace OrderingService.Application.Abstractions;

public interface IInventoryReservationClient
{
    Task<InventoryReservationResponse> ReserveAsync(Guid orderId, IReadOnlyList<InventoryReservationItem> items, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task CommitAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public sealed record InventoryReservationItem(Guid ProductId, int Quantity);
public sealed record InventoryReservationResponse(bool Succeeded, string? FailureReason);
