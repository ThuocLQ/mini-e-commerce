package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.domain.PurchaseOrder;
import com.microshop.supplier.domain.PurchaseOrderLine;
import com.microshop.supplier.domain.PurchaseOrderStatus;
import jakarta.persistence.CascadeType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.OneToMany;
import jakarta.persistence.OrderBy;
import jakarta.persistence.Table;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

@Entity
@Table(name = "purchase_orders")
class PurchaseOrderEntity {
    @Id UUID id;
    @Column(name = "purchase_order_number", nullable = false, unique = true, length = 48) String purchaseOrderNumber;
    @Column(name = "supplier_id", nullable = false) UUID supplierId;
    @Enumerated(EnumType.STRING) @Column(nullable = false, length = 32) PurchaseOrderStatus status;
    @Column(nullable = false, length = 3) String currency;
    @Column(name = "created_at_utc", nullable = false) Instant createdAtUtc;
    @Column(name = "submitted_at_utc") Instant submittedAtUtc;
    @Column(name = "receipt_id") UUID receiptId;
    @Column(name = "receipt_requested_at_utc") Instant receiptRequestedAtUtc;
    @Column(name = "received_at_utc") Instant receivedAtUtc;
    @OneToMany(mappedBy = "purchaseOrder", cascade = CascadeType.ALL, orphanRemoval = true, fetch = FetchType.EAGER)
    @OrderBy("id")
    List<PurchaseOrderLineEntity> lines = new ArrayList<>();

    protected PurchaseOrderEntity() { }

    static PurchaseOrderEntity fromDomain(PurchaseOrder purchaseOrder) {
        var entity = new PurchaseOrderEntity();
        entity.apply(purchaseOrder);
        return entity;
    }

    void apply(PurchaseOrder purchaseOrder) {
        id = purchaseOrder.id();
        purchaseOrderNumber = purchaseOrder.purchaseOrderNumber();
        supplierId = purchaseOrder.supplierId();
        status = purchaseOrder.status();
        currency = purchaseOrder.currency();
        createdAtUtc = purchaseOrder.createdAtUtc();
        submittedAtUtc = purchaseOrder.submittedAtUtc();
        receiptId = purchaseOrder.receiptId();
        receiptRequestedAtUtc = purchaseOrder.receiptRequestedAtUtc();
        receivedAtUtc = purchaseOrder.receivedAtUtc();
        lines.clear();
        purchaseOrder.lines().forEach(line -> lines.add(PurchaseOrderLineEntity.fromDomain(this, line)));
    }

    PurchaseOrder toDomain() {
        return new PurchaseOrder(id, purchaseOrderNumber, supplierId, status, currency, lines.stream().map(PurchaseOrderLineEntity::toDomain).toList(), createdAtUtc, submittedAtUtc, receiptId, receiptRequestedAtUtc, receivedAtUtc);
    }
}
