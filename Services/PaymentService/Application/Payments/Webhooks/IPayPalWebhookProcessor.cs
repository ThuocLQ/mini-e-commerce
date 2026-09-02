using Microsoft.AspNetCore.Http;

namespace PaymentService.Application.Payments.Webhooks;

public interface IPayPalWebhookProcessor
{
    Task<PaymentWebhookProcessingResult> ProcessAsync(
        IHeaderDictionary headers,
        string rawBody,
        CancellationToken cancellationToken = default);
}