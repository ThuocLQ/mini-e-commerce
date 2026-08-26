package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.application.port.SupplierRepository;
import com.microshop.supplier.domain.Supplier;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
class PostgresSupplierRepository implements SupplierRepository {
    private final SpringDataSupplierRepository repository;

    PostgresSupplierRepository(SpringDataSupplierRepository repository) {
        this.repository = repository;
    }

    @Override
    public Supplier save(Supplier supplier) {
        return repository.save(SupplierEntity.fromDomain(supplier)).toDomain();
    }

    @Override
    public Optional<Supplier> findById(UUID supplierId) {
        return repository.findById(supplierId).map(SupplierEntity::toDomain);
    }

    @Override
    public List<Supplier> findAll() {
        return repository.findAll().stream().map(SupplierEntity::toDomain).toList();
    }
}