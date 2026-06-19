namespace PaymentService.Infrastructure.Outbox;

public sealed class PaymentOutboxDispatcherOptions
{
    public const string SectionName = "PaymentOutboxDispatcher";

    public bool Enabled { get; init; } = true;
    public int BatchSize { get; init; } = 20;
    public int IntervalSeconds { get; init; } = 5;
    public int MaxRetryCount { get; init; } = 10;
    public int LockSeconds { get; init; } = 60;
    public int RetryDelaySeconds { get; init; } = 15;
    public int MaxRetryDelaySeconds { get; init; } = 300;
}
