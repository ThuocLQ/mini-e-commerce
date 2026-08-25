using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Inventory;
using InventoryService.Application.Abstractions;
using InventoryService.Domain.Outbox;
using MassTransit;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;

namespace InventoryService.Infrastructure.Outbox;

public sealed class InventoryOutboxPublisherBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryOutboxPublisherBackgroundService> _logger;
    private readonly InventoryOutboxPublisherOptions _options;

    public InventoryOutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryOutboxPublisherBackgroundService> logger,
        IOptions<InventoryOutboxPublisherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Inventory outbox publisher is disabled.");
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
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IInventoryOutboxRepository>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var messages = await outboxRepository.ClaimPendingAsync(
            _options.BatchSize,
            _options.MaxRetryCount,
            lockId,
            TimeSpan.FromSeconds(_options.LockSeconds),
            cancellationToken);

        if (messages.Count > 0)
        {
            MicroShopMetrics.RecordOutboxMessage("InventoryService", "claimed", messages.Count);
        }

        foreach (var message in messages)
        {
            await PublishMessageAsync(outboxRepository, publishEndpoint, message, lockId, cancellationToken);
        }
    }

    private async Task PublishMessageAsync(
        IInventoryOutboxRepository outboxRepository,
        IPublishEndpoint publishEndpoint,
        InventoryOutboxMessage message,
        Guid lockId,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishIntegrationEventAsync(publishEndpoint, message, cancellationToken);

            if (!await outboxRepository.MarkAsProcessedAsync(message.Id, lockId, cancellationToken))
            {
                _logger.LogWarning("Inventory outbox lease was lost before message {OutboxMessageId} could be marked as processed.", message.Id);
                return;
            }

            MicroShopMetrics.RecordOutboxMessage("InventoryService", "published");
            _logger.LogInformation("Published inventory outbox message {OutboxMessageId} of type {OutboxMessageType}.", message.Id, message.Type);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var nextAttemptAtUtc = CalculateNextAttemptAtUtc(message.RetryCount + 1);
            if (!await outboxRepository.MarkAsFailedAsync(message.Id, lockId, exception.Message, nextAttemptAtUtc, cancellationToken))
            {
                _logger.LogWarning("Inventory outbox lease was lost before failed message {OutboxMessageId} could be scheduled for retry.", message.Id);
                return;
            }

            MicroShopMetrics.RecordOutboxMessage("InventoryService", "failed");
            _logger.LogWarning(exception, "Failed to publish inventory outbox message {OutboxMessageId}. RetryCount={RetryCount}, NextAttemptAtUtc={NextAttemptAtUtc}.", message.Id, message.RetryCount + 1, nextAttemptAtUtc);
        }
    }

    private static async Task PublishIntegrationEventAsync(
        IPublishEndpoint publishEndpoint,
        InventoryOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Type is nameof(InventoryCommittedIntegrationEvent) or "BuildingBlocks.Contracts.Events.Inventory.InventoryCommittedIntegrationEvent")
        {
            var integrationEvent = JsonSerializer.Deserialize<InventoryCommittedIntegrationEvent>(message.Content, JsonOptions)
                ?? throw new InvalidOperationException($"Cannot deserialize inventory outbox message {message.Id} to {nameof(InventoryCommittedIntegrationEvent)}.");
            await PublishWithCorrelationAsync(publishEndpoint, integrationEvent, cancellationToken);
            return;
        }

        if (message.Type is nameof(InventoryReleasedIntegrationEvent) or "BuildingBlocks.Contracts.Events.Inventory.InventoryReleasedIntegrationEvent")
        {
            var integrationEvent = JsonSerializer.Deserialize<InventoryReleasedIntegrationEvent>(message.Content, JsonOptions)
                ?? throw new InvalidOperationException($"Cannot deserialize inventory outbox message {message.Id} to {nameof(InventoryReleasedIntegrationEvent)}.");
            await PublishWithCorrelationAsync(publishEndpoint, integrationEvent, cancellationToken);
            return;
        }

        throw new NotSupportedException($"Unsupported inventory outbox message type: {message.Type}");
    }

    private static async Task PublishWithCorrelationAsync<T>(IPublishEndpoint publishEndpoint, T integrationEvent, CancellationToken cancellationToken)
        where T : BuildingBlocks.Contracts.Events.IntegrationEvent
    {
        using var correlationScope = CorrelationContext.BeginScope(integrationEvent.CorrelationId);
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

    private DateTime CalculateNextAttemptAtUtc(int retryCount)
    {
        var delaySeconds = Math.Min(
            _options.MaxRetryDelaySeconds,
            _options.RetryDelaySeconds * Math.Max(1, retryCount));

        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}

