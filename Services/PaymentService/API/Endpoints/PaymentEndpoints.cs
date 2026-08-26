using MediatR;
using PaymentService.API.Contracts;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Application.Payments.GetPaymentById;
using PaymentService.Application.Payments.GetPayments;
using System.Security.Claims;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .WithTags("Payments")
            .RequireAuthorization("authenticated");

        group.MapPost("", async (CreatePaymentRequest request, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!TryGetCustomerId(user, out var customerId)) return Results.Unauthorized();
            var result = await sender.Send(new CreatePaymentCommand(request.OrderId, customerId), cancellationToken);
            return Results.Created($"/payments/{result.Id}", result);
        });

        group.MapGet("/admin", async (int? limit, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPaymentsQuery(limit ?? 100), cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization("administrator");

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPaymentByIdQuery(id), cancellationToken);
            if (result is null || !TryGetCustomerId(user, out var customerId) || result.CustomerId != customerId) return Results.NotFound();
            return Results.Ok(result);
        });

        return app;
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var customerIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(customerIdValue, out customerId);
    }
}
