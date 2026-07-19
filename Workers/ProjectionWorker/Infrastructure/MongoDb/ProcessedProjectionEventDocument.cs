using MongoDB.Bson.Serialization.Attributes;

namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class ProcessedProjectionEventDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; init; } = default!;

    [BsonElement("eventId")]
    public string EventId { get; init; } = default!;

    [BsonElement("orderId")]
    public string OrderId { get; init; } = default!;

    [BsonElement("eventType")]
    public string EventType { get; init; } = default!;

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; init; }

    [BsonElement("processedAtUtc")]
    public DateTime ProcessedAtUtc { get; init; }
}
