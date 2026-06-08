# MicroShop Service Boundary Review

## Purpose

Document current service responsibilities and boundary risks.

This keeps future lessons from moving behavior into the wrong service just because it is convenient.

## Services

| Service | Owns | Should Not Own | Notes |
| --- | --- | --- | --- |
| `CatalogService` | Product catalog data, product lookup REST/gRPC | Basket/order/payment workflows | Product lookup is a service capability; Basket should call it through an adapter. |
| `BasketService` | Basket state and basket operations | Product ownership, order creation | Redis is an implementation detail in Infrastructure. |
| `OrderingService` | Order write-side, checkout, outbox basics | Read model storage, projection worker behavior | RabbitMQ outbox flow belongs here; Kafka publishing is not implemented yet. |
| `OrderQueryService` | Order read model query API | Order write-side decisions, checkout rules | MongoDB read model is a query-side store. |
| `DiscountService` | Coupon lookup and discount calculation | Payment processing, order ownership | Discount rules stay separate from checkout orchestration for now. |
| `IdentityService` | Auth/JWT foundation | Business order/catalog/payment rules | This is a learning identity service, not a complete production IdP. |
| `PaymentService` | Payment creation and webhook foundation | Catalog/order ownership | Webhook security and provider validation remain hardening work. |
| `ApiGateway` | Routing/proxy and public entrypoint | Business logic | YARP route configuration should stay declarative. |

## Workers

| Worker | Owns | Should Not Own | Notes |
| --- | --- | --- | --- |
| `NotificationWorker` | RabbitMQ workflow/task handling | Projection/read model updates | Consumes shared integration event contracts via MassTransit. |
| `ProjectionWorker` | Kafka -> MongoDB projection | Order write-side behavior | Kafka is used for event stream/projection learning. |

## Messaging Boundaries

RabbitMQ:

```text
workflow/task messages
NotificationWorker
OrderingService outbox publisher
```

Kafka:

```text
event stream/projection learning
ProjectionWorker
MongoDB read model updates
```

Rules:

```text
Kafka does not replace RabbitMQ.
OrderingService does not publish Kafka events yet unless future code implements it.
ProjectionWorker must not make order write-side decisions.
NotificationWorker must not update the MongoDB read model.
```

## Data Boundaries

| Store | Current Owner |
| --- | --- |
| PostgreSQL write-side databases | Catalog, Ordering, Discount, Identity, Payment |
| Redis | Basket |
| MongoDB `MicroShop_OrderReadDb.order_summaries` | OrderQueryService read side, updated by ProjectionWorker |
| MongoDB `projection_failures` | ProjectionWorker failure tracking |

## Boundary Risks To Watch

```text
Gateway route changes that hide service responsibility.
Debug endpoints being used as production ingestion paths.
Application handlers accumulating transport-specific concerns.
Workers gaining too much policy logic in one class.
Inconsistent error responses hiding service boundary behavior.
```
