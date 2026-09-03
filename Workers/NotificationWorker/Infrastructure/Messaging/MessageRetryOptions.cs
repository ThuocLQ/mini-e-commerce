namespace NotificationWorker.Infrastructure.Messaging;

public sealed class MessageRetryOptions
{
    public const string SectionName = "Messaging:Retry";

    public int RetryCount { get; set; } = 3;
    public int IntervalSeconds { get; set; } = 2;
}
