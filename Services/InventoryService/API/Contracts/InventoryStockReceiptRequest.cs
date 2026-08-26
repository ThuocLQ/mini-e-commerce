namespace InventoryService.API.Contracts;

public sealed record InventoryStockReceiptRequest(Guid ReceiptId, Guid SourcePurchaseOrderId, IReadOnlyList<InventoryStockReceiptItemRequest> Items);
public sealed record InventoryStockReceiptItemRequest(string ProductId, int Quantity);