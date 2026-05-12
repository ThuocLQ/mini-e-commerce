using CatalogService.Application.Abstractions;
using CatalogService.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IProductRepository, DapperProductRepository>();

        return services;
    }
}
