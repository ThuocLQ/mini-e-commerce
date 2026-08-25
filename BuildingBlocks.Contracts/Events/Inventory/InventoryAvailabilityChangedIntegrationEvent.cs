using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Inventory;

public sealed record InventoryAvailabilityChangedIntegrationEvent : IntegrationEvent
{
    public string ProductId { get; init; } = string.Empty;
    public int StockQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int AvailableQuantity { get; init; }
    public DateTime InventoryUpdatedAtUtc { get; init; }
}
