package com.microshop.supplier.api;

import com.microshop.supplier.application.OperationContext;
import com.microshop.supplier.application.SupplierApplicationService;
import com.microshop.supplier.domain.Supplier;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.Authentication;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.time.Instant;
import java.util.UUID;

@RestController
@Validated
@RequestMapping("/suppliers")
@PreAuthorize("hasRole('ADMIN')")
public class SupplierController {
    private final SupplierApplicationService service;

    public SupplierController(SupplierApplicationService service) {
        this.service = service;
    }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    SupplierResponse create(
            @Valid @RequestBody CreateSupplierRequest request,
            Authentication authentication,
            HttpServletRequest httpRequest) {
        return SupplierResponse.from(service.createSupplier(request.name(), request.contactEmail(), operationContext(authentication, httpRequest)));
    }

    @GetMapping
    PageResponse<SupplierResponse> list(
            @RequestParam(defaultValue = "0") @Min(0) int page,
            @RequestParam(defaultValue = "25") @Min(1) @Max(100) int pageSize) {
        return PageResponse.from(service.getSuppliers(page, pageSize), SupplierResponse::from);
    }

    static OperationContext operationContext(Authentication authentication, HttpServletRequest request) {
        return new OperationContext(authentication == null ? null : authentication.getName(), request.getHeader("X-Correlation-ID"));
    }

    public record CreateSupplierRequest(
            @NotBlank @Size(max = 160) String name,
            @Email @Size(max = 320) String contactEmail) { }

    public record SupplierResponse(
            UUID id,
            String name,
            String contactEmail,
            boolean active,
            Instant createdAtUtc,
            Instant updatedAtUtc) {
        static SupplierResponse from(Supplier supplier) {
            return new SupplierResponse(
                    supplier.id(),
                    supplier.name(),
                    supplier.contactEmail(),
                    supplier.active(),
                    supplier.createdAtUtc(),
                    supplier.updatedAtUtc());
        }
    }
}