package com.microshop.supplier.infrastructure.inventory;

import jakarta.validation.constraints.NotBlank;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.validation.annotation.Validated;

@Validated
@ConfigurationProperties(prefix = "microshop.inventory")
public record InventoryClientProperties(
        @NotBlank String baseUrl,
        @NotBlank String internalApiKey) { }
