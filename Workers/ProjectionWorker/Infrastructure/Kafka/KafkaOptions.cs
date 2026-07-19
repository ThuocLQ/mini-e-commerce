namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string Topic { get; init; } = "microshop.order-events";
    public string RetryTopic { get; init; } = "microshop.order-events.retry";
    public string DeadLetterTopic { get; init; } = "microshop.order-events.dlt";
    public string GroupId { get; init; } = "projection-worker";
    public string AutoOffsetReset { get; init; } = "Earliest";
    public int MaxRetryAttempts { get; init; } = 3;
    public int InitialRetryDelaySeconds { get; init; } = 5;
    public int MaxRetryDelaySeconds { get; init; } = 60;
    public int ConsumerErrorDelaySeconds { get; init; } = 3;
}
