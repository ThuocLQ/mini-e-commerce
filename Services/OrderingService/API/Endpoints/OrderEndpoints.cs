using MediatR;
using OrderingService.Application.Orders.CreateOrder;
using OrderingService.Application.Orders.GetOrderById;
using OrderingService.Application.Orders.GetOrders;

namespace OrderingService.API.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders");

        group.MapGet("", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrdersQuery(), cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetOrderByIdQuery(id), cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("", async (CreateOrderRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateOrderCommand(
                request.CustomerId,
                request.Items.Select(item => new CreateOrderItemCommand(
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity)).ToList());

            var result = await sender.Send(command, cancellationToken);

            return Results.Created($"/orders/{result.Id}", result);
        });

        return app;
    }

    private sealed record CreateOrderRequest(
        Guid CustomerId,
        IReadOnlyList<CreateOrderItemRequest> Items);

    private sealed record CreateOrderItemRequest(
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity);
}
