using MediatR;
using PaymentService.API.Contracts;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Application.Payments.GetPaymentById;
using PaymentService.Application.Payments.GetPayments;
using PaymentService.Application.Payments.Providers;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Application.Abstractions;
using System.Security.Claims;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments")
            .WithTags("Payments")
            .RequireAuthorization("authenticated");

        group.MapPost("", async (CreatePaymentRequest request, HttpRequest httpRequest, ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!TryGetCustomerId(user, out var customerId)) return Results.Unauthorized();
            var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
            var result = await sender.Send(new CreatePaymentCommand(request.OrderId, customerId, idempotencyKey), cancellationToken);
            return result.IsReplay
                ? Results.Ok(result)
                : Results.Created($"/payments/{result.Payment.Id}", result);
        });

        if (app.ServiceProvider.GetService<ISandboxPaymentProvider>() is not null)
        {
            group.MapPost("/{id:guid}/sandbox-completion", CompleteSandboxPaymentAsync)
                .ExcludeFromDescription();
        }

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

    private static async Task<IResult> CompleteSandboxPaymentAsync(
        Guid id,
        SandboxPaymentCompletionRequest request,
        ClaimsPrincipal user,
        IPaymentRepository paymentRepository,
        ISandboxPaymentProvider sandboxProvider,
        IPaymentWebhookProcessor webhookProcessor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(user, out var customerId)) return Results.Unauthorized();

        var payment = await paymentRepository.GetByIdAsync(id, cancellationToken);
        if (payment is null || payment.CustomerId != customerId) return Results.NotFound();

        if (!Enum.TryParse<SandboxPaymentOutcome>(request.Outcome, ignoreCase: true, out var outcome) ||
            !Enum.IsDefined(outcome))
        {
            return Results.BadRequest(new { Error = "Outcome must be 'Approve' or 'Decline'." });
        }

        var logger = loggerFactory.CreateLogger("PaymentService.SandboxCompletion");
        logger.LogInformation(
            "Processing sandbox payment completion. PaymentId={PaymentId}, OrderId={OrderId}, Outcome={Outcome}, SessionExpired={SessionExpired}",
            payment.Id,
            payment.OrderId,
            outcome,
            payment.PaymentActionExpiresAtUtc <= DateTime.UtcNow);

        var webhook = await sandboxProvider.CompleteAsync(payment, outcome, cancellationToken);
        var result = await webhookProcessor.ProcessAsync(webhook.RawBody, webhook.Signature, cancellationToken);
        if (result.Payment is null)
        {
            return Results.NotFound(new { Error = "Payment was not found." });
        }

        return Results.Accepted($"/payments/{id}", new
        {
            payment = result.Payment,
            actionExpiresAtUtc = payment.PaymentActionExpiresAtUtc,
            actionExpired = payment.PaymentActionExpiresAtUtc <= DateTime.UtcNow,
            serverConfirmed = true
        });
    }

    private static bool TryGetCustomerId(ClaimsPrincipal user, out Guid customerId)
    {
        var customerIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(customerIdValue, out customerId);
    }
}
