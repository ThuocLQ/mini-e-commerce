using BuildingBlocks.Contracts.Correlation;
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
        var correlationId = message.CorrelationId
                            ?? context.Headers.Get<string>("X-Correlation-ID");

        using (CorrelationContext.BeginScope(correlationId))
        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            _logger.LogInformation(
                "OrderCreatedIntegrationEvent received. EventId={EventId}, OrderId={OrderId}, CustomerId={CustomerId}, TotalAmount={TotalAmount}, Currency={Currency}, OccurredAtUtc={OccurredAtUtc}, Version={Version}, CorrelationId={CorrelationId}",
                message.EventId,
                message.OrderId,
                message.CustomerId,
                message.TotalAmount,
                message.Currency,
                message.OccurredAtUtc,
                message.Version,
                correlationId);

            var notification = new OrderCreatedNotification(
                message.EventId,
                message.OrderId,
                message.CustomerId,
                message.TotalAmount,
                message.Currency,
                message.OccurredAtUtc,
                message.Version,
                correlationId);

            await _handler.HandleAsync(notification, context.CancellationToken);
        }
    }
}
