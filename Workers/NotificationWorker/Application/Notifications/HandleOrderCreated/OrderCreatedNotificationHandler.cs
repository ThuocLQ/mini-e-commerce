using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications;

namespace NotificationWorker.Application.Notifications.HandleOrderCreated;

public sealed class OrderCreatedNotificationHandler
{
    private readonly NotificationDeliveryProcessor _processor;
    private readonly INotificationSender _sender;

    public OrderCreatedNotificationHandler(
        NotificationDeliveryProcessor processor,
        INotificationSender sender)
    {
        _processor = processor;
        _sender = sender;
    }

    public Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken) =>
        _processor.ProcessAsync(
            new NotificationDelivery(
                notification.EventId,
                "microshop.order.created",
                "order-created",
                "email",
                notification.CustomerId,
                notification.OrderId,
                notification.CorrelationId),
            token => _sender.SendOrderCreatedAsync(notification, token),
            cancellationToken);
}