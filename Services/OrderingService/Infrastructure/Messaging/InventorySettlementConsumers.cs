using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Inventory;
using MassTransit;
using MediatR;
using OrderingService.Application.OrderPaymentSagas.ApplyInventorySettlement;

namespace OrderingService.Infrastructure.Messaging;

public sealed class InventoryCommittedConsumer(ISender sender)
    : IConsumer<InventoryCommittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryCommittedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);

        var result = await sender.Send(
            new ApplyInventorySettlementEventCommand(
                context.Message.EventId,
                OrderInventorySettlementEventType.InventoryCommitted,
                context.Message.OrderId,
                null,
                InventorySettlementCausation.Parse(context.Message.CausationId)),
            context.CancellationToken);

        if (!result.OrderFound)
        {
            throw new InvalidOperationException($"Cannot apply inventory settlement because order '{context.Message.OrderId}' was not found.");
        }
    }
}

public sealed class InventoryReleasedConsumer(ISender sender)
    : IConsumer<InventoryReleasedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryReleasedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);

        var result = await sender.Send(
            new ApplyInventorySettlementEventCommand(
                context.Message.EventId,
                OrderInventorySettlementEventType.InventoryReleased,
                context.Message.OrderId,
                context.Message.Reason,
                InventorySettlementCausation.Parse(context.Message.CausationId)),
            context.CancellationToken);

        if (!result.OrderFound)
        {
            throw new InvalidOperationException($"Cannot apply inventory settlement because order '{context.Message.OrderId}' was not found.");
        }
    }
}

internal static class InventorySettlementCausation
{
    public static Guid? Parse(string? causationId) =>
        Guid.TryParse(causationId, out var eventId) ? eventId : null;
}
