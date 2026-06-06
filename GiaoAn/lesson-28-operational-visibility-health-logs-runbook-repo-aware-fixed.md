---
lesson: 28
title: "Operational Visibility Review - Health, Logs, Kafka Lag, Runbook"
duration: "90-120 minutes"
phase: "Phase 1.7 - Foundation Review"
project: "MicroShop"
testing: "Postman-first + Docker logs + Kafka CLI"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown to avoid mojibake in Notion/Rider/GitHub"
---

# Lesson 28: Operational Visibility Review - Health, Logs, Kafka Lag, Runbook

## 0. Why this version exists

This is the corrected source-of-truth version for Lesson 28.

Previous drafts had implementation risks:

```text
- They could create a duplicate MongoDbHealthCheck in OrderQueryService.
- They assumed /health exists in Docker for ApiGateway, but MapDefaultEndpoints may only expose it in Development.
- They said OpenTelemetry is not added, while ServiceDefaults already configures OpenTelemetry.
- Kafka sample payload missed required ProjectionWorker fields.
- ApiGateway build path was wrong.
- Multiple lesson files could confuse the implementation plan.
```

Use this file as the main Lesson 28 plan.

Compact versions are only summaries.

---

## 1. Current repo context

Current completed lesson:

```text
Lesson 27: Kafka MongoDB ProjectionWorker
Tag: v27/kafka-mongodb-projection-worker
Commit: a21f300
```

Current projection flow:

```text
Kafka CLI produces demo order events
-> Workers/ProjectionWorker consumes topic microshop.order-events
-> ProjectionWorker upserts MongoDB MicroShop_OrderReadDb.order_summaries
-> Invalid/unsupported messages go to projection_failures
-> OrderQueryService reads MongoDB
-> ApiGateway proxies order summary endpoints
```

Correct endpoints:

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

Do not use old examples:

```text
/orders/read-model
```

Responsibilities remain unchanged:

```text
RabbitMQ:
    workflow/task messaging, NotificationWorker

Kafka:
    event stream/projection/read model learning

MongoDB:
    order read model
```

---

## 2. Goal of Lesson 28

Lesson 28 is not about adding a full observability stack.

Goal:

```text
Review and improve local operational visibility.
```

By the end:

```text
[ ] Health checks are understood and verified.
[ ] Existing OrderQueryService Mongo health check is reused, not duplicated.
[ ] ApiGateway /health behavior is clarified for Docker.
[ ] ProjectionWorker structured logs are reviewed.
[ ] Kafka lag check is documented.
[ ] projection_failures verification is documented.
[ ] Runtime debug runbook is created.
[ ] Build and verification commands match the repo.
```

---

## 3. Scope guard

Do in this lesson:

```text
[ ] Verify existing health check implementation.
[ ] Add missing health mapping only if needed.
[ ] Review logs and improve structured log fields if missing.
[ ] Add documentation/runbook.
[ ] Add Postman/CLI verification steps.
```

Do not do in this lesson:

```text
[ ] Do not create a second MongoDbHealthCheck if one already exists.
[ ] Do not change RabbitMQ/Kafka responsibilities.
[ ] Do not add Kafka retry topic/DLT.
[ ] Do not add processed-event collection.
[ ] Do not add projection rebuild command.
[ ] Do not add Grafana/Prometheus.
[ ] Do not add OTLP collector.
[ ] Do not claim production observability is complete.
```

Important wording:

```text
The project already has OpenTelemetry configured in ServiceDefaults.
Lesson 28 does not add a full production observability stack yet.
```

Better limitation wording:

```text
Not complete yet:
- OTLP collector/export pipeline
- Grafana dashboards
- alerts
- production correlation propagation
- centralized log querying
```

---

## 4. Review existing health check first

Before writing code, inspect current files.

Run:

```powershell
git status --short
```

Search health check:

```powershell
Get-ChildItem -Recurse -Filter *HealthCheck*.cs
```

Expected:

```text
OrderQueryService already has MongoDbHealthCheck.
It is already registered in Infrastructure.
```

Do not create:

```text
Services/OrderQueryService/Infrastructure/Health/MongoDbHealthCheck.cs
```

if an equivalent file already exists.

The correct task is:

```text
Review current health check.
Verify it is registered.
Verify /health is reachable in the intended environment.
```

---

## 5. OrderQueryService health

Check these things in OrderQueryService:

```text
[ ] MongoDbHealthCheck exists.
[ ] It pings MongoDB or checks the configured database.
[ ] It is registered in Infrastructure/DI.
[ ] /health is mapped by ServiceDefaults or Program.cs.
```

If /health is already mapped through ServiceDefaults, do not map it again.

If it is not mapped, add one clear mapping in the right startup location.

Verify:

```text
GET {{order_query_url}}/health
```

Expected:

```text
Healthy
```

or JSON health response depending on current mapping.

If MongoDB is down:

```text
OrderQueryService health should become Unhealthy or degraded depending on implementation.
```

---

## 6. ApiGateway health and Docker environment warning

Important repo detail:

```text
MapDefaultEndpoints() may only map /health in Development.
docker-compose.yml runs ApiGateway with ASPNETCORE_ENVIRONMENT=Docker.
```

Therefore, in Docker flow:

```text
GET /health on ApiGateway may not exist.
```

Choose one implementation path.

### Option A - Local learning only

For local lesson testing, run ApiGateway with:

```text
ASPNETCORE_ENVIRONMENT=Development
```

Then /health from MapDefaultEndpoints should be available if ServiceDefaults maps it.

Use this if you only need local learning verification.

### Option B - Explicit gateway health endpoint

If you want /health available in Docker too, explicitly map health checks in ApiGateway.

In `Services/ApiGateway/Program.cs`, ensure:

```csharp
builder.Services.AddHealthChecks();
```

Map:

```csharp
app.MapHealthChecks("/health");
```

Use this if the team wants health available in Docker environment.

Recommended for Lesson 28:

```text
Use Option B if it does not conflict with existing ServiceDefaults.
Document why it is needed:
ApiGateway runs with ASPNETCORE_ENVIRONMENT=Docker in docker-compose.
```

Verify:

```text
GET {{gateway_url}}/health
```

Expected:

```text
Healthy
```

Also verify gateway route:

```text
GET {{gateway_url}}/order-summaries
GET {{gateway_url}}/order-summaries/{orderId}
```

---

## 7. ProjectionWorker logs review

ProjectionWorker should log enough fields to debug Kafka projection.

Required fields for success logs:

```text
service
topic
partition
offset
key
eventId
eventType
orderId
customerId
```

Success log shape:

```csharp
_logger.LogInformation(
    "Projection event applied. Service={Service}, Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}, EventId={EventId}, EventType={EventType}, OrderId={OrderId}, CustomerId={CustomerId}",
    "ProjectionWorker",
    result.Topic,
    result.Partition.Value,
    result.Offset.Value,
    result.Message.Key,
    message.EventId,
    message.EventType,
    message.OrderId,
    message.CustomerId);
```

Invalid/unsupported message log should include:

```text
topic
partition
offset
key
reason
```

MongoDB failure log should include:

```text
topic
partition
offset
key
exception
```

Offset rule:

```text
Invalid/unsupported message:
    save to projection_failures
    commit offset

MongoDB failure:
    do not commit offset
```

Do not change this rule in Lesson 28.

---

## 8. Kafka payload must match current ProjectionWorker validation

Use GUID values for:

```text
orderId
customerId
```

Kafka key:

```text
key = orderId
```

Current ProjectionWorker validation requires extra fields.

Valid OrderCreated payload:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

Valid OrderPaid payload:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","eventType":"OrderPaid","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:05:00Z"}
```

Valid OrderCancelled payload:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"cccccccc-cccc-cccc-cccc-cccccccccccc","eventType":"OrderCancelled","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:10:00Z"}
```

If `customerName`, `itemCount`, or `items` is missing:

```text
The message may be stored in projection_failures.
It may not create/update order_summaries.
```

---

## 9. Runtime verification

Start runtime:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker api-gateway
```

Create topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

Start producer:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Paste valid OrderCreated payload from section 8.

Check ProjectionWorker logs:

```powershell
docker logs microshop-projectionworker --tail 100
```

Check Kafka lag:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Check read API:

```text
GET {{gateway_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

Expected:

```text
Order summary exists.
Projection metadata exists.
lastProjectedEventId = aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
lastProjectedEventType = OrderCreated
```

Then produce OrderPaid.

Expected:

```text
Order summary status changes to Paid.
paidAtUtc has value.
lastProjectedEventType = OrderPaid.
```

---

## 10. Verify projection_failures

Produce invalid payload:

```text
33333333-3333-3333-3333-333333333333:{"bad":"payload"}
```

Expected:

```text
ProjectionWorker stores the message in projection_failures.
The offset is committed.
The partition is not blocked forever by this bad training message.
```

Check logs:

```powershell
docker logs microshop-projectionworker --tail 100
```

Check MongoDB manually:

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

Inside shell:

```javascript
use MicroShop_OrderReadDb
db.projection_failures.find().sort({createdAtUtc:-1}).limit(5).pretty()
```

If the timestamp field has a different name in repo, use the actual field.

---

## 11. Health verification

OrderQueryService:

```text
GET {{order_query_url}}/health
```

ApiGateway:

```text
GET {{gateway_url}}/health
```

If gateway health is missing in Docker:

```text
Check ASPNETCORE_ENVIRONMENT.
If it is Docker, MapDefaultEndpoints may not expose /health.
Use Option B from section 6.
```

Gateway proxy verification:

```text
GET {{gateway_url}}/order-summaries
GET {{gateway_url}}/order-summaries/11111111-1111-1111-1111-111111111111
```

---

## 12. Kafka lag runbook

Command:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Fields:

```text
CURRENT-OFFSET
LOG-END-OFFSET
LAG
```

Healthy expectation:

```text
LAG eventually becomes 0.
```

If lag grows:

```text
[ ] Is ProjectionWorker running?
[ ] Is MongoDB healthy?
[ ] Is the payload valid?
[ ] Is the worker stuck on a poison message?
[ ] Is group id correct?
[ ] Are logs showing MongoDB failure?
```

---

## 13. Create docs/operational-visibility.md

Create:

```text
docs/operational-visibility.md
```

Suggested content:

~~~md
# MicroShop Operational Visibility

## Current Goal

Provide local operational visibility for Stage 1.

## Existing Observability Foundation

ServiceDefaults already configures OpenTelemetry logging/metrics/tracing basics.

This lesson does not add a full production observability stack yet.

Not complete yet:

- OTLP collector/export pipeline
- Grafana dashboards
- alerts
- production correlation propagation
- centralized log querying

## Health

OrderQueryService:

```text
GET /health
```

ApiGateway:

```text
GET /health
```

Note:

```text
ApiGateway health in Docker may require explicit mapping because ASPNETCORE_ENVIRONMENT=Docker.
```

## Important APIs

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

## Kafka Checks

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

## ProjectionWorker Logs

Required fields:

```text
topic
partition
offset
key
eventId
eventType
orderId
customerId
```

## MongoDB

Database:

```text
MicroShop_OrderReadDb
```

Collections:

```text
order_summaries
projection_failures
```
~~~

---

## 14. Create docs/runbooks/microshop-local-debug-runbook.md

Create:

```text
docs/runbooks/microshop-local-debug-runbook.md
```

Suggested content:

~~~md
# MicroShop Local Debug Runbook

## Start runtime

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker api-gateway
```

## Check containers

```powershell
docker compose ps
```

## Check Kafka topic

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

If missing:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

## Check ProjectionWorker logs

```powershell
docker logs microshop-projectionworker --tail 100
```

Look for:

```text
Projection event applied
Projection message stored as failure
Projection MongoDB apply failed
```

## Check consumer group lag

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Healthy:

```text
LAG eventually becomes 0.
```

## Produce valid event

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Payload:

```text
11111111-1111-1111-1111-111111111111:{"eventId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","eventType":"OrderCreated","orderId":"11111111-1111-1111-1111-111111111111","customerId":"22222222-2222-2222-2222-222222222222","customerName":"Demo Customer","totalAmount":1977.3,"currency":"VND","itemCount":0,"items":[],"occurredAtUtc":"2026-05-28T10:00:00Z"}
```

## Check read API

```text
GET /order-summaries
GET /order-summaries/11111111-1111-1111-1111-111111111111
```

## Check MongoDB manually

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

```javascript
use MicroShop_OrderReadDb
db.order_summaries.find().pretty()
db.projection_failures.find().sort({createdAtUtc:-1}).limit(5).pretty()
```

## Common issues

### ApiGateway /health not found in Docker

Cause:

```text
MapDefaultEndpoints may only expose /health in Development.
ApiGateway runs as ASPNETCORE_ENVIRONMENT=Docker.
```

Fix:

```text
Map /health explicitly or run local gateway as Development.
```

### Order summary is not created

Check:

```text
Payload has customerName?
Payload has itemCount?
Payload has items?
orderId/customerId are GUID?
Kafka key equals orderId?
ProjectionWorker logs show success?
Message went to projection_failures?
```

### Lag grows

Check:

```text
ProjectionWorker running?
MongoDB healthy?
Poison message?
Consumer group id correct?
```

### Invalid message

Expected:

```text
Invalid/unsupported messages are stored in projection_failures and committed.
```

### MongoDB failure

Expected:

```text
MongoDB failure should not commit Kafka offset.
```
~~~

---

## 15. Build checklist

Use correct repo paths.

```powershell
git status --short
dotnet build Services/OrderQueryService/OrderQueryService.csproj
dotnet build Services/ApiGateway/ApiGateway.csproj
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj
```

If reasonable:

```powershell
dotnet build
```

Wrong old path:

```powershell
dotnet build ApiGateway/ApiGateway.csproj
```

Do not use it.

---

## 16. Pass checklist

```text
[ ] No duplicate MongoDbHealthCheck is created.
[ ] Existing OrderQueryService Mongo health check is verified.
[ ] OrderQueryService /health works.
[ ] ApiGateway /health behavior is fixed or documented for Docker.
[ ] Gateway proxies /order-summaries.
[ ] Valid Kafka payload includes customerName, itemCount, items.
[ ] ProjectionWorker success logs include eventId/orderId/partition/offset/key.
[ ] Invalid payload goes to projection_failures.
[ ] MongoDB failure does not commit Kafka offset.
[ ] Kafka lag can be checked by CLI.
[ ] Build paths are correct.
[ ] docs/operational-visibility.md exists.
[ ] docs/runbooks/microshop-local-debug-runbook.md exists.
```

---

## 17. Next lesson

Lesson 29:

```text
README + Architecture Diagram + ADR + OpenAPI Review
```

Goal:

```text
Make MicroShop understandable as a portfolio project:
- README
- architecture diagram
- ADR review
- OpenAPI/Swagger review
- demo script prep for Lesson 30
```
