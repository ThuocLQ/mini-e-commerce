# P0 Customer Commerce Experience Specification

- Feature ID: P0-COMMERCE-2026-08
- Owner: Product Owner and Commerce Tech Lead
- Status: Portfolio acceptance verified on 2026-09-02; commercial release gate remains open
- Design authority: `docs/product/canonical-system-design.md`, ADR-004
- Target: portfolio must demonstrate the same business journey as the commercial product, with Sandbox instead of live payment.

## 1. Product Outcome

A signed-in customer can discover a real catalog product, manage a cart, choose an owned delivery address, submit one idempotent order, complete or fail a Sandbox payment, and see a trustworthy order timeline. Operations staff can see the same order/payment facts and perform only actions backed by an audited backend workflow.

The release is not allowed to display a provider payment, shipment, refund, notification, or inventory claim that has not been confirmed by its owning backend.

## 2. Scope And Release Boundary

### In scope

- Authenticated catalog browsing, product detail, search/filter, price, media, availability advisory.
- Cart mutation with real Basket/Catalog data and controlled unavailable-dependency behavior.
- Address-book selection, checkout review, immutable order snapshots, idempotent submit.
- Sandbox hosted payment redirect or approval, verified callback, duplicate/late callback handling, payment retry where policy permits.
- Customer account: profile basics, address book, order list/detail, payment/order timeline, eligible cancellation request.
- Operations: order search/detail, payment exception visibility, fulfillment work queue only after a real backend workflow exists.
- Responsive, accessible storefront with loading, empty, validation, unauthorized, error, retry and success states.

### Out of scope for P0

- Guest checkout, marketplace, loyalty, returns/restock, partial refund, multi-warehouse, split shipment, customer-service chat, stored card data, manual direct database operations.

## 3. Ownership And Rules

| Decision or data | Owner | UI rule |
| --- | --- | --- |
| Catalog content and display price | Catalog | Display current value; checkout uses snapshot returned by Ordering. |
| Mutable cart | Basket | Cart is never a reservation or order proof. |
| Address book | Identity | Customer can select only owned active addresses. Ordering stores an immutable copy. |
| Order lifecycle and cancellation policy | Ordering | Customer sees only own order; terminal decisions come from server. |
| Stock reservation and availability | Inventory | Do not promise stock until checkout/server result. |
| Coupon validation/reservation | Discount | UI shows server response, not locally calculated discount truth. |
| Provider session and payment state | Payment | Redirect is not payment success; verified callback is authoritative. |
| Shipment/tracking | Fulfillment module in Ordering initially | Hide until persistence, API, event, authorization and operations workflow exist. |

All side-effecting requests require idempotency. All cross-service events require event-id deduplication and correlation metadata. Financial state changes must have an audit record.

## 4. Experience Specifications

### S1 - Storefront discovery

**Persona:** Customer

- Home/catalog shows only sellable products from Catalog API, with image fallback, current price, availability advisory, loading skeleton, empty result, retryable failure.
- Search/filter/sort state is represented in URL and survives reload/share.
- Product detail has image/media, description, price, availability advisory, quantity control and add-to-cart feedback from server-confirmed cart state.

**Acceptance:** a signed-in customer and a visitor can browse; a failed catalog call presents a clear retry state with no invented products or prices.

### S2 - Cart and checkout

**Persona:** Customer

- Cart panel/page supports quantity update, remove, clear, subtotal and product availability revalidation from real APIs.
- Checkout requires an authenticated session, non-empty cart and owned selected address.
- Review explicitly states that final price, coupon and stock are verified by the server.
- Submit sends an idempotency key. Double click, refresh, retry and network ambiguity must not create a second order.
- On validation failure, preserve form/cart context and identify the action needed.

**Acceptance:** duplicate submit returns the original order; invalid address/coupon/unavailable stock produces controlled errors; no UI success is shown before Ordering succeeds.

### S3 - Payment

**Persona:** Customer, Payment operations

- Customer starts payment only for an eligible order and sees provider, amount, currency and current payment status from Payment.
- Sandbox flow uses a provider session/reference; return/cancel pages re-fetch server state and never trust query parameters as payment truth.
- Webhook endpoint verifies signature, tolerates duplicate and late delivery, writes audit/outbox evidence, and drives the order saga exactly once.
- Customer may retry only when payment/order policy allows it. Payment actions have stable error codes and support correlation IDs.

**Commercial release gate:** real provider credentials, public HTTPS callback, signed callback test, duplicate callback test, late/out-of-order callback test, reconciliation drill, and audited void/refund workflow all pass.

### S4 - Customer account and order history

**Persona:** Customer

- Account has profile/session controls, address book and order history from real APIs.
- Order detail includes immutable item, money and address snapshots; order timeline; payment state; retry/cancel controls only when API reports eligibility.
- Empty account history, unauthorized access, expired session and unavailable read model are distinct states.

**Acceptance:** a customer cannot retrieve another customer order by changing URL; stale query projection is identified as pending/retry rather than fabricated data.

### S5 - Operations order handling

**Persona:** Operations, Support, Administrator

- Operations read surface searches/filter orders with RBAC and correlation/business IDs.
- Payment exception actions are backend-audited commands, never browser-only status mutation.
- Fulfillment UI is blocked until its backend slice exists: paid-order queue, shipment aggregate, status history, tracking contract, authorization and audit.

**Acceptance:** support is read-only by default; staff action produces an audit entry and visible result from a server reload.

## 5. Required UI State Matrix

Every customer or operations screen must define and implement: initial loading, refresh/loading mutation, empty, validation error, authorization failure, dependency failure, retry, success confirmation, and stale/eventual-consistency notice where applicable. Mobile, keyboard navigation, focus handling, semantic labels and contrast are part of acceptance.

## 6. Nonfunctional Requirements

- BFF is the browser boundary; browser does not call internal services or broker endpoints.
- Do not expose secrets, internal IDs beyond needed customer references, stack traces, or payment webhook details.
- Preserve `X-Correlation-ID` through Gateway/BFF/service/event paths.
- Measure checkout creation failures, payment callback failures/duplicates, order-query lag, cart dependency failures and frontend errors.
- Performance target for portfolio: key pages remain usable on a normal mobile network; costly catalog queries use pagination/cursor and optimized media.

## 7. Release Acceptance

The P0 release passes only when the scenarios in `docs/governance/quality-gates.md` have evidence, the critical E2E journey passes through Storefront BFF and Gateway, and the team has verified success, failure, duplicate, retry, cancellation and dependency-outage paths. A deploy tag, rollback target and demo account/data set must be recorded.