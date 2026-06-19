using System.Text.Json;
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
            Content = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            NextAttemptAtUtc = integrationEvent.OccurredAtUtc
        };
    }
}
