package com.microshop.supplier.domain;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

public record PurchaseOrder(
        UUID id,
        String purchaseOrderNumber,
        UUID supplierId,
        PurchaseOrderStatus status,
        String currency,
        List<PurchaseOrderLine> lines,
        Instant createdAtUtc,
        Instant submittedAtUtc,
        UUID receiptId,
        Instant receiptRequestedAtUtc,
        Instant receivedAtUtc) {
    public static PurchaseOrder draft(String purchaseOrderNumber, UUID supplierId, String currency, List<PurchaseOrderLine> lines, Instant now) {
        if (supplierId == null) throw new IllegalArgumentException("Supplier id is required.");
        if (currency == null || !currency.matches("[A-Za-z]{3}")) throw new IllegalArgumentException("Currency must be a three-letter ISO code.");
        if (lines == null || lines.isEmpty()) throw new IllegalArgumentException("A purchase order must contain at least one line.");
        var distinctProducts = lines.stream().map(PurchaseOrderLine::productId).distinct().count();
        if (distinctProducts != lines.size()) throw new IllegalArgumentException("A purchase order cannot contain duplicate products.");
        return new PurchaseOrder(UUID.randomUUID(), purchaseOrderNumber, supplierId, PurchaseOrderStatus.DRAFT, currency.toUpperCase(), List.copyOf(lines), now, null, null, null, null);
    }

    public PurchaseOrder submit(Instant now) {
        if (status != PurchaseOrderStatus.DRAFT) throw new IllegalStateException("Only draft purchase orders can be submitted.");
        return new PurchaseOrder(id, purchaseOrderNumber, supplierId, PurchaseOrderStatus.SUBMITTED, currency, lines, createdAtUtc, now, receiptId, receiptRequestedAtUtc, receivedAtUtc);
    }

    public PurchaseOrder requestReceipt(Instant now) {
        if (status == PurchaseOrderStatus.RECEIPT_PENDING) return this;
        if (status != PurchaseOrderStatus.SUBMITTED) throw new IllegalStateException("Only submitted purchase orders can be received.");
        return new PurchaseOrder(id, purchaseOrderNumber, supplierId, PurchaseOrderStatus.RECEIPT_PENDING, currency, lines, createdAtUtc, submittedAtUtc, UUID.randomUUID(), now, null);
    }

    public PurchaseOrder markReceived(Instant now) {
        if (status == PurchaseOrderStatus.RECEIVED) return this;
        if (status != PurchaseOrderStatus.RECEIPT_PENDING || receiptId == null) throw new IllegalStateException("A pending receipt is required before marking a purchase order as received.");
        return new PurchaseOrder(id, purchaseOrderNumber, supplierId, PurchaseOrderStatus.RECEIVED, currency, lines, createdAtUtc, submittedAtUtc, receiptId, receiptRequestedAtUtc, now);
    }
}
