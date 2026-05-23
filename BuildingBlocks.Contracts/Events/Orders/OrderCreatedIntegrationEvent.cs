using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Orders;

public sealed record OrderCreatedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "VND";
}
