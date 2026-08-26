package com.microshop.supplier;

import com.microshop.supplier.domain.PurchaseOrder;
import com.microshop.supplier.domain.PurchaseOrderLine;
import com.microshop.supplier.domain.PurchaseOrderStatus;
import org.junit.jupiter.api.Test;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class PurchaseOrderTest {
    @Test
    void draftCanBeSubmittedOnlyOnce() {
        var draft = PurchaseOrder.draft("PO-20260825-TEST", UUID.randomUUID(), "usd", List.of(PurchaseOrderLine.create("product-1", "Keyboard", 2, new BigDecimal("109.00"))), Instant.parse("2026-08-25T00:00:00Z"));
        var submitted = draft.submit(Instant.parse("2026-08-25T01:00:00Z"));

        assertEquals(PurchaseOrderStatus.SUBMITTED, submitted.status());
        assertThrows(IllegalStateException.class, () -> submitted.submit(Instant.parse("2026-08-25T02:00:00Z")));
    }

    @Test
    void draftRejectsDuplicateProducts() {
        var supplierId = UUID.randomUUID();
        var first = PurchaseOrderLine.create("product-1", "Keyboard", 1, new BigDecimal("109.00"));
        var second = PurchaseOrderLine.create("product-1", "Keyboard duplicate", 1, new BigDecimal("109.00"));

        assertThrows(IllegalArgumentException.class, () -> PurchaseOrder.draft("PO-20260825-DUP", supplierId, "USD", List.of(first, second), Instant.now()));
    }

    @Test
    void receiptIsStableAcrossRetriesAndCanOnlyStartAfterSubmission() {
        var now = Instant.parse("2026-08-25T00:00:00Z");
        var draft = PurchaseOrder.draft("PO-20260825-RECEIPT", UUID.randomUUID(), "USD", List.of(PurchaseOrderLine.create("product-1", "Keyboard", 2, new BigDecimal("109.00"))), now);

        assertThrows(IllegalStateException.class, () -> draft.requestReceipt(now));

        var pending = draft.submit(now.plusSeconds(60)).requestReceipt(now.plusSeconds(120));
        var retried = pending.requestReceipt(now.plusSeconds(180));
        var received = pending.markReceived(now.plusSeconds(240));

        assertEquals(PurchaseOrderStatus.RECEIPT_PENDING, pending.status());
        assertEquals(pending.receiptId(), retried.receiptId());
        assertEquals(PurchaseOrderStatus.RECEIVED, received.status());
        assertEquals(pending.receiptId(), received.receiptId());
        assertEquals(received, received.markReceived(now.plusSeconds(300)));
    }}
