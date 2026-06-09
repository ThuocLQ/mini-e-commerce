---
day: 31
title: "Clean Architecture + Hexagonal Review"
duration: "90-120 minutes"
phase: "Stage 2 - Production Hardening"
project: "MicroShop"
testing: "Architecture review + build verification + targeted smoke checks"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "UTF-8 Markdown with Vietnamese accents"
---

# Day 31: Clean Architecture + Hexagonal Review

## 0. Vị trí hiện tại

Bạn đã hoàn thành:

```text
Day 30: Foundation Demo + Checkpoint
```

Day 30 closed the Stage 1 foundation:

```text
MicroShop has multiple services and workers.
RabbitMQ handles workflow/task messaging.
Kafka handles event stream/projection learning.
ProjectionWorker updates MongoDB read model.
OrderQueryService exposes /order-summaries.
Docs, runbooks, and checkpoint notes exist.
```

Day 31 starts Stage 2 production hardening.

Vi tri dung trong roadmap:

```text
Day 31: Clean Architecture / Hexagonal Review
Day 32: API Versioning + Backward Compatibility + Standard Error Format
Day 33: PostgreSQL + Migration + Schema Evolution hardening
Day 34: FluentValidation Pipeline + Mapping
Day 35: Specification Pattern
Day 36: Strategy Pattern + Audit Log + Advanced Identity Review
```

Day 31 is not about API error format yet.

API error format belongs to Day 32.

---

## 1. Bối cảnh repo hiện tại

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

Hạ tầng hiện tại:

```text
PostgreSQL - write-side relational databases
Redis - BasketService state/cache
RabbitMQ - workflow/task messaging, NotificationWorker
Kafka - event stream/projection learning, ProjectionWorker
MongoDB - OrderQueryService read model and projection failures
Docker Compose - local runtime
Aspire AppHost - local .NET orchestration
```

Current important routes:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries

GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox

GET /products
GET /products/{id}
GET /products/search
GET /products/count
GET /products/price-range
POST /products
PUT /products/{id}
DELETE /products/{id}

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

GET /discounts/{code}
POST /discounts/apply

POST /auth/login
GET /auth/me

POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment

GET /health
GET /alive
```

Do not use:

```text
/orders/read-model
ORD-900
CUST-900
```

---

## 2. Mục tiêu

Day 31 goal:

```text
Review current architecture boundaries and identify hardening gaps.
```

Sau khi hoàn thành:

```text
[ ] You understand Clean Architecture / Hexagonal Architecture in MicroShop terms.
[ ] You review service folders: API / Application / Domain / Infrastructure.
[ ] You identify dependency direction violations.
[ ] You identify endpoint logic that should move to Application layer later.
[ ] You identify Application code that leaks Infrastructure details.
[ ] You identify Domain code that depends on Infrastructure/frameworks.
[ ] You identify DTO/mapping issues.
[ ] You identify worker boundary issues.
[ ] You create an architecture review report.
[ ] You create a Stage 2 architecture refactor backlog.
```

Output chính:

```text
docs/architecture/clean-architecture-review-day-31.md
docs/architecture/service-boundary-review.md
docs/backlog/day-31-architecture-hardening-backlog.md
```

Optional code changes:

```text
Small safe cleanup only.
No broad refactor.
```

---

## 3. Giới hạn phạm vi

Nên làm:

```text
[ ] Review architecture.
[ ] Inspect dependencies.
[ ] Inspect folder responsibilities.
[ ] Document findings.
[ ] Create backlog.
[ ] Fix only tiny safe issues if obvious.
```

Không làm:

```text
[ ] Do not rewrite all services.
[ ] Do not move large amounts of code.
[ ] Do not introduce a new shared framework.
[ ] Do not add global middleware.
[ ] Do not change RabbitMQ/Kafka responsibilities.
[ ] Do not change ProjectionWorker behavior.
[ ] Do not add API versioning or standard error format yet.
[ ] Do not enable Swagger/OpenAPI unless chosen as a separate task.
[ ] Do not claim production-ready architecture.
```

Điều phần này chứng minh:

```text
MicroShop has a reviewed architecture baseline for Stage 2.
You know where boundaries are clean and where they need hardening.
```

Điều phần này chưa chứng minh:

```text
All services are fully Clean Architecture compliant.
All technical debt is fixed.
All APIs are standardized.
The system is production-ready.
```

---

## 4. Clean Architecture mental model

Clean Architecture is mainly about dependency direction.

Simple rule:

```text
Outer layers depend on inner layers.
Inner layers do not depend on outer layers.
```

Typical layers:

```text
API
-> Application
-> Domain

Infrastructure
-> Application
-> Domain
```

Allowed direction:

```text
API depends on Application.
Infrastructure implements Application abstractions.
Application depends on Domain.
Domain depends on nothing project-specific.
```

Tránh:

```text
Domain depends on Infrastructure.
Application depends directly on database driver if avoidable.
Application depends on HTTP framework if avoidable.
Infrastructure calls API layer.
```

Common MicroShop interpretation:

```text
API:
    endpoint mapping, request/response DTOs, HTTP concerns

Application:
    use cases, commands, queries, handlers, ports/interfaces

Domain:
    entities, value objects, domain rules, domain events if any

Infrastructure:
    persistence, message broker, external clients, Redis, MongoDB, Kafka, RabbitMQ
```

---

## 5. Hexagonal Architecture mental model

Hexagonal Architecture is about ports and adapters.

Core idea:

```text
Application core defines ports.
Adapters implement ports.
```

Examples:

```text
IOrderRepository = port
PostgresOrderRepository = adapter

IOutboxRepository = port
DapperOutboxRepository = adapter

IOrderSummaryRepository = port
MongoOrderSummaryRepository = adapter

Kafka consumer worker = inbound adapter
MongoDB repository = outbound adapter
```

Important rule:

```text
Business use case should not know too much about Kafka, MongoDB, HTTP, Redis, or RabbitMQ.
```

Training-stage exception:

```text
Some workers may contain simple orchestration code directly.
This is acceptable in early lessons but should be documented as hardening backlog if it grows.
```

---

## 6. Pre-check

Run:

```powershell
git status --short
docker compose config --services
```

Inspect solution/projects:

```powershell
Get-ChildItem
Get-ChildItem Services
Get-ChildItem Workers
Get-ChildItem BuildingBlocks.Contracts
Get-ChildItem MicroShop.ServiceDefaults
```

Inspect service folder structure:

```powershell
Get-ChildItem Services -Directory
Get-ChildItem Services -Recurse -Directory -Include API,Application,Domain,Infrastructure
```

Inspect endpoint files:

```powershell
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
```

Inspect worker files:

```powershell
Get-ChildItem Services/NotificationWorker -Recurse -Filter *.cs
Get-ChildItem Workers/ProjectionWorker -Recurse -Filter *.cs
```

Search dependency smells:

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "MongoDB.Driver|Npgsql|Dapper|StackExchange.Redis|Confluent.Kafka|MassTransit|HttpContext|IResult|Results."
```

Search old route/id examples in code and docs:

```powershell
Get-ChildItem . -Recurse -Include *.md,*.cs |
  Select-String -Pattern "/orders/read-model|ORD-900|CUST-900"
```

If old examples are in archived docs only, note them as historical.

Do not rewrite history unless asked.

---

## 7. Review checklist by layer

### API layer

Check:

```text
[ ] Endpoint files are thin.
[ ] HTTP details stay in API layer.
[ ] API maps request DTOs to Application commands/queries.
[ ] API does not contain complex business rules.
[ ] API does not directly talk to database/broker unless it is a deliberate debug endpoint.
```

Smells:

```text
Endpoint builds SQL.
Endpoint contains long business workflows.
Endpoint directly publishes RabbitMQ/Kafka messages.
Endpoint directly manipulates MongoDB documents.
```

Allowed exceptions:

```text
Small debug endpoints may exist in training-stage code.
They should be marked as debug/local only.
```

### Application layer

Check:

```text
[ ] Contains use cases: commands, queries, handlers.
[ ] Defines interfaces/ports for persistence or external systems.
[ ] Does not depend directly on API layer.
[ ] Avoids HTTP-specific types like HttpContext/IResult if possible.
[ ] Avoids concrete infrastructure types where possible.
```

Smells:

```text
Application handler uses IMongoCollection directly.
Application handler uses Kafka consumer/producer directly.
Application returns IResult.
Application depends on ASP.NET Core HTTP abstractions.
```

Training-stage exceptions should be documented.

### Domain layer

Check:

```text
[ ] Contains entities/value objects/domain rules.
[ ] Does not depend on Infrastructure.
[ ] Does not depend on ASP.NET Core.
[ ] Does not know about RabbitMQ/Kafka/MongoDB/PostgreSQL.
```

Smells:

```text
Domain entity has MongoDB attributes.
Domain entity has EF/Dapper-specific concerns.
Domain rule calls external service.
```

### Infrastructure layer

Check:

```text
[ ] Implements Application ports.
[ ] Contains database/message broker/external client details.
[ ] Owns Dapper/MongoDB/Redis/RabbitMQ/Kafka details.
[ ] Does not leak implementation details upward unnecessarily.
```

Smells:

```text
Infrastructure models are returned directly to API.
Infrastructure exceptions leak to response body.
Infrastructure code is mixed into endpoint files.
```

---

## 8. Service-by-service review plan

Do not review everything at unlimited depth.

Use a focused pass.

### OrderQueryService

Why first:

```text
Current read model service from Day 27-30.
Clear MongoDB boundary.
Current endpoints are important for demo.
```

Review:

```text
[ ] API endpoint files.
[ ] Application query/use case if present.
[ ] Mongo repository location.
[ ] Read model DTO vs Mongo document separation.
[ ] Health check location.
[ ] Debug seed endpoint scope.
```

Expected output:

```text
OrderQueryService is acceptable for training-stage read model.
Hardening candidates are documented.
```

### ProjectionWorker

Review:

```text
[ ] Kafka consumer is an inbound adapter.
[ ] MongoDB repository is outbound adapter.
[ ] Projection application logic is not too coupled to Confluent.Kafka.
[ ] Invalid/unsupported messages go to projection_failures.
[ ] MongoDB failure does not commit Kafka offset.
```

Hardening candidates:

```text
processed-event collection
retry topic/DLT
projection rebuild command
event sequence/version
schema/versioning
metrics for lag/failure
```

### OrderingService

Review:

```text
[ ] Outbox basics location.
[ ] Application use cases vs endpoint logic.
[ ] Domain order rules.
[ ] Infrastructure persistence.
[ ] RabbitMQ publish path.
```

Do not add Kafka publisher today.

### BasketService

Review:

```text
[ ] Redis logic is in Infrastructure.
[ ] gRPC/HTTP communication with CatalogService is behind adapter/client.
[ ] Basket domain/application logic is not mixed with Redis details.
```

### CatalogService

Review:

```text
[ ] Product endpoints thin enough.
[ ] Query/filter logic location.
[ ] Persistence details isolated.
```

### IdentityService and PaymentService

Review lightly:

```text
[ ] Routes documented.
[ ] Auth/payment concerns are not overclaimed as production-ready.
[ ] Any demo/webhook/debug logic is clearly scoped.
```

---

## 9. Dependency direction review

Create a table while reviewing:

```text
Project/File
Observed dependency
Allowed?
Reason
Action
```

Example:

```text
OrderQueryService/Application/GetOrderSummaryQuery.cs
Uses MongoDB.Driver
No/Maybe
Application should not depend on Mongo driver if avoidable
Backlog: move Mongo details to Infrastructure repository
```

Another example:

```text
ProjectionWorker/Worker.cs
Uses Confluent.Kafka
Yes
Worker is Kafka inbound adapter
Keep
```

Be practical.

Do not mark every training-stage shortcut as urgent.

Use severity:

```text
P0 - breaks current build/runtime
P1 - likely to cause bugs soon
P2 - architecture cleanup
P3 - nice-to-have
```

---

## 10. Create architecture review report

Create folder:

```text
docs/architecture
```

Create file:

```text
docs/architecture/clean-architecture-review-day-31.md
```

Nội dung gợi ý:

````md
# Day 31 Clean Architecture Review

## Goal

Review MicroShop architecture boundaries after Stage 1 foundation.

## Current stage

```text
Stage 2 starts here.
This is a production-minded architecture review, not a full rewrite.
```

## Layer rules

```text
API -> Application -> Domain
Infrastructure -> Application -> Domain
Domain should not depend on Infrastructure/API/frameworks.
Application should define ports where possible.
Infrastructure should implement adapters.
```

## Reviewed services

```text
OrderQueryService
ProjectionWorker
OrderingService
BasketService
CatalogService
IdentityService
PaymentService
```

## Findings

| Area | Finding | Severity | Action |
| --- | --- | --- | --- |
| OrderQueryService | TBD | P2 | TBD |
| ProjectionWorker | TBD | P2 | TBD |
| OrderingService | TBD | P2 | TBD |

## Good patterns observed

```text
RabbitMQ and Kafka responsibilities are separated.
ProjectionWorker is a separate worker.
OrderQueryService owns read model query API.
MongoDB read model is separate from write-side persistence.
```

## Hardening candidates

```text
Move any infrastructure details out of Application where needed.
Reduce endpoint business logic if found.
Keep debug endpoints clearly marked.
Add processed-event collection later.
Add Kafka retry topic/DLT later.
Add standard error format on Day 32.
```

## What this review proves

```text
We know the current architecture baseline.
We know where Stage 2 hardening should focus.
```

## What this review does not prove

```text
The architecture is fully production-ready.
All services are fully Clean Architecture compliant.
All hardening work is complete.
```
````

Fill in `TBD` after actual inspection.

---

## 11. Create service boundary review

Tạo:

```text
docs/architecture/service-boundary-review.md
```

Nội dung gợi ý:

````md
# MicroShop Service Boundary Review

## Purpose

Document current service responsibilities and boundary risks.

## Services

| Service | Owns | Should not own |
| --- | --- | --- |
| CatalogService | Product/catalog data | Basket/order workflows |
| BasketService | Basket state | Product ownership |
| OrderingService | Order write-side and outbox basics | Read model projection storage |
| OrderQueryService | Order read model queries | Order write-side decisions |
| DiscountService | Discount/coupon rules | Payment processing |
| IdentityService | Auth/JWT foundation | Business order rules |
| PaymentService | Payment/webhook foundation | Catalog/order ownership |
| ApiGateway | Routing/proxy | Business logic |

## Workers

| Worker | Owns | Should not own |
| --- | --- | --- |
| NotificationWorker | RabbitMQ workflow/task handling | Projection/read model updates |
| ProjectionWorker | Kafka -> MongoDB projection | Order write-side behavior |

## Messaging boundaries

```text
RabbitMQ:
    workflow/task messages

Kafka:
    event stream/projection learning
```

## Notes

```text
Kafka does not replace RabbitMQ.
OrderingService does not publish Kafka events yet unless future code implements it.
```
````

---

## 12. Create hardening backlog

Tạo:

```text
docs/backlog/day-31-architecture-hardening-backlog.md
```

Nội dung gợi ý:

````md
# Day 31 Architecture Hardening Backlog

## P1 - important soon

```text
[ ] Keep API endpoints thin where they currently contain too much logic.
[ ] Ensure Application layer does not depend on HTTP-specific types.
[ ] Ensure Infrastructure details do not leak into Domain.
```

## P2 - architecture cleanup

```text
[ ] Review repository abstractions service by service.
[ ] Review mapping between request DTO, application command/query, domain model, response DTO.
[ ] Review debug endpoints and mark them local/dev only.
[ ] Review service-specific dependency registration patterns.
```

## Event-driven hardening

```text
[ ] Kafka retry topic/DLT.
[ ] Processed-event collection for ProjectionWorker.
[ ] Projection rebuild command.
[ ] Event schema/versioning.
[ ] OrderingService outbox publisher to Kafka if roadmap chooses it.
```

## API hardening next

```text
[ ] Day 32: API Versioning + Backward Compatibility + Standard Error Format.
[ ] Standard ProblemDetails-style error responses.
[ ] API versioning policy.
[ ] Gateway route governance.
```
````

---

## 13. Optional tiny code cleanup rules

Only do code cleanup if:

```text
[ ] It is small.
[ ] It does not change runtime behavior.
[ ] It is easy to review.
[ ] It does not create broad refactor risk.
```

Allowed examples:

```text
Rename misleading local variable.
Move obviously misplaced private helper inside same file/folder.
Add comment to debug endpoint explaining local/training purpose.
Fix stale route comment.
```

Tránh:

```text
Moving repositories across projects.
Changing DTO shapes.
Changing endpoints.
Changing Kafka offset behavior.
Changing MongoDB projection logic.
```

If in doubt:

```text
Document as backlog, do not code it today.
```

---

## 14. Kế hoạch build/test

Build core reviewed projects:

```powershell
dotnet build Services/OrderQueryService/OrderQueryService.csproj
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
dotnet build Services/OrderingService/OrderingService.csproj
dotnet build Services/BasketService/BasketService.csproj
dotnet build Services/CatalogService/CatalogService.csproj
```

If reasonable:

```powershell
dotnet build
```

If full solution build fails due to unrelated issue:

```text
Document the failure.
Do not hide it.
Do not block architecture review if touched projects build.
```

---

## 15. Runtime verification

If no code changed, runtime verification can be light.

Lite projection demo:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Verify direct read API:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/{orderId}
```

If you need a valid event, use GUID payload shape from Day 30:

```text
key = orderId
eventType = OrderCreated / OrderPaid / OrderCancelled
required fields = eventId, eventType, orderId, customerId, customerName, totalAmount, currency, itemCount, items, occurredAtUtc
```

Do not use:

```text
ORD-900
CUST-900
```

Full system only if needed:

```powershell
docker compose up -d --build
```

Gateway optional in lite mode:

```powershell
docker compose up -d --build --no-deps api-gateway
```

---

## 16. Docs/Postman updates

Docs to create/update:

```text
docs/architecture/clean-architecture-review-day-31.md
docs/architecture/service-boundary-review.md
docs/backlog/day-31-architecture-hardening-backlog.md
docs/README.md
```

Update `docs/README.md` to link the new Day 31 architecture review docs:

```text
docs/architecture/clean-architecture-review-day-31.md
docs/architecture/service-boundary-review.md
docs/backlog/day-31-architecture-hardening-backlog.md
```

Postman:

```text
No new Postman collection is required if no behavior changed.
```

Optional Postman sanity check:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/{{orderId}}
```

---

## 17. Review độ phù hợp production-minded

Điều phần này cải thiện:

```text
Architecture risks become visible.
Stage 2 work becomes prioritized.
Future refactors are less random.
Service responsibilities become clearer.
```

Những phần còn là future work:

```text
Actual refactoring.
API versioning.
Standard error format.
Validation pipeline.
Specification pattern.
Event-driven reliability hardening.
Observability production stack.
```

Không claim:

```text
The architecture is fully production-ready.
All services are cleanly layered.
All dependencies are fixed.
```

Use:

```text
reviewed baseline
training-stage implementation
production-minded hardening backlog
```

---

## 18. Checklist đạt yêu cầu

You pass Day 31 when:

```text
[ ] Current repo structure is inspected.
[ ] Endpoint files are inspected.
[ ] Worker files are inspected.
[ ] Dependency smells are searched.
[ ] Old endpoint/id examples are checked.
[ ] clean-architecture-review-day-31.md exists.
[ ] service-boundary-review.md exists.
[ ] day-31-architecture-hardening-backlog.md exists.
[ ] docs/README.md links the new Day 31 architecture docs.
[ ] Build passes for reviewed/touched projects or failures are documented.
[ ] No broad refactor is introduced.
[ ] RabbitMQ/Kafka responsibilities remain unchanged.
[ ] Day 32 API hardening is clearly separated from Day 31 architecture review.
```

---

## 19. Commit va tag tuy chon after review

Do this only after implementation and review.

Recommended commit:

```text
Day 31: Clean Architecture Hexagonal Review
```

Recommended tag:

```text
day-31-clean-architecture-hexagonal-review
```

Commands:

```powershell
git add .
git commit -m "Day 31: Clean Architecture Hexagonal Review"
git tag day-31-clean-architecture-hexagonal-review
```

---

## 20. Ngày tiếp theo

Day 32:

```text
API Versioning + Backward Compatibility + Standard Error Format
```

Day 32 can reuse findings from Day 31:

```text
Which endpoints return inconsistent errors?
Which routes need governance?
Where should ProblemDetails-style responses start?
Which services should get versioning first?
```

