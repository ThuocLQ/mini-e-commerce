package com.microshop.supplier.domain;

import java.math.BigDecimal;
import java.util.UUID;

public record PurchaseOrderLine(UUID id, String productId, String productName, int quantity, BigDecimal unitCost) {
    public static PurchaseOrderLine create(String productId, String productName, int quantity, BigDecimal unitCost) {
        if (productId == null || productId.isBlank()) throw new IllegalArgumentException("Product id is required.");
        if (productName == null || productName.isBlank()) throw new IllegalArgumentException("Product name is required.");
        if (quantity <= 0) throw new IllegalArgumentException("Line quantity must be greater than zero.");
        if (unitCost == null || unitCost.signum() < 0) throw new IllegalArgumentException("Unit cost must be non-negative.");
        return new PurchaseOrderLine(UUID.randomUUID(), productId.trim(), productName.trim(), quantity, unitCost);
    }
}