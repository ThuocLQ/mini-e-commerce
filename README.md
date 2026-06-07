# MicroShop

MicroShop is a learning microservices backend built with .NET 10. The project is used to practice production-minded backend architecture while keeping the local setup understandable.

Current focus/completed:

```text
Ngay 29: README + Architecture Diagram + ADR + API Surface Review
```

## Architecture Goals

MicroShop focuses on:

```text
service boundaries
Clean Architecture style folders
REST/gRPC communication
RabbitMQ workflow messaging
Kafka event stream projection
MongoDB read model
Docker Compose local runtime
Aspire local orchestration
operational visibility basics
```

## Services

| Service | Responsibility |
| --- | --- |
| `ApiGateway` | YARP reverse proxy and public entrypoint |
| `CatalogService` | Product catalog APIs and gRPC product lookup |
| `BasketService` | Basket state, Redis-backed |
| `OrderingService` | Order write side, checkout, outbox basics |
| `DiscountService` | Coupon lookup and discount calculation |
| `IdentityService` | Authentication/JWT foundation |
| `PaymentService` | Payment creation and webhook foundation |
| `OrderQueryService` | MongoDB order summary read model API |

## Workers

| Worker | Transport | Responsibility |
| --- | --- | --- |
| `NotificationWorker` | RabbitMQ/MassTransit | Handles workflow/task messages such as order notifications |
| `ProjectionWorker` | Kafka | Consumes order event stream and updates MongoDB read model |

## Infrastructure

| Infrastructure | Usage |
| --- | --- |
| PostgreSQL | Write-side relational persistence where applicable |
| Redis | Basket state/cache |
| RabbitMQ | Workflow/task messaging |
| Kafka | Event stream/projection learning |
| MongoDB | Order read model and projection failures |
| Docker Compose | Local runtime |
| Aspire AppHost | Local orchestration and dashboard for .NET services |

## Gateway Routes

Routes are configured in `Services/ApiGateway/appsettings.json` and Docker destinations are overridden by `Services/ApiGateway/appsettings.Docker.json`.

| Public path | Target |
| --- | --- |
| `/catalog/{**catch-all}` | `CatalogService` |
| `/cart/{**catch-all}` | `BasketService`, transformed to `/basket/{**catch-all}` |
| `/orders/{**catch-all}` | `OrderingService` |
| `/order-summaries` | `OrderQueryService` |
| `/order-summaries/{**catch-all}` | `OrderQueryService` |
| `/debug/order-summaries` | `OrderQueryService`, development/debug flow |
| `/debug/order-summaries/{**catch-all}` | `OrderQueryService`, development/debug flow |
| `/discounts/{**catch-all}` | `DiscountService` |
| `/auth/{**catch-all}` | `IdentityService` |
| `/payments/{**catch-all}` | `PaymentService` |
| `/webhooks/{**catch-all}` | `PaymentService` |

## Important Service Endpoints

Order query:

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

Ordering:

```text
GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox
```

Catalog:

```text
GET /products
GET /products/{id}
GET /products/search
GET /products/count
GET /products/price-range
POST /products
PUT /products/{id}
DELETE /products/{id}
```

Basket:

```text
GET /basket/{userId}
POST /basket/{userId}/items
POST /basket/{userId}/items-grpc
PUT /basket/{userId}/items/{productId}
DELETE /basket/{userId}/items/{productId}
PUT /basket/{userId}/clear
DELETE /basket/{userId}
GET /basket/products/{productId}/validate
GET /basket/products/{productId}/validate-grpc
POST /basket/preview-item
POST /basket/preview-item-grpc
GET /basket/products/{productId}/compare-communication
```

Identity:

```text
POST /auth/login
GET /auth/me
```

Payment:

```text
POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment
```

Health:

```text
GET /health
GET /alive
```

`ApiGateway` maps `/health` explicitly for Docker because `ASPNETCORE_ENVIRONMENT=Docker` is not covered by the Development-only default health endpoint mapping.

## Runtime Flows

### RabbitMQ Workflow

```text
OrderingService
-> Outbox
-> RabbitMQ
-> NotificationWorker
```

RabbitMQ is used for workflow/task messages.

### Kafka Projection

```text
Kafka CLI demo producer
-> Kafka topic microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
-> ApiGateway
```

Kafka is currently used for event stream/projection learning. `OrderingService` does not publish Kafka events yet.

## Local Runtime

Lite projection demo:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker api-gateway
```

Full local system:

```powershell
docker compose up -d --build
```

Create Kafka topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

Check ProjectionWorker lag:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Produce a projection demo event:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Payload:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

## Documentation

| Document | Purpose |
| --- | --- |
| `docs/README.md` | Documentation map for new readers |
| `docs/architecture-diagram.md` | Current architecture diagram |
| `docs/communication-decisions.md` | REST/gRPC/RabbitMQ/Kafka decisions |
| `docs/operational-visibility.md` | Health/log/debug notes |
| `docs/runbooks/microshop-local-debug-runbook.md` | Local debug runbook |
| `docs/adr/README.md` | ADR index and links |
| `docs/api-surface-review.md` | API surface and OpenAPI/Swagger status |
| `docs/demo-script-foundation.md` | Foundation demo script |

## Current Limits

This is a learning project, not a production-ready platform.

Known limitations:

```text
No Kafka retry topic/DLT yet.
No processed-event collection yet.
No projection rebuild command yet.
No OrderingService Kafka publisher yet.
No schema registry yet.
No production observability stack yet.
No full CI/CD/deployment strategy yet.
```

## Next

```text
Ngay 30: Foundation Demo + Checkpoint
```
