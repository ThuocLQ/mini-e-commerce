using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Inventory;
using CatalogService.Application.Inventory.CommitInventory;
using CatalogService.Application.Inventory.ReleaseInventory;
using MassTransit;
using MediatR;

namespace CatalogService.Infrastructure.Messaging;

public sealed class InventoryCommitRequestedConsumer(ISender sender) : IConsumer<InventoryCommitRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryCommitRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);
        await sender.Send(new CommitInventoryCommand(context.Message.OrderId, context.Message.EventId), context.CancellationToken);
    }
}

public sealed class InventoryReleaseRequestedConsumer(ISender sender) : IConsumer<InventoryReleaseRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryReleaseRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);
        await sender.Send(new ReleaseInventoryCommand(context.Message.OrderId, context.Message.EventId), context.CancellationToken);
    }
}
