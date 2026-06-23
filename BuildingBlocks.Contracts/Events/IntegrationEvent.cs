namespace BuildingBlocks.Contracts.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public int Version { get; init; } = 1;
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
}
