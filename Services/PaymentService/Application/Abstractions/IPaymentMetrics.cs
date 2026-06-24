namespace PaymentService.Application.Abstractions;

public interface IPaymentMetrics
{
    void RecordWebhookRequest(string outcome);
}
