using MediatR;
using OrderingService.Application.Orders.GetOrderById;
using OrderingService.Application.Orders.GetOrders;
using OrderingService.Application.Orders.GetAllOrders;
using OrderingService.Application.Orders.CancelOrder;
using OrderingService.Application.Fulfillment;
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

        group.MapGet("/admin/{id:guid}/shipment", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetShipmentByOrderIdQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("administrator");

        group.MapPost("/admin/{id:guid}/shipment", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            if (!TryGetAuthenticatedCustomerId(user, out var actorId)) return Results.Forbid();
            var result = await sender.Send(new CreateShipmentCommand(id, actorId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("administrator");

        group.MapPost("/admin/{id:guid}/shipment/dispatch", async (Guid id, ShipmentDispatchRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            if (!TryGetAuthenticatedCustomerId(user, out var actorId)) return Results.Forbid();
            var result = await sender.Send(new DispatchShipmentCommand(id, actorId, request.Carrier, request.TrackingNumber), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("administrator");

        group.MapPost("/admin/{id:guid}/shipment/deliver", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            if (!TryGetAuthenticatedCustomerId(user, out var actorId)) return Results.Forbid();
            var result = await sender.Send(new DeliverShipmentCommand(id, actorId), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("administrator");
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

        group.MapGet("/{id:guid}/shipment", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            var order = await sender.Send(new GetOrderByIdQuery(id), ct);
            if (order is null || !TryGetAuthenticatedCustomerId(user, out var customerId) || order.CustomerId != customerId) return Results.NotFound();
            var shipment = await sender.Send(new GetShipmentByOrderIdQuery(id), ct);
            return shipment is null ? Results.NotFound() : Results.Ok(shipment);
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

    private sealed record ShipmentDispatchRequest(string Carrier, string TrackingNumber);
    private sealed record CancelOrderRequest(string? Reason);
}