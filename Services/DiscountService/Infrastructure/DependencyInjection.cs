using DiscountService.Application.Abstractions;
using DiscountService.Infrastructure.Persistence;

namespace DiscountService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IDiscountRepository, DapperDiscountRepository>();

        return services;
    }
}
