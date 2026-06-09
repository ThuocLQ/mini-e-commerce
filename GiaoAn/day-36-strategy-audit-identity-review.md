---
day: 36
title: "Advanced Strategy Pattern + Audit Log + Advanced Identity Review"
duration: "90-120 minutes"
phase: "Stage 2 - Production Hardening"
project: "MicroShop"
testing: "Architecture review + targeted smoke checks"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown"
---

# Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review

## 0. Current position

You have completed:

```text
Day 31: Clean Architecture + Hexagonal Review
Day 32: API Versioning + Backward Compatibility + Standard Error Format
Day 33: PostgreSQL + Migration + Schema Evolution Hardening
Day 34: FluentValidation Pipeline + Mapping
Day 35: Specification Pattern Lite / Query Criteria
```

Correct roadmap position:

```text
Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review
Day 37: CloudEvents / Event Envelope
Day 38: Standard Transactional Outbox
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
```

Day 36 closes Phase 2.1.

---

## 1. Current repo context

Current repo truth:

```text
Services:
- Services/ApiGateway
- Services/CatalogService
- Services/BasketService
- Services/OrderingService
- Services/DiscountService
- Services/IdentityService
- Services/PaymentService
- Services/OrderQueryService

Workers:
- Services/NotificationWorker
- Workers/ProjectionWorker

Shared:
- BuildingBlocks.Contracts
- MicroShop.AppHost
- MicroShop.ServiceDefaults
```

Current infrastructure:

```text
PostgreSQL - write-side relational databases
Redis - BasketService state/cache
RabbitMQ - workflow/task messaging, NotificationWorker
Kafka - event stream/projection learning, ProjectionWorker
MongoDB - OrderQueryService read model and projection failures
Docker Compose - local runtime
Aspire AppHost - local .NET orchestration
```

Never use:

```text
/orders/read-model
ORD-900
CUST-900
```


Current relevant routes:

```text
POST /auth/login
GET /auth/me
POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment
GET /discounts/{code}
POST /discounts/apply
```

---

## 2. Goal

By the end:

```text
[ ] Strategy Pattern candidate areas are reviewed.
[ ] Discount/Payment strategy candidates are identified.
[ ] Audit log requirements are documented.
[ ] IdentityService current auth/JWT behavior is reviewed.
[ ] SSO/OIDC direction is documented.
[ ] RBAC/ABAC mindset is documented.
[ ] Security-sensitive limitations are documented honestly.
[ ] A Stage 2 backlog is created for audit and identity hardening.
```

Main outputs:

```text
docs/patterns/strategy-pattern-review-day-36.md
docs/security/audit-log-policy.md
docs/security/identity-hardening-review-day-36.md
docs/security/sso-oidc-decision-note.md
docs/backlog/day-36-audit-identity-hardening-backlog.md
```

---

## 3. Scope guard

Do:

```text
[ ] Review current strategy candidates.
[ ] Review IdentityService routes and token behavior.
[ ] Document audit log direction.
[ ] Document SSO/OIDC decision direction.
[ ] Create backlog.
[ ] Do tiny safe cleanup only if obvious.
```

Do not:

```text
[ ] Do not rewrite IdentityService.
[ ] Do not introduce full RBAC/permission system today.
[ ] Do not add Keycloak/Auth0/Microsoft Entra integration today.
[ ] Do not add global audit infrastructure today.
[ ] Do not change payment behavior broadly.
[ ] Do not claim auth is production-ready.
```

What this proves:

```text
MicroShop has a security/audit/strategy hardening direction.
```

What this does not prove:

```text
Authentication is production-grade.
Audit logging is fully implemented.
SSO/OIDC is integrated.
All strategy candidates are refactored.
```

---

## 4. Pre-check

```powershell
git status --short
docker compose config --services
Get-ChildItem Services/IdentityService -Recurse -Filter *.cs
Get-ChildItem Services/PaymentService -Recurse -Filter *.cs
Get-ChildItem Services/DiscountService -Recurse -Filter *.cs
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "Strategy|Provider|Payment|Discount|Audit|Jwt|Token|Role|Claim|Authorize|Authentication|Policy"
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
Get-Content Services/ApiGateway/appsettings.json
Get-Content Services/ApiGateway/appsettings.Docker.json
```

---

## 5. Advanced Strategy Pattern review

Current repo-aware strategy review:

```text
DiscountService:
    Strategy Pattern is already implemented.
    Existing pieces:
    - IDiscountStrategy
    - DiscountStrategyFactory
    - FixedAmountDiscountStrategy
    - PercentageDiscountStrategy

    Day 36 should review extension points, naming, validation, and test coverage.
    Do not treat DiscountService as only a future candidate.

PaymentService:
    Payment provider strategy is still a candidate/future direction.
    Review whether current payment behavior is simple enough to leave as-is.

BasketService:
    REST vs gRPC product validation paths already exist as a communication strategy comparison.
    No refactor today.

IdentityService:
    Local JWT vs external OIDC/SSO is a future auth provider strategy direction.
```

Use criteria:

```text
Multiple algorithms?
Same interface?
Runtime selection?
Likely future providers?
Testing benefits?
```

If answer is no:

```text
Do not force Strategy Pattern.
Document as not needed yet.
```

---

## 6. Possible strategy shapes

Payment provider design reference:

```csharp
public interface IPaymentProviderStrategy
{
    string ProviderName { get; }

    Task<PaymentResult> ProcessAsync(
        PaymentRequest request,
        CancellationToken cancellationToken);
}
```

Discount strategy note:

```text
DiscountService already has IDiscountStrategy, DiscountStrategyFactory,
FixedAmountDiscountStrategy, and PercentageDiscountStrategy.
Day 36 should review this existing implementation instead of designing it from scratch.
```

Review questions:

```text
Can a new discount type be added without editing too many places?
Are strategy names clear?
Is invalid discount input handled safely?
Are strategy behaviors covered by tests or at least documented test cases?
```

Rules:

```text
Use this only as design reference.
Do not wire real external payment provider today.
Do not create fake abstraction if only one behavior exists.
```

---

## 7. Audit log policy

Create:

```text
docs/security/audit-log-policy.md
```

Suggested content:

```text
Goal:
Security-sensitive and business-critical actions should be auditable.

Events to consider:
- User login success/failure
- Token refresh if implemented
- Order checkout
- Payment created
- Payment webhook received
- Payment status changed
- Admin/catalog product mutation
- Discount mutation

Audit entry fields:
- auditId
- occurredAtUtc
- actorUserId
- action
- entityType
- entityId
- result
- sourceIp if available
- correlationId or traceId
- metadata

Rules:
- Do not store secrets or raw passwords.
- Do not log full JWT tokens.
- Do not log sensitive payment details.
- Prefer append-only audit records.
- Keep audit separate from normal application logs.

Current stage:
Day 36 documents policy only.
Implementation is future work.
```

---

## 8. Identity hardening review

Create:

```text
docs/security/identity-hardening-review-day-36.md
```

Include:

```text
Current routes:
POST /auth/login
GET /auth/me

Review checklist:
[ ] Password handling is safe for training-stage.
[ ] JWT signing key/config is not hardcoded in production docs.
[ ] Token lifetime is documented.
[ ] /auth/me requires valid token if implemented.
[ ] Failed login behavior is documented.
[ ] No raw token is logged.
[ ] Claims are minimal and understandable.

Not implemented yet:
Refresh tokens.
RBAC/permissions.
External OIDC/SSO.
Account lockout.
Audit log implementation.
Secret rotation.
```

---

## 9. SSO/OIDC decision note

Create:

```text
docs/security/sso-oidc-decision-note.md
```

Include:

```text
MicroShop currently has IdentityService/JWT foundation for learning.

Production enterprise systems often delegate authentication to an external Identity Provider:
Keycloak, Microsoft Entra ID, Auth0, Okta.

Decision direction:
Keep local JWT foundation for learning.
Future path is OIDC with external Identity Provider.
Gateway validates tokens.
Services enforce authorization policies.

Not implemented today:
Keycloak integration.
Microsoft Entra ID integration.
Auth0/Okta integration.
Refresh token production flow.
RBAC/ABAC full implementation.
```

---

## 10. Strategy review doc

Create:

```text
docs/patterns/strategy-pattern-review-day-36.md
```

Include a candidate table:

```text
DiscountService -> discount calculation strategy -> Already implemented; review extension points and tests
PaymentService -> payment provider strategy -> Candidate / future
BasketService -> HTTP/gRPC validation strategy -> Existing comparison paths; no refactor today
IdentityService -> auth provider strategy -> Future OIDC/SSO
```

Decision:

```text
Do not force a generic strategy framework today.
Use Strategy Pattern only where multiple behaviors share a stable interface.
```

---

## 11. Backlog

Create:

```text
docs/backlog/day-36-audit-identity-hardening-backlog.md
```

Include:

```text
Audit:
[ ] Define audit log storage.
[ ] Implement append-only audit writer.
[ ] Audit login success/failure.
[ ] Audit payment webhook received.
[ ] Audit order checkout.
[ ] Add correlationId/traceId to audit entries.

Identity:
[ ] Review JWT signing key storage.
[ ] Add token lifetime policy.
[ ] Consider refresh tokens.
[ ] Consider RBAC/permissions.
[ ] Consider account lockout after repeated failures.
[ ] Consider external OIDC/SSO.

Strategy Pattern:
[ ] Review existing DiscountService strategy implementation.
[ ] Add tests/docs for discount strategies if missing.
[ ] Payment provider strategy later.
[ ] Auth provider strategy later.
```

---

## 12. Runtime smoke checks

No full runtime is required unless code changes.

If checking Identity routes:

```powershell
docker compose up -d --build identityservice
```

If checking through gateway, use full system or optional gateway only when configured:

```powershell
docker compose up -d --build
```

Postman:

```text
POST {identity_url}/auth/login
GET {identity_url}/auth/me
```

Gateway if running:

```text
POST {gateway_url}/auth/login
GET {gateway_url}/auth/me
```

Use actual request payloads from current docs/Postman.

Do not invent credentials.

---

## 13. Build/test plan

```powershell
dotnet build Services/IdentityService/IdentityService.csproj
dotnet build Services/PaymentService/PaymentService.csproj
dotnet build Services/DiscountService/DiscountService.csproj
```

If strategy code was changed:

```powershell
dotnet build
```

---

## 14. Docs update

Update:

```text
docs/README.md
```

Add links to the Day 36 docs.

---

## 15. Production fit review

What this improves:

```text
Security and audit gaps become visible.
Strategy candidates are identified without over-engineering.
Identity limitations are documented honestly.
```

What remains future work:

```text
Actual audit log implementation.
RBAC/permissions.
Refresh tokens.
External OIDC/SSO.
Payment provider abstraction.
```

---

## 16. Pass checklist

```text
[ ] Strategy candidates are reviewed.
[ ] Audit policy doc exists.
[ ] Identity hardening review exists.
[ ] SSO/OIDC decision note exists.
[ ] Day 36 backlog exists.
[ ] docs/README.md links new docs.
[ ] Build passes for reviewed/touched services or failures are documented.
[ ] No broad identity/payment refactor is introduced.
[ ] No production-ready auth claim is made.
```

---

## 17. Optional commit/tag after review

```text
Commit: Day 36: Strategy Audit Identity Review
Tag: day-36-strategy-audit-identity-review
```

---

## 18. Next day

```text
Day 37: CloudEvents / Event Envelope
```
