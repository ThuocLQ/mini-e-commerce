using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Application.Events;
using ProjectionWorker.Application.Projections;
using ProjectionWorker.Infrastructure.MongoDb;

namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaProjectionWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly KafkaOptions _options;
    private readonly OrderProjectionHandler _projectionHandler;
    private readonly IProjectionFailureStore _failureStore;
    private readonly IMongoProjectionInitializer _mongoProjectionInitializer;
    private readonly ILogger<KafkaProjectionWorker> _logger;

    public KafkaProjectionWorker(
        IOptions<KafkaOptions> options,
        OrderProjectionHandler projectionHandler,
        IProjectionFailureStore failureStore,
        IMongoProjectionInitializer mongoProjectionInitializer,
        ILogger<KafkaProjectionWorker> logger)
    {
        _options = options.Value;
        _projectionHandler = projectionHandler;
        _failureStore = failureStore;
        _mongoProjectionInitializer = mongoProjectionInitializer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _mongoProjectionInitializer.InitializeAsync(stoppingToken);

        using var consumer = BuildConsumer();
        consumer.Subscribe(_options.Topic);

        _logger.LogInformation(
            "ProjectionWorker subscribed to Kafka topic {Topic} with group {GroupId}.",
            _options.Topic,
            _options.GroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult = null;

            try
            {
                consumeResult = consumer.Consume(stoppingToken);

                await ProcessAsync(consumeResult, stoppingToken);

                consumer.Commit(consumeResult);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                _logger.LogError(exception, "Kafka consume failed.");
            }
            catch (KafkaException exception)
            {
                _logger.LogError(exception, "Kafka commit failed. The message may be replayed.");
            }
            catch (Exception exception)
            {
                if (consumeResult is null)
                {
                    _logger.LogError(exception, "ProjectionWorker failed before receiving a Kafka message.");
                    continue;
                }

                _logger.LogError(
                    exception,
                    "Projection failed for topic {Topic}, partition {Partition}, offset {Offset}. Offset was not committed.",
                    consumeResult.Topic,
                    consumeResult.Partition.Value,
                    consumeResult.Offset.Value);
            }
        }

        consumer.Close();
    }

    private IConsumer<string, string> BuildConsumer()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = ResolveAutoOffsetReset(_options.AutoOffsetReset),
            AllowAutoCreateTopics = false
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }

    private async Task ProcessAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        OrderProjectionEvent? orderEvent = null;

        try
        {
            orderEvent = JsonSerializer.Deserialize<OrderProjectionEvent>(
                consumeResult.Message.Value,
                JsonSerializerOptions);

            if (orderEvent is null)
            {
                throw new InvalidOperationException("Kafka message body is empty.");
            }

            ValidateMessageKey(consumeResult, orderEvent);

            await _projectionHandler.ApplyAsync(orderEvent, cancellationToken);

            _logger.LogInformation(
                "Projected {EventType} for order {OrderId} from Kafka offset {TopicPartitionOffset}.",
                orderEvent.EventType,
                orderEvent.OrderId,
                consumeResult.TopicPartitionOffset);
        }
        catch (JsonException exception)
        {
            await SaveFailureAsync(consumeResult, orderEvent, exception.Message, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            await SaveFailureAsync(consumeResult, orderEvent, exception.Message, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await SaveFailureAsync(consumeResult, orderEvent, exception.Message, cancellationToken);
        }
    }

    private static void ValidateMessageKey(
        ConsumeResult<string, string> consumeResult,
        OrderProjectionEvent orderEvent)
    {
        var expectedKey = orderEvent.OrderId.ToString("D");

        if (!string.Equals(consumeResult.Message.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Kafka message key must match orderId '{expectedKey}'.");
        }
    }

    private async Task SaveFailureAsync(
        ConsumeResult<string, string> consumeResult,
        OrderProjectionEvent? orderEvent,
        string error,
        CancellationToken cancellationToken)
    {
        var failure = new ProjectionFailure
        {
            EventId = orderEvent?.EventId == Guid.Empty ? null : orderEvent?.EventId,
            Topic = consumeResult.Topic,
            Partition = consumeResult.Partition.Value,
            Offset = consumeResult.Offset.Value,
            Key = consumeResult.Message.Key,
            RawValue = consumeResult.Message.Value,
            Error = error,
            OccurredAtUtc = orderEvent?.OccurredAtUtc == default ? null : orderEvent?.OccurredAtUtc
        };

        await _failureStore.SaveAsync(failure, cancellationToken);

        _logger.LogWarning(
            "Stored projection failure at {TopicPartitionOffset}: {Error}",
            consumeResult.TopicPartitionOffset,
            error);
    }

    private static AutoOffsetReset ResolveAutoOffsetReset(string value)
    {
        return string.Equals(value, "Latest", StringComparison.OrdinalIgnoreCase)
            ? AutoOffsetReset.Latest
            : AutoOffsetReset.Earliest;
    }
}
