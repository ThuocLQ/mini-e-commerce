using Microsoft.AspNetCore.Diagnostics;
using MongoDB.Driver;
using OrderQueryService.API.Endpoints;

namespace OrderQueryService.API;

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
        app.MapOrderSummaryEndpoints();

        if (app is WebApplication webApplication && webApplication.Environment.IsDevelopment())
        {
            app.MapDebugOrderSummaryEndpoints();
        }

        return app;
    }

    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                if (exception is ArgumentException or InvalidOperationException)
                {
                    await Results.BadRequest(new { error = exception.Message }).ExecuteAsync(context);
                    return;
                }

                if (exception is MongoException)
                {
                    await Results.Json(
                        new { error = "MongoDB read model is unavailable." },
                        statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
