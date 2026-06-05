using Microsoft.Extensions.DependencyInjection;
using ProjectionWorker.Application.Projections;

namespace ProjectionWorker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<OrderProjectionHandler>();

        return services;
    }
}
