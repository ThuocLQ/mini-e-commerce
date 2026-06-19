using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;
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
        HttpRequest httpRequest,
        IOptions<PaymentWebhookOptions> options,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadRawBodyAsync(httpRequest, cancellationToken);
        var payloadHash = ComputeSha256Hash(rawBody);

        PaymentWebhookRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PaymentWebhookRequest>(
                rawBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { Error = "Webhook payload is not valid JSON." });
        }

        if (request is null)
        {
            return Results.BadRequest(new { Error = "Webhook payload is required." });
        }

        var signatureStatus = VerifySignature(httpRequest, options.Value, rawBody)
            ? "Verified"
            : "Failed";

        var command = new PaymentWebhookCommand(
            request.PaymentId,
            request.ProviderEventId,
            request.ProviderTransactionId,
            request.Status,
            request.FailureReason,
            payloadHash,
            signatureStatus);

        var result = await sender.Send(command, cancellationToken);

        return result is null
            ? Results.NotFound(new { Error = "Payment was not found." })
            : Results.Ok(result);
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

    private static bool VerifySignature(
        HttpRequest request,
        PaymentWebhookOptions options,
        string rawBody)
    {
        if (!options.RequireSignature)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.SharedSecret))
        {
            return false;
        }

        if (!request.Headers.TryGetValue(options.SignatureHeaderName, out var actualSignatureValues))
        {
            return false;
        }

        var actualSignature = NormalizeSignature(actualSignatureValues.ToString());
        var expectedSignature = ComputeHmacSha256(rawBody, options.SharedSecret);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualSignature),
            Encoding.UTF8.GetBytes(expectedSignature));
    }

    private static string ComputeSha256Hash(string rawBody)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeHmacSha256(string rawBody, string secret)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(rawBody));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeSignature(string signature)
    {
        const string prefix = "sha256=";
        signature = signature.Trim();

        return signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? signature[prefix.Length..].Trim().ToLowerInvariant()
            : signature.ToLowerInvariant();
    }
}
