using CatalogService.Application.Abstractions;
using CatalogService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IProductRepository, DapperProductRepository>();
        services.AddPostgresReadinessCheck(configuration, "CatalogDb");

        return services;
    }
}
