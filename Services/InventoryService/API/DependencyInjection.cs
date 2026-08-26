using InventoryService.API.Endpoints;
using Microsoft.AspNetCore.Diagnostics;

namespace InventoryService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration["InternalApi:Key"];
        if (string.IsNullOrWhiteSpace(key) || (!environment.IsDevelopment() && key.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("InternalApi:Key must be configured.");
        }

        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddMicroShopJwtAuthentication(configuration, environment);

        return services;
    }

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapInventoryEndpoints();
        return app;
    }

    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(context => Results.Problem().ExecuteAsync(context)));
        return app;
    }
}
