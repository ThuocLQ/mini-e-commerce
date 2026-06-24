using MicroShop.ServiceDefaults.Diagnostics;
using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure.Observability;

public sealed class PaymentMetrics : IPaymentMetrics
{
    public void RecordWebhookRequest(string outcome)
    {
        MicroShopMetrics.RecordWebhookRequest(outcome);
    }
}
