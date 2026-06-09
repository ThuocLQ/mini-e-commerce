# Day 38 Transactional Outbox Backlog

## Transaction Boundary

```text
[ ] Keep CheckoutHandler transaction path covered by tests.
[ ] Document DapperOrderingUnitOfWork BeginTransaction/Commit/Rollback behavior in deeper architecture docs.
[ ] Add regression test for order insert + outbox insert atomicity later.
```

## Schema

```text
[ ] Document outbox table fields in API/runbook docs.
[ ] Decide whether a separate Status column is needed later.
[ ] Review pending outbox indexes under load.
```

## Day 39 Handoff

```text
[ ] Review existing publisher retry/failure states.
[ ] Review ClaimPendingAsync and FOR UPDATE SKIP LOCKED behavior.
[ ] Add advanced idempotency direction.
[ ] Add Inbox/processed messages direction.
[ ] Add WebhookLog for PaymentService direction.
[ ] Add monitoring for stuck/poison outbox messages later.
```
