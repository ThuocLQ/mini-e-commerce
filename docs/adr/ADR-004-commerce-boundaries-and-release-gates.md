# ADR-004: Commerce Boundaries And Release Gates

- Status: Accepted
- Date: 2026-08-28

## Context

MicroShop accumulated overlapping roadmaps while customer-facing capabilities evolved. The main risks are extracting empty services, treating enum states as a delivered workflow, and claiming a payment provider is live before its callback and operations paths are proven.

## Decision

1. `docs/product/canonical-system-design.md` is the product/system-design source of truth. `docs/governance/` is the delivery and quality source of truth.
2. `IdentityService` owns the P0 customer address book. A CustomerProfile service is deferred until contact, preferences, CRM, or channel ownership requires an independent lifecycle.
3. Fulfillment starts as a bounded module within `OrderingService`. It may be extracted only when multi-warehouse, multi-shipment, carrier/WMS integration, or separate operations ownership creates a real boundary.
4. `PaymentService` owns provider sessions, provider callbacks, reconciliation, void/refund provider actions, and payment audit. `OrderingService` owns customer cancellation policy and order status projection. No public endpoint invokes internal RabbitMQ consumer commands.
5. Portfolio uses Sandbox payment only. Commercial payment remains blocked until provider credentials, signed public callback verification, duplicate/late callback drills, reconciliation, audit-backed operations actions, and release evidence pass.

## Consequences

- Existing `Confirmed`, `Shipped`, and `Delivered` order states are transitional operational states, not proof that shipment/tracking is implemented.
- UI cannot expose shipment, live payment, refund approval, or notification delivery as completed capability until its owner, API/event contract, persistence, and quality-gate evidence exist.
- New service creation requires an ADR that demonstrates independent ownership and operational benefit.
- Product roadmaps that conflict with this ADR are historical references only.

## Revisit When

- Fulfillment requirements require independent persistence, scaling, or operational ownership.
- Customer profile/contact lifecycle exceeds IdentityService ownership.
- A second commercial payment provider or multi-currency settlement requires a payment orchestration boundary.