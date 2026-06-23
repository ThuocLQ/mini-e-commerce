using System.Text.Json;
using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events;
using OrderingService.Domain.Outbox;

namespace OrderingService.Application.Outbox;

public static class OutboxMessageFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static OutboxMessage Create<TEvent>(TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        return new OutboxMessage
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
