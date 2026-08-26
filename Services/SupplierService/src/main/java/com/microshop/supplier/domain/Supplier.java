package com.microshop.supplier.domain;

import java.time.Instant;
import java.util.UUID;

public record Supplier(UUID id, String name, String contactEmail, boolean active, Instant createdAtUtc, Instant updatedAtUtc) {
    public static Supplier create(String name, String contactEmail, Instant now) {
        if (name == null || name.isBlank()) throw new IllegalArgumentException("Supplier name is required.");
        if (name.trim().length() > 160) throw new IllegalArgumentException("Supplier name must be at most 160 characters.");
        if (contactEmail != null && contactEmail.trim().length() > 320) throw new IllegalArgumentException("Supplier email must be at most 320 characters.");
        return new Supplier(UUID.randomUUID(), name.trim(), blankToNull(contactEmail), true, now, now);
    }

    private static String blankToNull(String value) {
        return value == null || value.isBlank() ? null : value.trim();
    }
}