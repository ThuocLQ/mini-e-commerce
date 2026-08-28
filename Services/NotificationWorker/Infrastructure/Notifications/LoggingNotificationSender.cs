using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Notifications;

public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;
    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) => _logger = logger;

    public Task SendOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken) => LogAsync("Order created", notification.EventId);
    public Task SendOrderStatusChangedAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken) => LogAsync("Order status changed", notification.EventId);
    public Task SendEmailVerificationAsync(EmailVerificationNotification notification, CancellationToken cancellationToken) => LogAsync("Email verification", notification.EventId);
    private Task LogAsync(string type, Guid eventId) { _logger.LogInformation("Simulating {NotificationType} notification. EventId={EventId}", type, eventId); return Task.CompletedTask; }
}