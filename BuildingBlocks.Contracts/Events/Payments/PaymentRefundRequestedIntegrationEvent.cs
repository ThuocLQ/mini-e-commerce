namespace BuildingBlocks.Contracts.Events.Payments;

public sealed record PaymentRefundRequestedIntegrationEvent : PaymentOperationRequestedIntegrationEvent
{
    public string Reason { get; init; } = string.Empty;
}
