# MicroShop Validation And Mapping Standard

## Goal

Requests should be validated before business behavior runs, and mappings should make boundaries obvious.

## Validation Rules

```text
Validate at the API/Application boundary.
Keep validation rules explicit and testable.
Do not rely only on database constraints.
Return documented validation errors.
Avoid spreading manual validation across endpoint bodies.
Do not add a global validation framework until one service slice proves the pattern.
```

## Mapping Rules

```text
Map request DTOs to commands, queries, or read models explicitly.
Map domain/read models to response DTOs explicitly.
Do not expose persistence models directly unless intentionally documented.
Prefer local mapping methods over a mapping library until duplication becomes real.
```

## Day 34 Target Slice

```text
Service: OrderQueryService
Endpoint: POST /debug/order-summaries
Request DTO: DebugUpsertOrderSummaryRequest
Validator: DebugUpsertOrderSummaryRequestValidator
```

The debug endpoint is intended for Development/local learning mode.
