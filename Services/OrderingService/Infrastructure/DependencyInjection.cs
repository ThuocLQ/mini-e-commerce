using Microsoft.Extensions.DependencyInjection;
using OrderingService.Application.Abstractions;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IOrderRepository, DapperOrderRepository>();

        return services;
    }
}
