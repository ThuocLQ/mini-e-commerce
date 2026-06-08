using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using MongoDB.Driver;
using OrderQueryService.API.Endpoints;
using OrderQueryService.API.Validation;

namespace OrderQueryService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddValidatorsFromAssemblyContaining<DebugUpsertOrderSummaryRequestValidator>();

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
                    await ApiProblemResults.BadRequest(context, exception.Message).ExecuteAsync(context);
                    return;
                }

                if (exception is MongoException)
                {
                    await ApiProblemResults.ServiceUnavailable(
                        context,
                        "MongoDB read model is unavailable.").ExecuteAsync(context);
                    return;
                }

                await ApiProblemResults.InternalServerError(
                    context,
                    "An unexpected error occurred while processing the request.").ExecuteAsync(context);
            });
        });

        return app;
    }
}
