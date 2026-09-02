using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments;
using PaymentService.Application.Payments.Webhooks;

namespace PaymentService.Infrastructure.Providers;

public sealed class PayPalWebhookProcessor : IPayPalWebhookProcessor
{
    private readonly PayPalApiClient _apiClient;
    private readonly IPaymentRepository _payments;
    private readonly ISender _sender;

    public PayPalWebhookProcessor(
        PayPalApiClient apiClient,
        IPaymentRepository payments,
        ISender sender)
    {
        _apiClient = apiClient;
        _payments = payments;
        _sender = sender;
    }

    public async Task<PaymentWebhookProcessingResult> ProcessAsync(
        IHeaderDictionary headers,
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        if (!await _apiClient.VerifyWebhookSignatureAsync(headers, rawBody, cancellationToken))
        {
            throw new UnauthorizedAccessException("Payment provider webhook signature verification failed.");
        }

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var eventType = ReadRequiredString(root, "event_type");
        if (!TryMapStatus(eventType, out var status))
        {
            return new PaymentWebhookProcessingResult(null, IsDuplicate: false);
        }

        var resource = root.TryGetProperty("resource", out var resourceElement)
            ? resourceElement
            : throw new ArgumentException("PayPal webhook resource is required.", nameof(rawBody));
        var payment = await ResolvePaymentAsync(resource, cancellationToken);
        if (payment is null)
        {
            return new PaymentWebhookProcessingResult(null, IsDuplicate: false);
        }

        var providerEventId = root.TryGetProperty("id", out var eventId) && !string.IsNullOrWhiteSpace(eventId.GetString())
            ? eventId.GetString()!
            : $"paypal:{payment.Id:N}:{eventType}:{ReadRequiredString(resource, "id")}";
        var transactionId = ReadRequiredString(resource, "id");
        var failureReason = ReadFailureReason(resource);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

        var result = await _sender.Send(new PaymentWebhookCommand(
            payment.Id,
            providerEventId,
            transactionId,
            status,
            failureReason,
            payloadHash,
            "Verified"), cancellationToken);

        return new PaymentWebhookProcessingResult(result.Payment is null ? null : PaymentMapper.ToDto(result.Payment), result.IsDuplicate);
    }

    private async Task<PaymentService.Domain.Payments.Payment?> ResolvePaymentAsync(JsonElement resource, CancellationToken cancellationToken)
    {
        if (resource.TryGetProperty("custom_id", out var customId) &&
            Guid.TryParse(customId.GetString(), out var paymentId))
        {
            var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
            if (payment is not null && string.Equals(payment.Provider, "PayPal", StringComparison.OrdinalIgnoreCase))
            {
                return payment;
            }
        }

        if (resource.TryGetProperty("supplementary_data", out var supplementaryData) &&
            supplementaryData.TryGetProperty("related_ids", out var relatedIds) &&
            relatedIds.TryGetProperty("order_id", out var orderId) &&
            !string.IsNullOrWhiteSpace(orderId.GetString()))
        {
            return await _payments.GetByProviderSessionIdAsync("PayPal", orderId.GetString()!, cancellationToken);
        }

        return null;
    }

    private static bool TryMapStatus(string eventType, out string status)
    {
        status = eventType switch
        {
            "PAYMENT.AUTHORIZATION.CREATED" => "AUTHORIZED",
            "PAYMENT.CAPTURE.COMPLETED" => "CAPTURED",
            "PAYMENT.AUTHORIZATION.VOIDED" => "VOIDED",
            "PAYMENT.CAPTURE.REFUNDED" => "REFUNDED",
            "PAYMENT.AUTHORIZATION.DENIED" or "PAYMENT.CAPTURE.DENIED" => "FAILED",
            _ => string.Empty
        };

        return status.Length > 0;
    }

    private static string? ReadFailureReason(JsonElement resource)
    {
        if (!resource.TryGetProperty("status_details", out var details) ||
            !details.TryGetProperty("reason", out var reason))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(reason.GetString()) ? null : reason.GetString();
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ArgumentException($"PayPal webhook field '{propertyName}' is required.");
        }

        return value.GetString()!;
    }
}