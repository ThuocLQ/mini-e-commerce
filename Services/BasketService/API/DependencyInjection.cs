using BasketService.API.Endpoints;
using BasketService.Application.Catalog;
using Microsoft.AspNetCore.Diagnostics;

namespace BasketService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddAuthorization();

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
                    await Results.Json(
                        new
                        {
                            ErrorCode = "DOWNSTREAM_UNAVAILABLE",
                            Message = "CatalogService is unavailable. Please try again later."
                        },
                        statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
                    return;
                }

                if (exception is ArgumentException or InvalidOperationException)
                {
                    await Results.BadRequest(exception.Message).ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
