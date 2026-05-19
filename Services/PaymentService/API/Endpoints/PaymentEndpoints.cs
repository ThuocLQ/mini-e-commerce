using MediatR;
using PaymentService.API.Contracts;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Application.Payments.GetPaymentById;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .WithTags("Payments");

        group.MapPost("", async (
            CreatePaymentRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new CreatePaymentCommand(
                request.OrderId,
                request.CustomerId,
                request.Amount,
                request.Currency);

            var result = await sender.Send(command, cancellationToken);

            return Results.Created($"/payments/{result.Id}", result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPaymentByIdQuery(id), cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        });

        return app;
    }
}
