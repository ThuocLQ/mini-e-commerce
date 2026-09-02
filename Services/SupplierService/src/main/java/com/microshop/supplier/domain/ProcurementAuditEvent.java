package com.microshop.supplier.domain;

import java.time.Instant;
import java.util.UUID;

public record ProcurementAuditEvent(
        UUID id,
        UUID supplierId,
        UUID purchaseOrderId,
        UUID receiptId,
        String action,
        String actor,
        String correlationId,
        Instant occurredAtUtc) {
    public static ProcurementAuditEvent create(
            UUID supplierId,
            UUID purchaseOrderId,
            UUID receiptId,
            String action,
            String actor,
            String correlationId,
            Instant occurredAtUtc) {
        if (action == null || action.isBlank()) throw new IllegalArgumentException("Audit action is required.");
        if (actor == null || actor.isBlank()) throw new IllegalArgumentException("Audit actor is required.");
        return new ProcurementAuditEvent(
                UUID.randomUUID(),
                supplierId,
                purchaseOrderId,
                receiptId,
                action.trim(),
                actor.trim(),
                correlationId == null || correlationId.isBlank() ? null : correlationId.trim(),
                occurredAtUtc);
    }
}