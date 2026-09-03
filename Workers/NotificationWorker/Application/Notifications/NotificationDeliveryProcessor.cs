using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications.HandleOrderCreated;

namespace NotificationWorker.Application.Notifications;

public sealed class NotificationDeliveryProcessor
{
    private readonly INotificationDeliveryStore _deliveries;
    private readonly ILogger<NotificationDeliveryProcessor> _logger;

    public NotificationDeliveryProcessor(
        INotificationDeliveryStore deliveries,
        ILogger<NotificationDeliveryProcessor> logger)
    {
        _deliveries = deliveries;
        _logger = logger;
    }

    public async Task ProcessAsync(
        NotificationDelivery delivery,
        Func<CancellationToken, Task> send,
        CancellationToken cancellationToken)
    {
        var acquisition = await _deliveries.TryStartAsync(delivery, cancellationToken);
        if (acquisition.Result == NotificationDeliveryStartResult.AlreadySent)
        {
            _logger.LogInformation(
                "Skipping duplicate notification delivery. EventId={EventId}, Template={Template}, Channel={Channel}",
                delivery.EventId,
                delivery.Template,
                delivery.Channel);
            return;
        }

        if (acquisition.Result == NotificationDeliveryStartResult.AlreadyProcessing)
        {
            throw new NotificationProcessingInProgressException(delivery.EventId);
        }

        var deliveryId = acquisition.DeliveryId
            ?? throw new InvalidOperationException("A started notification delivery must include a delivery id.");
        var leaseToken = acquisition.LeaseToken
            ?? throw new InvalidOperationException("A started notification delivery must include a processing lease token.");

        try
        {
            await send(cancellationToken);
            if (!await _deliveries.MarkSentAsync(deliveryId, leaseToken, cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Notification delivery lease was lost before event {delivery.EventId:D} could be marked sent.");
            }
        }
        catch (Exception exception)
        {
            try
            {
                await _deliveries.MarkRetryableFailureAsync(deliveryId, leaseToken, exception, cancellationToken);
            }
            catch (Exception persistenceException)
            {
                _logger.LogError(
                    persistenceException,
                    "Failed to persist notification delivery failure. EventId={EventId}, DeliveryId={DeliveryId}",
                    delivery.EventId,
                    deliveryId);
            }

            throw;
        }
    }
}