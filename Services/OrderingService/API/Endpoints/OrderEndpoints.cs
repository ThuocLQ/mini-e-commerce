using MediatR;
using OrderingService.API.Contracts;
using OrderingService.Application.Orders.ApplyPaymentResult;
using OrderingService.Application.Orders.GetOrderById;
using OrderingService.Application.Orders.GetOrders;
using System.Security.Claims;

namespace OrderingService.API.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization("authenticated");

        group.MapGet("", async (ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedCustomerId(user, out var customerId))
            {
                return Results.Forbid();
            }

            var result = await sender.Send(new GetOrdersQuery(customerId), cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);

            if (result is null || !TryGetAuthenticatedCustomerId(user, out var customerId) || result.CustomerId != customerId)
            {
                return Results.NotFound();
            }

            return Results.Ok(result);
        });

        app.MapPost("/orders/{id:guid}/payment-result", async (
            Guid id,
            ApplyOrderPaymentResultRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParsePaymentResult(request.Status, out var paymentResult))
            {
                return Results.BadRequest(new { Error = "Status must be either 'succeeded' or 'failed'." });
            }

            var result = await sender.Send(
                new ApplyOrderPaymentResultCommand(id, paymentResult),
                cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }

    private static bool TryGetAuthenticatedCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        return Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out customerId);
    }

    private static bool TryParsePaymentResult(string status, out OrderPaymentResult result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return Enum.TryParse(status, ignoreCase: true, out result) &&
               Enum.IsDefined(result);
    }
}
