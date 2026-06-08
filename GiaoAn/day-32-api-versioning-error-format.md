---
day: 32
title: "API Versioning + Backward Compatibility + Standard Error Format"
duration: "90-120 minutes"
phase: "Stage 2 - Production Hardening"
project: "MicroShop"
testing: "Postman-first + API behavior review"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown to avoid mojibake in Notion/Rider/GitHub"
---

# Day 32: API Versioning + Backward Compatibility + Standard Error Format

## 0. Current position

You completed:

```text
Day 31: Clean Architecture + Hexagonal Review
```

Day 31 reviewed architecture boundaries.

Day 32 now focuses on API contract stability:

```text
API versioning direction
Backward compatibility rules
Standard error format
ProblemDetails-style responses
Postman negative tests
```

This lesson should use findings from Day 31, but should not refactor all services.

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

Background workers:
- Services/NotificationWorker
- Workers/ProjectionWorker

Shared:
- BuildingBlocks.Contracts
- MicroShop.AppHost
- MicroShop.ServiceDefaults
```

Important routes:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries

GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox

GET /products
GET /products/{id}
GET /products/search
GET /products/count
GET /products/price-range
POST /products
PUT /products/{id}
DELETE /products/{id}

GET /basket/{userId}
POST /basket/{userId}/items
POST /basket/{userId}/items-grpc
PUT /basket/{userId}/items/{productId}
DELETE /basket/{userId}/items/{productId}
PUT /basket/{userId}/clear
DELETE /basket/{userId}
GET /basket/products/{productId}/validate
GET /basket/products/{productId}/validate-grpc
POST /basket/preview-item
POST /basket/preview-item-grpc
GET /basket/products/{productId}/compare-communication

GET /discounts/{code}
POST /discounts/apply

POST /auth/login
GET /auth/me

POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment

GET /health
GET /alive
```

Never use:

```text
/orders/read-model
ORD-900
CUST-900
```


Gateway route rules should be verified before implementation:

```powershell
Get-Content Services/ApiGateway/appsettings.json
Get-Content Services/ApiGateway/appsettings.Docker.json
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
```

OpenAPI/Swagger rule:

```text
Do not claim Swagger/OpenAPI UI exists unless code enables it.
If only AddEndpointsApiExplorer exists, say API surface is documented manually.
```

---

## 2. Goal

By the end:

```text
[ ] API versioning strategy is documented.
[ ] Backward compatibility rules are documented.
[ ] Standard error response format is documented.
[ ] A small slice implements ProblemDetails-style errors.
[ ] API versioning package is not added unless explicitly chosen as a code task.
[ ] OrderQueryService is the first target slice.
[ ] ApiGateway is tested only for preserving downstream errors.
[ ] Postman negative tests exist.
```

Main outputs:

```text
docs/api/api-versioning-policy.md
docs/api/api-error-handling-standard.md
docs/api/day-32-api-hardening-notes.md
postman/MicroShop.Day32.ApiHardening.postman_collection.json
```

Optional code outputs:

```text
ProblemDetails-style responses in OrderQueryService
Local helper inside OrderQueryService if useful
```

---

## 3. Scope guard

Do:

```text
[ ] Document API versioning policy.
[ ] Document backward compatibility rules.
[ ] Implement standard errors only in a small slice.
[ ] Keep code changes local to OrderQueryService first.
[ ] Test gateway only if gateway is running.
```

Do not:

```text
[ ] Do not version every endpoint today.
[ ] Do not introduce global shared middleware across the repo yet.
[ ] Do not enable Swagger/OpenAPI unless chosen as a separate task.
[ ] Do not change RabbitMQ/Kafka behavior.
[ ] Do not refactor domain/application layers.
[ ] Do not claim all APIs are standardized.
```

What this proves:

```text
MicroShop has a direction for stable API evolution.
ProblemDetails-style errors can be introduced safely in one slice.
```

What this does not prove:

```text
All APIs are versioned.
All errors across all services are standardized.
OpenAPI is production-ready.
```

---

## 4. Pre-check

Run:

```powershell
git status --short
docker compose config --services
```

Inspect current API/error behavior:

```powershell
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "ProblemDetails|Results.Problem|BadRequest|NotFound|Validation|UseExceptionHandler|AddProblemDetails"
```

Inspect gateway routes:

```powershell
Get-Content Services/ApiGateway/appsettings.json
Get-Content Services/ApiGateway/appsettings.Docker.json
```

Check Swagger/OpenAPI status:

```powershell
Get-ChildItem . -Recurse -Filter *.cs |
  Select-String -Pattern "AddSwaggerGen|UseSwagger|UseSwaggerUI|AddOpenApi|MapOpenApi|AddEndpointsApiExplorer"
```

---

## 5. API versioning policy

Create:

```text
docs/api/api-versioning-policy.md
```

Suggested content:

````md
# MicroShop API Versioning Policy

## Goal

API changes should be explicit and backward compatible where possible.

## Current Stage

MicroShop is a training-stage project.

Versioning is documented first before broad implementation.

## Recommended Strategy

Use URL versioning for public APIs when versioning is introduced.

Example:

```text
/api/v1/products
/api/v1/orders
```

Current unversioned routes remain for Stage 1/early Stage 2 compatibility:

```text
/products
/orders
/order-summaries
```

## Compatibility Rules

Non-breaking changes:

```text
Adding optional response fields.
Adding new endpoints.
Adding optional request fields.
```

Breaking changes:

```text
Removing fields.
Renaming fields.
Changing field types.
Changing status codes without compatibility note.
Changing route shape.
Changing validation rules in a way that rejects valid old clients.
```

## Current Decision

Day 32 does not migrate every route to /api/v1.

Day 32 does not require adding an API versioning package yet.

Day 32 documents policy and focuses on standard errors first.

## Future Work

```text
Add API versioning package or route groups.
Decide gateway route versioning strategy.
Document deprecated routes.
Add OpenAPI version docs if Swagger/OpenAPI is enabled later.
```
````

---

## 6. Standard error format

Create:

```text
docs/api/api-error-handling-standard.md
```

Use ProblemDetails-style format:

````md
# MicroShop API Error Handling Standard

## Goal

APIs should return predictable error responses.

## Standard Shape

```json
{
  "type": "https://microshop.local/problems/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Order summary was not found.",
  "instance": "/order-summaries/11111111-1111-1111-1111-111111111111",
  "traceId": "..."
}
```

## Validation Shape

```json
{
  "type": "https://microshop.local/problems/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "field": [
      "Error message"
    ]
  },
  "traceId": "..."
}
```

## Rules

```text
Do not leak stack traces.
Log exceptions server-side.
Use stable status/title/type.
Include traceId when possible.
```

## Day 32 Target Slice

```text
OrderQueryService first.
ApiGateway only verifies downstream error preservation.
```

## Not Complete Yet

```text
All services are not fully standardized.
Auth-specific errors are not fully standardized.
Gateway-level normalization is future work.
```
````

---

## 7. Implementation target

Target only OrderQueryService first:

```text
GET /order-summaries/{orderId} missing -> 404 ProblemDetails-style
POST /debug/order-summaries invalid -> 400 ProblemDetails-style
MongoDB unavailable -> 503 ProblemDetails-style if cleanly mappable
```

Important invalid GUID behavior:

```text
If route is /order-summaries/{orderId:guid},
then /order-summaries/not-a-guid usually does not match endpoint.
ASP.NET returns 404 before endpoint logic.
Document this behavior.
Do not force it into 400 today by changing route shape.
```

ApiGateway scope:

```text
Only verify whether gateway preserves downstream error shape.
Do not implement gateway-level error normalization today.
```

---

## 8. Minimal local helper option

If useful, add a local helper inside OrderQueryService.

Example:

```csharp
internal static class ApiProblemResults
{
    public static IResult NotFound(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Resource not found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: "https://microshop.local/problems/not-found",
            instance: context.Request.Path);
    }

    public static IResult BadRequest(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Bad request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://microshop.local/problems/bad-request",
            instance: context.Request.Path);
    }

    public static IResult ServiceUnavailable(HttpContext context, string detail)
    {
        return Results.Problem(
            title: "Service unavailable",
            detail: detail,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "https://microshop.local/problems/service-unavailable",
            instance: context.Request.Path);
    }
}
```

Rules:

```text
Keep it local to OrderQueryService on Day 32.
Do not move to ServiceDefaults/shared package yet.
Verify the actual response body after using Results.Problem.
If traceId is required in a specific shape, document whether custom ProblemDetails handling is needed.
```

---

## 9. Runtime tests

Lite mode:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Direct service tests:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/99999999-9999-9999-9999-999999999999
GET {{order_query_url}}/order-summaries/not-a-guid
```

Expected:

```text
Missing GUID order -> 404 ProblemDetails-style if implemented.
not-a-guid -> likely 404 due to route constraint.
Verify actual response body. Results.Problem may not include traceId in the exact shape unless customized.
```

Optional gateway test:

```powershell
docker compose up -d --build --no-deps api-gateway
```

Then:

```text
GET {{gateway_url}}/order-summaries/99999999-9999-9999-9999-999999999999
```

Expected:

```text
Gateway preserves downstream error shape or behavior is documented.
```

---

## 10. Postman updates

Create or update:

```text
postman/MicroShop.Day32.ApiHardening.postman_collection.json
```

Requests:

```text
GET {{order_query_url}}/order-summaries
GET {{order_query_url}}/order-summaries/{{missingOrderId}}
GET {{order_query_url}}/order-summaries/not-a-guid
```

Optional gateway:

```text
GET {{gateway_url}}/order-summaries/{{missingOrderId}}
```

Environment:

```text
order_query_url
gateway_url
missingOrderId = 99999999-9999-9999-9999-999999999999
```

---

## 11. Build/test plan

Build touched projects:

```powershell
dotnet build Services/OrderQueryService/OrderQueryService.csproj
```

If gateway was tested or touched:

```powershell
dotnet build Services/ApiGateway/ApiGateway.csproj
```

If shared code was touched:

```powershell
dotnet build
```

---

## 12. Docs update

Create:

```text
docs/api/day-32-api-hardening-notes.md
```

Suggested content:

````md
# Day 32 API Hardening Notes

## Reviewed target

```text
OrderQueryService
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries
```

## Findings

| Scenario | Current behavior | Target behavior | Status |
| --- | --- | --- | --- |
| Missing order summary | TBD | 404 ProblemDetails-style | TBD |
| Invalid GUID route | 404 due to route constraint | Documented 404 | TBD |
| Debug invalid payload | TBD | 400 ProblemDetails-style | TBD |
| MongoDB unavailable | TBD | 503 ProblemDetails-style if cleanly mapped | TBD |

## Gateway

```text
Gateway is tested only for preserving downstream error shape.
No gateway-level normalization is implemented on Day 32.
```

## Future work

```text
API versioning implementation.
Service-wide error standardization.
Validation pipeline.
OpenAPI/Swagger enablement if desired.
```
````

Update `docs/README.md` to link:

```text
docs/api/api-versioning-policy.md
docs/api/api-error-handling-standard.md
docs/api/day-32-api-hardening-notes.md
```

---

## 13. Production fit review

What this improves:

```text
API contracts become more predictable.
Error responses become easier for clients to handle.
Versioning policy reduces random future breaking changes.
```

What remains future work:

```text
Actual route versioning implementation.
Service-wide validation pipeline.
Auth/authorization error consistency.
Gateway error normalization.
OpenAPI/Swagger enablement if desired.
```

---

## 14. Pass checklist

```text
[ ] API versioning policy doc exists.
[ ] API error handling standard doc exists.
[ ] Day 32 API hardening notes exist.
[ ] OrderQueryService missing order behavior is standardized or documented.
[ ] Invalid GUID route behavior is documented as likely 404.
[ ] Debug invalid payload behavior is standardized or documented.
[ ] Mongo unavailable behavior is documented or mapped to 503 if safe.
[ ] Gateway only verifies downstream behavior if running.
[ ] No shared/global error middleware is introduced.
[ ] Actual ProblemDetails response body is verified, including whether traceId appears.
[ ] Postman negative tests exist.
[ ] Build passes for touched projects.
[ ] docs/README.md links new API docs.
```

---

## 15. Optional commit/tag after review

Recommended commit:

```text
Day 32: API Versioning Error Format
```

Recommended tag:

```text
day-32-api-versioning-error-format
```

Commands:

```powershell
git add .
git commit -m "Day 32: API Versioning Error Format"
git tag day-32-api-versioning-error-format
```

---

## 16. Next day

Day 33:

```text
PostgreSQL + Migration + Schema Evolution Hardening
```
