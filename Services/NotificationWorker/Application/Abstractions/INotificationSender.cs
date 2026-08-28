namespace NotificationWorker.Application.Abstractions;

public interface INotificationSender
{
    Task SendOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken);
    Task SendOrderStatusChangedAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken);
    Task SendEmailVerificationAsync(EmailVerificationNotification notification, CancellationToken cancellationToken);
}