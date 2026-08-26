---
name: microshop-frontend-design
description: Design and implement MicroShop customer or operations interfaces when building the Storefront, Operations Portal, or shared UI. Use it to keep ecommerce workflows concrete, accessible, and verified rather than producing generic AI-looking screens.
---

# MicroShop Frontend Design

Build a calm, reliable ecommerce product interface. The interface is evidence of a real workflow, not a marketing mockup. Begin by reading the relevant Gateway route, request/response contract, authorization rule, and current backend state before designing the screen.

## Product Boundaries

- `Storefront` serves customers: catalog discovery, product decision, basket, checkout, payment state, and their own orders.
- `Operations Portal` serves staff: product, stock, promotion, order, payment, and fulfillment exceptions. Optimize it for scanning and repeated action, not visual promotion.
- The frontend calls only `ApiGateway`. It never calls service containers, brokers, databases, or internal/debug routes directly.
- Never render a business success state before the API confirms it. Payment and order states must reflect the backend state machine and communicate pending/eventual-consistency states plainly.
- Treat the current payment flow as sandbox-only until a real PSP adapter is explicitly added. Do not place webhook secrets, internal API keys, or privileged mock controls in browser code.

## Workflow-First Design

For each feature, define the primary user action, its confirmation, recoverable failure, and return path before choosing layout or styling.

Customer workflows must cover:

1. Catalog: loading, no results, unavailable product, price, stock availability, and product detail.
2. Basket: add, adjust quantity, remove, an empty state, stock/price changes, and an actionable error.
3. Authentication: validation, incorrect credentials, forbidden ownership, and session-expired recovery.
4. Checkout: idempotent submit, in-progress lock, controlled inventory/coupon failure, payment pending, and final order reference.
5. Orders: a human-readable status, timestamp, item snapshot, total, and clear note when the read model is still catching up.

Operations workflows must show who may perform an action, the impact of the action, the resulting state, and an audit-friendly confirmation. Destructive or irreversible actions need explicit confirmation and a server-confirmed result.

## Visual Direction

- Use a clear product-led storefront: real product media when available, otherwise an honest neutral product placeholder. Do not use vague stock imagery, decorative gradients, floating blobs, or fake analytics.
- Use a dense, restrained operations layout: persistent navigation, tables with stable columns, useful filters, compact status badges, and row-level actions.
- Prefer system typography and a small neutral palette with a purposeful accent. Do not make every surface a rounded card; page sections stay unframed unless content needs a real boundary.
- Use Lucide icons in icon buttons. Give every icon-only control an accessible label and tooltip.
- Use familiar controls correctly: icon buttons for compact tools, segmented controls for modes, inputs/steppers for quantity, menus for option sets, and buttons only for explicit commands.
- Keep text readable at mobile and desktop widths. Do not use viewport-scaled type, negative letter spacing, clipped labels, or overlapping controls.

## Interaction And Accessibility

- Every form field has a visible label, helpful validation, keyboard navigation, and a submitted/loading state.
- Preserve focus after dialogs, errors, or route transitions. Use native semantics before ARIA additions.
- Provide sufficient contrast and never rely only on color for order, payment, inventory, or validation state.
- Disable duplicate-submit controls while a mutation is in flight, while still preserving an understandable recovery path after timeout.
- Render API errors as user-safe messages. Log or display only a support trace identifier, never stack traces, access tokens, secrets, or raw downstream payloads.

## Frontend Architecture

- Keep Gateway calls behind a typed client/adapter. Components receive domain-shaped view models rather than constructing URLs or parsing raw HTTP payloads.
- Separate server-rendered catalog/read screens from client components that need form state or browser interaction. Keep client boundaries narrow.
- Do not persist sensitive credentials in `localStorage`. The portfolio adapter may keep a short-lived access token in memory only; production uses the approved gateway cookie/session design.
- Put environment-specific API URLs in public build configuration only when they are not secrets. Keep all secrets server-side.
- Add loading, error, empty, unauthorized, and unavailable states with every API-backed view.

## Completion Checklist

Before considering a UI slice complete:

1. Verify the route against a live or controlled API response; do not rely on mocked success alone.
2. Test desktop and mobile widths for overflow, focus order, legible labels, and meaningful empty/error states.
3. Exercise duplicate submission, unauthenticated access, and a downstream failure relevant to the feature.
4. Capture Playwright screenshots for the main workflow once browser testing is available.
5. Confirm the UI neither exposes internal infrastructure nor claims a business outcome that the backend did not persist.
