# Day 31 Architecture Hardening Backlog

## P1 - Important Soon

```text
[ ] Day 32: standardize API error response shape for a small slice first.
[ ] Keep API endpoints thin where they currently contain repeated error response logic.
[ ] Ensure Application layer does not return `IResult` or depend on `HttpContext`.
[ ] Keep Infrastructure exceptions from leaking into API response decisions where practical.
```

## P2 - Architecture Cleanup

```text
[ ] Review repository abstractions service by service.
[ ] Review mapping between request DTO, application command/query, domain model, and response DTO.
[ ] Review debug endpoints and keep them Development/local only.
[ ] Review service-specific dependency registration naming and consistency.
[ ] Split large endpoint files only when it improves clarity.
```

## OrderQueryService

```text
[ ] Replace direct API dependency on `MongoException` with a more stable error mapping strategy.
[ ] Keep Mongo document models isolated in Infrastructure.
[ ] Keep `/debug/order-summaries` clearly scoped to Development.
```

## ProjectionWorker

```text
[ ] Add Kafka retry topic/DLT.
[ ] Add processed-event collection.
[ ] Add projection rebuild command.
[ ] Add event schema/versioning.
[ ] Add event sequence/version support for safer out-of-order handling.
[ ] Consider splitting Kafka message decoding, failure policy, and commit policy if the worker grows.
```

## OrderingService

```text
[ ] Keep RabbitMQ outbox publisher in Infrastructure.
[ ] Review checkout use case for saga-style hardening later.
[ ] Add OrderingService outbox publisher to Kafka only if roadmap chooses dual transport/projection publishing.
```

## BasketService

```text
[ ] Reduce repeated endpoint error response logic during Day 32 error hardening.
[ ] Keep Catalog REST/gRPC clients behind Application-facing abstractions.
[ ] Keep Redis details isolated in Infrastructure.
```

## API Hardening Next

```text
[ ] Day 32: API Versioning + Backward Compatibility + Standard Error Format.
[ ] Standard ProblemDetails-style error responses.
[ ] API versioning policy.
[ ] Gateway route governance.
[ ] Negative Postman tests for error responses.
```

## What Not To Do Yet

```text
[ ] Do not rewrite all services.
[ ] Do not move large code blocks just to satisfy folder purity.
[ ] Do not add a shared framework before one or two services prove the pattern.
[ ] Do not change RabbitMQ/Kafka responsibilities during architecture cleanup.
```
