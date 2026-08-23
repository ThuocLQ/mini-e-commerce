using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Orders;

public sealed record OrderPaymentSagaStateChangedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid PaymentId { get; init; }
    public string PreviousState { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
