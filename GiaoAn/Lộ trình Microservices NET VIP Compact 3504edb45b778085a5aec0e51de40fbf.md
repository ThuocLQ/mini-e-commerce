# Lộ trình Microservices .NET VIP Compact

[Buổi 1: Microservices Mindset + Minimal API + CQRS với MediatR](https://www.notion.so/Bu-i-1-Microservices-Mindset-Minimal-API-CQRS-v-i-MediatR-34e4edb45b77800ab4e2d567456d16c7?pvs=21)

[Buổi 4: REST/Refit Service Communication - Production-Ready Mindset](https://www.notion.so/Bu-i-4-REST-Refit-Service-Communication-Production-Ready-Mindset-3504edb45b778002ad48c673ce83df52?pvs=21)

[Buổi 7: YARP API Gateway](https://www.notion.so/Bu-i-7-YARP-API-Gateway-3564edb45b778011af61f13ad705f18a?pvs=21)

[Buổi 10: Config + Local Secrets](https://www.notion.so/Bu-i-10-Config-Local-Secrets-35b4edb45b7780ca891cd5ef3c2b0fdc?pvs=21)

[Buổi 11: Clean Architecture Baseline](https://www.notion.so/Bu-i-11-Clean-Architecture-Baseline-35c4edb45b77800fbba3e655779d02e3?pvs=21)

[Buổi 2: Database + Repository Pattern + Dapper](https://www.notion.so/Bu-i-2-Database-Repository-Pattern-Dapper-34e4edb45b7780bcb1f7c0f92e0b8b77?pvs=21)

[Buổi 5: gRPC Service Communication - CatalogService gRPC + BasketService gRPC Client](https://www.notion.so/Bu-i-5-gRPC-Service-Communication-CatalogService-gRPC-BasketService-gRPC-Client-3524edb45b77801f82daea215ea5c75a?pvs=21)

[Buổi 8: Docker Compose cơ bản](https://www.notion.so/Bu-i-8-Docker-Compose-c-b-n-3574edb45b77809db319ec1b2ae2cd23?pvs=21)

[Buổi 3: Tách Service thật - CatalogService + BasketService + Redis](https://www.notion.so/Bu-i-3-T-ch-Service-th-t-CatalogService-BasketService-Redis-34f4edb45b77809088a9f48a7b17cea1?pvs=21)

[Buổi 6: REST vs gRPC + Failure Mindset](https://www.notion.so/Bu-i-6-REST-vs-gRPC-Failure-Mindset-3544edb45b7780fa85a5f49feac2c71a?pvs=21)

[Buổi 9: .NET Aspire Intro](https://www.notion.so/Bu-i-9-NET-Aspire-Intro-3594edb45b7780bdad54f77b52130830?pvs=21)

# Lộ trình Microservices .NET VIP Compact V2

## 0. Mục tiêu bản V2

Bản V2 nâng cấp từ bản VIP Compact sau khi review các phần production còn thiếu như Webhook, API docs, alerting, Inbox/WebhookLog, background jobs, standard error response, SSO/OIDC, audit logging, centralized configuration và các chuẩn vận hành production.

Nguyên tắc giữ nguyên:

```
1. Không phân mảnh roadmap thành quá nhiều buổi rời rạc.
2. Một buổi = một learning outcome chính + một output rõ ràng.
3. Các phần production nhỏ được gắn vào bài liên quan dưới dạng Production add-on.
4. Kafka và MongoDB vẫn được học tương đối sớm, nhưng đúng vai trò.
5. Webhook được gắn với PaymentService/Saga vì đây là use case thực tế nhất.
6. Identity không dừng ở JWT tự cấp; roadmap có hướng nâng cấp lên SSO/OIDC/External Identity Provider như Keycloak, Microsoft Entra ID, Auth0/Okta ở mức production mindset.
7. Project cuối khóa phải có đủ dấu hiệu của hệ production: audit log, config/secrets chuẩn, error format chuẩn, observability, runbook, failure testing và deployment mindset.
```

---

## 1. Tổng quan mới

| Stage | Tên | Số buổi | Mục tiêu |
| --- | --- | --- | --- |
| Stage 1 | Foundation Build | 30 | Build MicroShop end-to-end |
| Stage 2 | Production Hardening | 20 | Nâng project lên senior backend |
| Stage 3 | Enterprise Deep Dive | 10-15 | Cloud-native/tech lead showcase |

Tổng chính:

```
60 buổi
```

Full optional:

```
65 buổi
```

Hiện tại:

```
Done: Buổi 1 → Buổi 6 + Checkpoint
Current: Buổi 8 - Docker Compose cơ bản
Next: Buổi 9 - .NET Aspire Intro
```

---

# STAGE 1: FOUNDATION BUILD — 30 buổi

## Mục tiêu Stage 1

Sau Stage 1, project có flow:

```
Client
  ↓
ApiGateway
  ↓
CatalogService  → SQL DB
BasketService   → Redis
IdentityService → JWT/Auth → SSO/OIDC mindset
OrderingService → SQL DB + Outbox basic
DiscountService → coupon/discount rule
PaymentService  → payment giả lập + webhook intro
  ↓
RabbitMQ → NotificationWorker
  ↓
Kafka → ProjectionWorker / AnalyticsWorker
  ↓
MongoDB → Read Model
```

Cấu trúc project mục tiêu:

```
MicroShop/
├── ApiGateway
├── Aspire
├── BuildingBlocks
│   ├── Contracts
│   ├── Logging
│   └── Messaging
├── Services
│   ├── CatalogService
│   ├── BasketService
│   ├── IdentityService
│   ├── OrderingService
│   ├── DiscountService
│   └── PaymentService
├── Workers
│   ├── NotificationWorker
│   ├── ProjectionWorker
│   └── AnalyticsWorker
├── docker-compose.yml
└── docs
    ├── architecture-diagram.md
    ├── communication-decisions.md
    ├── docker-compose-decision.md
    ├── api-error-format.md
    ├── webhook-decision.md
    └── adr
```

---

## Phase 1.1 - Service Foundation

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 1 | Catalog Minimal API + CQRS + MediatR | Catalog CRUD/Search | Endpoint mỏng, command/query rõ |
| 2 | Catalog DB + Repository + Dapper | Catalog lưu DB | Migration/seed mindset nhẹ |
| 3 | BasketService + Redis | Basket lưu Redis | Data ownership, TTL mindset |
| 4 | REST/Refit communication | Basket gọi Catalog bằng REST | Runtime coupling note |
| 5 | gRPC communication | Catalog gRPC + Basket client | Contract-first note |
| 6 | REST vs gRPC + Failure Mindset | Timeout/log/error handling | Standard error response intro + Checkpoint |

Checkpoint 1:

```
Endpoint → MediatR → Handler → Repository → DB
Basket/Catalog tách boundary rõ
REST/gRPC chạy được
Hiểu runtime coupling và downstream failure
Biết error response nên có errorCode/message/traceId
```

---

## Phase 1.2 - Gateway + Local Runtime

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 7 | YARP API Gateway | Client gọi qua Gateway | Gateway không ôm business logic |
| 8 | Docker Compose | Redis + RabbitMQ chạy bằng Compose | localhost vs service name |
| 9 | .NET Aspire Intro | AppHost chạy local distributed app | Aspire vs Compose mindset |
| 10 | Config + Local Secrets | appsettings/env/user-secrets | Không hardcode secrets |

Checkpoint 2:

```
Client gọi Catalog/Basket qua Gateway.
Infra chạy được bằng Docker Compose.
Hiểu Compose và Aspire dùng để giải quyết vấn đề gì.
Config tách khỏi code.
```

---

## Phase 1.3 - Clean Baseline + Security

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 11 | Clean Architecture Baseline | Tách API/Application/Domain/Infrastructure nhẹ | Dependency rule |
| 12 | Validation + Explicit Mapping | Request validation + DTO mapping | Không dùng magic mapping bừa |
| 13 | IdentityService + JWT | Login/token/protected endpoint | Token expiration, refresh token mindset, SSO/OIDC/External IdP overview |
| 14 | Role/Claim + Secure Internal Call Intro | Policy/role demo | RBAC/ABAC mindset, Gateway token validation, internal API security |

Checkpoint 3:

```
Project bắt đầu có structure rõ.
Endpoint không còn ôm logic nặng.
Có IdentityService và JWT cơ bản.
Biết hướng nâng cấp production là SSO/OIDC/External Identity Provider, không tự build auth quá sâu nếu không cần.
```

---

## Phase 1.4 - Ordering + Checkout + Payment/Webhook Intro

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 15 | OrderingService | Order API + Order DB | Order owns data |
| 16 | Checkout Flow | Basket → Order | Distributed consistency intro |
| 17 | DiscountService + Strategy Intro | Coupon/discount rule | Strategy pattern mindset |
| 18 | PaymentService + Payment Webhook Intro | Payment success/fail giả lập + `POST /webhooks/payment` demo | Webhook khác RabbitMQ/Kafka, webhook log mindset |

Checkpoint 4:

```
Có checkout flow.
Có Order, Discount, Payment skeleton.
Có webhook endpoint giả lập cho PaymentService.
Hiểu webhook là external system gọi HTTP vào hệ thống mình.
```

---

## Phase 1.5 - RabbitMQ + Reliability Basic

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 19 | RabbitMQ + MassTransit | Publish OrderCreatedEvent | RabbitMQ vs sync call |
| 20 | BuildingBlocks.Contracts + Event Design | Shared event contracts | Event naming/versioning note |
| 21 | NotificationWorker | Consume OrderCreatedEvent | Worker separation |
| 22 | Retry/DLQ + Idempotency Basic | Retry, DLQ, processed message check | Duplicate message + Inbox mindset |
| 23 | Outbox Basic + Background Publisher Intro | Outbox table + simple publisher | DB saved but publish failed problem, background job concept |

Checkpoint 5:

```
Order event publish được.
Worker consume được.
Có retry/DLQ/idempotency cơ bản.
Hiểu Outbox giải quyết lỗi mất event.
Hiểu background publisher là nền móng của outbox worker.
```

---

## Phase 1.6 - Kafka + MongoDB đúng vai trò

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 24 | RabbitMQ vs Kafka | Decision matrix | Queue vs event stream |
| 25 | Kafka Intro | Topic/partition/offset/consumer group demo | Kafka không thay RabbitMQ bừa bãi |
| 26 | MongoDB Read Model | OrderSummaryReadModel collection | MongoDB cho read model/projection |
| 27 | Kafka → MongoDB Projection | ProjectionWorker demo | Projection rebuild note nhẹ |

Checkpoint 6:

```
Biết RabbitMQ dùng cho workflow nghiệp vụ.
Biết Kafka dùng cho streaming/analytics/projection.
Biết MongoDB dùng cho read model/document projection.
Có flow event → projection → MongoDB.
```

---

## Phase 1.7 - Foundation Review

| Buổi | Chủ đề | Output chính | Production add-on |
| --- | --- | --- | --- |
| 28 | Logging + Health Checks | /health + structured logs nhẹ | Runbook intro |
| 29 | README + Architecture Diagram + ADR + OpenAPI Review | README, diagram, ADR đầu tiên, Swagger/OpenAPI docs review | Portfolio documentation + API docs |
| 30 | Foundation Demo + Checkpoint | Demo end-to-end + tag v30-foundation | Final review Stage 1 |

Kết quả cuối Stage 1:

```
Có project MicroShop end-to-end.
Có Gateway, Docker Compose, Aspire Intro.
Có Catalog, Basket, Identity, Order, Discount, Payment.
Có Payment webhook intro.
Có Redis, RabbitMQ, Kafka intro, MongoDB read model.
Có Outbox, Idempotency, Worker, Health, Logging, README, Diagram, ADR, OpenAPI review.
```

---

# STAGE 2: PRODUCTION HARDENING — 20 buổi

## Mục tiêu Stage 2

Không tạo project mới. Nâng chính MicroShop thành project senior backend.

---

## Phase 2.1 - Architecture + API/Data Lifecycle

| Buổi | Chủ đề | Output chính |
| --- | --- | --- |
| 31 | Clean Architecture / Hexagonal Review | Refactor backlog + ports/adapters |
| 32 | API Versioning + Backward Compatibility + Standard Error Format | v1/v2 API strategy + `errorCode/message/traceId` format |
| 33 | SQLite → PostgreSQL + Database Migration + Schema Evolution | Nâng Catalog/Order từ SQLite sang PostgreSQL, migration/seed/rollback mindset |
| 34 | FluentValidation Pipeline + Mapping nâng cao | Validation pipeline + mapping rules |
| 35 | Specification Pattern | Query specifications |
| 36 | Strategy Pattern nâng cao + Audit Log + Advanced Identity Review | Discount/Payment strategies + audit log policy + SSO/OIDC decision note |

---

## Phase 2.2 - Reliable Event-Driven nâng cao

| Buổi | Chủ đề | Output chính |
| --- | --- | --- |
| 37 | CloudEvents / Event Envelope | Standard event envelope |
| 38 | Transactional Outbox chuẩn | DB + Outbox same transaction |
| 39 | Outbox Publisher + Idempotency nâng cao + Inbox/WebhookLog | Robust publisher + processed messages + webhook event log |
| 40 | Kafka Consumer Group + Rebalance | Scale consumer correctly |
| 41 | MongoDB Projection Rebuild | Rebuild read model strategy |

---

## Phase 2.3 - Distributed Workflow + Resilience + Webhook Production

| Buổi | Chủ đề | Output chính |
| --- | --- | --- |
| 42 | Distributed Consistency Mindset | Strong vs eventual consistency note |
| 43 | Payment Saga Orchestration | Order → Payment saga orchestrator |
| 44 | Saga Choreography + Compensation + Payment Webhook Production Handling | Event choreography + cancel/refund/release flow + signature/idempotency/logging |
| 45 | Timeout / Retry / Circuit Breaker | Resilience policy demo |
| 46 | Gateway Load Balancing + Rate Limiting + Auth at Edge | YARP multi-destination/rate limit demo + Gateway JWT/SSO integration mindset |

Webhook production handling phải có:

```
[ ] POST /webhooks/payment
[ ] Verify signature / shared secret concept
[ ] Check eventId idempotency
[ ] Save webhook event log
[ ] Return 2xx nhanh
[ ] Publish PaymentSucceeded/PaymentFailed event
[ ] Không xử lý nghiệp vụ nặng trực tiếp trong HTTP webhook request
```

---

## Phase 2.4 - Observability + Testing + Ops

| Buổi | Chủ đề | Output chính |
| --- | --- | --- |
| 47 | Correlation ID + OpenTelemetry | Trace Gateway → Basket → Catalog |
| 48 | Metrics + Prometheus/Grafana + Alerting Intro | Basic dashboard/report + alert concept |
| 49 | Testing: Unit + Integration + Testcontainers | Test strategy + samples |
| 50 | Production Hardening Checkpoint | Failure test report + parity checklist |

Kết quả cuối Stage 2:

```
Project có Clean/Hexagonal structure tốt hơn.
Có API versioning, migration mindset, validation pipeline.
Có audit log policy và SSO/OIDC decision note cho hướng production.
Có standard error response.
Có Outbox/Inbox/WebhookLog/Idempotency/Saga/Resilience nghiêm túc hơn.
Có webhook production handling.
Có tracing/metrics/alerting/testing/failure report.
Đủ làm side project senior backend/microservices.
```

---

# STAGE 3: ENTERPRISE DEEP DIVE — 10 buổi chính + 5 buổi optional

## 10 buổi chính

| Buổi | Chủ đề | Output chính |
| --- | --- | --- |
| 51 | Dockerfile Production | Dockerfile review |
| 52 | Kubernetes Fundamentals | Pod/Service/Deployment/ConfigMap/Secret |
| 53 | Kubernetes Service Load Balancing | Replica + service LB concept |
| 54 | Helm | Helm chart basic |
| 55 | Gateway Options | YARP/Ocelot/NGINX/Azure API Gateway matrix |
| 56 | Deployment Strategies | Rolling/Blue-Green/Canary/Rollback |
| 57 | Secrets Management nâng cao | K8s Secret / Azure Key Vault concept |
| 58 | CI/CD Basic | Build/test pipeline |
| 59 | Operational Runbook + Incident Simulation | docs/runbooks + incident report |
| 60 | Final Enterprise Demo | Final presentation + portfolio review |

## Optional Deep Dive

| Buổi | Chủ đề | Khi nào học |
| --- | --- | --- |
| 61 | Event Sourcing | Khi muốn hiểu event stream sâu hơn |
| 62 | Schema Registry Concept | Khi dùng Kafka/event contract nghiêm túc hơn |
| 63 | Contract Testing nâng cao | Khi cần bảo vệ consumer/provider contract |
| 64 | Performance/Load Testing với k6 | Khi muốn benchmark p95/p99 |
| 65 | Service Mesh / Istio | Khi đã hiểu K8s và cần traffic/mTLS nâng cao |

---

# 4. Production Add-on Map

Các mục production quan trọng không bị bỏ, nhưng được gắn vào bài phù hợp:

| Production topic | Gắn vào buổi |
| --- | --- |
| Standard Error Response | 6, 32 |
| Local Secrets / Env vars | 10 |
| Centralized Config mindset | 10, 57 |
| Load Balancing concept | 7, 46, 53 |
| Rate Limiting | 46 |
| CORS / Security Headers | 7, 32 |
| SSO / OIDC / External Identity Provider | 13, 14, 36, 46 |
| RBAC / ABAC mindset | 14, 36 |
| Audit Logging | 36, 59 |
| API Versioning | 32 |
| SQLite → PostgreSQL upgrade | 33 |
| Database Migration | 33 |
| Backup/Restore mindset | 33, 59 |
| Webhook Intro | 18 |
| Webhook Security / Idempotency / Log | 44 |
| Inbox Pattern / WebhookLog | 39, 44 |
| Background Jobs / Publisher | 23, 39, 41 |
| API Docs / OpenAPI | 29 |
| Alerting | 48 |
| Event Contract Governance | 20, 37, 62 optional |
| Distributed Consistency | 42 |
| Runbook / Incident Response | 28, 59 |
| Performance / Load Testing | 64 optional |
| Deployment Strategies | 56 |
| Secrets Management nâng cao | 57 |

---

# 5. Final Project Parity Checklist

Project hoàn chỉnh cần có:

```
[ ] ApiGateway
[ ] Aspire AppHost intro
[ ] BuildingBlocks.Contracts / EventBus.Messages
[ ] BuildingBlocks.Logging
[ ] CatalogService
[ ] BasketService
[ ] IdentityService
[ ] OrderingService
[ ] DiscountService
[ ] PaymentService
[ ] Payment webhook endpoint
[ ] Webhook signature/idempotency/log concept
[ ] NotificationWorker
[ ] ProjectionWorker
[ ] Redis
[ ] RabbitMQ
[ ] Kafka
[ ] MongoDB read model/projection
[ ] PostgreSQL cho Catalog/Ordering production persistence
[ ] SQL Server hoặc MySQL comparison note nếu muốn mở rộng
[ ] Docker Compose
[ ] Clean Architecture baseline
[ ] CQRS + MediatR
[ ] Repository + Specification Pattern
[ ] Strategy Pattern cho Discount/Payment
[ ] gRPC internal communication
[ ] REST public/debug API
[ ] JWT auth
[ ] SSO/OIDC decision note, hướng tích hợp Keycloak / Microsoft Entra ID / Auth0 / Okta
[ ] RBAC/ABAC authorization mindset
[ ] Audit log policy cho hành động quan trọng
[ ] API Versioning
[ ] Standard Error Response
[ ] Centralized configuration mindset
[ ] Không hardcode secrets, có hướng quản lý secrets production
[ ] Database Migration mindset
[ ] Outbox Pattern
[ ] Inbox/WebhookLog mindset
[ ] Idempotent Consumer
[ ] Saga Payment success/failure
[ ] Compensation flow
[ ] Gateway rate limiting/load balancing concept
[ ] Health Checks
[ ] Structured Logging
[ ] Correlation ID
[ ] OpenTelemetry intro
[ ] Metrics dashboard concept
[ ] Alerting concept
[ ] Audit logs / security logs concept
[ ] Unit/Integration/Testcontainers samples
[ ] README + Architecture Diagram
[ ] OpenAPI/Swagger review
[ ] ADR folder
[ ] Runbooks
[ ] Branch/tag theo lesson milestone
[ ] Failure test report
[ ] Final demo script
```

---

# 6. Quy tắc giữ khóa học liền mạch

Không tách lẻ quá nhiều bài chỉ để “có topic”. Mỗi buổi phải thuộc một trong ba loại:

```
1. Build feature mới.
2. Nâng cấp feature cũ để production hơn.
3. Review/checkpoint để chắc kiến thức.
```

Nếu một nội dung chỉ là mindset hoặc checklist nhỏ, nó sẽ được đưa vào phần **Production add-on** của bài liên quan, không tách thành buổi riêng.

Ví dụ:

```
Identity → JWT → SSO/OIDC
vì hệ production doanh nghiệp thường không tự xử lý toàn bộ đăng nhập mà tích hợp Identity Provider như Keycloak, Microsoft Entra ID, Auth0/Okta.

Payment → Webhook → Saga
vì hệ payment thật thường nhận kết quả từ bên ngoài qua webhook.

Gateway → Docker Compose → Aspire → Config
vì sau khi có Gateway, ta cần chạy nhiều service và quản lý local runtime.

Order → RabbitMQ → Outbox → Saga
vì sau khi có checkout, ta cần xử lý workflow phân tán đáng tin cậy.

Outbox/Event → Kafka → MongoDB Projection
vì sau khi có event, ta có thể stream event sang read model/analytics.

Logging/Health → Tracing/Metrics/Alerting → Runbook
vì muốn xử lý failure tốt thì phải quan sát được failure trước.
```