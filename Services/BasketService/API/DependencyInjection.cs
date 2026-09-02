using BasketService.API.Endpoints;
using BasketService.Application.Catalog;
using BasketService.Application.Baskets;
using Microsoft.AspNetCore.Diagnostics;
using MicroShop.ServiceDefaults.Diagnostics;

namespace BasketService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddMicroShopJwtAuthentication(configuration, environment);

        return services;
    }

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapBasketEndpoints();

        return app;
    }

    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                if (exception is CatalogUnavailableException)
                {
                    await ApiProblemResults.ServiceUnavailable("CatalogService is unavailable. Please try again later.", "DOWNSTREAM_UNAVAILABLE").ExecuteAsync(context);
                    return;
                }

                if (exception is BasketConcurrencyException)
                {
                    await ApiProblemResults.Conflict(exception.Message, "BASKET_CONCURRENCY_CONFLICT").ExecuteAsync(context);
                    return;
                }

                if (exception is ArgumentException or InvalidOperationException)
                {
                    await ApiProblemResults.BadRequest(exception.Message).ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
