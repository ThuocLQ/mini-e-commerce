using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Payments;

public sealed record PaymentSucceededIntegrationEvent : IntegrationEvent
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string ProviderTransactionId { get; init; } = string.Empty;
}
