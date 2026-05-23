using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Notifications;

public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Simulating notification: Order {OrderId} was created for customer {CustomerId}. Total={TotalAmount} {Currency}.",
            notification.OrderId,
            notification.CustomerId,
            notification.TotalAmount,
            notification.Currency);

        return Task.CompletedTask;
    }
}
