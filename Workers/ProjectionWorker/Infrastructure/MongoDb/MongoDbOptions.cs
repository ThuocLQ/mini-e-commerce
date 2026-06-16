namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = default!;
    public string DatabaseName { get; init; } = "MicroShop_OrderReadDb";
    public string OrderSummariesCollectionName { get; init; } = "order_summaries";
    public bool RebuildModeEnabled { get; init; }
    public string RebuildOrderSummariesCollectionName { get; init; } = "order_summaries_rebuild";
    public string ProjectionFailuresCollectionName { get; init; } = "projection_failures";
    public int InitializeMaxRetryCount { get; init; } = 10;
    public int InitializeRetryDelaySeconds { get; init; } = 3;

    public string EffectiveOrderSummariesCollectionName =>
        RebuildModeEnabled
            ? RebuildOrderSummariesCollectionName
            : OrderSummariesCollectionName;
}
