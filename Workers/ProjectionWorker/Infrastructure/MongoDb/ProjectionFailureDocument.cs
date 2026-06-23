using MongoDB.Bson.Serialization.Attributes;

namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class ProjectionFailureDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; init; } = default!;

    [BsonElement("eventId")]
    public string? EventId { get; init; }

    [BsonElement("correlationId")]
    public string? CorrelationId { get; init; }

    [BsonElement("topic")]
    public string Topic { get; init; } = default!;

    [BsonElement("partition")]
    public int Partition { get; init; }

    [BsonElement("offset")]
    public long Offset { get; init; }

    [BsonElement("key")]
    public string? Key { get; init; }

    [BsonElement("rawValue")]
    public string RawValue { get; init; } = default!;

    [BsonElement("error")]
    public string Error { get; init; } = default!;

    [BsonElement("occurredAtUtc")]
    public DateTime? OccurredAtUtc { get; init; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }
}
