using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderingService.Application.Abstractions;
using OrderingService.Infrastructure.Clients;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IOrderRepository, DapperOrderRepository>();

        var basketBaseUrl = configuration["ServiceUrls:BasketHttp"]
                            ?? throw new InvalidOperationException("ServiceUrls:BasketHttp is missing.");

        services.AddHttpClient<IBasketClient, HttpBasketClient>(client =>
        {
            client.BaseAddress = new Uri(basketBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
