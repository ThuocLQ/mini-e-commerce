# .NET Aspire Decision

## Why Aspire?

We use .NET Aspire to improve local distributed application development.
It helps run projects, containers and dependencies from one AppHost and provides a dashboard for resources, logs and endpoints.

## Current AppHost Resources

- ApiGateway
- CatalogService
- BasketService
- Redis
- RabbitMQ

## Why not replace Docker Compose completely?

Docker Compose is still useful for understanding and running infrastructure containers.
Aspire improves local orchestration and developer experience for .NET distributed apps.

## Is Aspire a production runtime?

No. Aspire AppHost is mainly used for development-time orchestration.
Production deployment will be handled later with Dockerfile, Kubernetes, Helm and CI/CD.

## Current Rule

- Docker Compose: good for infrastructure local baseline.
- Aspire: good for running and observing the distributed .NET app locally.
- Redis and RabbitMQ are managed from AppHost when running the Aspire workflow.
