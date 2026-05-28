using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

public sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoClient _mongoClient;
    private readonly MongoDbOptions _options;

    public MongoDbHealthCheck(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        _mongoClient = mongoClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _mongoClient.GetDatabase(_options.DatabaseName);
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB read model is unavailable.", ex);
        }
    }
}
