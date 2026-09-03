using BuildingBlocks.Contracts.Events.Identity;
using MassTransit;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications.HandleEmailVerification;

namespace NotificationWorker.Infrastructure.Messaging;

public sealed class CustomerEmailVerificationRequestedIntegrationEventConsumer : IConsumer<CustomerEmailVerificationRequestedIntegrationEvent>
{
    private readonly EmailVerificationNotificationHandler _handler;

    public CustomerEmailVerificationRequestedIntegrationEventConsumer(EmailVerificationNotificationHandler handler) => _handler = handler;

    public Task Consume(ConsumeContext<CustomerEmailVerificationRequestedIntegrationEvent> context) =>
        _handler.HandleAsync(new EmailVerificationNotification(
            context.Message.EventId,
            context.Message.CustomerId,
            context.Message.Token,
            context.Message.ExpiresAtUtc,
            context.Message.CorrelationId ?? context.Headers.Get<string>("X-Correlation-ID")),
            context.CancellationToken);
}