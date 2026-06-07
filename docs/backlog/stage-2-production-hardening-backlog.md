# Stage 2 Production Hardening Backlog

This backlog starts after Day 30.

It tracks production-minded improvements that should be implemented as separate focused slices.

## Event-Driven Reliability

```text
[ ] Kafka retry topic and DLT.
[ ] Projection processed-event collection.
[ ] Projection rebuild command.
[ ] OrderingService outbox publisher to Kafka.
[ ] Contract/schema versioning for Kafka events.
[ ] Atomic Mongo projection updates with event sequence/version.
[ ] Idempotent consumers for every event handler.
```

## Observability

```text
[ ] Correlation ID propagation across gateway, services, and workers.
[ ] OpenTelemetry export pipeline outside Aspire local defaults.
[ ] Metrics for Kafka lag.
[ ] Metrics for failed projections.
[ ] Metrics for consumer health.
[ ] RabbitMQ queue depth/error queue monitoring.
[ ] Grafana dashboard.
[ ] Alerting intro.
```

## API And Architecture

```text
[ ] Standard error response across services.
[ ] API versioning policy.
[ ] Swagger/OpenAPI enablement for Development if still not enabled.
[ ] OpenAPI auth documentation.
[ ] Gateway route review.
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
[ ] Secrets management beyond local development.
```

## Testing

```text
[ ] Unit tests for handlers.
[ ] Integration tests with Testcontainers.
[ ] Contract tests for integration events.
[ ] Failure scenario tests for ProjectionWorker.
[ ] Gateway route tests.
[ ] Smoke test script for Docker Compose.
```

## Delivery

```text
[ ] CI build workflow.
[ ] CI test workflow.
[ ] Docker image tagging strategy.
[ ] Release tag policy.
[ ] Environment-specific deployment notes.
```
