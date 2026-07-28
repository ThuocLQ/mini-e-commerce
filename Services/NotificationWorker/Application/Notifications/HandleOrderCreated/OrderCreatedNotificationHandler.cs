using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Application.Notifications.HandleOrderCreated;

public sealed class OrderCreatedNotificationHandler
{
    private readonly IProcessedEventStore _processedEventStore;
    private readonly INotificationSender _notificationSender;
    private readonly ILogger<OrderCreatedNotificationHandler> _logger;

    public OrderCreatedNotificationHandler(
        IProcessedEventStore processedEventStore,
        INotificationSender notificationSender,
        ILogger<OrderCreatedNotificationHandler> logger)
    {
        _processedEventStore = processedEventStore;
        _notificationSender = notificationSender;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        var acquisition = await _processedEventStore.TryStartProcessingAsync(
            notification.EventId,
            cancellationToken);

        if (acquisition.Result == ProcessedEventStartResult.AlreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping duplicate OrderCreatedIntegrationEvent. EventId={EventId}, OrderId={OrderId}",
                notification.EventId,
                notification.OrderId);

            return;
        }

        if (acquisition.Result == ProcessedEventStartResult.AlreadyProcessing)
        {
            _logger.LogWarning(
                "Deferring concurrent OrderCreatedIntegrationEvent delivery until its processing lease is released. EventId={EventId}, OrderId={OrderId}",
                notification.EventId,
                notification.OrderId);

            throw new NotificationProcessingInProgressException(notification.EventId);
        }

        var leaseToken = acquisition.LeaseToken
            ?? throw new InvalidOperationException("A started notification event must include a processing lease token.");

        try
        {
            await _notificationSender.SendOrderCreatedAsync(notification, cancellationToken);
            var markedProcessed = await _processedEventStore.MarkAsProcessedAsync(
                notification.EventId,
                leaseToken,
                cancellationToken);

            if (!markedProcessed)
            {
                throw new InvalidOperationException(
                    $"Notification processing lease was lost before event {notification.EventId:D} could be completed.");
            }
        }
        catch
        {
            await _processedEventStore.MarkAsFailedAsync(
                notification.EventId,
                leaseToken,
                cancellationToken);
            throw;
        }
    }
}
