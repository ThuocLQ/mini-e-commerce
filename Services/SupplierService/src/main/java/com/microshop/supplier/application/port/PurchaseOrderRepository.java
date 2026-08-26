package com.microshop.supplier.application.port;

import com.microshop.supplier.domain.PurchaseOrder;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface PurchaseOrderRepository {
    PurchaseOrder save(PurchaseOrder purchaseOrder);
    Optional<PurchaseOrder> findById(UUID purchaseOrderId);
    List<PurchaseOrder> findAll();
}