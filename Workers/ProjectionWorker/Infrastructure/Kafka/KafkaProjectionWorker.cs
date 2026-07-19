using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectionWorker.Infrastructure.MongoDb;

namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaProjectionWorker : BackgroundService
{
    private readonly KafkaOptions _options;
    private readonly MongoDbOptions _mongoOptions;
    private readonly IMongoProjectionInitializer _mongoProjectionInitializer;
    private readonly KafkaProjectionMessageProcessor _messageProcessor;
    private readonly KafkaProjectionFailureRouter _failureRouter;
    private readonly ILogger<KafkaProjectionWorker> _logger;

    public KafkaProjectionWorker(
        IOptions<KafkaOptions> options,
        IOptions<MongoDbOptions> mongoOptions,
        IMongoProjectionInitializer mongoProjectionInitializer,
        KafkaProjectionMessageProcessor messageProcessor,
        KafkaProjectionFailureRouter failureRouter,
        ILogger<KafkaProjectionWorker> logger)
    {
        _options = options.Value;
        _mongoOptions = mongoOptions.Value;
        _mongoProjectionInitializer = mongoProjectionInitializer;
        _messageProcessor = messageProcessor;
        _failureRouter = failureRouter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _mongoProjectionInitializer.InitializeAsync(stoppingToken);

        using var consumer = BuildConsumer();
        var subscribedTopics = _mongoOptions.RebuildModeEnabled
            ? new[] { _options.Topic }
            : new[] { _options.Topic, _options.RetryTopic };
        consumer.Subscribe(subscribedTopics);

        _logger.LogInformation(
            "ProjectionWorker subscribed to Kafka topics {Topics} with group {GroupId}. RetryTopic={RetryTopic}, DeadLetterTopic={DeadLetterTopic}, MaxRetryAttempts={MaxRetryAttempts}, RebuildMode={RebuildMode}, TargetCollection={TargetCollection}.",
            string.Join(", ", subscribedTopics),
            _options.GroupId,
            _options.RetryTopic,
            _options.DeadLetterTopic,
            _options.MaxRetryAttempts,
            _mongoOptions.RebuildModeEnabled,
            _mongoOptions.EffectiveOrderSummariesCollectionName);

        WarnAboutDefaultRebuildGroup();

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult = null;

            try
            {
                consumeResult = consumer.Consume(stoppingToken);
                await WaitForRetryWindowAsync(consumeResult, stoppingToken);

                var result = await _messageProcessor.ProcessAsync(consumeResult, stoppingToken);
                if (result.Outcome == ProjectionProcessingOutcome.PermanentFailure)
                {
                    await _failureRouter.RoutePermanentFailureAsync(
                        consumeResult,
                        result,
                        stoppingToken);
                }

                Commit(consumer, consumeResult, result.Outcome.ToString());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                _logger.LogError(exception, "Kafka consume failed. Retrying consumer poll.");
                await DelayAfterConsumerErrorAsync(stoppingToken);
            }
            catch (KafkaException exception)
            {
                _logger.LogError(
                    exception,
                    "Kafka publish or commit failed. Source offset was not acknowledged and may be replayed.");
                await DelayAfterConsumerErrorAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                if (consumeResult is null)
                {
                    _logger.LogError(exception, "ProjectionWorker failed before receiving a Kafka message.");
                    await DelayAfterConsumerErrorAsync(stoppingToken);
                    continue;
                }

                await RouteTransientFailureSafelyAsync(
                    consumer,
                    consumeResult,
                    exception,
                    stoppingToken);
            }
        }

        consumer.Close();
    }

    private IConsumer<string, string> BuildConsumer()
    {
        var maxPollIntervalSeconds = Math.Max(
            300,
            _options.MaxRetryDelaySeconds + 60);
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            EnableAutoCommit = false,
            AutoOffsetReset = ResolveAutoOffsetReset(_options.AutoOffsetReset),
            AllowAutoCreateTopics = false,
            MaxPollIntervalMs = checked(maxPollIntervalSeconds * 1000)
        };

        return new ConsumerBuilder<string, string>(config)
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                _logger.LogInformation(
                    "ProjectionWorker Kafka partitions assigned. Service={Service}, Partitions={Partitions}.",
                    "ProjectionWorker",
                    FormatTopicPartitions(partitions));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                _logger.LogWarning(
                    "ProjectionWorker Kafka partitions revoked during rebalance. Service={Service}, Partitions={Partitions}.",
                    "ProjectionWorker",
                    FormatTopicPartitionOffsets(partitions));
            })
            .SetPartitionsLostHandler((_, partitions) =>
            {
                _logger.LogWarning(
                    "ProjectionWorker Kafka partitions lost during rebalance. Service={Service}, Partitions={Partitions}. Uncommitted messages may be replayed.",
                    "ProjectionWorker",
                    FormatTopicPartitionOffsets(partitions));
            })
            .Build();
    }

    private async Task RouteTransientFailureSafelyAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> consumeResult,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var route = await _failureRouter.RouteTransientFailureAsync(
                consumeResult,
                exception,
                cancellationToken);
            Commit(consumer, consumeResult, route);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception routingException)
        {
            _logger.LogError(
                routingException,
                "Projection retry/DLT routing failed. Topic={Topic}, Partition={Partition}, Offset={Offset}. Source offset was not committed.",
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value);
            await DelayAfterConsumerErrorAsync(cancellationToken);
        }
    }

    private async Task WaitForRetryWindowAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(consumeResult.Topic, _options.RetryTopic, StringComparison.Ordinal))
        {
            return;
        }

        var notBeforeUtc = KafkaProjectionHeaders.ReadNotBeforeUtc(consumeResult.Message.Headers);
        if (notBeforeUtc is null)
        {
            return;
        }

        var remaining = notBeforeUtc.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        var boundedDelay = TimeSpan.FromSeconds(
            Math.Min(remaining.TotalSeconds, _options.MaxRetryDelaySeconds));
        await Task.Delay(boundedDelay, cancellationToken);
    }

    private void Commit(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> consumeResult,
        string outcome)
    {
        consumer.Commit(consumeResult);
        _logger.LogInformation(
            "Projection Kafka offset committed. Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}, Outcome={Outcome}.",
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            consumeResult.Message.Key,
            outcome);
    }

    private async Task DelayAfterConsumerErrorAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(_options.ConsumerErrorDelaySeconds),
            cancellationToken);
    }

    private void WarnAboutDefaultRebuildGroup()
    {
        if (_mongoOptions.RebuildModeEnabled
            && string.Equals(_options.GroupId, "projection-worker", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Projection rebuild mode is enabled with the default consumer group {GroupId}. Use a dedicated rebuild group id to avoid moving the live projection offset.",
                _options.GroupId);
        }
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
