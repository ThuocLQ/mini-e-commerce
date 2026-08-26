package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.domain.PurchaseOrderLine;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;

import java.math.BigDecimal;
import java.util.UUID;

@Entity
@Table(name = "purchase_order_lines")
class PurchaseOrderLineEntity {
    @Id UUID id;
    @ManyToOne(fetch = FetchType.LAZY, optional = false) @JoinColumn(name = "purchase_order_id", nullable = false) PurchaseOrderEntity purchaseOrder;
    @Column(name = "product_id", nullable = false, length = 128) String productId;
    @Column(name = "product_name", nullable = false, length = 200) String productName;
    @Column(nullable = false) int quantity;
    @Column(name = "unit_cost", nullable = false, precision = 18, scale = 2) BigDecimal unitCost;

    protected PurchaseOrderLineEntity() { }

    static PurchaseOrderLineEntity fromDomain(PurchaseOrderEntity purchaseOrder, PurchaseOrderLine line) {
        var entity = new PurchaseOrderLineEntity();
        entity.id = line.id();
        entity.purchaseOrder = purchaseOrder;
        entity.productId = line.productId();
        entity.productName = line.productName();
        entity.quantity = line.quantity();
        entity.unitCost = line.unitCost();
        return entity;
    }

    PurchaseOrderLine toDomain() {
        return new PurchaseOrderLine(id, productId, productName, quantity, unitCost);
    }
}