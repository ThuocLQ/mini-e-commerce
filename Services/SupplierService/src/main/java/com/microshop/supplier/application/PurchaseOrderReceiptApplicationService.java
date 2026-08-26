package com.microshop.supplier.application;

import com.microshop.supplier.application.port.InventoryReceiptClient;
import com.microshop.supplier.application.port.PurchaseOrderRepository;
import com.microshop.supplier.domain.PurchaseOrder;
import com.microshop.supplier.domain.PurchaseOrderStatus;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;

import java.time.Clock;
import java.time.Instant;
import java.util.NoSuchElementException;
import java.util.UUID;

@Service
public class PurchaseOrderReceiptApplicationService {
    private final PurchaseOrderRepository purchaseOrders;
    private final InventoryReceiptClient inventoryReceipts;
    private final Clock clock;
    private final TransactionTemplate transactions;

    @Autowired
    public PurchaseOrderReceiptApplicationService(
            PurchaseOrderRepository purchaseOrders,
            InventoryReceiptClient inventoryReceipts,
            PlatformTransactionManager transactionManager) {
        this(purchaseOrders, inventoryReceipts, Clock.systemUTC(), new TransactionTemplate(transactionManager));
    }

    PurchaseOrderReceiptApplicationService(
            PurchaseOrderRepository purchaseOrders,
            InventoryReceiptClient inventoryReceipts,
            Clock clock,
            TransactionTemplate transactions) {
        this.purchaseOrders = purchaseOrders;
        this.inventoryReceipts = inventoryReceipts;
        this.clock = clock;
        this.transactions = transactions;
    }

    public PurchaseOrder receivePurchaseOrder(UUID purchaseOrderId) {
        var pendingReceipt = requestReceipt(purchaseOrderId);
        if (pendingReceipt.status() == PurchaseOrderStatus.RECEIVED) return pendingReceipt;

        var response = inventoryReceipts.receive(new InventoryReceiptClient.InventoryStockReceipt(
                pendingReceipt.receiptId(),
                pendingReceipt.id(),
                pendingReceipt.lines().stream()
                        .map(line -> new InventoryReceiptClient.InventoryStockReceiptItem(line.productId(), line.quantity()))
                        .toList()));

        if (!pendingReceipt.receiptId().equals(response.receiptId())) {
            throw new IllegalStateException("Inventory confirmed a different receipt.");
        }

        return completeReceipt(purchaseOrderId, pendingReceipt.receiptId());
    }

    private PurchaseOrder requestReceipt(UUID purchaseOrderId) {
        return transactions.execute(status -> {
            var purchaseOrder = purchaseOrders.findById(purchaseOrderId)
                    .orElseThrow(() -> new NoSuchElementException("Purchase order was not found."));

            if (purchaseOrder.status() == PurchaseOrderStatus.RECEIVED) return purchaseOrder;
            return purchaseOrders.save(purchaseOrder.requestReceipt(Instant.now(clock)));
        });
    }

    private PurchaseOrder completeReceipt(UUID purchaseOrderId, UUID receiptId) {
        return transactions.execute(status -> {
            var purchaseOrder = purchaseOrders.findById(purchaseOrderId)
                    .orElseThrow(() -> new NoSuchElementException("Purchase order was not found."));

            if (purchaseOrder.status() == PurchaseOrderStatus.RECEIVED) return purchaseOrder;
            if (!receiptId.equals(purchaseOrder.receiptId())) {
                throw new IllegalStateException("Receipt does not match the purchase order.");
            }

            return purchaseOrders.save(purchaseOrder.markReceived(Instant.now(clock)));
        });
    }
}
