using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events;
using PaymentService.Domain.Outbox;

namespace PaymentService.Application.Outbox;

public static class PaymentOutboxMessageFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PaymentOutboxMessage Create<TEvent>(TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        return new PaymentOutboxMessage
        {
            Id = integrationEvent.EventId,
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
            Type = typeof(TEvent).FullName ?? typeof(TEvent).Name,
            Content = JsonSerializer.Serialize(AttachCorrelation(integrationEvent), JsonOptions),
            CorrelationId = Normalize(integrationEvent.CorrelationId) ?? CorrelationContext.CorrelationId,
            CausationId = Normalize(integrationEvent.CausationId),
            NextAttemptAtUtc = integrationEvent.OccurredAtUtc
        };
    }

    private static TEvent AttachCorrelation<TEvent>(TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        var correlationId = Normalize(integrationEvent.CorrelationId) ?? CorrelationContext.CorrelationId;

        return integrationEvent with
        {
            CorrelationId = correlationId,
            CausationId = Normalize(integrationEvent.CausationId)
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
