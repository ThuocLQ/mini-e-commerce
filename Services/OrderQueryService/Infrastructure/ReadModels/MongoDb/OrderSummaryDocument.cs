using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

internal sealed class OrderSummaryDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; init; } = default!;

    [BsonElement("orderId")]
    public string OrderId { get; init; } = default!;

    [BsonElement("customerId")]
    public string CustomerId { get; init; } = default!;

    [BsonElement("customerName")]
    public string CustomerName { get; init; } = default!;

    [BsonElement("status")]
    public string Status { get; init; } = default!;

    [BsonElement("totalAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalAmount { get; init; }

    [BsonElement("currency")]
    public string Currency { get; init; } = default!;

    [BsonElement("itemCount")]
    public int ItemCount { get; init; }

    [BsonElement("items")]
    public IReadOnlyList<OrderSummaryItemDocument> Items { get; init; } = [];

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [BsonElement("lastUpdatedAtUtc")]
    public DateTime LastUpdatedAtUtc { get; init; }
}

internal sealed class OrderSummaryItemDocument
{
    [BsonElement("productId")]
    public string ProductId { get; init; } = default!;

    [BsonElement("productName")]
    public string ProductName { get; init; } = default!;

    [BsonElement("quantity")]
    public int Quantity { get; init; }

    [BsonElement("unitPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; init; }
}
