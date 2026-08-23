using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Orders;
using BuildingBlocks.Contracts.Events.Inventory;
using Confluent.Kafka;
using MassTransit;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Outbox;
using OrderingService.Infrastructure.Messaging;

namespace OrderingService.Infrastructure.Outbox;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;
    private readonly OutboxPublisherOptions _options;
    private readonly KafkaOutboxOptions _kafkaOptions;
    private readonly IProducer<string, string> _kafkaProducer;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger,
        IOptions<OutboxPublisherOptions> options,
        IOptions<KafkaOutboxOptions> kafkaOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _kafkaOptions = kafkaOptions.Value;
        _kafkaProducer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All
        }).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox publisher is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));

        await PublishPendingMessagesAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PublishPendingMessagesAsync(stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var lockId = Guid.NewGuid();

        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await outboxRepository.ClaimPendingAsync(
            _options.BatchSize,
            _options.MaxRetryCount,
            lockId,
            DateTime.UtcNow,
            TimeSpan.FromSeconds(_options.LockSeconds),
            cancellationToken);

        if (messages.Count > 0)
        {
            MicroShopMetrics.RecordOutboxMessage("OrderingService", "claimed", messages.Count);
        }

        foreach (var message in messages)
        {
            await PublishMessageAsync(outboxRepository, publishEndpoint, message, lockId, cancellationToken);
        }
    }

    private async Task PublishMessageAsync(
        IOutboxRepository outboxRepository,
        IPublishEndpoint publishEndpoint,
        OutboxMessage message,
        Guid lockId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_options.SimulatePublishFailure)
            {
                throw new InvalidOperationException("Simulated outbox publish failure.");
            }

            await PublishIntegrationEventAsync(publishEndpoint, message, cancellationToken);

            await outboxRepository.MarkAsProcessedAsync(
                message.Id,
                lockId,
                DateTime.UtcNow,
                cancellationToken);

            MicroShopMetrics.RecordOutboxMessage("OrderingService", "published");

            _logger.LogInformation(
                "Published outbox message {OutboxMessageId} of type {OutboxMessageType}.",
                message.Id,
                message.Type);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var nextAttemptAtUtc = CalculateNextAttemptAtUtc(message.RetryCount + 1);

            await outboxRepository.MarkAsFailedAsync(
                message.Id,
                lockId,
                ex.Message,
                nextAttemptAtUtc,
                cancellationToken);

            MicroShopMetrics.RecordOutboxMessage("OrderingService", "failed");

            _logger.LogWarning(
                ex,
                "Failed to publish outbox message {OutboxMessageId}. RetryCount={RetryCount}, NextAttemptAtUtc={NextAttemptAtUtc}.",
                message.Id,
                message.RetryCount + 1,
                nextAttemptAtUtc);
        }
    }

    private async Task PublishIntegrationEventAsync(
        IPublishEndpoint publishEndpoint,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (string.Equals(message.Transport, OutboxTransport.Kafka, StringComparison.Ordinal))
        {
            await PublishKafkaAsync(message, cancellationToken);
            return;
        }

        if (!string.Equals(message.Transport, OutboxTransport.RabbitMq, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported outbox transport: {message.Transport}");
        }

        var orderCreatedTypeName = typeof(OrderCreatedIntegrationEvent).FullName;

        if (message.Type is nameof(OrderCreatedIntegrationEvent) || message.Type == orderCreatedTypeName)
        {
            var integrationEvent = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(
                message.Content,
                JsonOptions);

            if (integrationEvent is null)
            {
                throw new InvalidOperationException(
                    $"Cannot deserialize outbox message {message.Id} to {nameof(OrderCreatedIntegrationEvent)}.");
            }

            using (CorrelationContext.BeginScope(integrationEvent.CorrelationId))
            {
                await publishEndpoint.Publish(integrationEvent, publishContext =>
                {
                    if (!string.IsNullOrWhiteSpace(integrationEvent.CorrelationId))
                    {
                        publishContext.Headers.Set("X-Correlation-ID", integrationEvent.CorrelationId);
                    }

                    if (!string.IsNullOrWhiteSpace(integrationEvent.CausationId))
                    {
                        publishContext.Headers.Set("X-Causation-ID", integrationEvent.CausationId);
                    }
                }, cancellationToken);
            }
            return;
        }

        var orderStatusChangedTypeName = typeof(OrderStatusChangedIntegrationEvent).FullName;
        if (message.Type is nameof(OrderStatusChangedIntegrationEvent) || message.Type == orderStatusChangedTypeName)
        {
            var integrationEvent = JsonSerializer.Deserialize<OrderStatusChangedIntegrationEvent>(message.Content, JsonOptions)
                ?? throw new InvalidOperationException($"Cannot deserialize outbox message {message.Id} to {nameof(OrderStatusChangedIntegrationEvent)}.");

            using (CorrelationContext.BeginScope(integrationEvent.CorrelationId))
            {
                await publishEndpoint.Publish(integrationEvent, publishContext =>
                {
                    if (!string.IsNullOrWhiteSpace(integrationEvent.CorrelationId))
                    {
                        publishContext.Headers.Set("X-Correlation-ID", integrationEvent.CorrelationId);
                    }

                    if (!string.IsNullOrWhiteSpace(integrationEvent.CausationId))
                    {
                        publishContext.Headers.Set("X-Causation-ID", integrationEvent.CausationId);
                    }
                }, cancellationToken);
            }
            return;
        }

        var sagaStateChangedTypeName = typeof(OrderPaymentSagaStateChangedIntegrationEvent).FullName;
        if (message.Type is nameof(OrderPaymentSagaStateChangedIntegrationEvent) || message.Type == sagaStateChangedTypeName)
        {
            var integrationEvent = JsonSerializer.Deserialize<OrderPaymentSagaStateChangedIntegrationEvent>(message.Content, JsonOptions)
                ?? throw new InvalidOperationException($"Cannot deserialize outbox message {message.Id} to {nameof(OrderPaymentSagaStateChangedIntegrationEvent)}.");

            using (CorrelationContext.BeginScope(integrationEvent.CorrelationId))
            {
                await publishEndpoint.Publish(integrationEvent, publishContext =>
                {
                    if (!string.IsNullOrWhiteSpace(integrationEvent.CorrelationId))
                    {
                        publishContext.Headers.Set("X-Correlation-ID", integrationEvent.CorrelationId);
                    }

                    if (!string.IsNullOrWhiteSpace(integrationEvent.CausationId))
                    {
                        publishContext.Headers.Set("X-Causation-ID", integrationEvent.CausationId);
                    }
                }, cancellationToken);
            }
            return;
        }

        var inventoryCommitRequestedTypeName = typeof(InventoryCommitRequestedIntegrationEvent).FullName;
        var inventoryReleaseRequestedTypeName = typeof(InventoryReleaseRequestedIntegrationEvent).FullName;
        if (message.Type is nameof(InventoryCommitRequestedIntegrationEvent) or nameof(InventoryReleaseRequestedIntegrationEvent)
            || message.Type == inventoryCommitRequestedTypeName
            || message.Type == inventoryReleaseRequestedTypeName)
        {
            if (message.Type is nameof(InventoryCommitRequestedIntegrationEvent) || message.Type == inventoryCommitRequestedTypeName)
            {
                var integrationEvent = JsonSerializer.Deserialize<InventoryCommitRequestedIntegrationEvent>(message.Content, JsonOptions)
                    ?? throw new InvalidOperationException($"Cannot deserialize outbox message {message.Id} to {nameof(InventoryCommitRequestedIntegrationEvent)}.");

                using (CorrelationContext.BeginScope(integrationEvent.CorrelationId))
                {
                    await publishEndpoint.Publish(integrationEvent, publishContext =>
                    {
                        SetCorrelationHeaders(publishContext, integrationEvent.CorrelationId, integrationEvent.CausationId);
                    }, cancellationToken);
                }
            }
            else
            {
                var integrationEvent = JsonSerializer.Deserialize<InventoryReleaseRequestedIntegrationEvent>(message.Content, JsonOptions)
                    ?? throw new InvalidOperationException($"Cannot deserialize outbox message {message.Id} to {nameof(InventoryReleaseRequestedIntegrationEvent)}.");

                using (CorrelationContext.BeginScope(integrationEvent.CorrelationId))
                {
                    await publishEndpoint.Publish(integrationEvent, publishContext =>
                    {
                        SetCorrelationHeaders(publishContext, integrationEvent.CorrelationId, integrationEvent.CausationId);
                    }, cancellationToken);
                }
            }
            return;
        }

        throw new NotSupportedException($"Unsupported outbox message type: {message.Type}");
    }

    private async Task PublishKafkaAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // The payload is a versioned contract envelope. The aggregate id is the Kafka key,
        // preserving in-partition order for all transitions of one order.
        var orderId = GetOrderIdFromSubject(message.Content);
        await _kafkaProducer.ProduceAsync(
            _kafkaOptions.Topic,
            new Message<string, string>
            {
                Key = orderId,
                Value = message.Content,
                Headers = new Confluent.Kafka.Headers
                {
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(message.Type) },
                    { "correlation-id", System.Text.Encoding.UTF8.GetBytes(message.CorrelationId ?? string.Empty) },
                    { "causation-id", System.Text.Encoding.UTF8.GetBytes(message.CausationId ?? string.Empty) }
                }
            },
            cancellationToken);
    }

    private static string GetOrderIdFromSubject(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("subject", out var subject)
            || subject.GetString() is not { } value
            || !value.StartsWith("orders/", StringComparison.Ordinal)
            || !Guid.TryParse(value["orders/".Length..], out var orderId))
        {
            throw new InvalidOperationException("Kafka outbox message has an invalid order subject.");
        }

        return orderId.ToString("D");
    }

    private static void SetCorrelationHeaders(PublishContext publishContext, string? correlationId, string? causationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            publishContext.Headers.Set("X-Correlation-ID", correlationId);
        }

        if (!string.IsNullOrWhiteSpace(causationId))
        {
            publishContext.Headers.Set("X-Causation-ID", causationId);
        }
    }

    private DateTime CalculateNextAttemptAtUtc(int retryCount)
    {
        var delaySeconds = Math.Min(
            _options.MaxRetryDelaySeconds,
            _options.RetryDelaySeconds * Math.Max(1, retryCount));

        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    public override void Dispose()
    {
        _kafkaProducer.Flush(TimeSpan.FromSeconds(10));
        _kafkaProducer.Dispose();
        base.Dispose();
    }
}
