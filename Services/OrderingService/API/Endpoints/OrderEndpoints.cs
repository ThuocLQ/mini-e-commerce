using MediatR;
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

        return app;
    }
}
