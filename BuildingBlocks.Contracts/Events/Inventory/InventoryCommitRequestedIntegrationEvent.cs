using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Inventory;

public sealed record InventoryCommitRequestedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
}
