using System.Net.Http.Json;
using BuildingBlocks.Contracts.Events.Payments;

namespace PaymentService.Infrastructure.Outbox;

public sealed class OrderingPaymentSagaClient
{
    private readonly HttpClient _httpClient;

    public OrderingPaymentSagaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task ApplyPaymentSucceededAsync(
        PaymentSucceededIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await SendAsync(
            integrationEvent.OrderId,
            new ApplyPaymentSagaEventRequest(
                integrationEvent.EventId,
                integrationEvent.PaymentId,
                "PaymentSucceeded",
                null),
            cancellationToken);
    }

    public async Task ApplyPaymentFailedAsync(
        PaymentFailedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await SendAsync(
            integrationEvent.OrderId,
            new ApplyPaymentSagaEventRequest(
                integrationEvent.EventId,
                integrationEvent.PaymentId,
                "PaymentFailed",
                integrationEvent.FailureReason),
            cancellationToken);
    }

    private async Task SendAsync(
        Guid orderId,
        ApplyPaymentSagaEventRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/orders/{orderId}/payment-events")
        {
            Content = JsonContent.Create(request)
        };

        var correlationId = BuildingBlocks.Contracts.Correlation.CorrelationContext.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Ordering saga endpoint returned {(int)response.StatusCode}. Body: {body}");
        }
    }

    private sealed record ApplyPaymentSagaEventRequest(
        Guid EventId,
        Guid PaymentId,
        string EventType,
        string? FailureReason);
}
