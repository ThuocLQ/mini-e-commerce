# MicroShop Audit Log Policy

## Goal

Security-sensitive and business-critical actions should be auditable without leaking secrets or payment-sensitive data.

## Candidate Audit Events

```text
User login success.
User login failure.
Token refresh if implemented later.
Order checkout.
Payment created.
Payment webhook received.
Payment status changed.
Admin/catalog product mutation.
Discount mutation.
```

## Audit Entry Fields

```text
AuditId
OccurredAtUtc
ActorUserId
Action
EntityType
EntityId
Result
SourceIp if available
CorrelationId or TraceId
Metadata
```

## Rules

```text
Do not store raw passwords.
Do not log full JWT tokens.
Do not log sensitive payment details.
Prefer append-only audit records.
Keep audit records separate from normal application logs.
Make audit writes observable and failure-aware when implemented.
```

## Current Stage

Day 36 documents the policy only. Audit storage and write paths are future work.
