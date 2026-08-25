using InventoryService.Application.Abstractions;
using MediatR;

namespace InventoryService.Application.Inventory.UpsertStock;

public sealed record UpsertInventoryStockCommand(string ProductId, int StockQuantity) : IRequest;

public sealed class UpsertInventoryStockHandler(IInventoryItemRepository repository)
    : IRequestHandler<UpsertInventoryStockCommand>
{
    public Task Handle(UpsertInventoryStockCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId) || request.StockQuantity < 0)
        {
            throw new ArgumentException("A product id and non-negative stock quantity are required.");
        }

        return repository.UpsertStockAsync(request.ProductId.Trim(), request.StockQuantity, cancellationToken);
    }
}
