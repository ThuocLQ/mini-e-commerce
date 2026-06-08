# Day 31 Clean Architecture Review

## Goal

Review MicroShop architecture boundaries after the Stage 1 foundation checkpoint.

This review is intentionally documentation-first. It does not rewrite services or change runtime behavior.

## Current Stage

```text
Stage 2 starts here.
This is a production-minded architecture review, not a full rewrite.
```

## Layer Rules

```text
API -> Application -> Domain
Infrastructure -> Application -> Domain
Domain should not depend on Infrastructure/API/frameworks.
Application should define ports where possible.
Infrastructure should implement adapters.
```

Hexagonal view:

```text
Inbound adapters:
    Minimal API endpoints
    gRPC service endpoints
    Kafka worker loop
    RabbitMQ consumers

Application core:
    commands, queries, handlers, ports

Outbound adapters:
    PostgreSQL/Dapper repositories
    Redis repository
    MongoDB read model repositories
    RabbitMQ publisher
    Catalog REST/gRPC clients
```

## Reviewed Areas

```text
OrderQueryService
ProjectionWorker
OrderingService
BasketService
CatalogService
IdentityService
PaymentService
NotificationWorker
ApiGateway
```

## Findings

| Area | Finding | Severity | Action |
| --- | --- | --- | --- |
| Overall services | Most business services use `API`, `Application`, `Domain`, and `Infrastructure` folders. This is a solid Stage 1 baseline. | P2 | Keep this folder contract for new services. |
| OrderQueryService | API exception handling references `MongoException` directly in `API/DependencyInjection.cs`. This is acceptable for the current small read model, but it leaks an infrastructure exception into the API layer. | P2 | Day 32 can normalize error handling; later consider an application-level exception or standardized problem mapping. |
| OrderQueryService | Read model repository abstraction lives in Application and MongoDB implementation is in Infrastructure. Mongo document mapping is separated from read model DTOs. | Keep | Preserve this direction. |
| OrderQueryService | Debug upsert endpoint exists under `/debug/order-summaries` and writes through repository directly. It is scoped to Development in endpoint registration. | P3 | Keep marked as debug/local; avoid using it as a production ingestion path. |
| ProjectionWorker | Kafka consumer is correctly placed in Infrastructure as an inbound adapter. Projection application logic is in `Application/Projections`. | Keep | Preserve separation. |
| ProjectionWorker | `KafkaProjectionWorker` currently owns consume loop, deserialization, key validation, invalid-message failure storage, offset commit, and logging. This is acceptable for training-stage code but will grow quickly. | P2 | Later split message decoding/failure policy/commit policy into smaller adapter services if complexity grows. |
| ProjectionWorker | MongoDB projection repository and failure store are Infrastructure adapters. Application ports are clean. | Keep | Preserve direction. |
| OrderingService | Checkout endpoint is thin and delegates to `CheckoutHandler`. Checkout use case coordinates basket, domain order creation, unit of work, and outbox. | Keep | Preserve use-case orchestration in Application. |
| OrderingService | Outbox publisher is Infrastructure and uses MassTransit there. This keeps broker detail out of Application. | Keep | Preserve RabbitMQ publisher as an adapter. |
| OrderingService | Checkout handler depends on `IOptions<OrderEventOptions>`. This is a small configuration dependency in Application. | P3 | Accept for now; if options grow, map config into a simple application settings object. |
| BasketService | API endpoints delegate to MediatR and use Application commands/queries. Redis and Catalog REST/gRPC clients are in Infrastructure. | Keep | Preserve current direction. |
| BasketService | `BasketEndpoints.cs` is relatively large and handles several error shapes and helper methods. It is not a layer violation, but the API adapter is getting dense. | P2 | Day 32 error standardization can reduce repeated error response logic. |
| CatalogService | Product endpoints are mostly thin, but inline validation and comments make endpoint file more procedural. Persistence is correctly isolated. | P3 | Keep for now; future cleanup can move repeated validation response patterns into local helpers or endpoint filters. |
| IdentityService | Authentication concerns are scoped to IdentityService. Dapper/PostgreSQL details remain Infrastructure. | Keep | Avoid treating this as production identity yet; continue hardening later. |
| PaymentService | Payment/webhook endpoints delegate to Application commands. Dapper/PostgreSQL details remain Infrastructure. | Keep | Future work should standardize webhook validation/security. |
| NotificationWorker | RabbitMQ/MassTransit consumer is Infrastructure and uses shared contracts. | Keep | Preserve RabbitMQ as workflow/task messaging. |
| ApiGateway | Gateway contains routing/proxy only and no business workflow. | Keep | Preserve as routing boundary. |

## Good Patterns Observed

```text
RabbitMQ and Kafka responsibilities are separated.
ProjectionWorker is a separate worker.
OrderQueryService owns read model query API.
MongoDB read model is separate from write-side persistence.
OrderingService uses outbox basics for RabbitMQ workflow.
Repositories are generally implemented in Infrastructure behind Application abstractions.
Domain entities do not show direct dependencies on database/broker/framework types.
```

## Dependency Direction Notes

| Dependency | Location | Review |
| --- | --- | --- |
| `Dapper`, `Npgsql` | Infrastructure persistence in write-side services | Correct adapter placement. |
| `StackExchange.Redis` | BasketService Infrastructure | Correct adapter placement. |
| `MongoDB.Driver` | OrderQueryService Infrastructure, ProjectionWorker Infrastructure | Correct adapter placement. |
| `MongoDB.Driver.MongoException` | OrderQueryService API exception handler | Acceptable short-term, but a boundary smell. |
| `Confluent.Kafka` | ProjectionWorker Infrastructure/Kafka | Correct inbound adapter placement. |
| `MassTransit` | OrderingService Infrastructure, NotificationWorker Infrastructure | Correct broker adapter placement. |
| `Results`, `IResult` | API endpoint files | Correct for Minimal API adapters. |

## Hardening Candidates

```text
Day 32:
    API versioning, route governance, and standard error format.

Event-driven hardening:
    Kafka retry topic/DLT.
    Projection processed-event collection.
    Projection rebuild command.
    Event schema/versioning.
    OrderingService Kafka publisher only if roadmap chooses it.

Architecture cleanup:
    Keep debug endpoints clearly marked.
    Reduce repeated endpoint error response logic.
    Keep Application free of HTTP-specific types.
    Keep Infrastructure exceptions from leaking into API response decisions where practical.
```

## What This Review Proves

```text
MicroShop has a reviewed Stage 2 architecture baseline.
The main service boundaries are understandable.
The biggest near-term architecture risks are visible and documented.
```

## What This Review Does Not Prove

```text
The architecture is fully production-ready.
All services are fully Clean Architecture compliant.
All hardening work is complete.
All APIs have standardized error responses.
```
