# Feature Specification Template

Use this template before implementation. Keep it short, concrete, and testable.

## Identity

- Feature ID:
- Owner:
- Status: Draft | Approved | Implemented | Released
- Related ADRs:
- Target release:

## Problem And Outcome

- Customer or operator problem:
- Primary persona:
- Observable outcome:
- Success metric:

## Scope

### In scope

-

### Out of scope

-

## Business Rules

- Source of truth and bounded-context owner:
- State transitions and terminal states:
- Idempotency key or deduplication rule:
- Authorization and data ownership:
- Failure, timeout, retry, and compensation behavior:
- Audit and retention requirement:

## Experience And Contracts

- User entry point and supported devices:
- Loading, empty, validation, error, retry, and success states:
- API or event contracts changed:
- Backward compatibility and versioning:
- Accessibility and localization considerations:

## Nonfunctional Requirements

- Availability and latency target:
- Consistency model:
- Rate limits or abuse controls:
- Logs, metrics, traces, and alerts:
- Secret or personal-data handling:

## Acceptance Scenarios

Write executable examples.

1. Given ... when ... then ...
2. Given ... when ... then ...
3. Given a duplicate request or event ... when ... then ...
4. Given a dependent service is unavailable ... when ... then ...

## Verification And Release

- Unit/integration/E2E tests:
- Manual smoke evidence:
- Migration and backfill plan:
- Feature flag or rollout plan:
- Rollback plan:
- Dashboard, runbook, and alert updates: