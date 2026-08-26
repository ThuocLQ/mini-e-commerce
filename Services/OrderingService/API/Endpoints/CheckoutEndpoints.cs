using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderingService.Application.Orders.Checkout;
using System.Security.Claims;

namespace OrderingService.API.Endpoints;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/checkout", CheckoutAsync)
            .WithTags("Checkout")
            .RequireAuthorization("authenticated");

        app.MapPost("/checkout", CheckoutAsync)
            .WithTags("Checkout")
            .ExcludeFromDescription()
            .RequireAuthorization("authenticated");

        return app;
    }

    private static async Task<IResult> CheckoutAsync(
        CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedCustomerId(user, out var customerId))
        {
            return Results.Forbid();
        }

        var result = await sender.Send(
            new CheckoutCommand(customerId, idempotencyKey ?? request.IdempotencyKey, request.CouponCode, request.BasketId, request.BasketVersion),
            cancellationToken);

        return Results.Created($"/orders/{result.Id}", result);
    }

    private static bool TryGetAuthenticatedCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var customerIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? user.FindFirstValue("sub");
        return Guid.TryParse(customerIdValue, out customerId);
    }

    private sealed record CheckoutRequest(string? IdempotencyKey, string? CouponCode, Guid BasketId, long BasketVersion);
}
