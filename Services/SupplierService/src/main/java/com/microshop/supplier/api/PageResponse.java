package com.microshop.supplier.api;

import com.microshop.supplier.application.PagedResult;

import java.util.List;
import java.util.function.Function;

public record PageResponse<T>(List<T> items, int page, int pageSize, long totalItems, int totalPages) {
    static <TSource, TResponse> PageResponse<TResponse> from(PagedResult<TSource> result, Function<TSource, TResponse> mapper) {
        return new PageResponse<>(
                result.items().stream().map(mapper).toList(),
                result.page(),
                result.pageSize(),
                result.totalItems(),
                result.totalPages());
    }
}