package com.microshop.supplier.application.port;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.domain.PurchaseOrder;

import java.util.Optional;
import java.util.UUID;

public interface PurchaseOrderRepository {
    PurchaseOrder save(PurchaseOrder purchaseOrder);
    Optional<PurchaseOrder> findById(UUID purchaseOrderId);
    Optional<PurchaseOrder> findByIdForUpdate(UUID purchaseOrderId);
    PagedResult<PurchaseOrder> findPage(int page, int pageSize);
}