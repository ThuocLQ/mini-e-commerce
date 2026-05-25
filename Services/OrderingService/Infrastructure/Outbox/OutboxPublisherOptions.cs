namespace OrderingService.Infrastructure.Outbox;

public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    public bool Enabled { get; init; } = true;
    public int BatchSize { get; init; } = 20;
    public int IntervalSeconds { get; init; } = 5;
    public int MaxRetryCount { get; init; } = 10;
    public int LockSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 15;
    public int MaxRetryDelaySeconds { get; init; } = 300;
    public bool SimulatePublishFailure { get; init; }
}
