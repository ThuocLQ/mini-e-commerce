# Docker Compose Decision

## Why Docker Compose?

We use Docker Compose to run local infrastructure consistently across machines.

## Current Compose Services

- Redis: used by BasketService for temporary basket storage.
- RabbitMQ: prepared for event-driven workflows in later lessons.

## Current Development Mode

At this stage, .NET services are still run by Rider.
Infrastructure services are run by Docker Compose.

## Connection Rules

- App running on host/Rider → use localhost:port.
- App running inside Docker Compose → use service-name:port.

## Future Plan

Later, .NET services may also be containerized with Dockerfiles.
Aspire will also be introduced to improve local distributed app orchestration.