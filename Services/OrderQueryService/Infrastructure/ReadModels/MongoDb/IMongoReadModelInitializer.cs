namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

public interface IMongoReadModelInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
