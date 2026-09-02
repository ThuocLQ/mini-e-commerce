package com.microshop.supplier.api;

import com.microshop.supplier.application.SupplierApplicationService;
import com.microshop.supplier.application.PurchaseOrderReceiptApplicationService;
import com.microshop.supplier.domain.PurchaseOrder;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.Authentication;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

@RestController
@Validated
@RequestMapping("/procurement/purchase-orders")
@PreAuthorize("hasRole('ADMIN')")
public class PurchaseOrderController {
    private final SupplierApplicationService service;
    private final PurchaseOrderReceiptApplicationService receiptService;

    public PurchaseOrderController(
            SupplierApplicationService service,
            PurchaseOrderReceiptApplicationService receiptService) {
        this.service = service;
        this.receiptService = receiptService;
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    PurchaseOrderResponse create(
            @Valid @RequestBody CreatePurchaseOrderRequest request,
            Authentication authentication,
            HttpServletRequest httpRequest) {
        var lines = request.lines().stream()
                .map(line -> new SupplierApplicationService.PurchaseOrderLineInput(
                        line.productId(), line.productName(), line.quantity(), line.unitCost()))
                .toList();
        return PurchaseOrderResponse.from(service.createDraftPurchaseOrder(
                request.supplierId(), request.currency(), lines, SupplierController.operationContext(authentication, httpRequest)));
    }

    @PostMapping("/{purchaseOrderId}/submit")
    PurchaseOrderResponse submit(
            @PathVariable UUID purchaseOrderId,
            Authentication authentication,
            HttpServletRequest httpRequest) {
        return PurchaseOrderResponse.from(service.submitPurchaseOrder(
                purchaseOrderId, SupplierController.operationContext(authentication, httpRequest)));
    }

    @PostMapping("/{purchaseOrderId}/receive")
    PurchaseOrderResponse receive(
            @PathVariable UUID purchaseOrderId,
            Authentication authentication,
            HttpServletRequest httpRequest) {
        return PurchaseOrderResponse.from(receiptService.receivePurchaseOrder(
                purchaseOrderId, SupplierController.operationContext(authentication, httpRequest)));
    }

    @GetMapping
    PageResponse<PurchaseOrderResponse> list(
            @RequestParam(defaultValue = "0") @Min(0) int page,
            @RequestParam(defaultValue = "25") @Min(1) @Max(100) int pageSize) {
        return PageResponse.from(service.getPurchaseOrders(page, pageSize), PurchaseOrderResponse::from);
    }

    public record CreatePurchaseOrderRequest(
            @NotNull UUID supplierId,
            @NotBlank @Pattern(regexp = "[A-Za-z]{3}") String currency,
            @NotEmpty List<@Valid PurchaseOrderLineRequest> lines) { }

    public record PurchaseOrderLineRequest(
            @NotBlank @Size(max = 128) String productId,
            @NotBlank @Size(max = 200) String productName,
            @Positive int quantity,
            @NotNull @DecimalMin(value = "0.00") BigDecimal unitCost) { }

    public record PurchaseOrderResponse(
            UUID id,
            String number,
            UUID supplierId,
            String status,
            String currency,
            List<PurchaseOrderLineResponse> lines,
            Instant createdAtUtc,
            Instant submittedAtUtc,
            UUID receiptId,
            Instant receiptRequestedAtUtc,
            Instant receivedAtUtc) {
        static PurchaseOrderResponse from(PurchaseOrder purchaseOrder) {
            return new PurchaseOrderResponse(
                    purchaseOrder.id(),
                    purchaseOrder.purchaseOrderNumber(),
                    purchaseOrder.supplierId(),
                    purchaseOrder.status().name(),
                    purchaseOrder.currency(),
                    purchaseOrder.lines().stream()
                            .map(line -> new PurchaseOrderLineResponse(
                                    line.id(), line.productId(), line.productName(), line.quantity(), line.unitCost()))
                            .toList(),
                    purchaseOrder.createdAtUtc(),
                    purchaseOrder.submittedAtUtc(),
                    purchaseOrder.receiptId(),
                    purchaseOrder.receiptRequestedAtUtc(),
                    purchaseOrder.receivedAtUtc());
        }
    }

    public record PurchaseOrderLineResponse(UUID id, String productId, String productName, int quantity, BigDecimal unitCost) { }
}