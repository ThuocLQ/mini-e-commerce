namespace OrderingService.Domain.Outbox;

public static class OutboxTransport
{
    public const string RabbitMq = "RabbitMq";
    public const string Kafka = "Kafka";
}
