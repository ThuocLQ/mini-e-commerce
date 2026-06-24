using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Outbox;

namespace OrderingService.Infrastructure.Outbox;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;
    private readonly OutboxPublisherOptions _options;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger,
        IOptions<OutboxPublisherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
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

    private static async Task PublishIntegrationEventAsync(
        IPublishEndpoint publishEndpoint,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
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

        throw new NotSupportedException($"Unsupported outbox message type: {message.Type}");
    }

    private DateTime CalculateNextAttemptAtUtc(int retryCount)
    {
        var delaySeconds = Math.Min(
            _options.MaxRetryDelaySeconds,
            _options.RetryDelaySeconds * Math.Max(1, retryCount));

        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}
