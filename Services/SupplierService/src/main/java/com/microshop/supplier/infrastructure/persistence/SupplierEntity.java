package com.microshop.supplier.infrastructure.persistence;

import com.microshop.supplier.domain.Supplier;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "suppliers")
class SupplierEntity {
    @Id UUID id;
    @Column(nullable = false, length = 160) String name;
    @Column(name = "contact_email", length = 320) String contactEmail;
    @Column(nullable = false) boolean active;
    @Column(name = "created_at_utc", nullable = false) Instant createdAtUtc;
    @Column(name = "updated_at_utc", nullable = false) Instant updatedAtUtc;

    protected SupplierEntity() { }

    static SupplierEntity fromDomain(Supplier supplier) {
        var entity = new SupplierEntity();
        entity.id = supplier.id();
        entity.name = supplier.name();
        entity.contactEmail = supplier.contactEmail();
        entity.active = supplier.active();
        entity.createdAtUtc = supplier.createdAtUtc();
        entity.updatedAtUtc = supplier.updatedAtUtc();
        return entity;
    }

    Supplier toDomain() {
        return new Supplier(id, name, contactEmail, active, createdAtUtc, updatedAtUtc);
    }
}