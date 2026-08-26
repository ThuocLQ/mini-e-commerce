package com.microshop.supplier.application;

public class InventoryReceiptRejectedException extends RuntimeException {
    public InventoryReceiptRejectedException() {
        super("Inventory rejected the goods receipt. Review the purchase-order products before retrying.");
    }
}
