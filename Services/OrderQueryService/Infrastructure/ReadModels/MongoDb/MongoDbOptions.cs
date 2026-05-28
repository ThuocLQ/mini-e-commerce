namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = default!;
    public string DatabaseName { get; init; } = "MicroShop_OrderReadDb";
    public string OrderSummariesCollectionName { get; init; } = "order_summaries";
    public int InitializeMaxRetryCount { get; init; } = 10;
    public int InitializeRetryDelaySeconds { get; init; } = 3;
}
