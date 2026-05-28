using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderQueryService.Application.Abstractions;
using OrderQueryService.Infrastructure.ReadModels.MongoDb;

namespace OrderQueryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "MongoDb:ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName), "MongoDb:DatabaseName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderSummariesCollectionName), "MongoDb:OrderSummariesCollectionName is required.")
            .Validate(options => options.InitializeMaxRetryCount > 0, "MongoDb:InitializeMaxRetryCount must be greater than 0.")
            .Validate(options => options.InitializeRetryDelaySeconds > 0, "MongoDb:InitializeRetryDelaySeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<IOrderSummaryReadRepository, MongoOrderSummaryReadRepository>();
        services.AddSingleton<IMongoReadModelInitializer, MongoReadModelInitializer>();
        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: ["ready"]);

        return services;
    }
}
