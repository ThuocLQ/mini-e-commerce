using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.Application.Abstractions;

namespace ProjectionWorker.Infrastructure.MongoDb;

public sealed class MongoProjectionFailureStore : IProjectionFailureStore
{
    private readonly IMongoCollection<ProjectionFailureDocument> _collection;

    public MongoProjectionFailureStore(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var mongoOptions = options.Value;
        var database = mongoClient.GetDatabase(mongoOptions.DatabaseName);

        _collection = database.GetCollection<ProjectionFailureDocument>(
            mongoOptions.ProjectionFailuresCollectionName);
    }

    public async Task SaveAsync(
        ProjectionFailure failure,
        CancellationToken cancellationToken = default)
    {
        var document = new ProjectionFailureDocument
        {
            Id = failure.Id.ToString("D"),
            EventId = failure.EventId?.ToString("D"),
            Topic = failure.Topic,
            Partition = failure.Partition,
            Offset = failure.Offset,
            Key = failure.Key,
            RawValue = failure.RawValue,
            Error = failure.Error,
            OccurredAtUtc = failure.OccurredAtUtc,
            CreatedAtUtc = failure.CreatedAtUtc
        };

        var filter = Builders<ProjectionFailureDocument>.Filter.And(
            Builders<ProjectionFailureDocument>.Filter.Eq(x => x.Topic, document.Topic),
            Builders<ProjectionFailureDocument>.Filter.Eq(x => x.Partition, document.Partition),
            Builders<ProjectionFailureDocument>.Filter.Eq(x => x.Offset, document.Offset));

        await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}
