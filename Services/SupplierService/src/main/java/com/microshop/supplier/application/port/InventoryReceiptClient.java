package com.microshop.supplier.application.port;

import java.util.List;
import java.util.UUID;

public interface InventoryReceiptClient {
    InventoryReceiptResponse receive(InventoryStockReceipt receipt);

    record InventoryStockReceipt(UUID receiptId, UUID sourcePurchaseOrderId, List<InventoryStockReceiptItem> items) { }
    record InventoryStockReceiptItem(String productId, int quantity) { }
    record InventoryReceiptResponse(UUID receiptId, boolean applied) { }
}
