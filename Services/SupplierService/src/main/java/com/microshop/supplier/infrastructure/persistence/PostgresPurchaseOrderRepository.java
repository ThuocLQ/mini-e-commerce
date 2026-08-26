package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.application.port.PurchaseOrderRepository;
import com.microshop.supplier.domain.PurchaseOrder;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

@Repository
class PostgresPurchaseOrderRepository implements PurchaseOrderRepository {
    private final SpringDataPurchaseOrderRepository repository;

    PostgresPurchaseOrderRepository(SpringDataPurchaseOrderRepository repository) {
        this.repository = repository;
    }

    @Override
    public PurchaseOrder save(PurchaseOrder purchaseOrder) {
        var entity = repository.findById(purchaseOrder.id()).orElseGet(() -> PurchaseOrderEntity.fromDomain(purchaseOrder));
        if (entity.id != null) entity.apply(purchaseOrder);
        return repository.save(entity).toDomain();
    }

    @Override
    public Optional<PurchaseOrder> findById(UUID purchaseOrderId) {
        return repository.findById(purchaseOrderId).map(PurchaseOrderEntity::toDomain);
    }

    @Override
    public List<PurchaseOrder> findAll() {
        return repository.findAllByOrderByCreatedAtUtcDesc().stream().map(PurchaseOrderEntity::toDomain).toList();
    }
}