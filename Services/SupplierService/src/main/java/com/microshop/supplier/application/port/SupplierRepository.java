package com.microshop.supplier.application.port;

import com.microshop.supplier.domain.Supplier;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface SupplierRepository {
    Supplier save(Supplier supplier);
    Optional<Supplier> findById(UUID supplierId);
    List<Supplier> findAll();
}