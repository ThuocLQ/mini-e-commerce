using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaProjectionPublisher : IDisposable
{
    private readonly KafkaOptions _options;
    private readonly IProducer<string, string> _producer;

    public KafkaProjectionPublisher(IOptions<KafkaOptions> options)
    {
        _options = options.Value;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All
        }).Build();
    }

    public Task<DeliveryResult<string, string>> PublishRetryAsync(
        ConsumeResult<string, string> source,
        int retryCount,
        DateTime notBeforeUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        return _producer.ProduceAsync(
            _options.RetryTopic,
            new Message<string, string>
            {
                Key = source.Message.Key,
                Value = source.Message.Value,
                Headers = KafkaProjectionHeaders.BuildRetryHeaders(
                    source,
                    retryCount,
                    notBeforeUtc,
                    reason)
            },
            cancellationToken);
    }

    public Task<DeliveryResult<string, string>> PublishDeadLetterAsync(
        ConsumeResult<string, string> source,
        int retryCount,
        string failureKind,
        string reason,
        CancellationToken cancellationToken)
    {
        return _producer.ProduceAsync(
            _options.DeadLetterTopic,
            new Message<string, string>
            {
                Key = source.Message.Key,
                Value = source.Message.Value,
                Headers = KafkaProjectionHeaders.BuildDeadLetterHeaders(
                    source,
                    retryCount,
                    failureKind,
                    reason)
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
