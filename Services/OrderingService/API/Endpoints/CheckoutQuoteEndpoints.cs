using System.Security.Claims;
using MediatR;
using OrderingService.API.Contracts;
using OrderingService.Application.Orders.CheckoutQuote;

namespace OrderingService.API.Endpoints;

public static class CheckoutQuoteEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders/checkout/quote", QuoteAsync)
            .WithTags("Checkout")
            .RequireAuthorization("authenticated");

        return app;
    }

    private static async Task<IResult> QuoteAsync(
        CheckoutQuoteRequest request,
        ClaimsPrincipal user,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedCustomerId(user, out var customerId))
        {
            return Results.Forbid();
        }

        var quote = await sender.Send(
            new CheckoutQuoteCommand(
                customerId,
                request.BasketId,
                request.BasketVersion,
                request.CouponCode,
                request.ShippingAddressId),
            cancellationToken);

        return Results.Ok(quote);
    }

    private static bool TryGetAuthenticatedCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var customerIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? user.FindFirstValue("sub");
        return Guid.TryParse(customerIdValue, out customerId);
    }
}
