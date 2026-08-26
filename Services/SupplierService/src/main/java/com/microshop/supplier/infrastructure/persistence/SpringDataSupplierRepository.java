package com.microshop.supplier.infrastructure.persistence;

import org.springframework.data.jpa.repository.JpaRepository;
import java.util.UUID;

interface SpringDataSupplierRepository extends JpaRepository<SupplierEntity, UUID> { }