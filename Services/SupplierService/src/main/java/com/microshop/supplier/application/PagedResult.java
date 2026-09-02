package com.microshop.supplier.application;

import java.util.List;

public record PagedResult<T>(
        List<T> items,
        int page,
        int pageSize,
        long totalItems,
        int totalPages) {
    public PagedResult {
        items = List.copyOf(items);
    }
}