# P0 Commerce Delivery Board

This board is the execution order for `p0-customer-commerce-experience-spec.md`. A workstream cannot claim completion until its evidence satisfies `docs/governance/quality-gates.md`.

## Milestone 0 - Release Baseline

| Team | Deliverable | Dependency | Exit evidence |
| --- | --- | --- | --- |
| Product/BA | Approve P0 scope, policy matrix and demo data | Canonical design | Signed feature spec and excluded scope |
| UX/UI | Screen inventory and state matrix for storefront/account/operations | Product spec | Desktop/mobile flows, accessibility review |
| QA | Critical E2E and failure scenario catalogue | Product spec | Automated/manual test cases mapped to acceptance |
| Platform/SRE | Reproducible compose environment, health and observability baseline | Existing runtime | One-command startup and smoke evidence |

## Milestone 1 - Trustworthy Customer Checkout

| Slice | Lead roles | Backend scope | Frontend scope | Required proof |
| --- | --- | --- | --- | --- |
| M1.1 Catalog discovery | Catalog dev, frontend, QA | Sellable product/read contract, pagination/search/filter | Catalog/product-detail screens with real media/data | Catalog down/retry, empty result, mobile flow |
| M1.2 Cart integrity | Basket/Catalog dev, frontend, QA | Version/idempotency behavior and controlled dependency errors | Cart page/panel, quantity/remove/clear, real totals | Concurrent/duplicate mutation and Catalog-down test |
| M1.3 Checkout/order | Ordering/Inventory/Discount/Identity dev, frontend, QA | Address ownership, immutable snapshots, idempotent create, stable errors | Address selection, review, submit/retry states | Double-submit, stock/coupon/address failures, trace |

## Milestone 2 - Sandbox Payment End To End

| Slice | Lead roles | Backend scope | Frontend scope | Required proof |
| --- | --- | --- | --- | --- |
| M2.1 Provider session | Payment dev, security, frontend | Provider adapter/session persistence, safe redirects | Payment start/return/cancel pages | No secret exposure, amount/order validation |
| M2.2 Callback truth | Payment/Ordering dev, QA, SRE | Signature verification, dedup, outbox/saga, status projection | Poll/re-fetch confirmed status; no redirect-as-success | Valid, invalid, duplicate and late callback drills |
| M2.3 Payment operations | Payment dev, operations UI, QA | Audited capture/void/refund action persistence and authorization | Exception/status surface; no browser-only mutation | Audit query, RBAC, reconciliation drill |

## Milestone 3 - Account And Operations Readiness

| Slice | Lead roles | Backend scope | Frontend scope | Required proof |
| --- | --- | --- | --- | --- |
| M3.1 Customer account | Identity/Ordering/OrderQuery dev, frontend, QA | Owned order reads, projection-lag contract | Account, address book, order list/detail | Cross-user authorization and stale-read handling |
| M3.2 Operations order view | Ordering/Payment dev, operations UI, QA | RBAC read contracts, audit identity | Search/detail/exception views | Support read-only and staff-action audit |
| M3.3 Fulfillment foundation | Ordering dev, operations UI, QA | Paid queue, shipment aggregate, tracking/status history, events | Fulfillment queue/detail only after API exists | State-policy, idempotency, audit, restart test |

## Milestone 4 - Portfolio Release Candidate

| Team | Deliverable | Exit evidence |
| --- | --- | --- |
| QA | Browser E2E critical journey and failure suite | CI results plus recorded manual run |
| SRE | Dashboard, alert/runbook, backup/restore and rollback verification | Environment evidence and commands |
| Security | Auth/RBAC review, secrets/callback/config review | Remediation list closed or accepted risk |
| Release owner | Deployment, demo script and rollback target | Image/tag, deployed version, release checklist |

## Operating Rules

1. Product/BA owns acceptance and scope; architecture lead owns boundaries; engineering lead owns implementation plan; QA can reject a slice without evidence; release owner can stop deployment.
2. One pull request maps to one slice or a tightly coupled vertical increment. It links the feature spec, tests, migration/event contract and rollout note.
3. UI work starts from a real API contract or an explicitly approved contract stub, never permanent mock data.
4. A later milestone may not be used to mask an earlier failure. For example, do not create fulfillment UI before payment/order ownership is reliable.
5. New service extraction requires an ADR and a demonstrated ownership/scaling/operations boundary.
## Current Portfolio Evidence

Verified on 2026-08-29 against the local portfolio stack:

- `scripts/portfolio-customer-smoke.ps1 -Scenario Cancellation` passed: registration, address ownership, catalog/cart/quote/checkout, Sandbox payment initiation, immutable address snapshot and pre-fulfillment cancellation.
- `scripts/portfolio-customer-smoke.ps1 -Scenario Fulfillment` passed: Sandbox capture, shipment create/dispatch/deliver, shipment audit history and Kafka-to-Mongo order projection.
- Operations Portal production Docker build passed after shipment audit history UI was added.
- `scripts/notification-preference-smoke.ps1` passed through Gateway: preference defaults to enabled, opt-out persists and `/auth/me` reflects it. The Storefront BFF cookie flow was also verified against `/api/notification-preferences`.
- Notification tests passed: verified opted-out customers do not invoke lifecycle SMTP delivery; duplicate delivery and retry-after-failure behavior remain covered.
- 2026-09-02 regression reran both customer scenarios after a clean Compose network recreate.

This evidence closes the P0 portfolio business journey. It does not close the commercial release gate: live provider credentials/public callback/reconciliation, a production-valid MediatR license or approved replacement, and encrypted Data Protection key storage remain external release blockers.