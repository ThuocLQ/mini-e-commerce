using MediatR;
using OrderingService.Application.Orders.GetOrderById;
using OrderingService.Application.Orders.GetOrders;
using OrderingService.Application.Orders.GetAllOrders;
using OrderingService.Application.Orders.CancelOrder;
using OrderingService.Application.Orders.AdvanceFulfillment;
using OrderingService.Domain.Orders;
using System.Security.Claims;

namespace OrderingService.API.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization("authenticated");

        group.MapGet("/admin", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetAllOrdersQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization("administrator");

        group.MapPost("/admin/{id:guid}/fulfillment", async (
            Guid id,
            FulfillmentTransitionRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseFulfillmentStatus(request.TargetStatus, out var targetStatus))
            {
                return Results.BadRequest(new { error = "targetStatus must be Confirmed, Shipped, or Delivered." });
            }

            var result = await sender.Send(new AdvanceFulfillmentCommand(id, targetStatus), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization("administrator");

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

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelOrderRequest? request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetAuthenticatedCustomerId(user, out var customerId))
            {
                return Results.Forbid();
            }

            var result = await sender.Send(new CancelOrderCommand(id, customerId, request?.Reason), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }

    private static bool TryGetAuthenticatedCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var customerIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? user.FindFirstValue("sub");
        return Guid.TryParse(customerIdValue, out customerId);
    }

    private static bool TryParseFulfillmentStatus(string? value, out OrderStatus targetStatus)
    {
        return Enum.TryParse(value?.Trim(), ignoreCase: true, out targetStatus) &&
               targetStatus is OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered;
    }

    private sealed record FulfillmentTransitionRequest(string? TargetStatus);
    private sealed record CancelOrderRequest(string? Reason);
}
