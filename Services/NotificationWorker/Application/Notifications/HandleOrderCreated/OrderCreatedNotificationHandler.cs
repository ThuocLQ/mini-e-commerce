using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Application.Notifications.HandleOrderCreated;

public sealed class OrderCreatedNotificationHandler
{
    private readonly INotificationSender _notificationSender;

    public OrderCreatedNotificationHandler(INotificationSender notificationSender)
    {
        _notificationSender = notificationSender;
    }

    public Task HandleAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        return _notificationSender.SendOrderCreatedAsync(notification, cancellationToken);
    }
}
