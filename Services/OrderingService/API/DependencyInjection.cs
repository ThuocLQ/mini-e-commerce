using Microsoft.AspNetCore.Diagnostics;
using OrderingService.Application.Baskets;
using OrderingService.API.Endpoints;

namespace OrderingService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();

        return services;
    }

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapOrderEndpoints();
        app.MapCheckoutEndpoints();

        return app;
    }

    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                if (exception is BasketUnavailableException)
                {
                    await Results.Json(
                        new
                        {
                            ErrorCode = "DOWNSTREAM_UNAVAILABLE",
                            exception.Message
                        },
                        statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
                    return;
                }

                if (exception is ArgumentException or InvalidOperationException)
                {
                    await Results.BadRequest(new
                    {
                        error = exception.Message
                    }).ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
