using BuildingBlocks.Contracts.Events;

namespace BuildingBlocks.Contracts.Events.Identity;

public sealed record CustomerEmailVerificationRequestedIntegrationEvent : IntegrationEvent
{
    public Guid CustomerId { get; init; }
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}