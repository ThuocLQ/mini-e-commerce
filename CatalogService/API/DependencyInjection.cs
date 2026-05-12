using CatalogService.API.Endpoints;
using CatalogService.API.GrpcServices;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace CatalogService.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddGrpc();

        return services;
    }

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapProductEndpoints();
        app.MapGrpcService<CatalogGrpcService>();

        return app;
    }

    public static WebApplication UseApiExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                if (exception is ValidationException validationException)
                {
                    var errors = validationException.Errors
                        .GroupBy(error => error.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(error => error.ErrorMessage).ToArray());

                    await Results.ValidationProblem(errors).ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
