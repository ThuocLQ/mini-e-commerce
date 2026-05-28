# MongoDB Read Model Notes

## Purpose

MongoDB is used as the read-model store for order summaries. It does not replace the OrderingService PostgreSQL write database.

## Service Boundary

`OrderQueryService` owns read APIs:

- `GET /order-summaries`
- `GET /order-summaries/{orderId}`
- `POST /debug/order-summaries` in Development only

`OrderingService` remains the write-side service for checkout/order creation.

## Collection

Database:

```text
MicroShop_OrderReadDb
```

Collection:

```text
order_summaries
```

## Document Shape

Each order has one summary document. The MongoDB `_id` is the order id string, which makes projection upsert idempotent by default.

Important fields:

- `orderId`
- `customerId`
- `customerName`
- `status`
- `totalAmount`
- `currency`
- `itemCount`
- `items`
- `createdAtUtc`
- `lastUpdatedAtUtc`

## Indexes

`OrderQueryService` initializes indexes on startup:

- `UX_order_summaries_orderId`
- `IX_order_summaries_createdAtUtc_desc`
- `IX_order_summaries_customerId_createdAtUtc_desc`

These match the query patterns for lookup by order id and latest summaries.

## Production Notes

Read models are eventually consistent. The write-side order can exist before the MongoDB summary appears.

MongoDB read models should be rebuildable from Kafka events later. Lesson 27 will add a ProjectionWorker that consumes `microshop.order-events` and calls `UpsertAsync`.
