using MediatR;
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

        return app;
    }

    private static bool TryGetAuthenticatedCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        return Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out customerId);
    }
}
