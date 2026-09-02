namespace NotificationWorker.Infrastructure.Notifications;

public sealed class NotificationDeliveryRecoveryOptions
{
    public const string SectionName = "NotificationDeliveryRecovery";

    public int ScanIntervalSeconds { get; init; } = 10;
    public int FailureGraceSeconds { get; init; } = 10;
}