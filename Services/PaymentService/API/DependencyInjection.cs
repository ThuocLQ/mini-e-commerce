using Microsoft.AspNetCore.Diagnostics;
using PaymentService.API.Endpoints;

namespace PaymentService.API;

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
        app.MapPaymentEndpoints();
        app.MapWebhookEndpoints();

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
                    await Results.BadRequest(new
                    {
                        error = exception.Message
                    }).ExecuteAsync(context);
                    return;
                }

                if (exception is UnauthorizedAccessException)
                {
                    await Results.Unauthorized().ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
