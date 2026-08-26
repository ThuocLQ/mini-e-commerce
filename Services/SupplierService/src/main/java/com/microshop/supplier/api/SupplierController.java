package com.microshop.supplier.api;

import com.microshop.supplier.application.SupplierApplicationService;
import com.microshop.supplier.domain.Supplier;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;
import org.springframework.http.HttpStatus;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

@RestController
@RequestMapping("/suppliers")
@PreAuthorize("hasRole('ADMIN')")
public class SupplierController {
    private final SupplierApplicationService service;

    public SupplierController(SupplierApplicationService service) { this.service = service; }

    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    SupplierResponse create(@Valid @RequestBody CreateSupplierRequest request) {
        return SupplierResponse.from(service.createSupplier(request.name(), request.contactEmail()));
    }

    @GetMapping
    List<SupplierResponse> list() { return service.getSuppliers().stream().map(SupplierResponse::from).toList(); }

    public record CreateSupplierRequest(@NotBlank @Size(max = 160) String name, @Email @Size(max = 320) String contactEmail) { }
    public record SupplierResponse(UUID id, String name, String contactEmail, boolean active, Instant createdAtUtc, Instant updatedAtUtc) {
        static SupplierResponse from(Supplier supplier) { return new SupplierResponse(supplier.id(), supplier.name(), supplier.contactEmail(), supplier.active(), supplier.createdAtUtc(), supplier.updatedAtUtc()); }
    }
}