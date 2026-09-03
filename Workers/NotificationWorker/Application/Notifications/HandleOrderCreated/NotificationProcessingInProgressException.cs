namespace NotificationWorker.Application.Notifications.HandleOrderCreated;

public sealed class NotificationProcessingInProgressException : Exception
{
    public NotificationProcessingInProgressException(Guid eventId)
        : base($"Notification event '{eventId:D}' is still being processed by another consumer instance.")
    {
        EventId = eventId;
    }

    public Guid EventId { get; }
}
