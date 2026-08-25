namespace BuildingBlocks.Contracts.Events.Payments;

public sealed record PaymentVoidedIntegrationEvent : PaymentOperationCompletedIntegrationEvent
{
    public string Reason { get; init; } = string.Empty;
}
