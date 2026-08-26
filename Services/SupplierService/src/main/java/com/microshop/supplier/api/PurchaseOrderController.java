package com.microshop.supplier.api;

import com.microshop.supplier.application.PurchaseOrderReceiptApplicationService;
import com.microshop.supplier.application.SupplierApplicationService;
import com.microshop.supplier.domain.PurchaseOrder;
import jakarta.validation.Valid;
import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Positive;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

@RestController
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
    PurchaseOrderResponse create(@Valid @RequestBody CreatePurchaseOrderRequest request) {
        var lines = request.lines().stream().map(line -> new SupplierApplicationService.PurchaseOrderLineInput(line.productId(), line.productName(), line.quantity(), line.unitCost())).toList();
        return PurchaseOrderResponse.from(service.createDraftPurchaseOrder(request.supplierId(), request.currency(), lines));
    }

    @PostMapping("/{purchaseOrderId}/submit")
    PurchaseOrderResponse submit(@PathVariable UUID purchaseOrderId) {
        return PurchaseOrderResponse.from(service.submitPurchaseOrder(purchaseOrderId));
    }

    @PostMapping("/{purchaseOrderId}/receive")
    PurchaseOrderResponse receive(@PathVariable UUID purchaseOrderId) {
        return PurchaseOrderResponse.from(receiptService.receivePurchaseOrder(purchaseOrderId));
    }

    @GetMapping
    List<PurchaseOrderResponse> list() {
        return service.getPurchaseOrders().stream().map(PurchaseOrderResponse::from).toList();
    }

    public record CreatePurchaseOrderRequest(@NotNull UUID supplierId, @NotBlank @Pattern(regexp = "[A-Za-z]{3}") String currency, @NotEmpty List<@Valid PurchaseOrderLineRequest> lines) { }
    public record PurchaseOrderLineRequest(@NotBlank @Size(max = 128) String productId, @NotBlank @Size(max = 200) String productName, @Positive int quantity, @NotNull @DecimalMin(value = "0.00") BigDecimal unitCost) { }
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
                            .map(line -> new PurchaseOrderLineResponse(line.id(), line.productId(), line.productName(), line.quantity(), line.unitCost()))
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
