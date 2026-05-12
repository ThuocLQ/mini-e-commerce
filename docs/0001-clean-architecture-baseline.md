# ADR 0001: Clean Architecture Baseline

## Status

Accepted

## Context

CatalogService is growing beyond simple endpoints. Program.cs and endpoint handlers can become too large if HTTP concerns, business logic, database access and mapping are mixed together.

MicroShop will later include IdentityService, OrderingService, DiscountService, PaymentService, RabbitMQ, Outbox, Saga, Kafka and MongoDB Projection. We need a simple architecture baseline before adding more services.

## Decision

We will organize CatalogService using a folder-based Clean Architecture baseline:

- API: HTTP endpoints and request/response handling.
- Application: use cases, commands, queries, handlers and repository contracts.
- Domain: product model and core business rules.
- Infrastructure: Dapper repository and database access.

For now, all folders remain inside the same CatalogService project to keep the learning curve reasonable.

## Consequences

- Program.cs becomes smaller.
- Endpoint code becomes thinner.
- Handlers depend on abstractions instead of concrete persistence.
- SQL and Dapper stay in Infrastructure.
- Domain does not depend on Infrastructure.
- The service can later be upgraded to project-based layering when it becomes larger.