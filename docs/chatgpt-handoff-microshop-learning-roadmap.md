# MicroShop ChatGPT Handoff

This handoff explains how this MicroShop project is being built, how lessons should be updated, and what constraints should be preserved when another ChatGPT/Codex session continues the work.

## Project Direction

MicroShop is a learning microservices project, but the implementation should stay production-minded and clean. Do not blindly copy lesson code if it conflicts with the current architecture. Review the current codebase first, then adapt the lesson so it fits the project.

Main principles:

- Keep each service in a clean folder-based architecture inside a single project: `API`, `Application`, `Domain`, `Infrastructure`.
- Keep endpoints thin. Business flow belongs in `Application`; external clients and persistence belong in `Infrastructure`.
- Prefer explicit DTO mapping over leaking persistence/downstream models into API.
- Prefer Dapper/PostgreSQL for write-side services unless a service has a clear reason to use another store.
- Basket uses Redis.
- Order query/read side uses MongoDB.
- RabbitMQ is used for workflow/task messaging such as notifications.
- Kafka is used for event stream/projection/read model learning.
- Do not mix RabbitMQ and Kafka responsibilities unless the lesson explicitly covers bridge/dual-publish.
- Keep Docker local setup lightweight enough to run on a developer machine.
- Aspire is useful for local orchestration, but avoid adding heavy dependencies to Aspire if the current lite flow expects them from Docker.

## Current Architecture Snapshot

### Services

- `CatalogService`
- `BasketService`
- `OrderingService`
- `DiscountService`
- `IdentityService`
- `PaymentService`
- `OrderQueryService`
- `ApiGateway`

### Workers

- `Services/NotificationWorker`
  - Consumes RabbitMQ messages via MassTransit.
  - Handles `OrderCreatedIntegrationEvent`.
  - RabbitMQ remains its transport.

- `Workers/ProjectionWorker`
  - Added in lesson 27.
  - Consumes Kafka topic `microshop.order-events`.
  - Updates MongoDB read model `MicroShop_OrderReadDb.order_summaries`.
  - Stores invalid/unsupported messages in `projection_failures`.

### Building Blocks

- `BuildingBlocks.Contracts`
  - Contains shared integration event contracts.
  - RabbitMQ notification flow already uses shared contracts.
  - Kafka projection lesson currently uses a projection demo envelope, not the OrderingService outbox contract yet.

## Lesson Progress

Latest completed lesson:

```text
Buoi 27: Kafka MongoDB ProjectionWorker
Tag: v27/kafka-mongodb-projection-worker
Commit: a21f300
```

Recent lesson mapping:

- Lesson 23: OrderingService outbox basic + background publisher intro.
- Lesson 24: RabbitMQ vs Kafka decision.
- Lesson 25: Kafka core concepts demo.
- Lesson 26: OrderQueryService + MongoDB read model foundation.
- Lesson 27: Kafka -> ProjectionWorker -> MongoDB projection demo.

## Lesson 27 Final Shape

Lesson 27 should be understood as:

```text
Kafka CLI produces demo order events
-> Workers/ProjectionWorker consumes microshop.order-events
-> ProjectionWorker upserts MongoDB order_summaries
-> OrderQueryService reads /order-summaries
-> ApiGateway proxies /order-summaries
```

Important lesson 27 boundaries:

- Do not modify `OrderingService` outbox to publish Kafka yet.
- Do not replace RabbitMQ.
- Do not move projection logic into `OrderQueryService`.
- Do not use old endpoint examples like `/orders/read-model`.
- Use actual verification endpoints:
  - `GET /order-summaries`
  - `GET /order-summaries/{orderId}`
- Use GUID payloads for `orderId` and `customerId`, not strings like `ORD-900`.
- Kafka key must be `orderId`.
- Supported demo events:
  - `OrderCreated`
  - `OrderPaid`
  - `OrderCancelled`

Production-minded behavior in lesson 27:

- `EnableAutoCommit=false`.
- Commit Kafka offset only after MongoDB apply succeeds.
- Invalid/unsupported messages are stored in `projection_failures`, then committed so a bad training message does not block a partition forever.
- MongoDB failure should not commit the offset.
- Upsert by `OrderId`.
- Store projection metadata:
  - `lastProjectedEventId`
  - `lastProjectedEventType`
  - `lastProjectedEventOccurredAtUtc`
  - `lastProjectedAtUtc`
  - `paidAtUtc`
  - `cancelledAtUtc`

Known limitations of lesson 27:

- No retry topic.
- No Kafka DLT.
- No schema registry.
- No processed-event collection.
- No rebuild projection command.
- No OrderingService Kafka publisher yet.
- No Elasticsearch yet.
- Projection repository currently uses read-modify-replace, which is acceptable for the lesson but should be hardened later with atomic conditional updates and event sequence/version.

## How To Update Future Lessons

When updating a lesson file:

1. Read current implementation first.
2. Rewrite the lesson to match the repo, not the other way around.
3. Keep old lesson ideas only if they still fit the project.
4. Explicitly call out what is in scope and what is deferred.
5. Prefer real paths and commands from this repo.
6. Prefer actual endpoints and ports from this repo.
7. Include verification steps:
   - build command
   - docker compose command
   - Postman route
   - logs/diagnostics command
8. Add production-fit review:
   - what is acceptable for the lesson
   - what must be upgraded later for production
9. Keep lesson text honest. Do not claim a feature is production complete if it is a training-stage implementation.

## Coding Rules For Future Work

Before implementation:

- Run `git status --short`.
- Check recent commit/tag convention.
- Read the related existing service structure.
- Prefer existing patterns over new abstractions.

During implementation:

- Keep changes scoped to the lesson.
- Use `apply_patch` for manual edits.
- Do not revert unrelated user changes.
- Build the touched project first.
- Build the solution after wiring changes.

For commits and tags:

- Commit format currently follows:

```text
Buoi NN: Short Lesson Title
```

Some older commits use Vietnamese accents:

```text
Buoi 27: Kafka MongoDB ProjectionWorker
```

Use whichever style is already dominant in the immediately previous lesson commits. The latest commit used:

```text
Buoi 27: Kafka MongoDB ProjectionWorker
```

- Tag format currently follows:

```text
vNN/short-kebab-title
```

Example:

```text
v27/kafka-mongodb-projection-worker
```

## Runtime Notes

Docker compose currently includes:

- PostgreSQL
- Redis
- RabbitMQ
- Zookeeper
- Kafka
- MongoDB
- app services
- `projectionworker`

Lesson 27 runtime test:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker api-gateway
```

Create topic:

```powershell
docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic microshop.order-events --partitions 3 --replication-factor 1
```

Produce keyed messages:

```powershell
docker exec -it microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic microshop.order-events --property parse.key=true --property key.separator=:
```

Verify offset:

```powershell
docker exec microshop-kafka kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group projection-worker
```

Postman collection:

```text
postman/MicroShop.KafkaProjectionFlow.postman_collection.json
```

## Suggested Next Lessons

Possible lesson 28 direction:

```text
Logging, health checks, operational visibility, and runbook/debug checklist.
```

Recommended production-hardening lessons after that:

- Kafka retry topic and DLT.
- Projection processed-event collection.
- Projection rebuild command.
- OrderingService outbox publisher to Kafka.
- Contract/schema versioning for Kafka events.
- Metrics for lag, failed projections, and consumer health.
- Atomic Mongo projection updates with event sequence/version.
- Optional Elasticsearch lesson later for search/read analytics, not as a replacement for Mongo read model unless the lesson explicitly changes the read strategy.
