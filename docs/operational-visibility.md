# MicroShop Operational Visibility

## Current Goal

MicroShop needs enough local visibility to debug the Stage 1 services without adding a full observability platform yet.

This document tracks the health, logging, Kafka lag, and MongoDB checks used during local development.

## Existing Observability Foundation

`MicroShop.ServiceDefaults` already configures basic OpenTelemetry logging, metrics, and tracing hooks for services that call `builder.AddServiceDefaults()`.

Lesson 28 does not add a full production observability stack yet.

Not complete yet:

- OTLP collector/export pipeline
- Grafana dashboards
- alerts
- production correlation propagation
- centralized log querying

## Health Endpoints

OrderQueryService:

```text
GET /health
GET /alive
```

`/health` includes the MongoDB read model health check when running in Development through ServiceDefaults.

ApiGateway:

```text
GET /health
```

ApiGateway maps `/health` explicitly because the Docker environment is `ASPNETCORE_ENVIRONMENT=Docker`, while `MapDefaultEndpoints()` only exposes default health endpoints in Development.

## Important APIs

```text
GET /order-summaries
GET /order-summaries/{orderId}
```

These routes are served by OrderQueryService and proxied by ApiGateway.

## Kafka Checks

Describe the order events topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --describe --topic microshop.order-events
```

Check ProjectionWorker lag:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Healthy expectation:

```text
LAG eventually becomes 0.
```

## ProjectionWorker Logs

Success logs should include:

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

Invalid or unsupported messages should be stored in `projection_failures` and committed so a training message does not block a partition forever.

MongoDB infrastructure failures should not commit the Kafka offset.

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

Manual inspection:

```powershell
docker exec -it microshop-mongodb mongosh -u microshop -p microshop --authenticationDatabase admin
```

```javascript
use MicroShop_OrderReadDb
db.order_summaries.find().pretty()
db.projection_failures.find().sort({ createdAtUtc: -1 }).limit(5).pretty()
```

## Current Limits

This is local operational visibility, not full production observability.

Still deferred:

- Kafka retry topics and DLT
- processed-event collection
- projection rebuild command
- production dashboards and alerts
- centralized log search
