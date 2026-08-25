using BuildingBlocks.Contracts.Correlation;
using BuildingBlocks.Contracts.Events.Inventory;
using InventoryService.Application.Inventory.CommitInventory;
using InventoryService.Application.Inventory.ReleaseInventory;
using InventoryService.Application.Inventory.UpsertStock;
using MassTransit;
using MediatR;

namespace InventoryService.Infrastructure.Messaging;

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

public sealed class InventoryItemProvisionRequestedConsumer(ISender sender) : IConsumer<InventoryItemProvisionRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryItemProvisionRequestedIntegrationEvent> context)
    {
        using var correlationScope = CorrelationContext.BeginScope(context.Message.CorrelationId);
        await sender.Send(
            new UpsertInventoryStockCommand(context.Message.ProductId, context.Message.InitialStockQuantity),
            context.CancellationToken);
    }
}

