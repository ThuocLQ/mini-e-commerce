using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;
using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Application.Events;

namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaProjectionFailureRouter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly KafkaOptions _options;
    private readonly IProjectionFailureStore _failureStore;
    private readonly KafkaRetryPolicy _retryPolicy;
    private readonly KafkaProjectionPublisher _publisher;
    private readonly ILogger<KafkaProjectionFailureRouter> _logger;

    public KafkaProjectionFailureRouter(
        IOptions<KafkaOptions> options,
        IProjectionFailureStore failureStore,
        KafkaRetryPolicy retryPolicy,
        KafkaProjectionPublisher publisher,
        ILogger<KafkaProjectionFailureRouter> logger)
    {
        _options = options.Value;
        _failureStore = failureStore;
        _retryPolicy = retryPolicy;
        _publisher = publisher;
        _logger = logger;
    }

    internal async Task RoutePermanentFailureAsync(
        ConsumeResult<string, string> consumeResult,
        ProjectionProcessingResult result,
        CancellationToken cancellationToken)
    {
        var retryCount = KafkaProjectionHeaders.ReadRetryCount(consumeResult.Message.Headers);
        await SaveFailureBestEffortAsync(
            consumeResult,
            result.OrderEvent,
            result.Error!,
            "permanent",
            retryCount,
            cancellationToken);
        await _publisher.PublishDeadLetterAsync(
            consumeResult,
            retryCount,
            "permanent",
            result.Error!,
            cancellationToken);
        MicroShopMetrics.RecordProjectionEvent("failed", result.OrderEvent?.EventType);

        _logger.LogWarning(
            "Projection message sent to DLT. Topic={Topic}, Partition={Partition}, Offset={Offset}, DltTopic={DltTopic}, RetryCount={RetryCount}, FailureKind={FailureKind}, Reason={Reason}.",
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            _options.DeadLetterTopic,
            retryCount,
            "permanent",
            result.Error);
    }

    internal async Task<string> RouteTransientFailureAsync(
        ConsumeResult<string, string> consumeResult,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var currentRetryCount = KafkaProjectionHeaders.ReadRetryCount(consumeResult.Message.Headers);
        var reason = $"{exception.GetType().Name}: {exception.Message}";

        if (_retryPolicy.CanRetry(currentRetryCount))
        {
            var nextRetryCount = currentRetryCount + 1;
            var delay = _retryPolicy.GetDelay(nextRetryCount);
            var notBeforeUtc = DateTime.UtcNow.Add(delay);

            await _publisher.PublishRetryAsync(
                consumeResult,
                nextRetryCount,
                notBeforeUtc,
                reason,
                cancellationToken);
            MicroShopMetrics.RecordProjectionEvent(
                "retry_scheduled",
                TryDeserialize(consumeResult.Message.Value)?.EventType);

            _logger.LogWarning(
                exception,
                "Projection transient failure routed to retry topic. SourceTopic={SourceTopic}, Partition={Partition}, Offset={Offset}, RetryTopic={RetryTopic}, RetryCount={RetryCount}, NotBeforeUtc={NotBeforeUtc}.",
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value,
                _options.RetryTopic,
                nextRetryCount,
                notBeforeUtc);

            return "RetryScheduled";
        }

        var orderEvent = TryDeserialize(consumeResult.Message.Value);
        await SaveFailureBestEffortAsync(
            consumeResult,
            orderEvent,
            reason,
            "transient_exhausted",
            currentRetryCount,
            cancellationToken);
        await _publisher.PublishDeadLetterAsync(
            consumeResult,
            currentRetryCount,
            "transient_exhausted",
            reason,
            cancellationToken);
        MicroShopMetrics.RecordProjectionEvent("failed", orderEvent?.EventType);

        _logger.LogError(
            exception,
            "Projection retries exhausted; message sent to DLT. SourceTopic={SourceTopic}, Partition={Partition}, Offset={Offset}, DltTopic={DltTopic}, RetryCount={RetryCount}.",
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            _options.DeadLetterTopic,
            currentRetryCount);

        return "DeadLettered";
    }

    private async Task SaveFailureBestEffortAsync(
        ConsumeResult<string, string> consumeResult,
        OrderProjectionEvent? orderEvent,
        string error,
        string failureKind,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var failure = new ProjectionFailure
        {
            EventId = ToNullableGuid(orderEvent?.EventId),
            CorrelationId = orderEvent?.CorrelationId,
            Topic = consumeResult.Topic,
            Partition = consumeResult.Partition.Value,
            Offset = consumeResult.Offset.Value,
            Key = consumeResult.Message.Key,
            RawValue = consumeResult.Message.Value,
            Error = error,
            FailureKind = failureKind,
            RetryCount = retryCount,
            DeadLetterTopic = _options.DeadLetterTopic,
            OccurredAtUtc = ToNullableUtc(orderEvent?.OccurredAtUtc)
        };

        try
        {
            await _failureStore.SaveAsync(failure, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Failed to persist projection failure metadata. The Kafka DLT remains the durable fallback. Topic={Topic}, Partition={Partition}, Offset={Offset}.",
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);
        }
    }

    private static OrderProjectionEvent? TryDeserialize(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<OrderProjectionEvent>(value, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? ToNullableGuid(Guid? value)
    {
        return value is null || value.Value == Guid.Empty ? null : value.Value;
    }

    private static DateTime? ToNullableUtc(DateTime? value)
    {
        return value is null || value.Value == default ? null : value.Value;
    }
}
