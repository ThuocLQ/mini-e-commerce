namespace NotificationWorker.Application.Abstractions;

public interface INotificationDeliveryStore
{
    Task<NotificationDeliveryLeaseAcquisition> TryStartAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken = default);

    Task<bool> MarkSentAsync(
        Guid deliveryId,
        Guid leaseToken,
        CancellationToken cancellationToken = default);

    Task<int> MarkExhaustedAsDeadLetterAsync(
        int maxAttempts,
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default);
    Task<bool> MarkRetryableFailureAsync(
        Guid deliveryId,
        Guid leaseToken,
        Exception exception,
        CancellationToken cancellationToken = default);
}

public sealed record NotificationDelivery(
    Guid EventId,
    string EventType,
    string Template,
    string Channel,
    Guid CustomerId,
    Guid? OrderId,
    string? CorrelationId);

public sealed record NotificationDeliveryLeaseAcquisition(
    NotificationDeliveryStartResult Result,
    Guid? DeliveryId = null,
    Guid? LeaseToken = null);

public enum NotificationDeliveryStartResult
{
    Started = 1,
    AlreadySent = 2,
    AlreadyProcessing = 3
}