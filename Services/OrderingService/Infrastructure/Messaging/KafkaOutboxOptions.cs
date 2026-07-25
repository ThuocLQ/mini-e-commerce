namespace OrderingService.Infrastructure.Messaging;

public sealed class KafkaOutboxOptions
{
    public const string SectionName = "KafkaOutbox";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string Topic { get; init; } = "microshop.order-events";
}
