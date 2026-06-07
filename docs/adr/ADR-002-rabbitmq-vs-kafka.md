# ADR-002: RabbitMQ vs Kafka Usage

## Status

Accepted for Stage 1 learning.

## Context

MicroShop uses RabbitMQ and Kafka for different messaging roles. Treating them as interchangeable would make the architecture harder to reason about.

## Decision

Use RabbitMQ for workflow/task messaging.

Use Kafka for event stream/projection/read model learning.

## RabbitMQ Role

```text
NotificationWorker
workflow/task messages
retry/error queue style processing
```

## Kafka Role

```text
microshop.order-events
ProjectionWorker
MongoDB read model
future analytics/replay learning
```

## Consequences

```text
The project uses each broker for its strength.
Local runtime has more infrastructure.
Docs and runbooks must clarify the difference.
```
