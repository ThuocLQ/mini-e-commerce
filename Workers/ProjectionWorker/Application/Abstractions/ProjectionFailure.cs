namespace ProjectionWorker.Application.Abstractions;

public sealed class ProjectionFailure
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? EventId { get; init; }
    public string Topic { get; init; } = default!;
    public int Partition { get; init; }
    public long Offset { get; init; }
    public string? Key { get; init; }
    public string RawValue { get; init; } = default!;
    public string Error { get; init; } = default!;
    public DateTime? OccurredAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
