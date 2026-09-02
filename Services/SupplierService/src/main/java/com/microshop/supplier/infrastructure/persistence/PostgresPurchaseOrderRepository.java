package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.application.port.PurchaseOrderRepository;
import com.microshop.supplier.domain.PurchaseOrder;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Repository;

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
    public Optional<PurchaseOrder> findByIdForUpdate(UUID purchaseOrderId) {
        return repository.findByIdForUpdate(purchaseOrderId).map(PurchaseOrderEntity::toDomain);
    }

    @Override
    public PagedResult<PurchaseOrder> findPage(int page, int pageSize) {
        var result = repository.findAll(PageRequest.of(page, pageSize, Sort.by(Sort.Direction.DESC, "createdAtUtc")));
        return new PagedResult<>(
                result.getContent().stream().map(PurchaseOrderEntity::toDomain).toList(),
                result.getNumber(),
                result.getSize(),
                result.getTotalElements(),
                result.getTotalPages());
    }
}