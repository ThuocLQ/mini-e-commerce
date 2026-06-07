# MicroShop Communication Decisions

## REST

REST is used for public, debug, and request-response APIs.

Examples:

```text
Client/Postman -> ApiGateway
ApiGateway -> service endpoints
```

## gRPC

gRPC is used for selected internal service-to-service calls where strong contracts and performance matter.

Current learning example:

```text
BasketService -> CatalogService product lookup
```

BasketService also keeps REST catalog communication paths for comparison and local debugging.

## RabbitMQ

RabbitMQ is used for workflow/task messaging.

Current role:

```text
OrderingService outbox basics
-> RabbitMQ
-> NotificationWorker via MassTransit
```

Use RabbitMQ when:

```text
A message represents work to process.
One worker should handle the task.
Retry/error queue behavior is important.
```

## Kafka

Kafka is used for event stream/projection/read model learning.

Current role:

```text
Kafka topic microshop.order-events
-> ProjectionWorker
-> MongoDB order_summaries
```

Use Kafka when:

```text
Multiple consumer groups may read the same event stream.
Replay/rebuild matters.
Projection/analytics is the target.
```

## Outbox

Outbox is still needed when publishing to any broker because a service database and a broker do not share one transaction.

Current state:

```text
OrderingService has outbox basics for RabbitMQ workflow.
OrderingService Kafka publishing is not implemented yet.
```

## Rule Of Thumb

```text
RabbitMQ asks: who should process this work?
Kafka asks: who wants to observe this event stream?
```
