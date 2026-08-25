namespace BuildingBlocks.Contracts.Events.Payments;

public sealed record PaymentRefundedIntegrationEvent : PaymentOperationCompletedIntegrationEvent
{
    public string Reason { get; init; } = string.Empty;
}
