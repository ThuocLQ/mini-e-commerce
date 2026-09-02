package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.application.port.ProcurementAuditRepository;
import com.microshop.supplier.domain.ProcurementAuditEvent;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Repository;

import java.util.UUID;

@Repository
class PostgresProcurementAuditRepository implements ProcurementAuditRepository {
    private final SpringDataProcurementAuditRepository repository;

    PostgresProcurementAuditRepository(SpringDataProcurementAuditRepository repository) {
        this.repository = repository;
    }

    @Override
    public ProcurementAuditEvent save(ProcurementAuditEvent event) {
        return repository.save(ProcurementAuditEventEntity.fromDomain(event)).toDomain();
    }

    @Override
    public PagedResult<ProcurementAuditEvent> findPage(UUID purchaseOrderId, int page, int pageSize) {
        var pageable = PageRequest.of(page, pageSize, Sort.by(Sort.Direction.DESC, "occurredAtUtc"));
        var result = purchaseOrderId == null ? repository.findAll(pageable) : repository.findByPurchaseOrderId(purchaseOrderId, pageable);
        return new PagedResult<>(
                result.getContent().stream().map(ProcurementAuditEventEntity::toDomain).toList(),
                result.getNumber(),
                result.getSize(),
                result.getTotalElements(),
                result.getTotalPages());
    }
}