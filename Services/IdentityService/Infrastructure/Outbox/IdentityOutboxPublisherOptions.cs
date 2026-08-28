namespace IdentityService.Infrastructure.Outbox;

public sealed class IdentityOutboxPublisherOptions
{
    public const string SectionName = "IdentityOutboxPublisher";
    public bool Enabled { get; init; } = true;
    public int BatchSize { get; init; } = 20;
    public int IntervalSeconds { get; init; } = 5;
    public int LockSeconds { get; init; } = 60;
}