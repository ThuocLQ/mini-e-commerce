package com.microshop.supplier.application.port;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.domain.ProcurementAuditEvent;

import java.util.UUID;

public interface ProcurementAuditRepository {
    ProcurementAuditEvent save(ProcurementAuditEvent event);
    PagedResult<ProcurementAuditEvent> findPage(UUID purchaseOrderId, int page, int pageSize);
}