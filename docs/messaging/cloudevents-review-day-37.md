# Day 37 CloudEvents Review

## Reviewed Contracts

```text
BuildingBlocks.Contracts.Events.IntegrationEvent
BuildingBlocks.Contracts.Events.Orders.OrderCreatedIntegrationEvent
Kafka projection demo events: OrderCreated, OrderPaid, OrderCancelled
```

## RabbitMQ Contract Findings

Current `IntegrationEvent` base has:

```text
EventId
OccurredAtUtc
Version
```

Current `OrderCreatedIntegrationEvent` has:

```text
OrderId
CustomerId
TotalAmount
Currency
```

Gaps versus target envelope:

```text
No source.
No correlationId.
No causationId.
No subject.
No CloudEvents-style namespaced event type.
No nested data object.
```

## Kafka Demo Payload Findings

Current valid Kafka projection demo payload includes:

```text
eventId
eventType
orderId
customerId
customerName
totalAmount
currency
itemCount
items
occurredAtUtc
```

Gaps versus target envelope:

```text
No source.
No eventVersion.
No correlationId.
No causationId.
No subject.
Data is flattened instead of nested.
eventType is simple name, not namespaced.
```

## Decision

```text
Use CloudEvents as the reference model.
Document a MicroShop envelope standard.
Add a shared MicroShopEventEnvelope<TData> contract for future gradual adoption.
Review RabbitMQ and Kafka payloads separately.
Do not migrate runtime contracts on Day 37.
```
