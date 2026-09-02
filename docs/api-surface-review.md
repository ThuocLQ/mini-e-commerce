# MicroShop API Surface Review

## Goal

Review the current API surface and document whether OpenAPI/Swagger is enabled.

## OpenAPI/Swagger Status

Current repo observation:

```text
AddEndpointsApiExplorer appears in several API registration files.
Swagger/Swashbuckle/AddOpenApi/MapOpenApi is not currently enabled.
```

This review documents API surface, not generated OpenAPI output.

Future code task:

```text
Enable OpenAPI/Swagger for selected services in Development.
```

## Gateway Routes

The public gateway routes are defined in `Services/ApiGateway/appsettings.json`.

| Gateway path | Target |
| --- | --- |
| `/catalog/{**catch-all}` | `CatalogService` |
| `/cart/{**catch-all}` | `BasketService` |
| `/orders/{**catch-all}` | `OrderingService` |
| `/order-summaries` | `OrderQueryService` |
| `/order-summaries/{**catch-all}` | `OrderQueryService` |
| `/debug/order-summaries` | `OrderQueryService` |
| `/debug/order-summaries/{**catch-all}` | `OrderQueryService` |
| `/discounts/{**catch-all}` | `DiscountService` |
| `/auth/{**catch-all}` | `IdentityService` |
| `/payments/{**catch-all}` | `PaymentService` |
| `/webhooks/{**catch-all}` | `PaymentService` |

Docker destinations are defined in `Services/ApiGateway/appsettings.Docker.json`.

## Important Service Endpoints

Catalog:

```text
GET /products
GET /products/{id}
GET /products/search
GET /products/count
GET /products/price-range
POST /products
PUT /products/{id}
DELETE /products/{id}
```

Basket:

```text
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
```

Ordering:

```text
GET /orders
GET /orders/{id}
POST /orders/checkout
GET /debug/outbox
```

Order query:

```text
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries
```

Discount:

```text
GET /discounts/{code}
POST /discounts/apply
```

Identity:

```text
POST /auth/login
GET /auth/me
```

Payment:

```text
POST /payments
GET /payments/{id}
POST /webhooks/payment
POST /payments/webhooks/payment
```

Health:

```text
GET /health
GET /alive
```

## Review Checklist

```text
[ ] Service starts successfully.
[ ] Important endpoints are documented.
[ ] Request DTOs do not expose persistence models directly.
[ ] Response DTOs are explicit.
[ ] Error responses are understandable.
[ ] Auth-required endpoints are documented if applicable.
[ ] Deprecated or old endpoints are removed from docs.
[ ] OpenAPI/Swagger availability is verified, not assumed.
```

## Do Not Document

```text
/orders/read-model
```

Use:

```text
/order-summaries
/order-summaries/{orderId}
```

## Verified Development OpenAPI

All ASP.NET Core HTTP services call `AddServiceDefaults()` and `MapDefaultEndpoints()`. In `Development`, this registers and serves `GET /openapi/v1.json`; the endpoint is intentionally not mapped in Docker/local-prod/Kubernetes. Swagger UI is not required: consumers use the generated document in development tooling.

Authentication documentation baseline:

- Public: catalog discovery, discount lookup, `/auth/login`, `/auth/register`, `/health`, `/alive`.
- Bearer token: cart, orders, order summaries, payments, `/me/*`, inventory admin, supplier and procurement operations.
- Provider callback only: `/webhooks/*`; signature validation is owned by PaymentService.
- Internal contracts are never exposed through Gateway; service-to-service callers require `X-MicroShop-Internal-Key` and private network access.

## Internal Contract Boundary Review

Internal routes use the `/_internal/*` convention, are not routed by ApiGateway, and return `404` when the configured `X-MicroShop-Internal-Key` is absent or invalid. Catalog/Ordering/Payment callers attach the key through a delegating handler; NotificationWorker sends it explicitly to Identity. Internal callers remain on the private service network and must not trust a browser-supplied identity header.
