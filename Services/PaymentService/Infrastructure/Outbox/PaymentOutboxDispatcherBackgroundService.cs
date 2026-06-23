using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Payments;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Outbox;

namespace PaymentService.Infrastructure.Outbox;

public sealed class PaymentOutboxDispatcherBackgroundService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentOutboxDispatcherBackgroundService> _logger;
    private readonly PaymentOutboxDispatcherOptions _options;

    public PaymentOutboxDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentOutboxDispatcherBackgroundService> logger,
        IOptions<PaymentOutboxDispatcherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Payment outbox dispatcher is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));

        await DispatchPendingMessagesAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DispatchPendingMessagesAsync(stoppingToken);
        }
    }

    private async Task DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var lockId = Guid.NewGuid();

        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IPaymentOutboxRepository>();
        var sagaClient = scope.ServiceProvider.GetRequiredService<OrderingPaymentSagaClient>();

        var messages = await outboxRepository.ClaimPendingAsync(
            _options.BatchSize,
            _options.MaxRetryCount,
            lockId,
            DateTime.UtcNow,
            TimeSpan.FromSeconds(_options.LockSeconds),
            cancellationToken);

        foreach (var message in messages)
        {
            await DispatchMessageAsync(outboxRepository, sagaClient, message, lockId, cancellationToken);
        }
    }

    private async Task DispatchMessageAsync(
        IPaymentOutboxRepository outboxRepository,
        OrderingPaymentSagaClient sagaClient,
        PaymentOutboxMessage message,
        Guid lockId,
        CancellationToken cancellationToken)
    {
        try
        {
            using (CorrelationContext.BeginScope(message.CorrelationId))
            {
                await DispatchIntegrationEventAsync(sagaClient, message, cancellationToken);
            }

            await outboxRepository.MarkAsProcessedAsync(
                message.Id,
                lockId,
                DateTime.UtcNow,
                cancellationToken);

            _logger.LogInformation(
                "Dispatched payment outbox message {OutboxMessageId} of type {OutboxMessageType}.",
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

            _logger.LogWarning(
                ex,
                "Failed to dispatch payment outbox message {OutboxMessageId}. RetryCount={RetryCount}, NextAttemptAtUtc={NextAttemptAtUtc}.",
                message.Id,
                message.RetryCount + 1,
                nextAttemptAtUtc);
        }
    }

    private static async Task DispatchIntegrationEventAsync(
        OrderingPaymentSagaClient sagaClient,
        PaymentOutboxMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Type is nameof(PaymentSucceededIntegrationEvent) ||
            message.Type == typeof(PaymentSucceededIntegrationEvent).FullName)
        {
            var integrationEvent = JsonSerializer.Deserialize<PaymentSucceededIntegrationEvent>(
                message.Content,
                JsonOptions);

            if (integrationEvent is null)
            {
                throw new InvalidOperationException(
                    $"Cannot deserialize outbox message {message.Id} to {nameof(PaymentSucceededIntegrationEvent)}.");
            }

            await sagaClient.ApplyPaymentSucceededAsync(integrationEvent, cancellationToken);
            return;
        }

        if (message.Type is nameof(PaymentFailedIntegrationEvent) ||
            message.Type == typeof(PaymentFailedIntegrationEvent).FullName)
        {
            var integrationEvent = JsonSerializer.Deserialize<PaymentFailedIntegrationEvent>(
                message.Content,
                JsonOptions);

            if (integrationEvent is null)
            {
                throw new InvalidOperationException(
                    $"Cannot deserialize outbox message {message.Id} to {nameof(PaymentFailedIntegrationEvent)}.");
            }

            await sagaClient.ApplyPaymentFailedAsync(integrationEvent, cancellationToken);
            return;
        }

        throw new NotSupportedException($"Unsupported payment outbox message type: {message.Type}");
    }

    private DateTime CalculateNextAttemptAtUtc(int retryCount)
    {
        var delaySeconds = Math.Min(
            _options.MaxRetryDelaySeconds,
            _options.RetryDelaySeconds * Math.Max(1, retryCount));

        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }
}
