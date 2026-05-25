using System.Text.Json;
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
            Content = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            NextAttemptAtUtc = integrationEvent.OccurredAtUtc
        };
    }
}
