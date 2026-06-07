# MicroShop Architecture Diagram

## Current Stage 1 Architecture

```mermaid
flowchart LR
    Client[Client / Postman] --> Gateway[ApiGateway]

    Gateway --> Catalog[CatalogService]
    Gateway --> Basket[BasketService]
    Gateway --> Ordering[OrderingService]
    Gateway --> OrderQuery[OrderQueryService]
    Gateway --> Identity[IdentityService]
    Gateway --> Discount[DiscountService]
    Gateway --> Payment[PaymentService]

    Basket --> Redis[(Redis)]

    Catalog --> WriteDb[(PostgreSQL / write-side DBs)]
    Ordering --> WriteDb
    Identity --> WriteDb
    Discount --> WriteDb
    Payment --> WriteDb

    Ordering --> RabbitMQ[(RabbitMQ)]
    RabbitMQ --> NotificationWorker[NotificationWorker]

    KafkaCli[Kafka CLI demo producer] --> Kafka[(Kafka topic: microshop.order-events)]
    Kafka --> ProjectionWorker[ProjectionWorker]
    ProjectionWorker --> Mongo[(MongoDB: MicroShop_OrderReadDb.order_summaries)]
    ProjectionWorker --> Failures[(MongoDB: projection_failures)]
    OrderQuery --> Mongo
```

## Messaging Roles

```text
RabbitMQ:
    workflow/task messaging
    NotificationWorker

Kafka:
    event stream/projection learning
    ProjectionWorker -> MongoDB read model
```

## Query Endpoints

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

## Notes

The write-side services currently use separate logical PostgreSQL databases in local Docker. The diagram groups them as write-side DBs to avoid overclaiming a specific deployment topology.
