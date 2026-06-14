# MicroShop Roadmap V2 - Day 40-50 Production Failure Track

## 0. Mục Tiêu

Roadmap này nối tiếp MicroShop sau Day 39.

Mục tiêu không phải thêm thật nhiều tool cho đẹp. Mục tiêu là nâng project từ learning microservices lên mức:

```text
Production-minded backend project
Local production-like deployment ready
Senior interview defensible
```

Từ Day 40 trở đi, mỗi bài phải gắn với một production failure scenario cụ thể:

```text
Kafka lag tăng thì debug sao?
ProjectionWorker chết giữa chừng thì duplicate/replay xử lý sao?
Read model MongoDB sai thì rebuild thế nào?
Checkout/payment fail giữa chừng thì consistency ra sao?
Webhook duplicate hoặc giả mạo thì xử lý thế nào?
Downstream chậm thì retry/circuit breaker đặt ở đâu?
Production lỗi thì trace request/event từ đâu?
Metric nào báo hệ thống đang hỏng?
```

Nguyên tắc mới:

```text
1. Không đánh số lại Day 1-39.
2. Không học lại foundation đã làm ở Day 1-39.
3. Không tách topic nhỏ thành bài riêng nếu chỉ là mindset/checklist.
4. Kiến thức nằm trong giáo án chính, không bắt tạo nhiều docs phụ.
5. Mỗi bài có: vấn đề production, kiến thức cốt lõi, repo hiện tại, thực hành/failure drill, câu hỏi interview, kết luận.
6. Không claim production-ready nếu chưa có test/failure drill/chứng cứ rõ.
```

## 1. Trạng Thái Project Hiện Tại

Đã làm tới:

```text
Done: Day 1 -> Day 39
Current: Day 40 - Kafka Projection Reliability + Rebalance Risk
Next: Day 41 - MongoDB Projection Rebuild + Replay Safety
```

Repo hiện tại:

```text
MicroShop/
├── BuildingBlocks.Contracts
├── MicroShop.AppHost
├── MicroShop.ServiceDefaults
├── Services/
│   ├── ApiGateway
│   ├── BasketService
│   ├── CatalogService
│   ├── DiscountService
│   ├── IdentityService
│   ├── NotificationWorker
│   ├── OrderingService
│   ├── OrderQueryService
│   └── PaymentService
├── Workers/
│   └── ProjectionWorker
├── docker/
├── docs/
├── postman/
└── GiaoAn/
```

Hạ tầng local hiện tại:

```text
PostgreSQL - write-side databases
Redis - basket/cache
RabbitMQ - notification workflow
Kafka - event stream/projection learning
MongoDB - order read model
Docker Compose - local runtime
Aspire AppHost - local .NET orchestration
```

Lưu ý:

```text
NotificationWorker hiện nằm trong Services/NotificationWorker.
ProjectionWorker hiện nằm trong Workers/ProjectionWorker.
Chưa có AnalyticsWorker.
Chưa có BuildingBlocks.Logging hoặc BuildingBlocks.Messaging project riêng.
Chưa bật Swagger/OpenAPI UI thật.
```

## 2. Stage 2 Tổng Quan

Stage 2 chia thành 4 phase:

| Phase | Day | Mục tiêu |
| --- | --- | --- |
| 2.1 Architecture + API/Data Lifecycle | 31-36 | Clean boundary, API/data rules, validation, query/strategy/security review |
| 2.2 Reliable Messaging + Projection | 37-41 | Event envelope, outbox, webhook idempotency, Kafka projection reliability, rebuild |
| 2.3 Distributed Workflow + Resilience | 42-46 | Consistency decision, saga, compensation, retry/circuit breaker, gateway security |
| 2.4 Observability + Testing + Failure Drills | 47-50 | Trace, metrics, tests, failure drill, senior demo |

Day 31-39 đã có trước khi redesign. Từ Day 40 trở đi, bài học đi theo failure scenario.

## 3. Day 40-50 Roadmap

| Day | Chủ đề | Production scenario | Output chính |
| --- | --- | --- | --- |
| 40 | Kafka Projection Reliability + Rebalance Risk | Worker chết/rebalance trước hoặc sau khi commit offset | ProjectionWorker reliability review + duplicate/replay lab |
| 41 | MongoDB Projection Rebuild + Replay Safety | Read model sai, cần rebuild từ event stream | Rebuild strategy + replay lab |
| 42 | Distributed Consistency Decision Points | Checkout/payment không thể dùng một transaction toàn hệ thống | Consistency map cho checkout/payment |
| 43 | Payment Saga Orchestration | Order -> Payment fail giữa chừng | Saga state + happy/failure path |
| 44 | Webhook Production Handling + Compensation | Webhook duplicate/giả mạo/chậm, cần xử lý nhanh và an toàn | Webhook signature concept + payment event/compensation plan |
| 45 | Timeout / Retry / Circuit Breaker | Downstream chậm hoặc lỗi tạm thời | Resilience policy đúng chỗ |
| 46 | Gateway Edge Security + Rate Limit | Public edge bị spam hoặc gọi endpoint nhạy cảm | Rate limit/CORS/security headers/gateway rules |
| 47 | Correlation ID + Trace Propagation | Lỗi ở worker/service nhưng không lần được request/event gốc | Trace/correlation propagation drill |
| 48 | Prometheus + Grafana Failure Signals | Không biết outbox stuck/Kafka lag/webhook fail khi nào | Metrics + dashboard tối thiểu |
| 49 | Integration Testing + Testcontainers/k6 Smoke | Flow critical chỉ test thủ công nên dễ vỡ | Critical integration tests + smoke load |
| 50 | Failure Drills + Senior Production Demo | Cố tình gây lỗi và bảo vệ decision khi interview | Failure drill report + final demo script |

## 4. Chi Tiết Day 40-50

### Day 40 - Kafka Projection Reliability + Rebalance Risk

Không học lại Kafka basic.

Tập trung vào:

```text
ProjectionWorker commit offset khi nào?
Nếu ghi MongoDB xong nhưng chưa commit offset rồi worker chết thì sao?
Nếu commit offset trước khi ghi MongoDB thì mất gì?
key=orderId giúp giữ ordering theo aggregate như thế nào?
Scale consumer group bị giới hạn bởi partition count ra sao?
Kafka lag tăng thì debug từ đâu?
```

Thực hành:

```text
Inspect ProjectionWorker commit offset.
Produce nhiều event cùng orderId.
Check consumer group lag.
Simulate duplicate/replay bằng group id mới.
Verify MongoDB upsert không tạo duplicate summary.
```

### Day 41 - MongoDB Projection Rebuild + Replay Safety

Tập trung vào:

```text
Khi nào cần rebuild read model?
Rebuild vào collection hiện tại hay collection mới?
Kafka retention ảnh hưởng gì tới rebuild?
Duplicate eventId xử lý thế nào?
OrderPaid đến trước OrderCreated thì sao?
Có cần processed_projection_events không?
Khi nào cần aggregateVersion/sequence?
```

Thực hành:

```text
Tạo rebuild mode hoặc rebuild guide cho ProjectionWorker.
Rebuild vào collection mới.
Verify collection mới.
So sánh với order_summaries hiện tại.
Ghi rõ swap strategy và limitation.
```

### Day 42 - Distributed Consistency Decision Points

Không làm mindset suông. Bài này review checkout/payment flow thật.

Cần trả lời:

```text
Chỗ nào cần local transaction?
Chỗ nào cần outbox?
Chỗ nào chấp nhận eventual consistency?
Chỗ nào cần saga?
Chỗ nào cần compensation?
Chỗ nào cần idempotency?
Chỗ nào cần timeout?
```

Output:

```text
Consistency map cho Checkout -> Payment -> Notification -> Projection.
Quyết định rõ sync vs async cho từng đoạn.
```

### Day 43 - Payment Saga Orchestration

Tập trung vào orchestration trước, chưa nhảy sang full choreography.

Scenario:

```text
Order đã tạo.
Payment pending.
Payment succeeded -> Order paid.
Payment failed -> Order cancelled hoặc requires attention.
```

Thực hành:

```text
Thiết kế saga state.
Implement/review một happy path.
Implement/review một failure path.
Không claim distributed transaction.
```

### Day 44 - Webhook Production Handling + Compensation

Kế thừa Day 39 webhook idempotency.

Tập trung vào phần còn thiếu:

```text
Verify signature/shared secret concept.
Return 2xx nhanh.
Không làm business logic nặng trực tiếp trong HTTP request.
Publish PaymentSucceeded/PaymentFailed event hoặc tạo outbox message.
Compensation/refund/cancel/release flow.
```

Không làm:

```text
Không tích hợp provider thật nếu chưa cần.
Không claim payment production-ready.
Không lưu raw secret/token trong log.
```

### Day 45 - Timeout / Retry / Circuit Breaker

Tập trung vào chỗ đặt policy, không retry bừa.

Các câu hỏi:

```text
REST call nào retry được?
POST command có retry an toàn không?
Timeout đặt ở client hay server?
Circuit breaker giúp gì khi downstream chết?
Retry + idempotency liên quan thế nào?
```

Thực hành:

```text
Chọn một downstream call trong Basket/Catalog hoặc checkout flow.
Thêm timeout/retry policy nhỏ.
Simulate downstream chậm/lỗi.
Verify behavior và log.
```

### Day 46 - Gateway Edge Security + Rate Limit

Gateway bảo vệ edge, không ôm business logic.

Tập trung vào:

```text
Rate limiting.
CORS policy.
Security headers.
Route exposure review.
JWT validation at gateway direction.
Không public debug endpoints nhạy cảm.
```

Thực hành:

```text
Review ApiGateway route.
Thêm rate limit hoặc policy demo.
Test spam request.
Kiểm tra endpoint nào public/internal/debug.
```

### Day 47 - Correlation ID + Trace Propagation

Project đã có Aspire/ServiceDefaults làm nền observability. Bài này không phải cài OpenTelemetry từ số 0 nếu nền đã có.

Tập trung vào:

```text
Request từ Gateway sang service có trace/correlation id không?
Message publish sang worker có giữ context không?
Khi lỗi ở worker, có lần ngược được event/request gốc không?
```

Thực hành:

```text
Gọi một flow qua Gateway.
Tìm traceId/correlationId trong logs.
Đi qua service -> broker/worker nếu có.
Ghi limitation còn thiếu.
```

### Day 48 - Prometheus + Grafana Failure Signals

Không làm dashboard cho đẹp.

Metric phải trả lời được:

```text
Kafka lag có tăng không?
Outbox pending/stuck bao nhiêu?
Webhook failure rate bao nhiêu?
HTTP 5xx rate bao nhiêu?
p95/p99 latency có xấu không?
Container CPU/RAM/disk có nguy cơ không?
```

Stack local đề xuất:

```text
Prometheus
Grafana
optional Alertmanager
```

Output:

```text
Dashboard tối thiểu cho failure signals.
Không cần dashboard màu mè.
```

### Day 49 - Integration Testing + Testcontainers/k6 Smoke

Không mock mọi thứ.

Critical flow nên test với container thật:

```text
Payment webhook duplicate không xử lý lặp.
Checkout tạo order + outbox.
ProjectionWorker ghi MongoDB read model.
```

Giữ scope nhỏ:

```text
1-2 integration tests critical trước.
k6 chỉ smoke load nhẹ, chưa phải performance benchmark sâu.
```

### Day 50 - Failure Drills + Senior Production Demo

Day 50 là phiên defense project, không chỉ checkpoint.

Failure drills tối thiểu:

```text
RabbitMQ/consumer failure.
Outbox stuck.
Kafka lag tăng.
MongoDB projection failure.
Payment webhook duplicate.
Downstream timeout.
Gateway rate limit triggered.
```

Sau Day 50, người học phải trình bày được:

```text
System design.
Failure handling.
Trade-off.
Known limitations.
Next hardening steps.
```

## 5. Sau Day 50 - Local PROD Track

Mục tiêu sau Day 50 không phải public ngay. Mục tiêu là chạy MicroShop theo kiểu production-like trên localhost.

Định nghĩa local PROD:

```text
Docker image thật.
Docker Compose production profile.
Reverse proxy hoặc gateway-only entrypoint.
.env không commit.
Persistent volumes.
Health checks.
Restart policy.
Migration rõ ràng.
Logs/metrics/failure drill.
```

Roadmap local PROD đề xuất:

| Day | Chủ đề | Output |
| --- | --- | --- |
| 51 | Local PROD Compose | `compose.local-prod.yml`, chỉ expose gateway/reverse proxy |
| 52 | Production Dockerfiles | Multi-stage Dockerfile, Release image, không `dotnet run` |
| 53 | Config + Secrets Hygiene | `.env.example`, `.env.local-prod` ignored, config validation |
| 54 | Reverse Proxy Local | Caddy/Nginx local, gateway-only exposure |
| 55 | Healthcheck + Restart + Graceful Shutdown | Docker healthcheck, restart policy, shutdown behavior |
| 56 | Persistence + Backup/Restore | Volumes, backup/restore Postgres/Mongo, restart data survives |
| 57 | CI/CD Local PROD Pipeline | GitHub Actions build/test/docker build |
| 58 | Prometheus + Grafana Local | Metrics dashboard and failure signal checks |
| 59 | Integration Tests + k6 Smoke | Testcontainers critical flow + light load smoke |
| 60 | Local PROD Release Candidate | One-command run, Postman full flow, failure drill, runbook in giáo án |

Không ưu tiên Docker Swarm:

```text
Docker Compose -> Kubernetes/K3s là đường học/deploy phổ biến hơn.
Swarm đơn giản nhưng ít giá trị interview/cloud-native hơn hiện nay.
```

Kubernetes/K3s để sau khi local PROD ổn:

```text
K3s/Kubernetes
Helm
Ingress
ConfigMap/Secret
Readiness/Liveness
Rolling update
Resource limits
```

## 6. Production Topic Map

| Topic | Gắn vào Day |
| --- | --- |
| Standard Error Response | 32 |
| PostgreSQL Migration | 33 |
| Validation/Mapping | 34 |
| Query Criteria/Specification | 35 |
| Strategy/Audit/Identity Review | 36 |
| Event Envelope | 37 |
| Transactional Outbox | 38 |
| Webhook Idempotency/WebhookLog | 39 |
| Kafka Consumer Reliability | 40 |
| Projection Rebuild/Replay Safety | 41 |
| Distributed Consistency | 42 |
| Saga/Compensation | 43, 44 |
| Timeout/Retry/Circuit Breaker | 45 |
| Gateway Edge Security/Rate Limit | 46 |
| Correlation/Tracing | 47 |
| Prometheus/Grafana/Metrics | 48, 58 |
| Integration Testing/Testcontainers | 49, 59 |
| Failure Drills | 50, 60 |
| Docker Compose Local PROD | 51 |
| Production Dockerfiles | 52 |
| Secrets/Config | 53 |
| Reverse Proxy | 54 |
| Backup/Restore | 56 |
| CI/CD | 57 |
| Kubernetes/K3s/Helm | Sau local PROD ổn |

## 7. Final Target

Sau Day 50:

```text
Project đủ mạnh để giải thích senior-level design/failure trade-off.
Chưa gọi production-ready thật.
```

Sau Day 60 local PROD:

```text
Project chạy production-like trên localhost.
Có Docker images, compose profile, healthcheck, secrets hygiene, backup/restore, CI/CD cơ bản, metrics, tests, failure drill.
Sẵn sàng chuyển sang VPS hoặc K3s/Kubernetes với ít thay đổi hơn.
```

Định vị đúng:

```text
Production-minded Microservices Learning Project
Local Production-like Candidate
```

Không claim:

```text
Enterprise production-ready platform
```
