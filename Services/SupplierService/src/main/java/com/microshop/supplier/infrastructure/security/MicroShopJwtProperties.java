package com.microshop.supplier.infrastructure.security;

import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix = "microshop.jwt")
public record MicroShopJwtProperties(String issuer, String audience, String secretKey) { }