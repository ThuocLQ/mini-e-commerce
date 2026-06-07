# ADR-003: Order Read Model Projection

## Status

Accepted for Stage 1 learning.

## Context

OrderingService owns order write-side behavior. Order query/read side benefits from a dedicated read model.

Current projection flow:

```text
Kafka CLI demo events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
```

## Decision

Use MongoDB as the order summary read model store.

Use ProjectionWorker to consume Kafka topic:

```text
microshop.order-events
```

Use OrderQueryService to expose:

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

Invalid or unsupported projection messages are stored in:

```text
projection_failures
```

## Consequences

Positive:

```text
Read model is separated from write-side order persistence.
Kafka projection concepts are visible.
Projection replay can be explored later.
```

Trade-offs:

```text
Read model is eventually consistent.
Projection failures need monitoring.
Out-of-order events and processed-event tracking are not fully solved yet.
```

## Not Implemented Yet

```text
OrderingService Kafka publisher.
Projection rebuild command.
Kafka retry topic/DLT.
Processed-event collection.
Schema registry.
```
