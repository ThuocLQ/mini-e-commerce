using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Inventory;

public sealed record InventoryCommittedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
}
