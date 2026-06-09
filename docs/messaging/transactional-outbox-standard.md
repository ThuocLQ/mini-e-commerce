# MicroShop Transactional Outbox Standard

## Goal

Avoid losing integration events when business data is saved but broker publish fails.

## Core Rule

Business data and the outbox message must be saved in the same database transaction.

## Standard Flow

```text
1. Validate command.
2. Begin DB transaction.
3. Save business data.
4. Insert outbox message in the same DB transaction.
5. Commit transaction.
6. Background publisher reads pending outbox messages.
7. Publisher sends to broker.
8. Publisher marks message as processed or failed.
```

## Delivery Guarantee

```text
Outbox provides at-least-once delivery.
Consumers must be idempotent.
Exactly-once delivery is not guaranteed.
Distributed transactions are not used.
```

## Not In Day 38

```text
Kafka publisher from OrderingService.
Inbox implementation.
WebhookLog implementation.
Exactly-once semantics.
```
