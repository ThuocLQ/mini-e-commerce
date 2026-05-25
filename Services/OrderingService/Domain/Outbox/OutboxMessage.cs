namespace OrderingService.Domain.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Type { get; init; } = default!;
    public string Content { get; init; } = default!;
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public Guid? LockId { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
