package com.microshop.supplier.application;

public class InventoryUnavailableException extends RuntimeException {
    public InventoryUnavailableException() {
        super("Inventory is currently unavailable. The receipt remains pending and can be retried safely.");
    }
}
