# Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review

## 0. Vị trí hiện tại

Bạn đã hoàn thành:

```text
Day 31: Clean Architecture + Hexagonal Review
Day 32: API Versioning + Backward Compatibility + Standard Error Format
Day 33: PostgreSQL + Migration + Schema Evolution Hardening
Day 34: FluentValidation Pipeline + Mapping
Day 35: Specification Pattern Lite / Query Criteria
```

Vi tri dung trong roadmap:

```text
Day 36: Advanced Strategy Pattern + Audit Log + Advanced Identity Review
Day 37: CloudEvents / Event Envelope
Day 38: Standard Transactional Outbox
Day 39: Outbox Publisher + Advanced Idempotency + Inbox/WebhookLog
```

Day 36 khep lai Phase 2.1.

---

## 1. Bối cảnh repo hiện tại

Sự thật hiện tại của repo:

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

Hạ tầng hiện tại:

```text
PostgreSQL - write-side relational databases
Redis - BasketService state/cache
RabbitMQ - workflow/task messaging, NotificationWorker
Kafka - event stream/projection learning, ProjectionWorker
MongoDB - OrderQueryService read model and projection failures
Docker Compose - local runtime
Aspire AppHost - local .NET orchestration
```

Không dùng:

```text
/orders/read-model
ORD-900
CUST-900
```


Các route liên quan hiện tại:

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

## 2. Mục tiêu

Sau khi hoàn thành:

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

Output chính:

```text
docs/patterns/strategy-pattern-review-day-36.md
docs/security/audit-log-policy.md
docs/security/identity-hardening-review-day-36.md
docs/security/sso-oidc-decision-note.md
docs/backlog/day-36-audit-identity-hardening-backlog.md
```

---

## 3. Giới hạn phạm vi

Nên làm:

```text
[ ] Review current strategy candidates.
[ ] Review IdentityService routes and token behavior.
[ ] Document audit log direction.
[ ] Document SSO/OIDC decision direction.
[ ] Create backlog.
[ ] Do tiny safe cleanup only if obvious.
```

Không làm:

```text
[ ] Do not rewrite IdentityService.
[ ] Do not introduce full RBAC/permission system today.
[ ] Do not add Keycloak/Auth0/Microsoft Entra integration today.
[ ] Do not add global audit infrastructure today.
[ ] Do not change payment behavior broadly.
[ ] Do not claim auth is production-ready.
```

Điều phần này chứng minh:

```text
MicroShop has a security/audit/strategy hardening direction.
```

Điều phần này chưa chứng minh:

```text
Authentication is production-grade.
Audit logging is fully implemented.
SSO/OIDC is integrated.
All strategy candidates are refactored.
```

---

## 4. Kiểm tra trước khi làm

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

## 5. Review Advanced Strategy Pattern

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

Tiêu chí sử dụng:

```text
Multiple algorithms?
Same interface?
Runtime selection?
Likely future providers?
Testing benefits?
```

Nếu câu trả lời là không:

```text
Không ép dùng Strategy Pattern.
Document là chưa cần ở thời điểm hiện tại.
```

---

## 6. Hình dạng Strategy Pattern tham khảo

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

Quy tắc:

```text
Use this only as design reference.
Không wire payment provider thật hôm nay.
Không tạo abstraction giả nếu chỉ có một behavior.
```

---

## 7. Chính sách audit log

Tạo:

```text
docs/security/audit-log-policy.md
```

Nội dung gợi ý:

```text
Mục tiêu:
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

Quy tắc:
- Do not store secrets or raw passwords.
- Do not log full JWT tokens.
- Do not log sensitive payment details.
- Prefer append-only audit records.
- Keep audit separate from normal application logs.

Giai đoạn hiện tại:
Day 36 documents policy only.
Implementation is future work.
```

---

## 8. Review hardening Identity

Tạo:

```text
docs/security/identity-hardening-review-day-36.md
```

Bao gồm:

```text
Route hiện tại:
POST /auth/login
GET /auth/me

Checklist review:
[ ] Password handling is safe for training-stage.
[ ] JWT signing key/config is not hardcoded in production docs.
[ ] Token lifetime is documented.
[ ] /auth/me requires valid token if implemented.
[ ] Failed login behavior is documented.
[ ] No raw token is logged.
[ ] Claims are minimal and understandable.

Chưa triển khai:
Refresh tokens.
RBAC/permissions.
External OIDC/SSO.
Account lockout.
Audit log implementation.
Secret rotation.
```

---

## 9. Ghi chú quyết định SSO/OIDC

Tạo:

```text
docs/security/sso-oidc-decision-note.md
```

Bao gồm:

```text
MicroShop currently has IdentityService/JWT foundation for learning.

Production enterprise systems often delegate authentication to an external Identity Provider:
Keycloak, Microsoft Entra ID, Auth0, Okta.

Hướng quyết định:
Giu nen local JWT de hoc.
Huong sau nay la OIDC voi external Identity Provider.
Gateway validate token.
Cac service enforce authorization policy.

Chưa triển khai hôm nay:
Keycloak integration.
Microsoft Entra ID integration.
Auth0/Okta integration.
Refresh token production flow.
RBAC/ABAC full implementation.
```

---

## 10. Tài liệu review Strategy

Tạo:

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

Quyết định:

```text
Do not force a generic strategy framework today.
Use Strategy Pattern only where multiple behaviors share a stable interface.
```

---

## 11. Backlog

Tạo:

```text
docs/backlog/day-36-audit-identity-hardening-backlog.md
```

Bao gồm:

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

## 12. Smoke test runtime

Không cần chạy full runtime nếu không đổi code.

If checking Identity routes:

```powershell
docker compose up -d --build identityservice
```

Nếu kiểm tra qua gateway, dùng full system hoặc gateway tùy chọn khi đã cấu hình:

```powershell
docker compose up -d --build
```

Postman:

```text
POST {identity_url}/auth/login
GET {identity_url}/auth/me
```

Gateway nếu đang chạy:

```text
POST {gateway_url}/auth/login
GET {gateway_url}/auth/me
```

Dùng payload request thật từ docs/Postman hiện tại.

Không tự bịa credentials.

---

## 13. Kế hoạch build/test

```powershell
dotnet build Services/IdentityService/IdentityService.csproj
dotnet build Services/PaymentService/PaymentService.csproj
dotnet build Services/DiscountService/DiscountService.csproj
```

Nếu có đổi code strategy:

```powershell
dotnet build
```

---

## 14. Cập nhật docs

Update:

```text
docs/README.md
```

Add links to the Day 36 docs.

---

## 15. Review độ phù hợp production-minded

Điều phần này cải thiện:

```text
Security and audit gaps become visible.
Strategy candidates are identified without over-engineering.
Identity limitations are documented honestly.
```

Những phần còn là future work:

```text
Actual audit log implementation.
RBAC/permissions.
Refresh tokens.
External OIDC/SSO.
Payment provider abstraction.
```

---

## 16. Checklist đạt yêu cầu

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

## 17. Commit/tag tùy chọn sau review

```text
Commit: Day 36: Strategy Audit Identity Review
Tag: day-36-strategy-audit-identity-review
```

---

## 18. Ngày tiếp theo

```text
Day 37: CloudEvents / Event Envelope
```

