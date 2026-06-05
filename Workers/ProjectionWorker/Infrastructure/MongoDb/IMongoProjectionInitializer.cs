namespace ProjectionWorker.Infrastructure.MongoDb;

public interface IMongoProjectionInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
