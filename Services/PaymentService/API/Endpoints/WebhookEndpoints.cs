using System.Text;
using Microsoft.Extensions.Options;
using MicroShop.ServiceDefaults.Diagnostics;
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

        if (string.Equals(app.ServiceProvider.GetRequiredService<IConfiguration>()["PaymentProvider:Provider"], "PayPal", StringComparison.OrdinalIgnoreCase))
        {
            group.MapPost("/paypal", HandlePayPalWebhookAsync)
                .WithSummary("Receive verified PayPal payment lifecycle events");
        }

        return app;
    }

    private static async Task<IResult> HandlePaymentWebhookAsync(
        HttpRequest httpRequest,
        IOptions<PaymentWebhookOptions> options,
        IPaymentWebhookProcessor processor,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadRawBodyAsync(httpRequest, cancellationToken);
        httpRequest.Headers.TryGetValue(options.Value.SignatureHeaderName, out var signature);
        PaymentWebhookProcessingResult result;
        try
        {
            result = await processor.ProcessAsync(rawBody, signature.ToString(), cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return ApiProblemResults.BadRequest(exception.Message, "PAYMENT_WEBHOOK_INVALID");
        }

        return result.Payment is null
            ? ApiProblemResults.NotFound("Payment was not found.", "PAYMENT_NOT_FOUND")
            : Results.Ok(result.Payment);
    }

    private static async Task<IResult> HandlePayPalWebhookAsync(
        HttpRequest httpRequest,
        IPayPalWebhookProcessor processor,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadRawBodyAsync(httpRequest, cancellationToken);
        var result = await processor.ProcessAsync(httpRequest.Headers, rawBody, cancellationToken);
        return result.Payment is null ? Results.NoContent() : Results.Ok(result.Payment);
    }

    private static async Task<string> ReadRawBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: false);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}