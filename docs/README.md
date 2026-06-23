# MicroShop Docs Map

This page is the entrypoint for MicroShop documentation.

Use it when you are new to the codebase and do not know which document to read first.

## Start Here

Read these in order:

| Step | Document | Why read it |
| --- | --- | --- |
| 1 | [../README.md](../README.md) | Quick project overview, services, workers, infrastructure, and main routes |
| 2 | [architecture-diagram.md](architecture-diagram.md) | Visual map of services, databases, brokers, and workers |
| 3 | [communication-decisions.md](communication-decisions.md) | Explains why REST, gRPC, RabbitMQ, and Kafka are all used |
| 4 | [api-surface-review.md](api-surface-review.md) | Lists current API routes and Swagger/OpenAPI status |
| 5 | [demo-script-foundation.md](demo-script-foundation.md) | Script for running the foundation demo |
| 6 | [checkpoints/stage-1-foundation-checkpoint.md](checkpoints/stage-1-foundation-checkpoint.md) | Stage 1 checkpoint status |
| 7 | [architecture/clean-architecture-review-day-31.md](architecture/clean-architecture-review-day-31.md) | Stage 2 architecture baseline review |
| 8 | [api/api-versioning-policy.md](api/api-versioning-policy.md) | API compatibility and versioning policy |
| 9 | [api/api-error-handling-standard.md](api/api-error-handling-standard.md) | Standard API error response direction |
| 10 | [database/migration-policy.md](database/migration-policy.md) | Database migration and schema evolution policy |

## Read By Goal

| Goal | Read |
| --- | --- |
| Understand the whole system | [../README.md](../README.md), [architecture-diagram.md](architecture-diagram.md) |
| Write or review future lessons | [lesson-authoring-standard.md](lesson-authoring-standard.md) |
| Understand service communication | [communication-decisions.md](communication-decisions.md), [adr/ADR-001-service-communication.md](adr/ADR-001-service-communication.md) |
| Understand RabbitMQ vs Kafka | [communication-decisions.md](communication-decisions.md), [adr/ADR-002-rabbitmq-vs-kafka.md](adr/ADR-002-rabbitmq-vs-kafka.md) |
| Understand order read model projection | [adr/ADR-003-order-read-model-projection.md](adr/ADR-003-order-read-model-projection.md), [mongodb-read-model-notes.md](mongodb-read-model-notes.md) |
| Run a local demo | [demo-script-foundation.md](demo-script-foundation.md) |
| Review Stage 1 checkpoint | [checkpoints/stage-1-foundation-checkpoint.md](checkpoints/stage-1-foundation-checkpoint.md) |
| Plan production hardening | [backlog/stage-2-production-hardening-backlog.md](backlog/stage-2-production-hardening-backlog.md) |
| Review Clean Architecture boundaries | [architecture/clean-architecture-review-day-31.md](architecture/clean-architecture-review-day-31.md), [architecture/service-boundary-review.md](architecture/service-boundary-review.md) |
| Debug local services | [runbooks/microshop-local-debug-runbook.md](runbooks/microshop-local-debug-runbook.md), [operational-visibility.md](operational-visibility.md) |
| Run production failure drills | [runbooks/production-failure-drills.md](runbooks/production-failure-drills.md) |
| Review API routes | [api-surface-review.md](api-surface-review.md), [api/api-versioning-policy.md](api/api-versioning-policy.md), [api/api-error-handling-standard.md](api/api-error-handling-standard.md), [api/validation-and-mapping-standard.md](api/validation-and-mapping-standard.md) |
| Review database migration policy | [database/migration-policy.md](database/migration-policy.md), [database/postgresql-schema-evolution-review-day-33.md](database/postgresql-schema-evolution-review-day-33.md) |
| Review query and strategy patterns | [patterns/specification-pattern.md](patterns/specification-pattern.md), [patterns/day-35-specification-review.md](patterns/day-35-specification-review.md), [patterns/strategy-pattern-review-day-36.md](patterns/strategy-pattern-review-day-36.md) |
| Review security and identity hardening | [security/audit-log-policy.md](security/audit-log-policy.md), [security/identity-hardening-review-day-36.md](security/identity-hardening-review-day-36.md), [security/sso-oidc-decision-note.md](security/sso-oidc-decision-note.md) |
| Review messaging reliability | [messaging/event-envelope-standard.md](messaging/event-envelope-standard.md), [messaging/cloudevents-review-day-37.md](messaging/cloudevents-review-day-37.md), [messaging/transactional-outbox-standard.md](messaging/transactional-outbox-standard.md), [messaging/outbox-transaction-review-day-38.md](messaging/outbox-transaction-review-day-38.md) |
| Review architecture decisions | [adr/README.md](adr/README.md) |
| Continue the roadmap with ChatGPT/Codex | [chatgpt-handoff-microshop-learning-roadmap.md](chatgpt-handoff-microshop-learning-roadmap.md) |

## Documentation Types

| Type | Purpose | Files |
| --- | --- | --- |
| Overview | Fast onboarding | [../README.md](../README.md), [architecture-diagram.md](architecture-diagram.md) |
| Decisions | Why the project is designed this way | [adr/README.md](adr/README.md), [communication-decisions.md](communication-decisions.md) |
| Architecture | Service boundaries and layer reviews | [architecture/clean-architecture-review-day-31.md](architecture/clean-architecture-review-day-31.md), [architecture/service-boundary-review.md](architecture/service-boundary-review.md) |
| Operations | How to run and debug locally | [demo-script-foundation.md](demo-script-foundation.md), [operational-visibility.md](operational-visibility.md), [runbooks/microshop-local-debug-runbook.md](runbooks/microshop-local-debug-runbook.md), [runbooks/production-failure-drills.md](runbooks/production-failure-drills.md) |
| API | What routes exist | [api-surface-review.md](api-surface-review.md) |
| API Standards | API compatibility, errors, validation, and mapping | [api/api-versioning-policy.md](api/api-versioning-policy.md), [api/api-error-handling-standard.md](api/api-error-handling-standard.md), [api/validation-and-mapping-standard.md](api/validation-and-mapping-standard.md) |
| Database | Database ownership and migration policy | [database/migration-policy.md](database/migration-policy.md), [database/postgresql-schema-evolution-review-day-33.md](database/postgresql-schema-evolution-review-day-33.md) |
| Patterns | Query and design pattern notes | [patterns/specification-pattern.md](patterns/specification-pattern.md), [patterns/day-35-specification-review.md](patterns/day-35-specification-review.md), [patterns/strategy-pattern-review-day-36.md](patterns/strategy-pattern-review-day-36.md) |
| Security | Identity, audit, and SSO/OIDC hardening notes | [security/audit-log-policy.md](security/audit-log-policy.md), [security/identity-hardening-review-day-36.md](security/identity-hardening-review-day-36.md), [security/sso-oidc-decision-note.md](security/sso-oidc-decision-note.md) |
| Messaging | Event envelope and outbox reliability docs | [messaging/event-envelope-standard.md](messaging/event-envelope-standard.md), [messaging/cloudevents-review-day-37.md](messaging/cloudevents-review-day-37.md), [messaging/transactional-outbox-standard.md](messaging/transactional-outbox-standard.md), [messaging/outbox-transaction-review-day-38.md](messaging/outbox-transaction-review-day-38.md) |
| Checkpoints | Stage checkpoint reports | [checkpoints/stage-1-foundation-checkpoint.md](checkpoints/stage-1-foundation-checkpoint.md) |
| Backlog | Production hardening roadmap | [backlog/stage-2-production-hardening-backlog.md](backlog/stage-2-production-hardening-backlog.md) |
| Notes | Topic-specific learning notes | [mongodb-read-model-notes.md](mongodb-read-model-notes.md), [aspire-decision.md](aspire-decision.md), [docker-compose-decision.md](docker-compose-decision.md), [config-secrets-decision.md](config-secrets-decision.md) |
| Authoring | Rules for future lessons and plans | [lesson-authoring-standard.md](lesson-authoring-standard.md) |

## File Guide

| Document | Meaning |
| --- | --- |
| [architecture-diagram.md](architecture-diagram.md) | Current service and infrastructure diagram |
| [architecture/clean-architecture-review-day-31.md](architecture/clean-architecture-review-day-31.md) | Day 31 Clean Architecture and Hexagonal review |
| [architecture/service-boundary-review.md](architecture/service-boundary-review.md) | Service ownership and boundary map |
| [communication-decisions.md](communication-decisions.md) | REST, gRPC, RabbitMQ, Kafka, and outbox usage |
| [api-surface-review.md](api-surface-review.md) | Current API surface and OpenAPI/Swagger status |
| [api/api-versioning-policy.md](api/api-versioning-policy.md) | Day 32 API versioning and compatibility policy |
| [api/api-error-handling-standard.md](api/api-error-handling-standard.md) | Day 32 ProblemDetails-style error standard |
| [api/day-32-api-hardening-notes.md](api/day-32-api-hardening-notes.md) | Day 32 OrderQueryService API hardening notes |
| [api/validation-and-mapping-standard.md](api/validation-and-mapping-standard.md) | Day 34 validation and mapping direction |
| [api/day-34-validation-mapping-notes.md](api/day-34-validation-mapping-notes.md) | Day 34 OrderQueryService validation slice notes |
| [database/migration-policy.md](database/migration-policy.md) | Day 33 schema migration policy |
| [database/postgresql-schema-evolution-review-day-33.md](database/postgresql-schema-evolution-review-day-33.md) | Day 33 PostgreSQL schema evolution review |
| [patterns/specification-pattern.md](patterns/specification-pattern.md) | Day 35 Specification Lite / query criteria pattern |
| [patterns/day-35-specification-review.md](patterns/day-35-specification-review.md) | Day 35 CatalogService query criteria review |
| [patterns/strategy-pattern-review-day-36.md](patterns/strategy-pattern-review-day-36.md) | Day 36 strategy pattern candidate review |
| [security/audit-log-policy.md](security/audit-log-policy.md) | Day 36 audit log policy |
| [security/identity-hardening-review-day-36.md](security/identity-hardening-review-day-36.md) | Day 36 IdentityService hardening review |
| [security/sso-oidc-decision-note.md](security/sso-oidc-decision-note.md) | Day 36 SSO/OIDC direction note |
| [messaging/event-envelope-standard.md](messaging/event-envelope-standard.md) | Day 37 event envelope standard |
| [messaging/cloudevents-review-day-37.md](messaging/cloudevents-review-day-37.md) | Day 37 CloudEvents review |
| [messaging/transactional-outbox-standard.md](messaging/transactional-outbox-standard.md) | Day 38 transactional outbox standard |
| [messaging/outbox-transaction-review-day-38.md](messaging/outbox-transaction-review-day-38.md) | Day 38 OrderingService outbox transaction review |
| [demo-script-foundation.md](demo-script-foundation.md) | Step-by-step demo for the foundation stage |
| [checkpoints/stage-1-foundation-checkpoint.md](checkpoints/stage-1-foundation-checkpoint.md) | Stage 1 checkpoint report |
| [backlog/stage-2-production-hardening-backlog.md](backlog/stage-2-production-hardening-backlog.md) | Stage 2 production hardening backlog |
| [backlog/day-31-architecture-hardening-backlog.md](backlog/day-31-architecture-hardening-backlog.md) | Day 31 architecture hardening backlog |
| [backlog/day-33-database-hardening-backlog.md](backlog/day-33-database-hardening-backlog.md) | Day 33 database hardening backlog |
| [backlog/day-35-query-hardening-backlog.md](backlog/day-35-query-hardening-backlog.md) | Day 35 query hardening backlog |
| [backlog/day-36-audit-identity-hardening-backlog.md](backlog/day-36-audit-identity-hardening-backlog.md) | Day 36 audit and identity hardening backlog |
| [backlog/day-37-event-envelope-backlog.md](backlog/day-37-event-envelope-backlog.md) | Day 37 event envelope backlog |
| [backlog/day-38-transactional-outbox-backlog.md](backlog/day-38-transactional-outbox-backlog.md) | Day 38 transactional outbox backlog |
| [lesson-authoring-standard.md](lesson-authoring-standard.md) | Mandatory context/rules for future ChatGPT/Codex lessons |
| [operational-visibility.md](operational-visibility.md) | Health checks, logs, and local visibility notes |
| [runbooks/microshop-local-debug-runbook.md](runbooks/microshop-local-debug-runbook.md) | Practical debug checklist |
| [runbooks/production-failure-drills.md](runbooks/production-failure-drills.md) | Production failure drill checklist for senior demo |
| [adr/README.md](adr/README.md) | ADR index |
| [0001-clean-architecture-baseline.md](0001-clean-architecture-baseline.md) | Clean Architecture baseline decision |
| [aspire-decision.md](aspire-decision.md) | Why Aspire is used locally |
| [docker-compose-decision.md](docker-compose-decision.md) | Why Docker Compose is used locally |
| [config-secrets-decision.md](config-secrets-decision.md) | Configuration and local secrets decision |
| [mongodb-read-model-notes.md](mongodb-read-model-notes.md) | MongoDB read model notes |
| [chatgpt-handoff-microshop-learning-roadmap.md](chatgpt-handoff-microshop-learning-roadmap.md) | Handoff and roadmap context for future AI-assisted work |

## Suggested Reading Path For A New Developer

```text
README.md
-> docs/README.md
-> docs/architecture-diagram.md
-> docs/communication-decisions.md
-> docs/api-surface-review.md
-> docs/demo-script-foundation.md
```

After that, read ADRs only when you need to understand why a decision was made.
