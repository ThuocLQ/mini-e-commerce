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
            .Validate(options => !string.IsNullOrWhiteSpace(options.RetryTopic), "Kafka:RetryTopic is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DeadLetterTopic), "Kafka:DeadLetterTopic is required.")
            .Validate(options => new[] { options.Topic, options.RetryTopic, options.DeadLetterTopic }.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3, "Kafka topic, retry topic, and dead-letter topic must be different.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.GroupId), "Kafka:GroupId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AutoOffsetReset), "Kafka:AutoOffsetReset is required.")
            .Validate(options => options.MaxRetryAttempts >= 0, "Kafka:MaxRetryAttempts cannot be negative.")
            .Validate(options => options.InitialRetryDelaySeconds > 0, "Kafka:InitialRetryDelaySeconds must be greater than 0.")
            .Validate(options => options.MaxRetryDelaySeconds >= options.InitialRetryDelaySeconds, "Kafka:MaxRetryDelaySeconds must be greater than or equal to Kafka:InitialRetryDelaySeconds.")
            .Validate(options => options.MaxRetryDelaySeconds <= 3600, "Kafka:MaxRetryDelaySeconds cannot exceed 3600 seconds.")
            .Validate(options => options.ConsumerErrorDelaySeconds > 0, "Kafka:ConsumerErrorDelaySeconds must be greater than 0.")
            .ValidateOnStart();

        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "MongoDb:ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName), "MongoDb:DatabaseName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderSummariesCollectionName), "MongoDb:OrderSummariesCollectionName is required.")
            .Validate(options => !options.RebuildModeEnabled || !string.IsNullOrWhiteSpace(options.RebuildOrderSummariesCollectionName), "MongoDb:RebuildOrderSummariesCollectionName is required when rebuild mode is enabled.")
            .Validate(options => !options.RebuildModeEnabled || !string.Equals(options.OrderSummariesCollectionName, options.RebuildOrderSummariesCollectionName, StringComparison.OrdinalIgnoreCase), "MongoDb:RebuildOrderSummariesCollectionName must be different from MongoDb:OrderSummariesCollectionName when rebuild mode is enabled.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProjectionFailuresCollectionName), "MongoDb:ProjectionFailuresCollectionName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProcessedEventsCollectionName), "MongoDb:ProcessedEventsCollectionName is required.")
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
        services.AddSingleton<IProcessedProjectionEventStore, MongoProcessedProjectionEventStore>();
        services.AddSingleton<KafkaRetryPolicy>();
        services.AddSingleton<KafkaProjectionPublisher>();
        services.AddSingleton<KafkaProjectionMessageProcessor>();
        services.AddSingleton<KafkaProjectionFailureRouter>();
        services.AddHostedService<KafkaProjectionWorker>();

        return services;
    }
}
