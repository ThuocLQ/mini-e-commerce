using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderingService.Application.Abstractions;
using OrderingService.Infrastructure.Clients;
using OrderingService.Infrastructure.Messaging;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IOrderRepository, DapperOrderRepository>();

        var basketBaseUrl = configuration["ServiceUrls:BasketHttp"]
                            ?? throw new InvalidOperationException("ServiceUrls:BasketHttp is missing.");

        services.AddHttpClient<IBasketClient, HttpBasketClient>(client =>
        {
            client.BaseAddress = new Uri(basketBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        
        services.AddSingleton<IEventBus, RabbitMqEventBus>();

        return services;
    }
}
