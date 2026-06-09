namespace BuildingBlocks.Contracts.Events;

public sealed record MicroShopEventEnvelope<TData>
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = string.Empty;
    public int EventVersion { get; init; } = 1;
    public string Source { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string Subject { get; init; } = string.Empty;
    public TData Data { get; init; } = default!;
}
