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

                _logger.LogInformation(
                    "Projection Kafka offset committed. Service={Service}, Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}.",
                    "ProjectionWorker",
                    consumeResult.Topic,
                    consumeResult.Partition.Value,
                    consumeResult.Offset.Value,
                    consumeResult.Message.Key);
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
                    "Projection MongoDB apply failed. Service={Service}, Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}. Offset was not committed.",
                    "ProjectionWorker",
                    consumeResult.Topic,
                    consumeResult.Partition.Value,
                    consumeResult.Offset.Value,
                    consumeResult.Message.Key);
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

        return new ConsumerBuilder<string, string>(config)
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation(
                    "ProjectionWorker Kafka partitions assigned. Service={Service}, Topic={Topic}, Partitions={Partitions}.",
                    "ProjectionWorker",
                    _options.Topic,
                    FormatTopicPartitions(partitions));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogWarning(
                    "ProjectionWorker Kafka partitions revoked during rebalance. Service={Service}, Topic={Topic}, Partitions={Partitions}.",
                    "ProjectionWorker",
                    _options.Topic,
                    FormatTopicPartitionOffsets(partitions));
            })
            .SetPartitionsLostHandler((_, partitions) =>
            {
                _logger.LogWarning(
                    "ProjectionWorker Kafka partitions lost during rebalance. Service={Service}, Topic={Topic}, Partitions={Partitions}. Uncommitted messages may be replayed.",
                    "ProjectionWorker",
                    _options.Topic,
                    FormatTopicPartitionOffsets(partitions));
            })
            .Build();
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
                "Projection event applied. Service={Service}, Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}, EventId={EventId}, EventType={EventType}, OrderId={OrderId}, CustomerId={CustomerId}.",
                "ProjectionWorker",
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value,
                consumeResult.Message.Key,
                orderEvent.EventId,
                orderEvent.EventType,
                orderEvent.OrderId,
                orderEvent.CustomerId);
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

        if (orderEvent.OrderId != Guid.Empty
            && !string.Equals(consumeResult.Message.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
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
            OccurredAtUtc = ToNullableUtc(orderEvent?.OccurredAtUtc)
        };

        await _failureStore.SaveAsync(failure, cancellationToken);

        _logger.LogWarning(
            "Projection message stored as failure. Service={Service}, Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}, EventId={EventId}, EventType={EventType}, OrderId={OrderId}, Reason={Reason}.",
            "ProjectionWorker",
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            consumeResult.Message.Key,
            ToNullableGuid(orderEvent?.EventId),
            orderEvent?.EventType,
            ToNullableGuid(orderEvent?.OrderId),
            error);
    }

    private static Guid? ToNullableGuid(Guid? value)
    {
        return value is null || value.Value == Guid.Empty
            ? null
            : value.Value;
    }

    private static DateTime? ToNullableUtc(DateTime? value)
    {
        return value is null || value.Value == default
            ? null
            : value.Value;
    }

    private static AutoOffsetReset ResolveAutoOffsetReset(string value)
    {
        return string.Equals(value, "Latest", StringComparison.OrdinalIgnoreCase)
            ? AutoOffsetReset.Latest
            : AutoOffsetReset.Earliest;
    }

    private static string FormatTopicPartitions(IEnumerable<TopicPartition> partitions)
    {
        return string.Join(
            ", ",
            partitions.Select(partition => $"{partition.Topic}[{partition.Partition.Value}]"));
    }

    private static string FormatTopicPartitionOffsets(IEnumerable<TopicPartitionOffset> partitions)
    {
        return string.Join(
            ", ",
            partitions.Select(partition => $"{partition.Topic}[{partition.Partition.Value}]@{partition.Offset.Value}"));
    }
}
