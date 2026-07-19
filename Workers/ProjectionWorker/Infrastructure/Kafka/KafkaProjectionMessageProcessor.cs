using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;
using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Application.Events;
using ProjectionWorker.Application.Projections;
using ProjectionWorker.Infrastructure.MongoDb;

namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaProjectionMessageProcessor
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly MongoDbOptions _mongoOptions;
    private readonly OrderProjectionHandler _projectionHandler;
    private readonly IProcessedProjectionEventStore _processedEventStore;
    private readonly ILogger<KafkaProjectionMessageProcessor> _logger;

    public KafkaProjectionMessageProcessor(
        IOptions<MongoDbOptions> mongoOptions,
        OrderProjectionHandler projectionHandler,
        IProcessedProjectionEventStore processedEventStore,
        ILogger<KafkaProjectionMessageProcessor> logger)
    {
        _mongoOptions = mongoOptions.Value;
        _projectionHandler = projectionHandler;
        _processedEventStore = processedEventStore;
        _logger = logger;
    }

    internal async Task<ProjectionProcessingResult> ProcessAsync(
        Confluent.Kafka.ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        OrderProjectionEvent? orderEvent;

        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderProjectionEvent>(
                consumeResult.Message.Value,
                JsonSerializerOptions);
        }
        catch (JsonException exception)
        {
            return PermanentFailure(null, exception.Message);
        }

        if (orderEvent is null)
        {
            return PermanentFailure(null, "Kafka message body is empty.");
        }

        try
        {
            ValidateMessageKey(consumeResult, orderEvent);
            OrderProjectionHandler.Validate(orderEvent);
        }
        catch (ArgumentException exception)
        {
            return PermanentFailure(orderEvent, exception.Message);
        }

        using (CorrelationContext.BeginScope(orderEvent.CorrelationId))
        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["CorrelationId"] = orderEvent.CorrelationId,
                   ["EventId"] = orderEvent.EventId,
                   ["OrderId"] = orderEvent.OrderId
               }))
        {
            if (!_mongoOptions.RebuildModeEnabled
                && await _processedEventStore.IsProcessedAsync(orderEvent.EventId, cancellationToken))
            {
                MicroShopMetrics.RecordProjectionEvent("duplicate", orderEvent.EventType);
                _logger.LogInformation(
                    "Projection duplicate skipped. EventId={EventId}, EventType={EventType}, OrderId={OrderId}.",
                    orderEvent.EventId,
                    orderEvent.EventType,
                    orderEvent.OrderId);
                return new ProjectionProcessingResult(
                    ProjectionProcessingOutcome.Duplicate,
                    orderEvent,
                    null);
            }

            await _projectionHandler.ApplyAsync(orderEvent, cancellationToken);

            if (!_mongoOptions.RebuildModeEnabled)
            {
                await _processedEventStore.MarkProcessedAsync(
                    new ProcessedProjectionEvent
                    {
                        EventId = orderEvent.EventId,
                        OrderId = orderEvent.OrderId,
                        EventType = orderEvent.EventType,
                        OccurredAtUtc = orderEvent.OccurredAtUtc
                    },
                    cancellationToken);
            }

            MicroShopMetrics.RecordProjectionEvent("applied", orderEvent.EventType);
            _logger.LogInformation(
                "Projection event applied. Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}, EventId={EventId}, EventType={EventType}, OrderId={OrderId}, CustomerId={CustomerId}, CorrelationId={CorrelationId}.",
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value,
                consumeResult.Message.Key,
                orderEvent.EventId,
                orderEvent.EventType,
                orderEvent.OrderId,
                orderEvent.CustomerId,
                orderEvent.CorrelationId);
        }

        return new ProjectionProcessingResult(
            ProjectionProcessingOutcome.Applied,
            orderEvent,
            null);
    }

    private static ProjectionProcessingResult PermanentFailure(
        OrderProjectionEvent? orderEvent,
        string error)
    {
        return new ProjectionProcessingResult(
            ProjectionProcessingOutcome.PermanentFailure,
            orderEvent,
            error);
    }

    private static void ValidateMessageKey(
        Confluent.Kafka.ConsumeResult<string, string> consumeResult,
        OrderProjectionEvent orderEvent)
    {
        var expectedKey = orderEvent.OrderId.ToString("D");

        if (orderEvent.OrderId != Guid.Empty
            && !string.Equals(consumeResult.Message.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Kafka message key must match orderId '{expectedKey}'.");
        }
    }
}
