namespace BuildingBlocks.Contracts.Events.Payments;

public sealed record PaymentVoidRequestedIntegrationEvent : PaymentOperationRequestedIntegrationEvent
{
    public string Reason { get; init; } = string.Empty;
}
