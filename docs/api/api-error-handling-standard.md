# MicroShop API Error Handling Standard

## Goal

MicroShop APIs should return predictable error responses that clients can parse consistently.

## Standard Shape

Use ProblemDetails-style responses:

```json
{
  "type": "https://microshop.local/problems/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Order summary was not found.",
  "instance": "/order-summaries/99999999-9999-9999-9999-999999999999"
}
```

Validation responses should include field-level errors:

```json
{
  "type": "https://microshop.local/problems/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "CustomerName": [
      "'Customer Name' must not be empty."
    ]
  }
}
```

## Rules

```text
Do not leak stack traces.
Log exceptions server-side.
Use stable type/title/status values.
Include instance path.
Include traceId when the configured ProblemDetails implementation provides it.
Verify actual response bodies before documenting exact fields.
```

## Day 32 Target Slice

```text
Service: OrderQueryService
Routes:
- GET /order-summaries/{orderId}
- POST /debug/order-summaries
```

The ApiGateway only preserves downstream responses on Day 32. It does not normalize errors globally.

## Future Work

```text
Service-wide error standardization.
Auth and authorization error consistency.
Gateway-level normalization if needed.
OpenAPI examples if OpenAPI is enabled later.
```
