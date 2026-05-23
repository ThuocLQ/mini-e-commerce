using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications.HandleOrderCreated;

namespace NotificationWorker.Infrastructure.Messaging;

public sealed class OrderCreatedIntegrationEventConsumer
    : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly OrderCreatedNotificationHandler _handler;
    private readonly ILogger<OrderCreatedIntegrationEventConsumer> _logger;

    public OrderCreatedIntegrationEventConsumer(
        OrderCreatedNotificationHandler handler,
        ILogger<OrderCreatedIntegrationEventConsumer> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "OrderCreatedIntegrationEvent received. EventId={EventId}, OrderId={OrderId}, CustomerId={CustomerId}, TotalAmount={TotalAmount}, Currency={Currency}, OccurredAtUtc={OccurredAtUtc}, Version={Version}",
            message.EventId,
            message.OrderId,
            message.CustomerId,
            message.TotalAmount,
            message.Currency,
            message.OccurredAtUtc,
            message.Version);

        var notification = new OrderCreatedNotification(
            message.EventId,
            message.OrderId,
            message.CustomerId,
            message.TotalAmount,
            message.Currency,
            message.OccurredAtUtc,
            message.Version);

        await _handler.HandleAsync(notification, context.CancellationToken);
    }
}
