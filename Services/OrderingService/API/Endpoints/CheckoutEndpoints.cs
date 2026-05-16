using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderingService.Application.Orders.Checkout;

namespace OrderingService.API.Endpoints;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/checkout", CheckoutAsync)
            .WithTags("Checkout");

        app.MapPost("/checkout", CheckoutAsync)
            .WithTags("Checkout")
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> CheckoutAsync(
        CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CheckoutCommand(request.CustomerId, idempotencyKey ?? request.IdempotencyKey),
            cancellationToken);

        return Results.Created($"/orders/{result.Id}", result);
    }

    private sealed record CheckoutRequest(Guid CustomerId, string? IdempotencyKey);
}
