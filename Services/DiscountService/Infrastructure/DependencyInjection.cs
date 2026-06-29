using DiscountService.Application.Abstractions;
using DiscountService.Infrastructure.Persistence;

namespace DiscountService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IDiscountRepository, DapperDiscountRepository>();
        services.AddPostgresReadinessCheck(configuration, "DiscountDb");

        return services;
    }
}
