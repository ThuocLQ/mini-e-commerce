using MediatR;
using PaymentService.API.Contracts;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.API.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks")
            .WithTags("Webhooks");

        group.MapPost("/payment", HandlePaymentWebhookAsync);

        app.MapPost("/payments/webhooks/payment", HandlePaymentWebhookAsync)
            .WithTags("Webhooks");

        return app;
    }

    private static async Task<IResult> HandlePaymentWebhookAsync(
        PaymentWebhookRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new PaymentWebhookCommand(
            request.PaymentId,
            request.ProviderTransactionId,
            request.Status,
            request.FailureReason);

        var result = await sender.Send(command, cancellationToken);

        return result is null
            ? Results.NotFound(new { Error = "Payment was not found." })
            : Results.Ok(result);
    }
}
