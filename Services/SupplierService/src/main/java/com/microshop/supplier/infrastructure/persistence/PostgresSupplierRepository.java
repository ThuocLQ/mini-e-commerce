package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.application.port.SupplierRepository;
import com.microshop.supplier.domain.Supplier;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Repository;

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
    public PagedResult<Supplier> findPage(int page, int pageSize) {
        var result = repository.findAll(PageRequest.of(page, pageSize, Sort.by(Sort.Direction.DESC, "createdAtUtc")));
        return new PagedResult<>(
                result.getContent().stream().map(SupplierEntity::toDomain).toList(),
                result.getNumber(),
                result.getSize(),
                result.getTotalElements(),
                result.getTotalPages());
    }
}