# Repository Layout And Ownership

This document is the physical-layout contract for MicroShop. It complements the service-boundary and product-design documents: a folder is organized by deployment and ownership, not by a technology preference.

## Root Layout

```text
BuildingBlocks.Contracts/  Versioned cross-service contracts only.
Services/                  Deployable business APIs; .NET and Java are both valid.
Workers/                   Deployable asynchronous consumers, schedulers and projections.
Frontend/                  Customer and operations applications plus browser E2E tests.
Tests/                     Cross-service, integration and load tests.
data/                      Versioned, non-secret seed/import data.
deploy/                    Kubernetes/Helm and deployment manifests.
docker/                    Docker runtime configuration and observability assets.
scripts/                   Repeatable developer, QA and operational commands.
postman/                   Importable API collections and environments.
docs/                      Maintained product, architecture, operations and QA knowledge.
GiaoAn/                    Learning material; never a runtime dependency.
```

Generated outputs, local databases, IDE state, Docker volumes, secrets and test reports must not be committed. They are covered by `.gitignore`; any already tracked artifact must be removed from Git.

## Deployable Units

`Services/<Name>Service` owns an externally callable bounded context. A .NET service uses the following shape when its domain has meaningful business behavior:

```text
API/             Transport contracts and endpoint composition.
Application/     Use cases and ports (abstractions).
Domain/          Business entities, value objects and invariants.
Infrastructure/  Persistence, messaging and external adapters.
```

`ApiGateway` is deliberately an exception: it owns edge routing and security, not application use cases. `OrderQueryService` is a read-model API and may remain without a `Domain` folder while it has no domain behavior. A Java service keeps the conventional Maven `src/main` and `src/test` layout inside its own service folder; language differences must not change bounded-context ownership.

`Workers/<Name>Worker` owns an asynchronous processing capability. A worker should use `Application/` and `Infrastructure/`; add `Domain/` only when it owns business rules rather than merely delivering or projecting events. New workers must be created under `Workers/`. `NotificationWorker` and `ProjectionWorker` are both located under `Workers/`.

## Shared Code Rules

`BuildingBlocks.Contracts` contains stable integration contracts and event envelopes only. It must not reference a business service. Do not create a catch-all `Common`, `Shared`, or `Utilities` project: shared code needs an explicit owner and a narrow purpose. Service-internal DTOs, persistence models and clients stay with their owning service.

## Tests, QA And Evidence

Unit tests live beside the owning project only when a dedicated test project is introduced. Cross-service and Testcontainers tests belong in `Tests/MicroShop.IntegrationTests`; k6 checks belong in `Tests/k6`; browser E2E belongs in `Frontend/e2e`.

Manual test cases use a versioned CSV import source under `docs/qa/test-cases`, then execute in Excel or a test-management system. Test plans and reports are Markdown or DOCX under `docs/qa`. Screenshots and runtime evidence belong in `docs/qa/evidence` and are linked by test-case ID; they are not duplicated across documents.

## Documentation Lifecycle

Keep maintained documents in these folders: `docs/product`, `docs/architecture`, `docs/adr`, `docs/runbooks`, `docs/operations`, `docs/qa`, `docs/security`, `docs/api`, `docs/database` and `docs/messaging`. Each maintained document must be linked from `docs/README.md`.

Historical Day/Buoi material remains in `GiaoAn`. It is educational history, not the current source of truth. When it disagrees with `docs/product/canonical-system-design.md`, the canonical design wins. Do not mass-edit historical lessons merely because a runtime path moves; add a concise migration note instead.

## Build Governance

`Directory.Build.props` carries solution-wide compiler defaults and `Directory.Packages.props` owns NuGet package versions. A package version must not be declared inside an individual `.csproj`; upgrade versions centrally, then restore, build and test the full solution. `global.json` pins the supported .NET SDK line for local and CI reproducibility.

Run `powershell -ExecutionPolicy Bypass -File .\scripts\test-repository-layout.ps1` before a structural pull request.

## Naming And Change Rules

- Use PascalCase for .NET project folders and kebab-case only where tooling expects it, such as Docker image names or Java/Maven artifacts.
- Keep `Program.cs` and dependency-injection composition at the deployable unit root; do not put use-case logic there.
- Database migration files are immutable after they have run outside a disposable local database. Prefix anomalies are recorded as legacy exceptions; new migrations use the next monotonic number and must never reuse an existing prefix.
- A structural move requires a compatibility checklist: solution/AppHost references, Compose/Kubernetes paths, CI, test project references, documentation index and operational scripts.
- Do not move folders solely for alphabetic order or visual symmetry. Move only when ownership, deployability or discoverability improves.