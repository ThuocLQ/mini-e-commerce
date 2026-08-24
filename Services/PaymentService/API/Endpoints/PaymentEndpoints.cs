using MediatR;
using PaymentService.API.Contracts;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Application.Payments.GetPaymentById;
using System.Security.Claims;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .WithTags("Payments")
            .RequireAuthorization("authenticated");

        group.MapPost("", async (
            CreatePaymentRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var customerId))
            {
                return Results.Unauthorized();
            }

            var command = new CreatePaymentCommand(request.OrderId, customerId);

            var result = await sender.Send(command, cancellationToken);

            return Results.Created($"/payments/{result.Id}", result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPaymentByIdQuery(id), cancellationToken);

            if (result is null ||
                !Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var customerId) ||
                result.CustomerId != customerId)
            {
                return Results.NotFound();
            }

            return Results.Ok(result);
        });

        return app;
    }
}
