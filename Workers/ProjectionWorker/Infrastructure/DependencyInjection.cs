using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using ProjectionWorker.Application.Abstractions;
using ProjectionWorker.Infrastructure.Kafka;
using ProjectionWorker.Infrastructure.MongoDb;

namespace ProjectionWorker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<KafkaOptions>()
            .Bind(configuration.GetSection(KafkaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "Kafka:BootstrapServers is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Topic), "Kafka:Topic is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.GroupId), "Kafka:GroupId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AutoOffsetReset), "Kafka:AutoOffsetReset is required.")
            .ValidateOnStart();

        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "MongoDb:ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName), "MongoDb:DatabaseName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderSummariesCollectionName), "MongoDb:OrderSummariesCollectionName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProjectionFailuresCollectionName), "MongoDb:ProjectionFailuresCollectionName is required.")
            .Validate(options => options.InitializeMaxRetryCount > 0, "MongoDb:InitializeMaxRetryCount must be greater than 0.")
            .Validate(options => options.InitializeRetryDelaySeconds > 0, "MongoDb:InitializeRetryDelaySeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<IMongoProjectionInitializer, MongoProjectionInitializer>();
        services.AddSingleton<IOrderSummaryProjectionRepository, MongoOrderSummaryProjectionRepository>();
        services.AddSingleton<IProjectionFailureStore, MongoProjectionFailureStore>();
        services.AddHostedService<KafkaProjectionWorker>();

        return services;
    }
}
