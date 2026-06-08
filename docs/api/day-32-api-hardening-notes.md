# Day 32 API Hardening Notes

## Reviewed Target

```text
OrderQueryService
GET /order-summaries
GET /order-summaries/{orderId}
POST /debug/order-summaries
```

## Implemented Behavior

| Scenario | Behavior |
| --- | --- |
| Missing order summary | 404 ProblemDetails-style response |
| Invalid debug payload | 400 validation ProblemDetails-style response after Day 34 validator |
| MongoDB exception | 503 ProblemDetails-style response |
| Unexpected exception | 500 ProblemDetails-style response |

## Route Constraint Note

`GET /order-summaries/{orderId:guid}` only matches valid GUID route values.

This means:

```text
/order-summaries/not-a-guid
```

usually returns a routing-level 404 before endpoint logic runs. Day 32 documents this behavior instead of changing the route shape to force a 400.

## Gateway

ApiGateway is tested only for downstream response preservation.

```text
No gateway-level error normalization was introduced on Day 32.
```

## Future Work

```text
Introduce route versioning deliberately.
Standardize errors across all services.
Add OpenAPI examples if OpenAPI is enabled later.
```
