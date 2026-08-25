using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Payments;

public sealed record PaymentOperationFailedIntegrationEvent : IntegrationEvent
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public string FailureReason { get; init; } = "Payment operation failed.";
    public bool IsRetryable { get; init; }
}
