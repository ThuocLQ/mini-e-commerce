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
        var startResult = await _processedEventStore.TryStartProcessingAsync(
            notification.EventId,
            cancellationToken);

        if (startResult == ProcessedEventStartResult.AlreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping duplicate OrderCreatedIntegrationEvent. EventId={EventId}, OrderId={OrderId}",
                notification.EventId,
                notification.OrderId);

            return;
        }

        if (startResult == ProcessedEventStartResult.AlreadyProcessing)
        {
            _logger.LogInformation(
                "Skipping concurrent OrderCreatedIntegrationEvent delivery. EventId={EventId}, OrderId={OrderId}",
                notification.EventId,
                notification.OrderId);

            return;
        }

        try
        {
            await _notificationSender.SendOrderCreatedAsync(notification, cancellationToken);
            await _processedEventStore.MarkAsProcessedAsync(notification.EventId, cancellationToken);
        }
        catch
        {
            await _processedEventStore.MarkAsFailedAsync(notification.EventId, cancellationToken);
            throw;
        }
    }
}
