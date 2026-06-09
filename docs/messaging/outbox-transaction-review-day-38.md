# Day 38 Outbox Transaction Review

## Reviewed Flow

```text
OrderingService checkout
Order repository insert
Outbox message insert
Outbox publisher
RabbitMQ publish
NotificationWorker consume
```

## Transaction Boundary

| Step | Current behavior | Expected standard | Gap |
| --- | --- | --- | --- |
| Save order | `CheckoutHandler` calls `_orderRepository.CreateAsync(order, transaction, cancellationToken)` inside `_unitOfWork.ExecuteAsync` | Inside DB transaction | No gap found |
| Insert outbox | `CheckoutHandler` calls `_outboxRepository.AddAsync(outboxMessage, transaction, cancellationToken)` in the same operation | Same transaction as order write | No gap found |
| Commit | `DapperOrderingUnitOfWork` commits after operation succeeds and rolls back on exception | Commit after order and outbox succeed | No gap found |
| Broker publish | Background publisher reads outbox messages after commit | Publish after transaction commits | Matches pattern |

## Current Outbox Schema

```text
Id
OccurredAtUtc
Type
Content
NextAttemptAtUtc
ProcessedAtUtc
RetryCount
LastError
LockId
LockedUntilUtc
```

Current notes:

```text
No separate Status column yet.
Status is inferred from ProcessedAtUtc, RetryCount, LastError, LockId, and LockedUntilUtc.
No separate CreatedAtUtc column yet.
OccurredAtUtc is currently the event/outbox occurrence timestamp.
```

## Current Publisher Strengths

```text
ClaimPendingAsync claims batches.
FOR UPDATE SKIP LOCKED avoids multiple publishers claiming the same rows.
RetryCount and LastError are tracked.
NextAttemptAtUtc supports retry scheduling.
LockId and LockedUntilUtc support leasing.
MarkAsProcessedAsync and MarkAsFailedAsync update state after publish attempt.
```

## Remaining Gaps

```text
Inbox/processed-event tracking is not implemented.
Payment webhook log is not implemented.
Consumer idempotency still needs deeper review.
Monitoring stuck outbox messages is future work.
Kafka publisher from OrderingService is intentionally not implemented.
```
