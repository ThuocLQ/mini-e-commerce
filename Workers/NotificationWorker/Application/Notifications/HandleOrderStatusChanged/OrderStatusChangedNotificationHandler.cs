using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications;

namespace NotificationWorker.Application.Notifications.HandleOrderStatusChanged;

public sealed class OrderStatusChangedNotificationHandler
{
    private readonly NotificationDeliveryProcessor _processor;
    private readonly INotificationSender _sender;

    public OrderStatusChangedNotificationHandler(
        NotificationDeliveryProcessor processor,
        INotificationSender sender)
    {
        _processor = processor;
        _sender = sender;
    }

    public Task HandleAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken) =>
        _processor.ProcessAsync(
            new NotificationDelivery(
                notification.EventId,
                "microshop.order.status-changed",
                $"order-status-{notification.CurrentStatus.ToLowerInvariant()}",
                "email",
                notification.CustomerId,
                notification.OrderId,
                notification.CorrelationId),
            token => _sender.SendOrderStatusChangedAsync(notification, token),
            cancellationToken);
}