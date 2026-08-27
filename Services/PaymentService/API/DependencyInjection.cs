using Microsoft.AspNetCore.Diagnostics;
using PaymentService.API.Endpoints;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Application.Payments.Providers;
using PaymentService.Infrastructure.Clients;

namespace PaymentService.API;

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

                if (exception is PaymentWebhookIntegrityException)
                {
                    await Results.Conflict(new
                    {
                        error = exception.Message
                    }).ExecuteAsync(context);
                    return;
                }

                if (exception is PaymentActionIdempotencyConflictException)
                {
                    await Results.Conflict(new { error = exception.Message }).ExecuteAsync(context);
                    return;
                }

                if (exception is PaymentOrderNotAccessibleException)
                {
                    await Results.NotFound(new { error = "Order was not found." }).ExecuteAsync(context);
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

                if (exception is UnauthorizedAccessException)
                {
                    await Results.Unauthorized().ExecuteAsync(context);
                    return;
                }

                if (exception is KeyNotFoundException)
                {
                    await Results.NotFound(new { error = exception.Message }).ExecuteAsync(context);
                    return;
                }

                if (exception is OrderServiceUnavailableException)
                {
                    await Results.Json(
                        new { errorCode = "ORDERING_UNAVAILABLE", message = exception.Message },
                        statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
                    return;
                }

                await Results.Problem().ExecuteAsync(context);
            });
        });

        return app;
    }
}
