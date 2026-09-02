using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications;

namespace NotificationWorker.Application.Notifications.HandleEmailVerification;

public sealed class EmailVerificationNotificationHandler
{
    private readonly NotificationDeliveryProcessor _processor;
    private readonly INotificationSender _sender;

    public EmailVerificationNotificationHandler(
        NotificationDeliveryProcessor processor,
        INotificationSender sender)
    {
        _processor = processor;
        _sender = sender;
    }

    public Task HandleAsync(EmailVerificationNotification notification, CancellationToken cancellationToken) =>
        _processor.ProcessAsync(
            new NotificationDelivery(
                notification.EventId,
                "microshop.identity.email-verification-requested",
                "email-verification",
                "email",
                notification.CustomerId,
                null,
                notification.CorrelationId),
            token => _sender.SendEmailVerificationAsync(notification, token),
            cancellationToken);
}