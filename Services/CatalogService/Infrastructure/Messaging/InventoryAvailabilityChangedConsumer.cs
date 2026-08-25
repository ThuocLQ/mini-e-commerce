using BuildingBlocks.Contracts.Events.Inventory;
using CatalogService.Application.Abstractions;
using MassTransit;

namespace CatalogService.Infrastructure.Messaging;

public sealed class InventoryAvailabilityChangedConsumer : IConsumer<InventoryAvailabilityChangedIntegrationEvent>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<InventoryAvailabilityChangedConsumer> _logger;

    public InventoryAvailabilityChangedConsumer(
        IProductRepository productRepository,
        ILogger<InventoryAvailabilityChangedConsumer> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InventoryAvailabilityChangedIntegrationEvent> context)
    {
        var message = context.Message;
        if (string.IsNullOrWhiteSpace(message.ProductId) || message.AvailableQuantity < 0)
        {
            throw new InvalidOperationException("Inventory availability event is invalid.");
        }

        var applied = await _productRepository.UpdateInventoryAvailabilitySnapshotAsync(
            message.ProductId,
            message.AvailableQuantity,
            message.InventoryUpdatedAtUtc,
            context.CancellationToken);

        if (!applied)
        {
            _logger.LogInformation(
                "Ignored stale or unknown inventory availability snapshot. ProductId={ProductId}, InventoryUpdatedAtUtc={InventoryUpdatedAtUtc}",
                message.ProductId,
                message.InventoryUpdatedAtUtc);
        }
    }
}
