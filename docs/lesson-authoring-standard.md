# MicroShop Lesson Authoring Standard

Use this file as mandatory context before writing or updating any MicroShop lesson, plan, handoff, or roadmap.

The goal is to keep future ChatGPT/Codex output aligned with the real repository and avoid invented endpoints, paths, services, ports, or production claims.

## 1. Naming Rules

Use this naming style for current and future English-facing lesson docs:

```text
Day 30
Day 31
Day 32
```

Do not use these names for new current docs:

```text
Buoi
Ngay
Lesson
```

Historical files may still contain old naming. Do not rewrite old history unless the task explicitly asks for it.

Commit messages should use:

```text
Day NN: Short Title
```

Example:

```text
Day 30: Foundation Demo Checkpoint
```

Day-based checkpoint tags should use kebab-case without slashes:

```text
day-NN-short-kebab-title
```

Example:

```text
day-30-foundation-demo-checkpoint
```

Use semantic version tags only for real product releases:

```text
v1.0.0
v1.1.0
v2.0.0
```

## 2. Current Repository Truth

Current services:

```text
Services/ApiGateway
Services/CatalogService
Services/BasketService
Services/OrderingService
Services/DiscountService
Services/IdentityService
Services/PaymentService
Services/OrderQueryService
```

Current workers:

```text
Services/NotificationWorker
Workers/ProjectionWorker
```

Shared projects:

```text
BuildingBlocks.Contracts
MicroShop.AppHost
MicroShop.ServiceDefaults
```

Current infrastructure:

```text
PostgreSQL - write-side relational databases
Redis - BasketService state/cache
RabbitMQ - workflow/task messaging, NotificationWorker
Kafka - event stream/projection learning, ProjectionWorker
MongoDB - OrderQueryService read model and projection failures
Docker Compose - local runtime
Aspire AppHost - local .NET orchestration
```

## 3. Current Important Flows

RabbitMQ workflow:

```text
OrderingService
-> Outbox basics
-> RabbitMQ
-> NotificationWorker
```

Kafka projection demo:

```text
Kafka CLI demo producer
-> Kafka topic microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
-> ApiGateway when gateway is running
```

Important distinction:

```text
RabbitMQ asks: who should process this work?
Kafka asks: who wants to observe this event stream?
```

Do not say Kafka replaced RabbitMQ.

Do not say OrderingService publishes Kafka events unless code has been implemented and verified.

## 4. Current API Routes

Read model routes:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries
```

Do not use:

```text
/orders/read-model
```

Ordering routes:

```text
GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox
```

Catalog routes:

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

Basket routes:

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

Discount routes:

```text
GET /discounts/{code}
POST /discounts/apply
```

Identity routes:

```text
POST /auth/login
GET /auth/me
```

Payment routes:

```text
POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment
```

Health routes:

```text
GET /health
GET /alive
```

Gateway public route rules:

```text
/catalog/{**catch-all} -> CatalogService, removes /catalog
/cart/{**catch-all} -> BasketService, transforms to /basket/{**catch-all}
/orders/{**catch-all} -> OrderingService
/order-summaries -> OrderQueryService
/order-summaries/{**catch-all} -> OrderQueryService
/debug/order-summaries -> OrderQueryService
/debug/order-summaries/{**catch-all} -> OrderQueryService
/discounts/{**catch-all} -> DiscountService
/auth/{**catch-all} -> IdentityService
/payments/{**catch-all} -> PaymentService
/webhooks/{**catch-all} -> PaymentService
```

Before writing routes in a lesson, verify current code:

```powershell
Get-Content Services/ApiGateway/appsettings.json
Get-Content Services/ApiGateway/appsettings.Docker.json
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
```

## 5. Docker Compose Rules

Always inspect services before writing Docker commands:

```powershell
docker compose config --services
```

Current Docker Compose service names include:

```text
postgres
redis
rabbitmq
zookeeper
kafka
mongodb
catalogservice
basketservice
orderingservice
discountservice
identityservice
paymentservice
orderqueryservice
projectionworker
notificationworker
api-gateway
```

Lite Kafka projection demo should not include `api-gateway` by default because `api-gateway` depends on many services and may pull most of the system up.

True lite projection demo:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Full system:

```powershell
docker compose up -d --build
```

If testing gateway in lite mode, clearly mark it optional:

```powershell
docker compose up -d --build --no-deps api-gateway
```

Only use gateway URLs in demo instructions when `api-gateway` is running.

## 6. Kafka Projection Event Rules

Topic:

```text
microshop.order-events
```

Consumer group:

```text
projection-worker
```

Valid event types:

```text
OrderCreated
OrderPaid
OrderCancelled
```

Message key must be the same GUID as `orderId`.

Valid payload shape:

```json
{
  "eventId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "eventType": "OrderCreated",
  "orderId": "11111111-1111-1111-1111-111111111111",
  "customerId": "22222222-2222-2222-2222-222222222222",
  "customerName": "Demo Customer",
  "totalAmount": 1977.3,
  "currency": "VND",
  "itemCount": 0,
  "items": [],
  "occurredAtUtc": "2026-05-28T10:00:00Z"
}
```

Do not use string IDs such as:

```text
ORD-900
CUST-900
```

Use GUIDs.

Invalid or unsupported projection messages are stored in:

```text
MongoDB database: MicroShop_OrderReadDb
Collection: projection_failures
```

Order summaries are stored in:

```text
MongoDB database: MicroShop_OrderReadDb
Collection: order_summaries
```

## 7. OpenAPI And Swagger Rules

Do not claim Swagger/OpenAPI is available unless code explicitly enables it.

Check first:

```powershell
Select-String -Path .\**\*.cs -Pattern "AddSwaggerGen|UseSwagger|UseSwaggerUI|AddOpenApi|MapOpenApi|AddEndpointsApiExplorer"
```

If only `AddEndpointsApiExplorer` exists, say:

```text
Swagger/OpenAPI UI is not enabled yet.
API surface is documented manually.
```

## 8. Production Claim Rules

MicroShop is a learning project with production-minded patterns.

Do not say:

```text
production ready
complete production architecture
fully production-grade
```

Use:

```text
production-minded
production direction
Stage 2 production hardening backlog
training-stage implementation
```

Current known limitations:

```text
No Kafka retry topic/DLT yet.
No processed-event collection yet.
No projection rebuild command yet.
No OrderingService Kafka publisher yet.
No schema registry yet.
No production observability stack yet.
No full CI/CD/deployment strategy yet.
```

If a feature is not implemented, label it as:

```text
future work
Stage 2 backlog
not implemented yet
```

## 9. Lesson Structure Rules

Every new lesson should include:

```text
0. Current position
1. Current repo context
2. Goal
3. Scope guard
4. Pre-check
5. Implementation or demo steps
6. Build/test plan
7. Runtime verification, if relevant
8. Docs/Postman updates, if relevant
9. Production fit review
10. Pass checklist
11. Optional commit/tag
```

Every lesson must clearly state:

```text
Do
Do not
What this proves
What this does not prove
```

## 10. Repo Verification Checklist Before Writing A Lesson

Run or inspect:

```powershell
git status --short
docker compose config --services
Get-Content README.md
Get-Content docs/README.md
Get-Content Services/ApiGateway/appsettings.json
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
Get-ChildItem Workers -Recurse -Filter *.cs
```

For event lessons, inspect:

```powershell
Get-ChildItem BuildingBlocks.Contracts -Recurse -Filter *.cs
Get-ChildItem Services/OrderingService -Recurse -Filter *.cs
Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs
Get-ChildItem Services/NotificationWorker -Recurse -Filter *.cs
```

For database lessons, inspect:

```powershell
Get-ChildItem Services -Recurse -Directory -Filter Migrations
Get-ChildItem Services -Recurse -Filter *DatabaseInitializer*.cs
Get-ChildItem Services -Recurse -Filter *ConnectionFactory*.cs
```

## 11. Common Mistakes To Avoid

Avoid:

```text
Inventing endpoints.
Using old /orders/read-model endpoint.
Using ORD-900/CUST-900 ids.
Including api-gateway in lite demo without explaining dependencies.
Saying Kafka replaces RabbitMQ.
Saying OrderingService publishes Kafka when it does not.
Claiming Swagger UI exists without code verification.
Claiming production readiness.
Ignoring docs/README.md and ADR index.
Writing commit messages with Buoi/Ngay for new current docs.
Using slash-based day tags such as v30/foundation-demo-checkpoint.
```

## 12. Review Checklist For ChatGPT Output

Before accepting a generated lesson, verify:

```text
[ ] Uses Day naming.
[ ] Uses current repo service paths.
[ ] Uses current docker compose service names.
[ ] Lite and full runtime commands are separated.
[ ] ApiGateway is not included in lite mode unless marked optional.
[ ] Routes match current endpoint files and gateway config.
[ ] Kafka payload matches current ProjectionWorker event model.
[ ] No old /orders/read-model endpoint.
[ ] No ORD-900/CUST-900 ids.
[ ] RabbitMQ and Kafka roles are separate.
[ ] Swagger/OpenAPI status is not invented.
[ ] Production claims are modest and accurate.
[ ] Limitations and future work are clearly labeled.
[ ] Pass checklist is concrete.
```
