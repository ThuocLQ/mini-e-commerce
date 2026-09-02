package com.microshop.supplier.application.port;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.domain.Supplier;

import java.util.Optional;
import java.util.UUID;

public interface SupplierRepository {
    Supplier save(Supplier supplier);
    Optional<Supplier> findById(UUID supplierId);
    PagedResult<Supplier> findPage(int page, int pageSize);
}