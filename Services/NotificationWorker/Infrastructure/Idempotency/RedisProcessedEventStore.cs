using StackExchange.Redis;
using NotificationWorker.Application.Abstractions;

namespace NotificationWorker.Infrastructure.Idempotency;

public sealed class RedisProcessedEventStore : IProcessedEventStore
{
    private const string Processing = "processing";
    private const string Processed = "processed";
    private static readonly TimeSpan ProcessingTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ProcessedTtl = TimeSpan.FromDays(30);

    private readonly IDatabase _database;

    public RedisProcessedEventStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<ProcessedEventStartResult> TryStartProcessingAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var key = GetKey(eventId);
        if (await _database.StringSetAsync(key, Processing, ProcessingTtl, When.NotExists))
        {
            return ProcessedEventStartResult.Started;
        }

        var status = await _database.StringGetAsync(key);
        return status == Processed
            ? ProcessedEventStartResult.AlreadyProcessed
            : ProcessedEventStartResult.AlreadyProcessing;
    }

    public Task MarkAsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return _database.StringSetAsync(GetKey(eventId), Processed, ProcessedTtl);
    }

    public Task MarkAsFailedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return _database.KeyDeleteAsync(GetKey(eventId));
    }

    private static RedisKey GetKey(Guid eventId) => $"notification-worker:processed-events:{eventId:D}";
}
