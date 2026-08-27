using System.Text;
using Microsoft.Extensions.Options;
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
            return Results.BadRequest(new { Error = exception.Message });
        }

        return result.Payment is null
            ? Results.NotFound(new { Error = "Payment was not found." })
            : Results.Ok(result.Payment);
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
