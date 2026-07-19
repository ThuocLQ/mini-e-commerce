# Stage 2 Production Hardening Backlog

This backlog starts after Day 30.

It tracks production-minded improvements that should be implemented as separate focused slices.

## Event-Driven Reliability

```text
[x] Kafka retry topic and DLT.
[x] Projection processed-event collection.
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
[x] Local-prod observability smoke script.
[x] Alerting intro.
```

## API And Architecture

```text
[ ] Standard error response across services.
[ ] API versioning policy.
[ ] Swagger/OpenAPI enablement for Development if still not enabled.
[ ] OpenAPI auth documentation.
[x] Development OpenAPI JSON endpoint baseline.
[x] Gateway route review started.
[ ] Internal service contract review.
[x] Production configuration fail-fast baseline.
[x] Public gateway internal route guard baseline.
[x] Gateway JWT validation baseline for K3s protected routes.
```

## Data And Persistence

```text
[ ] PostgreSQL migration review.
[x] Local-prod PostgreSQL backup/restore scripts.
[x] Local-prod MongoDB backup/restore scripts.
[x] K3s PostgreSQL/MongoDB backup and restore scripts.
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
[x] CI build workflow.
[x] CI test workflow.
[x] CI local-prod compose validation.
[x] CI representative Docker image build.
[x] GHCR image build/push workflow.
[x] K3s Helm chart validation in CI.
[x] Docker image tagging strategy.
[ ] Release tag policy.
[x] Environment-specific deployment notes.
[x] Day 50 production failure drill runbook.
[x] Local-prod Docker Compose runtime.
[x] Local-prod container health checks.
[x] Dependency readiness checks for local-prod health endpoints.
[x] Local-prod reverse proxy edge.
[x] Local-prod stop grace periods.
[x] Local-prod one-command startup script.
[x] Local-prod gateway smoke script.
[x] Local-prod observability startup script.
[x] Local-prod release candidate verification script.
[x] K3s single-node deployment baseline.
[x] K3s secret creation script.
[x] K3s smoke script.
[x] K3s backup script.
[x] K3s restore script with application quiescing.
[x] K3s local validation script.
[x] K3s cert-manager install script.
[x] K3s Ingress TLS issuer annotation baseline.
[x] K3s observability deployment baseline.
[x] K3s observability smoke script.
[x] K3s manual deploy workflow.
[x] K3s deploy script with smoke and rollback.
[x] GHCR image pull secret helper script.
```
