using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Inventory;

public sealed record InventoryReleasedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
