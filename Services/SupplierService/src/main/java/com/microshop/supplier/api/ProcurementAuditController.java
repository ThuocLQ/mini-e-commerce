package com.microshop.supplier.api;

import com.microshop.supplier.application.PagedResult;
import com.microshop.supplier.application.port.ProcurementAuditRepository;
import com.microshop.supplier.domain.ProcurementAuditEvent;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import java.time.Instant;
import java.util.UUID;

@RestController
@Validated
@RequestMapping("/procurement/audit")
@PreAuthorize("hasRole('ADMIN')")
public class ProcurementAuditController {
    private final ProcurementAuditRepository auditEvents;

    public ProcurementAuditController(ProcurementAuditRepository auditEvents) {
        this.auditEvents = auditEvents;
    }

    @GetMapping
    PageResponse<AuditEventResponse> list(
            @RequestParam(required = false) UUID purchaseOrderId,
            @RequestParam(defaultValue = "0") @Min(0) int page,
            @RequestParam(defaultValue = "25") @Min(1) @Max(100) int pageSize) {
        PagedResult<ProcurementAuditEvent> result = auditEvents.findPage(purchaseOrderId, page, pageSize);
        return PageResponse.from(result, AuditEventResponse::from);
    }

    public record AuditEventResponse(
            UUID id,
            UUID supplierId,
            UUID purchaseOrderId,
            UUID receiptId,
            String action,
            String actor,
            String correlationId,
            Instant occurredAtUtc) {
        static AuditEventResponse from(ProcurementAuditEvent event) {
            return new AuditEventResponse(
                    event.id(), event.supplierId(), event.purchaseOrderId(), event.receiptId(), event.action(),
                    event.actor(), event.correlationId(), event.occurredAtUtc());
        }
    }
}