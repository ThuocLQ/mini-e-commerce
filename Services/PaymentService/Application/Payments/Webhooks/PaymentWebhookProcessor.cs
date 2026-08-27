using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Options;

namespace PaymentService.Application.Payments.Webhooks;

public sealed class PaymentWebhookProcessor : IPaymentWebhookProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOptions<PaymentWebhookOptions> _options;
    private readonly ISender _sender;

    public PaymentWebhookProcessor(IOptions<PaymentWebhookOptions> options, ISender sender)
    {
        _options = options;
        _sender = sender;
    }

    public async Task<PaymentWebhookProcessingResult> ProcessAsync(
        string rawBody,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        PaymentWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawBody, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Webhook payload is not valid JSON.", nameof(rawBody), exception);
        }

        if (payload is null)
        {
            throw new ArgumentException("Webhook payload is required.", nameof(rawBody));
        }

        var options = _options.Value;
        var signatureStatus = VerifySignature(signature, options, rawBody) ? "Verified" : "Failed";
        var result = await _sender.Send(new PaymentWebhookCommand(
            payload.PaymentId,
            payload.ProviderEventId,
            payload.ProviderTransactionId,
            payload.Status,
            payload.FailureReason,
            ComputeSha256Hash(rawBody),
            signatureStatus), cancellationToken);

        return new PaymentWebhookProcessingResult(result, IsDuplicate: false);
    }

    private static bool VerifySignature(string? signature, PaymentWebhookOptions options, string rawBody)
    {
        if (!options.RequireSignature)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.SharedSecret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expectedSignature = ComputeHmacSha256(rawBody, options.SharedSecret);
        var actualSignature = NormalizeSignature(signature);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualSignature),
            Encoding.UTF8.GetBytes(expectedSignature));
    }

    private static string ComputeSha256Hash(string rawBody) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

    private static string ComputeHmacSha256(string rawBody, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

    private static string NormalizeSignature(string signature)
    {
        const string prefix = "sha256=";
        signature = signature.Trim();
        return signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? signature[prefix.Length..].Trim().ToLowerInvariant()
            : signature.ToLowerInvariant();
    }
}
