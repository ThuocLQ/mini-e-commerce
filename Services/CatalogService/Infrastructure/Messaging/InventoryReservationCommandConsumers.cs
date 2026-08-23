using BuildingBlocks.Contracts.Events.Inventory;
using CatalogService.Application.Inventory.CommitInventory;
using CatalogService.Application.Inventory.ReleaseInventory;
using MassTransit;
using MediatR;

namespace CatalogService.Infrastructure.Messaging;

public sealed class InventoryCommitRequestedConsumer(ISender sender) : IConsumer<InventoryCommitRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<InventoryCommitRequestedIntegrationEvent> context) =>
        sender.Send(new CommitInventoryCommand(context.Message.OrderId), context.CancellationToken);
}

public sealed class InventoryReleaseRequestedConsumer(ISender sender) : IConsumer<InventoryReleaseRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<InventoryReleaseRequestedIntegrationEvent> context) =>
        sender.Send(new ReleaseInventoryCommand(context.Message.OrderId), context.CancellationToken);
}
