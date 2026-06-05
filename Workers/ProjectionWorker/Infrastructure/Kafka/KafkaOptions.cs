namespace ProjectionWorker.Infrastructure.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string Topic { get; init; } = "microshop.order-events";
    public string GroupId { get; init; } = "projection-worker";
    public string AutoOffsetReset { get; init; } = "Earliest";
}
