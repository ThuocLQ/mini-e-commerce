---
day: 30
title: "Foundation Demo + Checkpoint"
duration: "90-120 minutes"
phase: "Phase 1.7 - Foundation Review"
project: "MicroShop"
testing: "Demo-first + Postman + Docker logs + Kafka CLI"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown to avoid mojibake in Notion/Rider/GitHub"
---

# Day 30: Foundation Demo + Checkpoint

## 0. Current position

You have completed the Stage 1 foundation lessons up to:

```text
Day 27: Kafka -> MongoDB ProjectionWorker
Day 28: Operational Visibility - Health, Logs, Kafka Lag, Runbook
Day 29: README + Architecture Diagram + ADR + API Surface Review
```

Day 30 is a checkpoint day.

Goal:

```text
Run a clean foundation demo.
Verify what is actually working.
Document what is solid.
Document what is still training-stage.
Create a production hardening backlog for Stage 2.
```

This day is not about adding major new features.

It is about proving the foundation and preparing the next stage.

---

## 1. Current repo context

Current services:

```text
CatalogService
BasketService
OrderingService
DiscountService
IdentityService
PaymentService
OrderQueryService
ApiGateway
```

Current workers:

```text
Services/NotificationWorker
Workers/ProjectionWorker
```

Current important flow from recent lessons:

```text
Kafka CLI demo order events
-> Kafka topic microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
-> ApiGateway
-> GET /order-summaries
```

RabbitMQ role remains:

```text
RabbitMQ -> NotificationWorker workflow/task messaging
```

Kafka role remains:

```text
Kafka -> event stream/projection/read model learning
```

Do not use old endpoint examples:

```text
/orders/read-model
```

Use:

```text
/order-summaries
/order-summaries/{orderId}
```

---

## 2. Goal of Day 30

By the end:

```text
[ ] You can start the local runtime.
[ ] You can verify core containers are healthy.
[ ] You can verify gateway/order query routes.
[ ] You can produce a Kafka demo event.
[ ] ProjectionWorker applies the event to MongoDB.
[ ] OrderQueryService returns the read model.
[ ] Kafka lag can be checked.
[ ] projection_failures behavior can be explained.
[ ] README/docs/demo script are aligned with actual repo.
[ ] A Stage 1 checkpoint report exists.
[ ] A Stage 2 hardening backlog exists.
```

Main outputs:

```text
docs/checkpoints/stage-1-foundation-checkpoint.md
docs/demo-script-foundation.md
docs/backlog/stage-2-production-hardening-backlog.md
```

Optional after review:

```text
Tag: day-30-foundation-demo-checkpoint
```

---

## 3. Scope guard

Do:

```text
[ ] Run demo.
[ ] Verify health/read/projection flows.
[ ] Check logs and lag.
[ ] Review docs consistency.
[ ] Write checkpoint report.
[ ] Write hardening backlog.
```

Do not:

```text
[ ] Do not add Kafka DLT yet.
[ ] Do not add projection rebuild command yet.
[ ] Do not add processed-event collection yet.
[ ] Do not add OrderingService Kafka publisher yet.
[ ] Do not add OpenTelemetry/Grafana stack yet.
[ ] Do not refactor service architecture.
[ ] Do not claim production readiness.
```

Rule:

```text
Day 30 closes Stage 1 foundation.
It does not pretend Stage 2 hardening is already done.
```

---

## 4. Pre-check

Run:

```powershell
git status --short
```

Check latest docs exist:

```powershell
Test-Path README.md
Test-Path docs/architecture-diagram.md
Test-Path docs/communication-decisions.md
Test-Path docs/operational-visibility.md
Test-Path docs/runbooks/microshop-local-debug-runbook.md
Test-Path docs/demo-script-foundation.md
```

Check important project paths:

```powershell
Test-Path Services/ApiGateway/ApiGateway.csproj
Test-Path Services/OrderQueryService/OrderQueryService.csproj
Test-Path Workers/ProjectionWorker/ProjectionWorker.csproj
```

Check Docker services:

```powershell
docker compose config --services
```

Expected to include at least:

```text
zookeeper
kafka
mongodb
orderqueryservice
projectionworker
api-gateway
```

Names may vary by repo. Use actual names from compose.

---

## 5. Choose demo mode

Day 30 has two demo modes.

### Mode A - Lite projection demo

Use this for the recent Kafka -> MongoDB read model flow.

Important repo detail:

```text
ApiGateway depends on many services in docker-compose.
If you include api-gateway here, Docker Compose may also start most of the system.
So the true lite mode does not include api-gateway.
```

Start true lite mode:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

This mode proves:

```text
Kafka topic
ProjectionWorker
MongoDB read model
OrderQueryService direct API
Kafka lag
```

Test direct OrderQueryService endpoints:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/{orderId}
```

If you also want to test ApiGateway in lite mode, use one of these choices:

```powershell
docker compose up -d --build --no-deps api-gateway
```

or switch to full system mode.

### Mode B - Full local system

Use this to start everything available in docker-compose.

```powershell
docker compose up -d --build
```

This mode proves broader local runtime.

It may be heavier.

Use Mode B if your machine can handle it.

Recommended for checkpoint:

```text
Run Mode A first.
Run Mode B only if stable and needed for final portfolio demo.
```

---

## 6. Build checkpoint

Build touched/core projects:

```powershell
dotnet build Services/ApiGateway/ApiGateway.csproj
dotnet build Services/OrderQueryService/OrderQueryService.csproj
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
```

If reasonable:

```powershell
dotnet build
```

If solution build fails due to unrelated old service issues:

```text
Document it honestly in the checkpoint.
Do not hide it.
Also document which projects build successfully.
```

Checkpoint note example:

```text
ApiGateway, OrderQueryService, ProjectionWorker build successfully.
Full solution build has unrelated issue in X service.
```

---

## 7. Start runtime

Lite demo:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Check containers:

```powershell
docker compose ps
```

Check logs:

```powershell
docker logs microshop-projectionworker --tail 100
docker logs microshop-orderqueryservice --tail 100
```

Container names may differ.

If names differ, use:

```powershell
docker compose logs projectionworker --tail 100
docker compose logs orderqueryservice --tail 100
```

If running full system or optional gateway mode, also check:

```powershell
docker compose logs api-gateway --tail 100
```

---

## 8. Health checks

OrderQueryService:

```text
GET {{order_query_url}}/health
```

ApiGateway is optional in lite mode.

If testing full system or explicit gateway mode:

```text
GET {{gateway_url}}/health
```

Important caveat:

```text
ApiGateway /health in Docker may require explicit mapping because ASPNETCORE_ENVIRONMENT can be Docker.
If /health is missing, document whether the repo chose explicit mapping or Development-only behavior.
```

Pass condition:

```text
OrderQueryService health works.
ApiGateway health behavior is known and documented if gateway is included in the demo.
```

---

## 9. Create Kafka topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

Describe topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

Expected:

```text
Topic exists.
Partition count = 3.
Replication factor = 1 for local learning.
```

---

## 10. Produce valid demo events

Start producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Use GUID key = orderId.

### OrderCreated

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

### OrderPaid

```text
11111111-1111-1111-1111-111111111111:{"eventId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","eventType":"OrderPaid","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:05:00Z"}
```

Expected:

```text
ProjectionWorker applies OrderCreated.
ProjectionWorker applies OrderPaid.
MongoDB order summary status becomes Paid.
```

---

## 11. Verify ProjectionWorker logs

```powershell
docker logs microshop-projectionworker --tail 100
```

or:

```powershell
docker compose logs projectionworker --tail 100
```

Look for:

```text
Projection event applied
EventId
EventType
OrderId
CustomerId
Topic
Partition
Offset
Key
```

If no logs:

```text
Check topic.
Check consumer group.
Check ProjectionWorker container.
Check BootstrapServers.
Check payload validation.
```

---

## 12. Verify read model via API

Via service directly in lite mode:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

Via gateway in full system or optional gateway mode:

```text
GET {{gateway_url}}/order-summaries
GET {{gateway_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

Expected important fields:

```text
orderId = 11111111-1111-1111-1111-111111111111
customerId = 22222222-2222-2222-2222-222222222222
customerName = Demo Customer
status = Paid
paidAtUtc has value
lastProjectedEventType = OrderPaid
lastProjectedEventId = bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
```

Field casing depends on current API serialization.

Use actual response.

---

## 13. Verify Kafka lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Expected:

```text
LAG = 0
```

or:

```text
LAG decreases to 0 after a few seconds.
```

If lag grows:

```text
ProjectionWorker may be down.
MongoDB may be down.
Payload may be invalid.
Consumer may be stuck.
```

---

## 14. Verify projection_failures behavior

Produce invalid payload:

```text
33333333-3333-3333-3333-333333333333:{"bad":"payload"}
```

Expected:

```text
ProjectionWorker stores it in projection_failures.
Offset is committed.
Partition is not blocked forever by this invalid training message.
```

Check logs:

```powershell
docker logs microshop-projectionworker --tail 100
```

Check MongoDB:

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

Inside shell:

```javascript
use MicroShop_OrderReadDb
db.projection_failures.find().sort({createdAtUtc:-1}).limit(5).pretty()
```

If the timestamp field is different, use actual field.

---

## 15. Demo explanation script

When presenting the demo, explain in this order:

```text
1. ApiGateway is the entrypoint.
2. OrderQueryService exposes MongoDB read model via /order-summaries.
3. Kafka topic microshop.order-events represents order event stream for projection learning.
4. ProjectionWorker consumes Kafka and updates MongoDB order_summaries.
5. projection_failures stores bad training messages.
6. RabbitMQ is still used for workflow/task messaging with NotificationWorker.
7. Kafka does not replace RabbitMQ.
8. This is Stage 1 foundation, not production complete.
```

Short spoken version:

```text
MicroShop uses RabbitMQ for workflow messages and Kafka for event stream projection.
In this demo, I produce order events to Kafka, ProjectionWorker updates MongoDB, and OrderQueryService exposes the read model through /order-summaries.
```

---

## 16. Update README for Day 30

Before creating the checkpoint report, update README current status.

If README currently says:

```text
Day 29: README + Architecture Diagram + ADR + API Surface Review
```

change it to:

```text
Day 30: Foundation Demo + Checkpoint
```

Recommended README status wording:

```text
Current completed: Day 30 - Foundation Demo + Checkpoint
```

or, if implementation is still under review:

```text
Current focus: Day 30 - Foundation Demo + Checkpoint
```

Also verify README runtime commands separate:

```text
Lite projection demo:
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker

Full system:
docker compose up -d --build
```

---

## 17. Create checkpoint report

Create folder:

```text
docs/checkpoints
```

Create file:

```text
docs/checkpoints/stage-1-foundation-checkpoint.md
```

Suggested content:

````md
# Stage 1 Foundation Checkpoint

## Status

```text
Checkpoint date: YYYY-MM-DD
Current day: Day 30
```

## What works

```text
[ ] ApiGateway starts.
[ ] OrderQueryService starts.
[ ] ProjectionWorker starts.
[ ] Kafka topic microshop.order-events exists.
[ ] ProjectionWorker consumes valid events.
[ ] MongoDB order_summaries is updated.
[ ] GET /order-summaries works via OrderQueryService direct API.
[ ] GET /order-summaries/{orderId} works via OrderQueryService direct API.
[ ] Gateway route is verified only in full system or optional gateway mode.
[ ] projection_failures stores invalid messages.
[ ] Kafka lag can be checked.
```

## Verified demo flow

```text
Kafka CLI
-> microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
-> ApiGateway
-> /order-summaries/{orderId}
```

## Build results

```text
ApiGateway:
OrderQueryService:
ProjectionWorker:
Full solution:
```

## Runtime results

```text
docker compose ps:
Kafka lag:
Health endpoints:
```

## Known limitations

```text
No Kafka retry topic/DLT yet.
No processed-event collection yet.
No projection rebuild command yet.
No OrderingService Kafka publisher yet.
No schema registry yet.
No production observability stack yet.
No full CI/CD/deployment strategy yet.
```

## Stage 2 readiness

```text
Ready to start production hardening after Day 30.
```
````

---

## 18. Create Stage 2 hardening backlog

Create folder:

```text
docs/backlog
```

Create file:

```text
docs/backlog/stage-2-production-hardening-backlog.md
```

Suggested content:

````md
# Stage 2 Production Hardening Backlog

## Event-driven reliability

```text
[ ] Kafka retry topic and DLT.
[ ] Projection processed-event collection.
[ ] Projection rebuild command.
[ ] OrderingService outbox publisher to Kafka.
[ ] Contract/schema versioning for Kafka events.
[ ] Atomic Mongo projection updates with event sequence/version.
```

## Observability

```text
[ ] Correlation ID propagation.
[ ] OpenTelemetry export pipeline.
[ ] Metrics for Kafka lag.
[ ] Metrics for failed projections.
[ ] Metrics for consumer health.
[ ] Grafana dashboard.
[ ] Alerting intro.
```

## API and architecture

```text
[ ] Standard error response.
[ ] API versioning.
[ ] Swagger/OpenAPI enablement if not already enabled.
[ ] OpenAPI auth documentation.
[ ] Gateway route review.
```

## Data and persistence

```text
[ ] PostgreSQL migration review.
[ ] Backup/restore mindset.
[ ] Read model rebuild strategy.
[ ] Database index review.
```

## Security

```text
[ ] JWT/Identity review.
[ ] SSO/OIDC decision note.
[ ] Internal service security.
[ ] Audit log policy.
```

## Testing

```text
[ ] Unit tests for handlers.
[ ] Integration tests with Testcontainers.
[ ] Contract tests for events.
[ ] Failure scenario tests.
```
````

---

## 19. Update demo script if needed

Review:

```text
docs/demo-script-foundation.md
```

Make sure it uses:

```text
Day 30 naming
/order-summaries endpoints
valid Kafka payload with customerName, itemCount, items
lite projection demo and full system runtime separated
```

Do not use:

```text
/orders/read-model
ORD-900
CUST-900
payload without customerName/itemCount/items
```

---

## 20. Postman checkpoint collection

Create or update:

```text
postman/MicroShop.FoundationCheckpoint.postman_collection.json
```

Minimum requests for lite mode:

```text
GET {{order_query_url}}/health
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/{{orderId}}
```

Optional requests for full system or gateway mode:

```text
GET {{gateway_url}}/health
GET {{gateway_url}}/order-summaries
GET {{gateway_url}}/order-summaries/{{orderId}}
```

Environment:

```text
gateway_url
order_query_url
orderId
```

Demo value:

```text
orderId = 11111111-1111-1111-1111-111111111111
```

If gateway health is not available in Docker:

```text
Document the caveat in collection notes.
```

---

## 21. Review docs consistency

Check docs for wrong old names:

```powershell
Select-String -Path .\docs\**\*.md,README.md -Pattern "/orders/read-model|ORD-900|CUST-900|Buoi|Buoi|Lesson 30|Lesson 29"
```

Expected:

```text
No old endpoint.
No ORD-900/CUST-900 demo payload.
No user-facing Buoi/Buoi naming in current docs.
No Lesson 29/30 user-facing naming if you have standardized on Day.
```

If old docs intentionally contain history, mark them clearly as old or archived.

---

## 22. Pass checklist

You pass Day 30 when:

```text
[ ] Core projects build or build issues are documented honestly.
[ ] Lite projection demo starts.
[ ] Kafka topic exists.
[ ] Valid OrderCreated event is consumed.
[ ] Valid OrderPaid event is consumed.
[ ] MongoDB order_summaries is updated.
[ ] GET /order-summaries/{orderId} returns expected data.
[ ] Kafka lag is checked and understood.
[ ] Invalid message goes to projection_failures.
[ ] README/docs are consistent with current endpoints.
[ ] stage-1-foundation-checkpoint.md exists.
[ ] stage-2-production-hardening-backlog.md exists.
[ ] README current status is updated to Day 30.
[ ] demo-script-foundation.md is up to date.
[ ] No docs claim Stage 2 features are already complete.
```

---

## 23. Optional commit and tag after review

Do this only after implementation and docs are reviewed.

Recommended commit:

```text
Day 30: Foundation Demo Checkpoint
```

Recommended tag:

```text
day-30-foundation-demo-checkpoint
```

Commands:

```powershell
git add .
git commit -m "Day 30: Foundation Demo Checkpoint"
git tag day-30-foundation-demo-checkpoint
```

---

## 24. What Day 30 proves

Day 30 proves:

```text
MicroShop has a working Stage 1 foundation.
The project has multiple services and workers.
RabbitMQ and Kafka have separate responsibilities.
Kafka projection to MongoDB works in local demo.
OrderQueryService exposes the read model.
Operational debug basics exist.
Docs are good enough for portfolio review.
```

Day 30 does not prove:

```text
The system is production ready.
The event pipeline has DLT/rebuild/schema governance.
The observability stack is complete.
The whole system has CI/CD and deployment hardening.
```

This is exactly why Stage 2 exists.

---

## 25. Next stage

After Day 30:

```text
Stage 2: Production Hardening
```

Recommended first Stage 2 topics:

```text
API versioning + standard error response
PostgreSQL/migration hardening
Transactional Outbox standardization
Kafka retry topic/DLT
Projection rebuild
Correlation ID + OpenTelemetry export
Metrics + Prometheus/Grafana + alerting
Testing with Testcontainers
```

Do not start all at once.

Pick one hardening slice per day.
