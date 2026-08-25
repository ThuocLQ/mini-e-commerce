namespace BuildingBlocks.Contracts.Events.Discounts;

public sealed record PromotionRedeemRequestedIntegrationEvent : IntegrationEvent
{
    public Guid ReservationId { get; init; }
    public Guid OrderId { get; init; }
}
