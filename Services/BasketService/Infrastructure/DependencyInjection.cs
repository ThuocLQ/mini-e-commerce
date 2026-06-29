using BasketService.Application.Abstractions;
using BasketService.Infrastructure.Catalog;
using BasketService.Infrastructure.Persistence;
using CatalogService.Grpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;
using StackExchange.Redis;

namespace BasketService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>();

        var redisConnectionString = configuration.GetConnectionString("Redis")
                                    ?? redisOptions?.ConnectionString;

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException("Redis connection string is missing.");
        }

        var serviceUrls = configuration
            .GetSection(ServiceUrlsOptions.SectionName)
            .Get<ServiceUrlsOptions>();

        if (serviceUrls is null || string.IsNullOrWhiteSpace(serviceUrls.CatalogHttp))
        {
            throw new InvalidOperationException("Catalog HTTP URL is missing.");
        }

        if (string.IsNullOrWhiteSpace(serviceUrls.CatalogGrpc))
        {
            throw new InvalidOperationException("Catalog gRPC URL is missing.");
        }

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<ServiceUrlsOptions>(configuration.GetSection(ServiceUrlsOptions.SectionName));
        services.AddRedisReadinessCheck(redisConnectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddRefitClient<ICatalogApi>()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri(serviceUrls.CatalogHttp);
                client.Timeout = Timeout.InfiniteTimeSpan;
            });

        services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(options =>
        {
            options.Address = new Uri(serviceUrls.CatalogGrpc);
        });

        services.AddScoped<IBasketRepository, RedisBasketRepository>();
        services.AddScoped<ICatalogProductClient, CatalogProductClient>();

        return services;
    }
}
