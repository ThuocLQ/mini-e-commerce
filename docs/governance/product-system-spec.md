# Product And System Specification

## Product outcome

MicroShop is a customer-facing commerce system for authenticated customers and operations staff. The portfolio environment must demonstrate the same real business flows as the commercial product, except that Sandbox payment replaces a live provider.

## Personas

| Persona | Goal | Authority |
| --- | --- | --- |
| Customer | Browse, buy, pay, track, cancel eligible orders | Own data and own orders only |
| Operations agent | Fulfill orders and review payment/refund requests | No direct database writes; actions are audited |
| Administrator | Catalog, inventory, policy, operational configuration | RBAC-gated |
| Support | Read order/payment history and investigate | Read-only unless a policy grants action |

## Canonical customer journey

1. Discover active, sellable catalog products.
2. View product detail with current price and availability.
3. Add/update cart; cart is not stock reservation.
4. Select owned delivery address and review quote.
5. Create an idempotent order; price, promotion, inventory and address become immutable snapshots.
6. Start payment with configured provider; provider webhook is authoritative.
7. Customer tracks order, retries eligible payment, or cancels only when policy permits.
8. Operations fulfills paid orders; refunds and voids require an audited action.

## Bounded context ownership

| Context | Owns | Must not own |
| --- | --- | --- |
| Catalog | Sellability, catalog content, display price/media | Reserved stock, orders |
| Basket | Versioned mutable cart | Order/payment truth |
| Ordering | Order aggregate, snapshots, cancellation policy | Provider transaction state |
| Inventory | On-hand/reserved/committed stock | Customer cart |
| Discount | Coupon validity and promotion reservation | Order lifecycle |
| Payment | Provider session, authorization/capture/void/refund state | Order status mutation |
| Identity | Authentication and role claims | Customer shipping snapshots |
| Notification | Delivery attempts and notification audit | Business state decisions |
| OrderQuery | Read model only | Write-side decisions |

## Non-negotiable rules

- Ordering, Inventory and Payment are authoritative only for their owned data.
- Every money, price, discount and address on an order is an immutable snapshot.
- Commands with side effects are idempotent; events are deduplicated by event id.
- UI renders server-confirmed state. It must not invent payment, stock, shipment or refund state.
- A live provider callback is verified before state changes; frontend redirects are never proof of payment.
- Internal APIs, queues and service databases are never public gateway routes.

## Current release boundary

Portfolio: authenticated purchase, Sandbox payment, customer account/order detail, and operations read surfaces.

Commercial release blockers: live-provider credentials/webhook verification, audited refund/void approval workflow, fulfillment/shipment workflow, durable notification delivery, browser E2E coverage, and production runbook verification.