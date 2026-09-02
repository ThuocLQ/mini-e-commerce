package com.microshop.supplier.infrastructure.persistence;

import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Optional;
import java.util.UUID;

interface SpringDataPurchaseOrderRepository extends JpaRepository<PurchaseOrderEntity, UUID> {
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select purchaseOrder from PurchaseOrderEntity purchaseOrder where purchaseOrder.id = :id")
    Optional<PurchaseOrderEntity> findByIdForUpdate(@Param("id") UUID id);
}