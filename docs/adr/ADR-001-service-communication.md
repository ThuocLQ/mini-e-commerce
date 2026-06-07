# ADR-001: Service Communication Strategy

## Status

Accepted for Stage 1 learning.

## Context

MicroShop uses multiple services and needs both synchronous and asynchronous communication. Each style should have a clear role so the project remains understandable.

## Decision

Use REST for public and debug request-response APIs.

Use gRPC for selected internal service-to-service calls.

Use RabbitMQ for workflow/task messaging.

Use Kafka for event stream/projection/read model learning.

## Consequences

Positive:

```text
Each communication style has a clear role.
The project demonstrates practical backend patterns.
```

Trade-offs:

```text
More infrastructure to run locally.
More operational visibility needed.
Clear boundaries must be documented.
```
