# Stage 2 Production Hardening Backlog

This backlog starts after Day 30.

It tracks production-minded improvements that should be implemented as separate focused slices.

## Event-Driven Reliability

```text
[ ] Kafka retry topic and DLT.
[ ] Projection processed-event collection.
[x] Projection rebuild mode.
[ ] OrderingService outbox publisher to Kafka.
[ ] Contract/schema versioning for Kafka events.
[ ] Atomic Mongo projection updates with event sequence/version.
[x] Basic idempotent payment webhook/event handling.
[ ] Idempotent consumers for every event handler.
```

## Observability

```text
[x] Correlation ID propagation across gateway, services, outbox, and workers.
[x] OpenTelemetry export pipeline outside Aspire local defaults.
[x] Metrics for Kafka lag.
[x] Metrics for failed projections.
[x] Metrics for consumer health.
[ ] RabbitMQ queue depth/error queue monitoring.
[x] Grafana dashboard.
[ ] Alerting intro.
```

## API And Architecture

```text
[ ] Standard error response across services.
[ ] API versioning policy.
[ ] Swagger/OpenAPI enablement for Development if still not enabled.
[ ] OpenAPI auth documentation.
[x] Gateway route review started.
[ ] Internal service contract review.
```

## Data And Persistence

```text
[ ] PostgreSQL migration review.
[ ] Backup/restore mindset.
[ ] Read model rebuild strategy.
[ ] Database index review.
[ ] Connection pool and timeout review.
[ ] Seed data policy for demos.
```

## Security

```text
[ ] JWT/Identity review.
[ ] SSO/OIDC decision note.
[ ] Internal service security.
[ ] Audit log policy.
[x] Local-prod secrets moved out of committed compose file.
[x] Payment webhook HMAC verification.
[x] Gateway edge rate-limit/CORS/security-header baseline.
```

## Testing

```text
[x] Unit tests for critical handlers.
[x] Integration tests with Testcontainers.
[ ] Contract tests for integration events.
[x] Failure/replay scenario tests for ProjectionWorker.
[ ] Gateway route tests.
[x] Smoke test script for Docker Compose.
[x] Production failure drill Postman collection.
```

## Delivery

```text
[ ] CI build workflow.
[ ] CI test workflow.
[x] Docker image tagging strategy.
[ ] Release tag policy.
[x] Environment-specific deployment notes.
[x] Day 50 production failure drill runbook.
[x] Local-prod Docker Compose runtime.
[x] Local-prod container health checks.
[x] Dependency readiness checks for local-prod health endpoints.
[x] Local-prod reverse proxy edge.
[x] Local-prod stop grace periods.
```
