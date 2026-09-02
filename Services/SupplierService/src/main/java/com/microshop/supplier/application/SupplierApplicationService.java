package com.microshop.supplier.application;

import com.microshop.supplier.application.port.ProcurementAuditRepository;
import com.microshop.supplier.application.port.PurchaseOrderRepository;
import com.microshop.supplier.application.port.SupplierRepository;
import com.microshop.supplier.domain.ProcurementAuditEvent;
import com.microshop.supplier.domain.PurchaseOrder;
import com.microshop.supplier.domain.PurchaseOrderLine;
import com.microshop.supplier.domain.Supplier;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.NoSuchElementException;
import java.util.UUID;

@Service
public class SupplierApplicationService {
    private static final DateTimeFormatter NUMBER_DATE = DateTimeFormatter.ofPattern("uuuuMMdd").withZone(ZoneOffset.UTC);

    private final SupplierRepository suppliers;
    private final PurchaseOrderRepository purchaseOrders;
    private final ProcurementAuditRepository auditEvents;
    private final Clock clock;

    @Autowired
    public SupplierApplicationService(
            SupplierRepository suppliers,
            PurchaseOrderRepository purchaseOrders,
            ProcurementAuditRepository auditEvents) {
        this(suppliers, purchaseOrders, auditEvents, Clock.systemUTC());
    }

    SupplierApplicationService(
            SupplierRepository suppliers,
            PurchaseOrderRepository purchaseOrders,
            ProcurementAuditRepository auditEvents,
            Clock clock) {
        this.suppliers = suppliers;
        this.purchaseOrders = purchaseOrders;
        this.auditEvents = auditEvents;
        this.clock = clock;
    }

    @Transactional
    public Supplier createSupplier(String name, String contactEmail, OperationContext context) {
        var now = Instant.now(clock);
        var supplier = suppliers.save(Supplier.create(name, contactEmail, now));
        auditEvents.save(ProcurementAuditEvent.create(
                supplier.id(), null, null, "supplier.created", context.actor(), context.correlationId(), now));
        return supplier;
    }

    @Transactional(readOnly = true)
    public PagedResult<Supplier> getSuppliers(int page, int pageSize) {
        return suppliers.findPage(page, pageSize);
    }

    @Transactional
    public PurchaseOrder createDraftPurchaseOrder(
            UUID supplierId,
            String currency,
            List<PurchaseOrderLineInput> lines,
            OperationContext context) {
        var supplier = suppliers.findById(supplierId).orElseThrow(() -> new NoSuchElementException("Supplier was not found."));
        if (!supplier.active()) throw new IllegalStateException("Purchase orders cannot be created for an inactive supplier.");

        var purchaseOrderLines = lines.stream()
                .map(line -> PurchaseOrderLine.create(line.productId(), line.productName(), line.quantity(), line.unitCost()))
                .toList();
        var now = Instant.now(clock);
        var number = "PO-" + NUMBER_DATE.format(now) + "-" + UUID.randomUUID().toString().substring(0, 8).toUpperCase();
        var purchaseOrder = purchaseOrders.save(PurchaseOrder.draft(number, supplierId, currency, purchaseOrderLines, now));
        auditEvents.save(ProcurementAuditEvent.create(
                supplierId, purchaseOrder.id(), null, "purchase-order.created", context.actor(), context.correlationId(), now));
        return purchaseOrder;
    }

    @Transactional
    public PurchaseOrder submitPurchaseOrder(UUID purchaseOrderId, OperationContext context) {
        var existing = purchaseOrders.findByIdForUpdate(purchaseOrderId)
                .orElseThrow(() -> new NoSuchElementException("Purchase order was not found."));
        var now = Instant.now(clock);
        var purchaseOrder = purchaseOrders.save(existing.submit(now));
        auditEvents.save(ProcurementAuditEvent.create(
                purchaseOrder.supplierId(), purchaseOrder.id(), null, "purchase-order.submitted", context.actor(), context.correlationId(), now));
        return purchaseOrder;
    }

    @Transactional(readOnly = true)
    public PagedResult<PurchaseOrder> getPurchaseOrders(int page, int pageSize) {
        return purchaseOrders.findPage(page, pageSize);
    }

    public record PurchaseOrderLineInput(String productId, String productName, int quantity, BigDecimal unitCost) { }
}