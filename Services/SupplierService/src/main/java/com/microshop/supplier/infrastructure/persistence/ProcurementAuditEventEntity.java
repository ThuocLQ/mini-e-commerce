package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.domain.ProcurementAuditEvent;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "procurement_audit_events")
class ProcurementAuditEventEntity {
    @Id UUID id;
    @Column(name = "supplier_id") UUID supplierId;
    @Column(name = "purchase_order_id") UUID purchaseOrderId;
    @Column(name = "receipt_id") UUID receiptId;
    @Column(nullable = false, length = 96) String action;
    @Column(nullable = false, length = 200) String actor;
    @Column(name = "correlation_id", length = 128) String correlationId;
    @Column(name = "occurred_at_utc", nullable = false) Instant occurredAtUtc;

    protected ProcurementAuditEventEntity() { }

    static ProcurementAuditEventEntity fromDomain(ProcurementAuditEvent event) {
        var entity = new ProcurementAuditEventEntity();
        entity.id = event.id();
        entity.supplierId = event.supplierId();
        entity.purchaseOrderId = event.purchaseOrderId();
        entity.receiptId = event.receiptId();
        entity.action = event.action();
        entity.actor = event.actor();
        entity.correlationId = event.correlationId();
        entity.occurredAtUtc = event.occurredAtUtc();
        return entity;
    }

    ProcurementAuditEvent toDomain() {
        return new ProcurementAuditEvent(id, supplierId, purchaseOrderId, receiptId, action, actor, correlationId, occurredAtUtc);
    }
}