# MicroShop API Versioning Policy

## Goal

MicroShop APIs should evolve without surprising existing clients.

## Current Stage

The project is still in a learning and hardening stage. Current public routes remain unversioned for compatibility:

```text
/products
/orders
/order-summaries
/basket
/discounts
/auth
/payments
```

Day 32 documents the policy first. It does not migrate every route to a versioned URL.

## Recommended Direction

When public API versioning is introduced, prefer URL versioning at the gateway boundary:

```text
/api/v1/products
/api/v1/orders
/api/v1/order-summaries
```

Internal service routes can remain simple unless a service is exposed directly to external clients.

## Compatibility Rules

Non-breaking changes:

```text
Adding optional response fields.
Adding optional request fields.
Adding new endpoints.
Adding new enum/status values with documented behavior.
```

Breaking changes:

```text
Removing fields.
Renaming fields.
Changing field types.
Changing route shapes.
Changing status codes without a compatibility note.
Rejecting previously valid requests without a migration window.
```

## Current Decision

```text
Keep existing unversioned routes.
Do not add an API versioning package on Day 32.
Standardize error shape in one service slice first.
Document future route versioning before implementing it broadly.
```

## Future Work

```text
Add explicit versioned gateway routes.
Document deprecated routes.
Add OpenAPI version docs if OpenAPI is enabled later.
Add compatibility tests for public endpoints.
```
