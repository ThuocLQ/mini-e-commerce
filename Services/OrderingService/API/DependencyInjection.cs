using Microsoft.AspNetCore.Diagnostics;
using MicroShop.ServiceDefaults.Diagnostics;
using OrderingService.Application.Baskets;
using OrderingService.Application.Catalog;
using OrderingService.Application.Discounts;
using OrderingService.Application.Inventory;
using OrderingService.Application.Addresses;
using OrderingService.Application.Orders;
using OrderingService.Application.Orders.Checkout;
using OrderingService.Application.Orders.CheckoutQuote;
using OrderingService.API.Endpoints;

namespace OrderingService.API;

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
        app.MapOrderEndpoints();
        app.MapPaymentSagaEndpoints();
        app.MapCheckoutQuoteEndpoints();
        app.MapCheckoutEndpoints();

        if (app is WebApplication webApplication && webApplication.Environment.IsDevelopment())
        {
            app.MapOutboxEndpoints();
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

                if (exception is BasketUnavailableException)
                {
                    await ApiProblemResults.ServiceUnavailable(exception.Message, "DOWNSTREAM_UNAVAILABLE").ExecuteAsync(context);
                    return;
                }

                if (exception is CatalogUnavailableException)
                {
                    await ApiProblemResults.ServiceUnavailable(exception.Message, "DOWNSTREAM_UNAVAILABLE").ExecuteAsync(context);
                    return;
                }

                if (exception is DiscountUnavailableException)
                {
                    await ApiProblemResults.ServiceUnavailable(exception.Message, "DOWNSTREAM_UNAVAILABLE").ExecuteAsync(context);
                    return;
                }

                if (exception is InventoryUnavailableException)
                {
                    await ApiProblemResults.ServiceUnavailable(exception.Message, "DOWNSTREAM_UNAVAILABLE").ExecuteAsync(context);
                    return;
                }

                if (exception is AddressUnavailableException)
                {
                    await ApiProblemResults.ServiceUnavailable(exception.Message, "DOWNSTREAM_UNAVAILABLE").ExecuteAsync(context);
                    return;
                }

                if (exception is InsufficientInventoryException)
                {
                    await ApiProblemResults.Conflict(exception.Message, "CHECKOUT_CONFLICT").ExecuteAsync(context);
                    return;
                }

                if (exception is CheckoutIdempotencyConflictException)
                {
                    await ApiProblemResults.Conflict(exception.Message, "CHECKOUT_CONFLICT").ExecuteAsync(context);
                    return;
                }

                if (exception is CheckoutQuoteConflictException quoteConflict)
                {
                    await ApiProblemResults.Conflict(quoteConflict.Message, quoteConflict.ErrorCode).ExecuteAsync(context);
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
