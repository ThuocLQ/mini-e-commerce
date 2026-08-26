package com.microshop.supplier.infrastructure.persistence;

import org.springframework.data.jpa.repository.JpaRepository;
import java.util.List;
import java.util.UUID;

interface SpringDataPurchaseOrderRepository extends JpaRepository<PurchaseOrderEntity, UUID> {
    List<PurchaseOrderEntity> findAllByOrderByCreatedAtUtcDesc();
}