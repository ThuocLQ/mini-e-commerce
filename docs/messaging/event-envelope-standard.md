# MicroShop Event Envelope Standard

## Goal

Events should have consistent metadata for tracing, versioning, replay, and debugging.

## Target Envelope Fields

```text
eventId
eventType
eventVersion
source
occurredAtUtc
correlationId
causationId
subject
data
```

## CloudEvents Mapping

| MicroShop field | CloudEvents concept |
| --- | --- |
| eventId | id |
| eventType | type |
| source | source |
| occurredAtUtc | time |
| subject | subject |
| data | data |

## Event Type Naming

Recommended style:

```text
com.microshop.order.created.v1
com.microshop.order.paid.v1
com.microshop.order.cancelled.v1
```

## Compatibility Rules

```text
Add optional fields when possible.
Do not rename fields without versioning.
Do not change field types without versioning.
Do not remove fields without a migration plan.
Document event consumers before changing producer payloads.
```

## Current Stage

Day 37 adds the shared `MicroShopEventEnvelope<TData>` contract in `BuildingBlocks.Contracts`.

Runtime RabbitMQ and Kafka payloads are not migrated yet.
