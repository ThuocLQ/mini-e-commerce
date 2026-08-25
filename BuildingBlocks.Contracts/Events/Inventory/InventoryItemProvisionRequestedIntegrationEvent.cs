namespace BuildingBlocks.Contracts.Events.Inventory;

public sealed record InventoryItemProvisionRequestedIntegrationEvent : IntegrationEvent
{
    public string ProductId { get; init; } = string.Empty;
    public int InitialStockQuantity { get; init; }
}
