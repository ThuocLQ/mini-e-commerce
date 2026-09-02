package com.microshop.supplier.application;

import com.microshop.supplier.application.port.InventoryReceiptClient;
import com.microshop.supplier.application.port.ProcurementAuditRepository;
import com.microshop.supplier.application.port.PurchaseOrderRepository;
import com.microshop.supplier.domain.ProcurementAuditEvent;
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
    private final ProcurementAuditRepository auditEvents;
    private final InventoryReceiptClient inventoryReceipts;
    private final Clock clock;
    private final TransactionTemplate transactions;

    @Autowired
    public PurchaseOrderReceiptApplicationService(
            PurchaseOrderRepository purchaseOrders,
            ProcurementAuditRepository auditEvents,
            InventoryReceiptClient inventoryReceipts,
            PlatformTransactionManager transactionManager) {
        this(purchaseOrders, auditEvents, inventoryReceipts, Clock.systemUTC(), new TransactionTemplate(transactionManager));
    }

    PurchaseOrderReceiptApplicationService(
            PurchaseOrderRepository purchaseOrders,
            ProcurementAuditRepository auditEvents,
            InventoryReceiptClient inventoryReceipts,
            Clock clock,
            TransactionTemplate transactions) {
        this.purchaseOrders = purchaseOrders;
        this.auditEvents = auditEvents;
        this.inventoryReceipts = inventoryReceipts;
        this.clock = clock;
        this.transactions = transactions;
    }

    public PurchaseOrder receivePurchaseOrder(UUID purchaseOrderId, OperationContext context) {
        var request = requestReceipt(purchaseOrderId, context);
        if (request.purchaseOrder().status() == PurchaseOrderStatus.RECEIVED) return request.purchaseOrder();

        var pendingReceipt = request.purchaseOrder();
        var response = inventoryReceipts.receive(new InventoryReceiptClient.InventoryStockReceipt(
                pendingReceipt.receiptId(),
                pendingReceipt.id(),
                pendingReceipt.lines().stream()
                        .map(line -> new InventoryReceiptClient.InventoryStockReceiptItem(line.productId(), line.quantity()))
                        .toList()));

        if (!pendingReceipt.receiptId().equals(response.receiptId())) {
            throw new IllegalStateException("Inventory confirmed a different receipt.");
        }

        return completeReceipt(purchaseOrderId, pendingReceipt.receiptId(), context).purchaseOrder();
    }

    private ReceiptRequest requestReceipt(UUID purchaseOrderId, OperationContext context) {
        return transactions.execute(status -> {
            var purchaseOrder = purchaseOrders.findByIdForUpdate(purchaseOrderId)
                    .orElseThrow(() -> new NoSuchElementException("Purchase order was not found."));
            if (purchaseOrder.status() == PurchaseOrderStatus.RECEIVED || purchaseOrder.status() == PurchaseOrderStatus.RECEIPT_PENDING) {
                return new ReceiptRequest(purchaseOrder, false);
            }

            var now = Instant.now(clock);
            var pending = purchaseOrders.save(purchaseOrder.requestReceipt(now));
            auditEvents.save(ProcurementAuditEvent.create(
                    pending.supplierId(), pending.id(), pending.receiptId(), "purchase-order.receipt-requested", context.actor(), context.correlationId(), now));
            return new ReceiptRequest(pending, true);
        });
    }

    private ReceiptCompletion completeReceipt(UUID purchaseOrderId, UUID receiptId, OperationContext context) {
        return transactions.execute(status -> {
            var purchaseOrder = purchaseOrders.findByIdForUpdate(purchaseOrderId)
                    .orElseThrow(() -> new NoSuchElementException("Purchase order was not found."));
            if (purchaseOrder.status() == PurchaseOrderStatus.RECEIVED) {
                return new ReceiptCompletion(purchaseOrder, false);
            }
            if (!receiptId.equals(purchaseOrder.receiptId())) {
                throw new IllegalStateException("Receipt does not match the purchase order.");
            }

            var now = Instant.now(clock);
            var received = purchaseOrders.save(purchaseOrder.markReceived(now));
            auditEvents.save(ProcurementAuditEvent.create(
                    received.supplierId(), received.id(), received.receiptId(), "purchase-order.received", context.actor(), context.correlationId(), now));
            return new ReceiptCompletion(received, true);
        });
    }

    private record ReceiptRequest(PurchaseOrder purchaseOrder, boolean requestedNow) { }
    private record ReceiptCompletion(PurchaseOrder purchaseOrder, boolean completedNow) { }
}