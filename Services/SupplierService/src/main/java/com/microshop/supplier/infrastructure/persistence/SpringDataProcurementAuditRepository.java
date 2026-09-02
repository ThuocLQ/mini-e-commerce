package com.microshop.supplier.infrastructure.persistence;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.UUID;

interface SpringDataProcurementAuditRepository extends JpaRepository<ProcurementAuditEventEntity, UUID> {
    Page<ProcurementAuditEventEntity> findByPurchaseOrderId(UUID purchaseOrderId, Pageable pageable);
}