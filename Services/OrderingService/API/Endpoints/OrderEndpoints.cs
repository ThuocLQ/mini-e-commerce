using MediatR;
using OrderingService.API.Contracts;
using OrderingService.Application.Orders.ApplyPaymentResult;
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

        group.MapPost("/{id:guid}/payment-result", async (
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
