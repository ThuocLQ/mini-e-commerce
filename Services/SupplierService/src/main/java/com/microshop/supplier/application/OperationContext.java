package com.microshop.supplier.application;

public record OperationContext(String actor, String correlationId) {
    public OperationContext {
        actor = actor == null || actor.isBlank() ? "unknown" : actor.trim();
        correlationId = correlationId == null || correlationId.isBlank() ? null : correlationId.trim();
    }
}