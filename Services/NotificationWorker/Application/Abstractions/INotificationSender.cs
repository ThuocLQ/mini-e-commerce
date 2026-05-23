namespace NotificationWorker.Application.Abstractions;

public interface INotificationSender
{
    Task SendOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken);
}
