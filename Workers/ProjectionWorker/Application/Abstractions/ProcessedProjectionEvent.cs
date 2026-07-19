namespace ProjectionWorker.Application.Abstractions;

public sealed class ProcessedProjectionEvent
{
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public string EventType { get; init; } = default!;
    public DateTime OccurredAtUtc { get; init; }
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}
