package com.microshop.supplier.application;

import com.microshop.supplier.application.port.PurchaseOrderRepository;
import com.microshop.supplier.application.port.SupplierRepository;
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
    private final Clock clock;

    @Autowired
    public SupplierApplicationService(SupplierRepository suppliers, PurchaseOrderRepository purchaseOrders) {
        this(suppliers, purchaseOrders, Clock.systemUTC());
    }

    SupplierApplicationService(SupplierRepository suppliers, PurchaseOrderRepository purchaseOrders, Clock clock) {
        this.suppliers = suppliers;
        this.purchaseOrders = purchaseOrders;
        this.clock = clock;
    }

    @Transactional
    public Supplier createSupplier(String name, String contactEmail) {
        return suppliers.save(Supplier.create(name, contactEmail, Instant.now(clock)));
    }

    @Transactional(readOnly = true)
    public List<Supplier> getSuppliers() {
        return suppliers.findAll();
    }

    @Transactional
    public PurchaseOrder createDraftPurchaseOrder(UUID supplierId, String currency, List<PurchaseOrderLineInput> lines) {
        var supplier = suppliers.findById(supplierId).orElseThrow(() -> new NoSuchElementException("Supplier was not found."));
        if (!supplier.active()) throw new IllegalStateException("Purchase orders cannot be created for an inactive supplier.");
        var purchaseOrderLines = lines.stream().map(line -> PurchaseOrderLine.create(line.productId(), line.productName(), line.quantity(), line.unitCost())).toList();
        var now = Instant.now(clock);
        var number = "PO-" + NUMBER_DATE.format(now) + "-" + UUID.randomUUID().toString().substring(0, 8).toUpperCase();
        return purchaseOrders.save(PurchaseOrder.draft(number, supplierId, currency, purchaseOrderLines, now));
    }

    @Transactional
    public PurchaseOrder submitPurchaseOrder(UUID purchaseOrderId) {
        var existing = purchaseOrders.findById(purchaseOrderId).orElseThrow(() -> new NoSuchElementException("Purchase order was not found."));
        return purchaseOrders.save(existing.submit(Instant.now(clock)));
    }

    @Transactional(readOnly = true)
    public List<PurchaseOrder> getPurchaseOrders() {
        return purchaseOrders.findAll();
    }

    public record PurchaseOrderLineInput(String productId, String productName, int quantity, BigDecimal unitCost) { }
}