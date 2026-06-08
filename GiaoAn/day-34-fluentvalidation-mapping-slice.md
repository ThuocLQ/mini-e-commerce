---
day: 34
title: "FluentValidation Pipeline + Mapping"
duration: "90-120 minutes"
phase: "Stage 2 - Production Hardening"
project: "MicroShop"
testing: "Postman negative tests + build verification"
type: "lesson"
repo_aware: true
source_of_truth: true
encoding_note: "ASCII-safe Markdown to avoid mojibake in Notion/Rider/GitHub"
---

# Day 34: FluentValidation Pipeline + Mapping

## 0. Current position

You completed Day 33 database/schema evolution review.

Day 34 focuses on validation and DTO mapping.

Goal:

```text
Make request validation explicit, testable, and consistent in one small slice.
```

Do not apply validation everywhere today.

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


## 2. Goal

```text
[ ] Validation strategy is documented.
[ ] Mapping strategy is documented.
[ ] One target slice uses explicit validation.
[ ] Invalid requests return documented error shape.
[ ] Postman negative tests are added.
```

Main outputs:

```text
docs/api/validation-and-mapping-standard.md
docs/api/day-34-validation-mapping-notes.md
postman/MicroShop.Day34.Validation.postman_collection.json
```

Optional code output:

```text
FluentValidation validators for one target slice
Mapping helpers or explicit mapping methods for one target slice
```

## 3. Scope guard

Do:

```text
[ ] Inspect existing validation.
[ ] Choose one target service/endpoint.
[ ] Add or document FluentValidation.
[ ] Keep mapping explicit.
[ ] Add Postman negative tests.
```

Do not:

```text
[ ] Do not add validators to every service.
[ ] Do not introduce a huge shared validation framework today.
[ ] Do not change route shapes.
[ ] Do not change RabbitMQ/Kafka behavior.
[ ] Do not claim all validation is standardized.
```

What this proves:

```text
MicroShop has a validation/mapping direction and one verified slice.
```

What this does not prove:

```text
Every request in every service is validated.
All mapping is standardized.
```

## 4. Pre-check

```powershell
Get-ChildItem Services -Recurse -Filter *.cs |
  Select-String -Pattern "FluentValidation|IValidator|Validate|ValidationResult|MapTo|Mapper|AutoMapper|Mapster|BadRequest"
Get-ChildItem Services -Recurse -Filter *Endpoints.cs
Get-ChildItem Services -Recurse -Filter *.csproj |
  Select-String -Pattern "FluentValidation|AutoMapper|Mapster"
```

## 5. Choose target slice

Recommended target:

```text
OrderQueryService:
    POST /debug/order-summaries
    DTO: DebugUpsertOrderSummaryRequest
```

Why:

```text
It is current from recent lessons.
It is a debug endpoint, lower production risk.
It likely accepts a request body that can be validated.
POST /debug/order-summaries is expected only in Development/local learning mode.
If it is not mapped outside Development, document that behavior instead of changing it.
```

Alternative:

```text
CatalogService:
    POST /products
```

Avoid Identity/Payment as first validation slice because they are security/payment sensitive.

## 6. Validation standard

Create:

```text
docs/api/validation-and-mapping-standard.md
```

Include:

```text
Validate at API/Application boundary.
Keep validation messages clear.
Do not rely only on database constraints.
Return documented error format.
Do not expose persistence models directly.
Map request DTO -> command/query.
Map domain/read model -> response DTO.
Keep mapping explicit unless a mapping library is intentionally adopted.
```

## 7. Add FluentValidation package

OrderQueryService currently does not reference FluentValidation.

If target is OrderQueryService, add:

```powershell
dotnet add Services/OrderQueryService/OrderQueryService.csproj package FluentValidation
```

If the repo changes later and FluentValidation is already referenced, do not add it again.

## 8. Example validator

Adjust property names to real request DTO.

```csharp
using FluentValidation;

public sealed class DebugUpsertOrderSummaryRequestValidator
    : AbstractValidator<DebugUpsertOrderSummaryRequest>
{
    public DebugUpsertOrderSummaryRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.ItemCount).GreaterThanOrEqualTo(0);
    }
}
```

## 9. Endpoint usage

Example:

```csharp
var validationResult = await validator.ValidateAsync(request, cancellationToken);

if (!validationResult.IsValid)
{
    return Results.ValidationProblem(
        validationResult.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray()));
}
```

If Day 32 established a local ProblemDetails helper, align with it.

Do not create a global validation pipeline today unless repo already has one.

## 10. Mapping review

For target endpoint, check:

```text
Request DTO -> application command/query/read model
Read model/domain -> response DTO
```

Avoid returning persistence model directly unless intentionally accepted for training-stage and documented.

Prefer explicit mapping for now.

## 11. Postman negative tests

Lite mode:

```powershell
docker compose up -d --build zookeeper kafka mongodb orderqueryservice projectionworker
```

Test invalid debug payload:

```text
POST {{order_query_url}}/debug/order-summaries
```

Payload:

```json
{
  "customerName": "",
  "totalAmount": -1,
  "currency": "VN"
}
```

Expected:

```text
400 validation error
errors contains relevant fields
```

## 12. Day 34 notes

Create:

```text
docs/api/day-34-validation-mapping-notes.md
```

Include:

```text
Target service
Endpoint
Request DTO
Validator
Mapping notes
Verified negative tests
Future work
```

Update docs/README.md.

## 13. Build/test plan

```powershell
dotnet build Services/OrderQueryService/OrderQueryService.csproj
```

If Catalog target:

```powershell
dotnet build Services/CatalogService/CatalogService.csproj
```

Run Postman negative tests.

## 14. Production fit review

Improves:

```text
Bad input is rejected earlier.
Validation rules are explicit and testable.
Mapping direction is clearer.
```

Future work:

```text
Global validation pipeline.
Consistent validation errors across all services.
Mapping strategy across all services.
```

## 15. Pass checklist

```text
[ ] Existing validation/mapping is inspected.
[ ] Target slice is chosen.
[ ] validation-and-mapping-standard.md exists.
[ ] DebugUpsertOrderSummaryRequestValidator exists or validation decision is documented.
[ ] Negative Postman tests exist.
[ ] Mapping is reviewed for target endpoint.
[ ] day-34-validation-mapping-notes.md exists.
[ ] Build passes for touched service.
[ ] No broad validation refactor is introduced.
```

## 16. Optional commit/tag after review

```text
Commit: Day 34: FluentValidation Mapping Slice
Tag: day-34-fluentvalidation-mapping-slice
```

## 17. Next day

```text
Day 35: Specification Pattern
```
