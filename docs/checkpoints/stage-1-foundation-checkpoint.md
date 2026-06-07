# Stage 1 Foundation Checkpoint

## Status

```text
Checkpoint date: 2026-06-07
Current day: Day 30
Stage: Stage 1 Foundation closed, Stage 2 Production Hardening next
```

## What Works

```text
[x] ApiGateway builds.
[x] OrderQueryService builds.
[x] ProjectionWorker builds.
[x] Full solution builds.
[x] Kafka projection flow is documented with valid GUID payloads.
[x] OrderQueryService exposes /order-summaries.
[x] ProjectionWorker supports OrderCreated, OrderPaid, and OrderCancelled.
[x] Invalid projection messages are stored in projection_failures and offset can be committed.
[x] Lite runtime command is separated from full runtime command.
```

Runtime demo status:

```text
[x] Lite projection demo started.
[x] Kafka topic microshop.order-events verified.
[x] Valid OrderCreated event consumed.
[x] Valid OrderPaid event consumed.
[x] MongoDB order_summaries updated.
[x] GET /order-summaries/{orderId} returned expected read model.
[x] Kafka lag checked.
[x] Invalid message verified in projection_failures.
[x] Optional gateway route verified because api-gateway was already running.
```

## Verified Demo Flow

```text
Kafka CLI
-> microshop.order-events
-> ProjectionWorker
-> MongoDB MicroShop_OrderReadDb.order_summaries
-> OrderQueryService
-> /order-summaries/{orderId}
```

Gateway verification is optional in lite mode:

```text
ApiGateway is verified in full system mode or explicit optional gateway mode.
```

## Build Results

```text
ApiGateway: passed
OrderQueryService: passed
ProjectionWorker: passed
Full solution: passed
```

Commands:

```powershell
dotnet build Services/ApiGateway/ApiGateway.csproj --no-restore --nologo -v minimal
dotnet build Services/OrderQueryService/OrderQueryService.csproj --no-restore --nologo -v minimal
dotnet build Workers/ProjectionWorker/ProjectionWorker.csproj --no-restore --nologo -v minimal
dotnet build MicroShop.sln --no-restore --nologo -v minimal
```

## Runtime Results

Runtime verification should use true lite mode first:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Then verify:

```text
OrderQueryService health: Healthy
ApiGateway health: Healthy, optional because api-gateway was already running
Kafka topic: microshop.order-events, 3 partitions, replication factor 1
ProjectionWorker logs: OrderCreated and OrderPaid applied; invalid payload stored as failure
Order summary API: status Paid, lastProjectedEventType OrderPaid
Kafka lag: 0 for active partitions after processing
projection_failures: latest invalid payload stored with error "EventId is required."
```

Demo order id:

```text
11111111-1111-1111-1111-111111111111
```

## Known Limitations

```text
No Kafka retry topic/DLT yet.
No processed-event collection yet.
No projection rebuild command yet.
No OrderingService Kafka publisher yet.
No schema registry yet.
No production observability stack yet.
No full CI/CD/deployment strategy yet.
```

## Stage 2 Readiness

```text
Ready to start production hardening after Day 30.
```

Recommended first hardening slices:

```text
Standard error response and API versioning.
Transactional outbox standardization.
Kafka retry topic/DLT.
Projection rebuild and processed-event tracking.
Correlation ID and OpenTelemetry export.
Integration tests with Testcontainers.
```
