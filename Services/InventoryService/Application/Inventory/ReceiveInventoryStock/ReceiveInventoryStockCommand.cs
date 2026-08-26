using InventoryService.Application.Abstractions;
using MediatR;

namespace InventoryService.Application.Inventory.ReceiveInventoryStock;

public sealed record InventoryStockReceiptItem(string ProductId, int Quantity);
public sealed record InventoryStockReceiptResult(Guid ReceiptId, bool Applied);
public sealed record ReceiveInventoryStockCommand(Guid ReceiptId, Guid SourcePurchaseOrderId, IReadOnlyList<InventoryStockReceiptItem> Items) : IRequest<InventoryStockReceiptResult>;

public sealed class ReceiveInventoryStockHandler(IInventoryItemRepository repository)
    : IRequestHandler<ReceiveInventoryStockCommand, InventoryStockReceiptResult>
{
    public async Task<InventoryStockReceiptResult> Handle(ReceiveInventoryStockCommand request, CancellationToken cancellationToken)
    {
        if (request.ReceiptId == Guid.Empty || request.SourcePurchaseOrderId == Guid.Empty || request.Items.Count == 0 ||
            request.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductId) || item.Quantity <= 0))
        {
            throw new ArgumentException("A receipt id, purchase order id, and at least one positive stock item are required.");
        }

        var items = request.Items
            .GroupBy(item => item.ProductId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new InventoryStockReceiptItem(group.Key, group.Sum(item => item.Quantity)))
            .ToList();

        var applied = await repository.ReceiveStockAsync(request.ReceiptId, request.SourcePurchaseOrderId, items, cancellationToken);
        return new InventoryStockReceiptResult(request.ReceiptId, applied);
    }
}