using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications.HandleOrderCreated;

namespace NotificationWorker.Application.Notifications.HandleEmailVerification;

public sealed class EmailVerificationNotificationHandler
{
    private readonly IProcessedEventStore _processedEvents;
    private readonly INotificationSender _sender;

    public EmailVerificationNotificationHandler(IProcessedEventStore processedEvents, INotificationSender sender)
    {
        _processedEvents = processedEvents;
        _sender = sender;
    }

    public async Task HandleAsync(EmailVerificationNotification notification, CancellationToken cancellationToken)
    {
        var acquisition = await _processedEvents.TryStartProcessingAsync(notification.EventId, cancellationToken);
        if (acquisition.Result == ProcessedEventStartResult.AlreadyProcessed) return;
        if (acquisition.Result == ProcessedEventStartResult.AlreadyProcessing) throw new NotificationProcessingInProgressException(notification.EventId);

        var leaseToken = acquisition.LeaseToken ?? throw new InvalidOperationException("Verification event processing lease is missing.");
        try
        {
            await _sender.SendEmailVerificationAsync(notification, cancellationToken);
            if (!await _processedEvents.MarkAsProcessedAsync(notification.EventId, leaseToken, cancellationToken))
                throw new InvalidOperationException($"Verification notification lease was lost for event {notification.EventId:D}.");
        }
        catch
        {
            await _processedEvents.MarkAsFailedAsync(notification.EventId, leaseToken, cancellationToken);
            throw;
        }
    }
}