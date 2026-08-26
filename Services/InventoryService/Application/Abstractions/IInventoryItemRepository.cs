using InventoryService.Application.Inventory.GetInventoryItems;
using InventoryService.Application.Inventory.ReceiveInventoryStock;

namespace InventoryService.Application.Abstractions;

public interface IInventoryItemRepository
{
    Task<IReadOnlyList<InventoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpsertStockAsync(string productId, int stockQuantity, CancellationToken cancellationToken = default);
    Task<bool> ReceiveStockAsync(Guid receiptId, Guid sourcePurchaseOrderId, IReadOnlyList<InventoryStockReceiptItem> items, CancellationToken cancellationToken = default);
}
