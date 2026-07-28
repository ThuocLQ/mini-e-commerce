using NotificationWorker.Application.Abstractions;
using StackExchange.Redis;

namespace NotificationWorker.Infrastructure.Idempotency;

public sealed class RedisProcessedEventStore : IProcessedEventStore
{
    private const string Processed = "processed";
    private static readonly TimeSpan ProcessingTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ProcessedTtl = TimeSpan.FromDays(30);
    private const string MarkProcessedScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            redis.call('SET', KEYS[1], ARGV[2], 'EX', ARGV[3])
            return 1
        end
        return 0
        """;
    private const string ReleaseLeaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            redis.call('DEL', KEYS[1])
            return 1
        end
        return 0
        """;

    private readonly IDatabase _database;

    public RedisProcessedEventStore(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<ProcessedEventLeaseAcquisition> TryStartProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var key = GetKey(eventId);
        var leaseToken = CreateLeaseToken();

        if (await _database.StringSetAsync(key, leaseToken, ProcessingTtl, When.NotExists))
        {
            return new ProcessedEventLeaseAcquisition(ProcessedEventStartResult.Started, leaseToken);
        }

        var status = await _database.StringGetAsync(key);
        return new ProcessedEventLeaseAcquisition(
            status == Processed
                ? ProcessedEventStartResult.AlreadyProcessed
                : ProcessedEventStartResult.AlreadyProcessing);
    }

    public async Task<bool> MarkAsProcessedAsync(
        Guid eventId,
        string leaseToken,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.ScriptEvaluateAsync(
            MarkProcessedScript,
            [GetKey(eventId)],
            [leaseToken, Processed, (long)ProcessedTtl.TotalSeconds]);

        return (int)result == 1;
    }

    public async Task<bool> MarkAsFailedAsync(
        Guid eventId,
        string leaseToken,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.ScriptEvaluateAsync(
            ReleaseLeaseScript,
            [GetKey(eventId)],
            [leaseToken]);

        return (int)result == 1;
    }

    private static RedisKey GetKey(Guid eventId) => $"notification-worker:processed-events:{eventId:D}";

    private static string CreateLeaseToken() => $"processing:{Guid.NewGuid():N}";
}
