namespace OrderingService.Application.Abstractions;

public interface IInventoryAvailabilityClient
{
    Task<IReadOnlyList<InventoryAvailabilityItem>> GetAvailabilityAsync(
        IReadOnlyList<InventoryAvailabilityRequestItem> items,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryAvailabilityRequestItem(Guid ProductId, int Quantity);

public sealed record InventoryAvailabilityItem(Guid ProductId, bool Available);
