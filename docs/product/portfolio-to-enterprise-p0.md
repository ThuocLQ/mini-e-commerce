# Portfolio -> ecommerce enterprise: P0 executable backlog

## Mục tiêu và phạm vi

Đưa portfolio hiện tại thành một luồng mua hàng đáng tin cậy cho khách đã đăng nhập: xem sản phẩm, giỏ hàng, checkout, thanh toán, giao hàng, hủy/hoàn tiền, địa chỉ và thông báo. P0 không mở rộng thành marketplace, guest checkout, đa kho, partial shipment/refund hay loyalty.

**Nguyên tắc:** `Ordering` là nguồn sự thật của đơn và điều phối vòng đời; `Inventory` là nguồn sự thật duy nhất của khả dụng/tồn kho; `Payment` là nguồn sự thật của giao dịch nhà cung cấp; các service chỉ giao tiếp qua public API đã version hoá hoặc integration event outbox/inbox. Tất cả command có hiệu ứng phải idempotent theo `Idempotency-Key`/`eventId`, và tất cả tiền/giá/địa chỉ trên đơn phải là snapshot bất biến.

## Hiện trạng đã kiểm chứng

| Flow | Có | Chưa đủ/chưa có |
| --- | --- | --- |
| Browse | Storefront công khai gọi `GET /catalog/products`; Catalog có list/detail/search/price-range và snapshot tồn kho. | `IsActive` tồn tại trong domain nhưng không có trong DTO/query nên sản phẩm inactive vẫn có thể hiển thị; chưa có pagination, media/canonical availability, cache/read model. |
| Cart | Basket Redis có CRUD, version, kiểm tra sở hữu theo JWT; BFF chỉ proxy basket của session. | Không có cart guest/merge, quote expiry hay re-price UI; cart không phải reservation. |
| Checkout | `POST /orders/checkout` kiểm tra basket id/version, chụp lại Catalog price, giữ coupon + inventory 30 phút, transaction Order + Rabbit/Kafka outbox, và clear basket có điều kiện. | Chưa snapshot địa chỉ/ship method/tax; gọi reservation đồng bộ và compensation khi persist lỗi là best-effort; không có trạng thái checkout/payable expiry rõ ràng cho khách. |
| Stock | Inventory khóa row, `Reserved -> Committed/Released`, outbox availability + settlement; worker release expiry 1 phút. | Reservation expiry không bắt Ordering chuyển đơn sang hết hạn/hủy; lệnh release/commit sau terminal state ném lỗi; không có stock allocation/fulfillment integration. |
| Payment | Một payment/order, webhook HMAC + dedup log/outbox; authorization -> inventory commit -> capture, và void/refund command qua RabbitMQ. | Chưa có provider adapter/payment intent/redirect; không có public cancel/refund request; UI chỉ tạo payment; expiry và late callback cần reconciliation rõ ràng. |
| Fulfillment | `Confirmed`, `Shipped`, `Delivered` chỉ là enum OrderStatus. | Không service, API, shipment, transition, quyền vận hành, hoặc event. |
| Account/address | Identity có register/login/me và role. | Không profile/contact, address book, consent/preferences; không thể lưu snapshot shipping/billing. |
| Notification | NotificationWorker nhận `OrderCreated` idempotently. | Sender chỉ log; không event template cho paid/shipped/cancel/refund, preference hay delivery audit. |

## Ownership và API boundary

| Bounded context | Owns | Public API (qua Gateway/BFF) | Internal/event contract |
| --- | --- | --- | --- |
| Catalog | Product sellability, catalog price/media, catalogue availability projection | `GET /catalog/products`, `/{id}`, `search` | Product snapshot lookup for Ordering; publish `ProductAvailabilityChanged` only when public availability changes. |
| Basket | Mutable cart theo customer, version | `/cart/{customerId}` CRUD (BFF che `customerId`) | Ordering đọc + conditional clear; không giữ stock. |
| Customer Profile (mới) | Customer contact, address book, notification preferences | `/me`, `/me/addresses`, `/me/notification-preferences` | Ordering reads selected address then snapshots it; never share profile table/database. |
| Ordering | Order aggregate, immutable line/price/address snapshots, cancellation decision, customer order view | `/orders`, `/orders/{id}`, `/{id}/cancel`, `/{id}/refund-requests` | Private order lookup/payment-saga API; outbox `OrderCreated`, `OrderStatusChanged`, `OrderCancelled`, `RefundRequested`, `FulfillmentRequested`. |
| Inventory | On-hand, reserved, committed inventory | Admin inventory only | Internal reserve/release/commit; events `InventoryCommitted`, `InventoryReleased`, availability changed. |
| Payment | Payment and provider references, auth/capture/void/refund state | `POST /payments` (returns provider session), `GET /payments/{id}`; webhook only at `/webhooks/payment` | Events `PaymentAuthorized/Captured/Voided/Refunded/Failed`; consumes capture/void/refund requests. |
| Fulfillment (mới) | Fulfillment/shipment aggregate, carrier/tracking, dispatch/delivery | Customer order shipment view; operations shipment commands | consumes `FulfillmentRequested`; publishes `FulfillmentConfirmed/Shipped/Delivered/Failed`. |
| Notification | Delivery attempt/audit only | Preference is Profile public API, not Worker | consumes lifecycle events and emits no order decision. |

Do not expose `/_internal/*`, service databases, or RabbitMQ commands through Gateway. Public errors use ProblemDetails with stable codes (`OUT_OF_STOCK`, `CHECKOUT_EXPIRED`, `INVALID_TRANSITION`, `PAYMENT_PENDING`, `CANCEL_NOT_ALLOWED`) and include a correlation id. Internal events carry `eventId`, `occurredAtUtc`, `correlationId`, `causationId`, `schemaVersion`; consumers maintain an inbox/dedup record.

## Target state machines

### Cart and checkout

```text
Cart: Active --mutate--> Active(version+1) --conditional checkout clear--> Cleared
Order: CheckoutRequested -> PendingPayment -> PaymentAuthorized -> InventoryCommitted
       -> PaymentCapturePending -> Paid -> Confirmed -> Shipped -> Delivered
       PendingPayment/PaymentAuthorized/PaymentCapturePending --cancel/expire/fail--> Cancelled
       Paid/Confirmed/Shipped --approved refund--> RefundPending -> Refunded
```

`POST /orders/checkout` contract: `{basketId,basketVersion,addressId,shippingMethod,couponCode?}` + required `Idempotency-Key`. Ordering verifies ownership and basket version; gets current sellable product snapshots; validates selected address; computes/locks total; creates an order with `PaymentDeadlineUtc` and an inventory reservation **whose expiry is the same deadline**. A replay with the same request returns the existing order; same key with a different canonical request is `409`.

Only customer cancel is allowed before warehouse handoff; after `Paid`, Ordering creates a refund request rather than directly changing to cancelled. Every transition is a compare-and-set from allowed predecessor states, writes a status event in the same transaction, and is replay-safe.

### Reservation, payment, fulfillment and compensation

```text
reserve(order, lines, deadline) -> Reserved
payment authorized -> commit reservation -> InventoryCommitted -> request capture
capture success -> Paid -> request fulfillment -> Confirmed -> Shipped -> Delivered
payment fail/timeout/customer cancel -> release reservation -> Cancelled
late authorization/capture after cancel -> void/refund; terminal only after provider outcome
refund approved -> RefundPending -> payment refunded -> Refunded
```

Reservation contract is private/idempotent: `ReserveInventory(orderId, lines[], expiresAtUtc, commandId) -> {state, failureCode}`; `Release/Commit(orderId, commandId, reason) -> {state}`. `Released` and `Committed` must be idempotent no-ops for the same target state, not failures. Expiry publishes `InventoryReleased(reason=Expired)`; Ordering consumes it, atomically marks an unpaid order `Cancelled/Expired`, releases promotion, and rejects/voids subsequent payment. A late provider success must always trigger durable compensation and operational alerting.

Payment state: `PendingAuthorization -> Authorized -> CapturePending -> Captured`, with branches `Authorized/CapturePending -> VoidPending -> Voided`, `Captured -> RefundPending -> Refunded`, and `PendingAuthorization -> Failed`. `POST /payments` must return a provider session/next action, never imply paid. Provider callback is the only source for provider completion; authenticate it, deduplicate provider event id + payload hash, and tolerate out-of-order events through a recorded transition/reconciliation queue.

Fulfillment may start only after `Paid`. It owns `Requested -> Confirmed -> Shipped -> Delivered` (and `Failed/Cancelled` before ship), while Ordering projects those outcomes to its order status. It must never decrement stock; Inventory was committed before capture.

### Flow contract matrix

| Flow | Customer/operations contract | Required state/guarantee |
| --- | --- | --- |
| Browse product | `GET /catalog/products?query=&cursor=` and `GET /catalog/products/{id}` return only sellable DTO `{id,name,description,media,price,currency,availability}`. | `Draft -> Active -> Inactive`; only `Active` is browsable/addable. Catalog display availability is advisory; Inventory decides checkout. |
| Cart | Authenticated BFF maps current user to `GET/POST/PUT/DELETE /cart/{customerId}`; every response returns `{basketId,version,items,total}`. | `Active(vN)` only; a mutation increments version. No reservation and no payment/order side effect. |
| Checkout | `POST /orders/checkout` requires basket + address + shipping method and key; returns order, deadline and next payment action. | At-most-one canonical order per key/basket version; immutable snapshots; retryable `409` for stale basket/quote. |
| Reservation/release | Private Inventory command contains order, lines, deadline and command id; response exposes state/failure code, never raw stock rows. | `Reserved -> Committed` exactly once or `Reserved -> Released/Expired` exactly once; repeating target command succeeds idempotently; committed stock cannot be released. |
| Payment | Customer creates/retrieves a provider session; provider posts signed callback; support staff only uses audited operations commands. | Provider state machine above; callbacks are deduplicated and transition only valid predecessor states. |
| Fulfillment | Operations creates/confirms shipment then records ship/deliver with tracking; customer gets read-only shipment detail. | `Requested -> Confirmed -> Shipped -> Delivered`; `Cancelled/Failed` only pre-ship; command has idempotency key. |
| Cancellation/refund | Customer cancellation/refund request has `{reason}`; operations approval may add `{decision,reason}`. | Unpaid cancel releases; paid path is `RefundPending -> Refunded`; no cancellation after shipping without an explicit returns policy (out of P0). |
| Account/address/notification | Profile APIs manage address and channel preference; order receives selected address snapshot. Notification event uses `{eventId,orderId,template,channel,recipientSnapshot}`. | Address `Active/Archived` (archived cannot be newly selected); delivery `Queued -> Sending -> Sent | RetryableFailure | DeadLetter`; a delivery record unique on event/template/channel prevents duplicates. |

## Customer-demo implementation sequence

**Entry gate (complete before the slices below):** enforce authenticated BFF/Gateway ownership checks, same-origin mutation protection, rate limits and signed webhook verification; add inbox/outbox metrics and repair runbook. Close reservation lifecycle first: reservation expiry must emit a durable outcome consumed by Ordering, unpaid order must become non-payable, and repeated reserve/release/commit must be idempotent. No slice below may use a placeholder success, local-only state, or a UI control that has no real API outcome.

### 1. Account and address (first customer-visible slice)

* **Owner:** new `CustomerProfileService` owns profile/contact, address book and channel preference in its own database; `IdentityService` remains authentication/token issuer. Ordering obtains an owned address only through a private Profile query and stores a snapshot—never a foreign key to Profile data.
* **API/events:** Gateway+BFF expose `GET/PATCH /me/profile`, `GET/POST/PATCH/DELETE /me/addresses`, and `PUT /me/addresses/{id}/default`; private `GET /_internal/customers/{customerId}/addresses/{addressId}` is service-to-service only. No domain event is needed for an address change in P0 because existing orders are immutable; publish an audit event only if operations requires it.
* **Migrations:** Profile DB: `CustomerProfiles`, `CustomerAddresses` (status, normalized country/postal code, default uniqueness), `NotificationPreferences`, and audit timestamps/version. Add Profile outbox/inbox only if an event consumer is introduced.
* **UI:** real **Account** page/dialog with contact details, address create/edit/archive/default, validation errors from API, and persisted notification toggles. Checkout reads this data; it cannot render a free-text fake address form.
* **Acceptance:** a customer can create/select one owned active address after refresh; another customer's address is `404`; archived addresses remain in prior order snapshots but cannot be selected; the default is deterministic under concurrent updates; API, BFF and browser E2E prove JWT ownership.

### 2. Checkout quote, discount and shipping

* **Owner:** `OrderingService` owns quote orchestration, final total, deadline and order snapshots; `DiscountService` owns coupon eligibility/reservation; `CatalogService` supplies sellable price snapshots; `InventoryService` is final stock authority. For the demo, shipping policy lives in Ordering (configured, postcode/method based) rather than a fake Shipping service.
* **API/events:** add `POST /checkout/quotes` with `{basketId,basketVersion,addressId,shippingMethod,couponCode?}` and `GET /checkout/quotes/{id}`; return lines, subtotal, discount, shipping, tax, grand total, currency and `expiresAtUtc`. Change checkout to accept `{quoteId}` + `Idempotency-Key`. Emit `OrderCreated` only after order/reservation persistence; emit `OrderCheckoutExpired`/`OrderCancelled` when deadline/reservation expiry wins. Keep existing promotion reserve/redeem/release and inventory settlement contracts with event versioning.
* **Migrations:** Ordering DB: `CheckoutQuotes` + line/adjustment snapshots, quote expiry/index/idempotency hash; extend orders with shipping/billing address JSON snapshot, shipping method/amount, tax/total, `PaymentDeadlineUtc`, quote id and status history. Add a reconciliation table for reservation/payment expiry if not represented by the saga. Discount migration only if its reservation must bind `quoteId` in addition to order.
* **UI:** cart's real **Checkout** step loads addresses, selects method, requests/reloads a quote, shows every amount and expiry, then submits exactly that quote. Payment action is shown only from the payment provider session returned for the created order; stale quote, coupon rejection and out-of-stock states keep the user in checkout with actionable retry.
* **Acceptance:** price/coupon/shipping changes invalidate or replace the quote; a quote retry creates one order and one reservation; invalid/foreign address, stale basket and expired quote are rejected; inventory expiry changes the order to non-payable; an authenticated E2E completes checkout through provider sandbox/webhook without direct database mutation.

### 3. Fulfillment lifecycle and order tracking

* **Owner:** new `FulfillmentService` owns fulfillment, shipment, carrier/tracking and operational transitions. Ordering remains the customer order state and consumes fulfillment outcomes; Inventory is never called by fulfillment.
* **API/events:** Ordering outbox publishes `FulfillmentRequested` after `Paid`; Fulfillment consumes it idempotently and publishes `FulfillmentConfirmed`, `ShipmentShipped`, `ShipmentDelivered` (or `FulfillmentFailed`). Operations APIs: `GET /operations/fulfillments`, `POST /{orderId}/confirm`, `/ship`, `/deliver`; customer read: `GET /orders/{id}/fulfillment` (or enrich owned order DTO). Every command needs an idempotency key and expected version.
* **Migrations:** Fulfillment DB: `Fulfillments`, `Shipments`, `ShipmentEvents`, `ProcessedMessages`, `OutboxMessages`; Ordering adds fulfillment projection fields/status-history cause if needed. Store carrier and tracking as factual operation data, not seeded UI labels.
* **UI:** Operations portal gets a paid-order queue and working confirm/ship/deliver controls with tracking entry; Storefront order detail shows actual status, carrier and tracking only after shipment exists.
* **Acceptance:** only a paid order produces one fulfillment; duplicate paid event/command creates no duplicate shipment; ship requires confirm and tracking; customer cannot view another order's shipment; fulfillment event updates customer-visible order state through the normal outbox path.

### 4. Cancellation and refund

* **Owner:** Ordering owns customer request/policy and terminal order decision; Inventory releases an unpaid reservation; Discount releases/redeems its reservation; Payment owns void/refund provider actions and final payment state. Operations approves exceptional refund requests.
* **API/events:** `POST /orders/{id}/cancel` and `/refund-requests` with `{reason}`; operations `POST /operations/refund-requests/{id}/approve|reject`. Emit `OrderCancellationRequested`, `OrderCancelled`, `RefundRequested`, `RefundApproved/Rejected`; consume existing `PaymentVoided`, `PaymentRefunded`, `PaymentOperationFailed`, `InventoryReleased`. No public endpoint may invoke Payment's internal refund command.
* **Migrations:** Ordering DB: `OrderCancellationRequests`, `OrderRefundRequests`, immutable reason/actor/decision timestamps, transition/status history and idempotency key; payment operation correlation is unique per order/refund request. No stock restock in P0—returns policy is explicitly later.
* **UI:** Storefront order detail exposes Cancel only while server says eligible and displays submitted/refund state; Operations portal exposes a real approval queue with audit reason. Both derive buttons/status from GET order/request data, never local optimistic terminal state.
* **Acceptance:** unpaid cancellation releases inventory/promotion once; paid cancellation produces exactly one provider refund request after approval; duplicate, late and failed provider callbacks do not double refund; shipped order cancellation is denied with a stable error; customer history reflects the final webhook outcome.

### 5. Lifecycle notifications

* **Owner:** `NotificationWorker` owns delivery orchestration/audit; `CustomerProfileService` owns recipient contact and preferences; Ordering/Fulfillment/Payment own the lifecycle events that trigger messages.
* **API/events:** consume versioned `OrderCreated`, `OrderStatusChanged`, fulfillment and refund outcomes; resolve a recipient snapshot at event time, then persist `NotificationDelivery` and send through a configured email provider adapter. Profile APIs from slice 1 are the only preference surface; expose no direct public send endpoint.
* **Migrations:** Notification DB (replace Redis-only processed marker for durable demo): `ProcessedEvents`, `NotificationDeliveries` (unique event/template/channel), `DeliveryAttempts`, template version/config reference, retry/dead-letter metadata. Keep an outbox only if the sender itself emits a business event.
* **UI:** Account preferences displays actual persisted channel choices; storefront order detail shows business status, not a fabricated "email sent" badge. Operations may display delivery audit/read-only failure reason.
* **Acceptance:** an opted-in customer receives sandbox-provider email for placed, paid, shipped, cancelled and refunded events; a duplicate event sends once; opt-out suppresses permitted channels; provider failure retries then dead-letters without changing order/payment state; audit ties each delivery to correlation/event id.

**Slice exit rule:** implement and demonstrate slices in the order above. Each has API contract tests, migration upgrade tests, outbox/inbox duplicate and restart tests, authorization tests, and one browser E2E that calls real APIs. Only then expose its UI surface in the customer-demo profile.

## P0 vertical slices and acceptance criteria

1. **P0-1 — Sellable browse and cart quote.** Catalog returns only active, purchasable products with price, media and `availableQuantity`/availability flag; Storefront supports search/detail and an explicit out-of-stock state. Cart mutation remains versioned and accepts only sellable products. **Accept:** inactive/out-of-stock SKU cannot be added or checked out; a price changed after add-to-cart is re-quoted at checkout and shown before confirmation.

2. **P0-2 — Addressed, durable checkout.** Add Profile/Address API and immutable order `shippingAddress`, method, tax/total snapshots and `paymentDeadlineUtc`. Make checkout idempotent end-to-end and persist a durable checkout/reservation intent so a crash can reconcile release/commit. **Accept:** only an owned validated address can be selected; retry produces one order/reservation; basket changes yield `409`; persistence/remote failure leaves no leaked reservation or is visible in reconciliation.

3. **P0-3 — Reservation expiry and real payment handoff.** Close the current expiry gap: `InventoryReleased(Expired)` cancels the unpaid order; create an actual provider session; webhook-driven authorization/commit/capture and all commands are inbox/outbox idempotent. **Accept:** expiry frees stock and order cannot be paid; duplicate/out-of-order callbacks create no double charge/stock movement; late success becomes void/refund + alert; customer sees payment state and retry guidance.

4. **P0-4 — Customer cancellation and refund.** Add authenticated `POST /orders/{id}/cancel` and `/refund-requests` with reason, transition policy and operations approval where required. **Accept:** unpaid cancellation releases stock/promotion once; paid cancellation produces one refund request, never a direct state overwrite; refund webhook moves order to `Refunded`; invalid transitions return the documented code.

5. **P0-5 — Fulfillment and customer order tracking.** Introduce Fulfillment aggregate/API, operations pick/pack/ship commands and carrier/tracking snapshot; consume paid event only. **Accept:** no shipment is created for unpaid/refunded/cancelled order; operations can progress valid states exactly once; customer sees shipment/tracking and ordered history is consistent with write-side status.

6. **P0-6 — Account contact and lifecycle notification.** Store verified contact + address/preference ownership in Profile; replace logging sender with audited provider adapter and idempotent delivery records for placed, paid, shipped, cancelled, refund states. **Accept:** opt-out is honored by channel (except transactional policy as defined); duplicate events do not duplicate sends; a provider failure retries/dead-letters without changing order state.

## Architectural risks / release gates

* **Block P0 release:** current reservation expiry can leave `PendingPayment` alive; a later authorized payment then cannot safely commit previously released stock. Implement P0-3 before accepting real money.
* `OrderStatus` includes fulfillment states but no commands or legal transition guards; do not let an operations UI mutate status directly.
* Current `PaymentService` has webhook reliability but no provider-session/redirect adapter, so the current “Start secure payment” is only a pending record, not a payment experience.
* Current checkout compensates remote reservations after local persistence failure on a best-effort call; add intent/reconciliation and dashboards for dangling reservation, saga timeout, outbox/inbox lag and compensation age.
* The catalog currently exposes a stock snapshot, while Inventory is authoritative. Treat it as eventually consistent display information; checkout must remain the final stock decision.
* `NotificationWorker` is safely deduplicated but is logging-only and Identity lacks customer contact data; notification cannot be claimed as delivered until P0-6.

**Definition of P0 done:** each slice has consumer-driven contract tests, integration tests for duplicate/out-of-order/timeout paths, a storefront E2E journey, correlated audit logs/metrics, and an operations runbook for stuck payment, reservation and fulfillment messages.
