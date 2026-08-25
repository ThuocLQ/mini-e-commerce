namespace BuildingBlocks.Contracts.Events.Discounts;

public sealed record PromotionReleaseRequestedIntegrationEvent : IntegrationEvent
{
    public Guid ReservationId { get; init; }
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
