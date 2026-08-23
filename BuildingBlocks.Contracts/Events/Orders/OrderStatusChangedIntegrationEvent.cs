using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Orders;

public sealed record OrderStatusChangedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string PreviousStatus { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "USD";
}
