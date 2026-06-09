# Day 36 Strategy Pattern Review

## Reviewed Areas

| Area | Current state | Decision |
| --- | --- | --- |
| DiscountService discount calculation | Already has `IDiscountStrategy`, `DiscountStrategyFactory`, `FixedAmountDiscountStrategy`, and `PercentageDiscountStrategy` | Review extension points and tests instead of redesigning |
| PaymentService provider processing | Single local/simple flow today | Future candidate for provider strategies |
| BasketService product validation communication | REST and gRPC comparison paths already exist | Treat as communication strategy demo; no refactor today |
| IdentityService auth provider | Local JWT today | Future OIDC/SSO direction |

## Criteria

Use Strategy Pattern when:

```text
There are multiple algorithms.
The behaviors share a stable interface.
Runtime selection is useful.
Testing becomes clearer.
Future providers are likely.
```

Avoid Strategy Pattern when:

```text
There is only one behavior.
The abstraction is more complex than direct code.
The project would create a fake framework before real variation exists.
```

## Day 36 Decision

```text
Do not force a generic strategy framework.
Keep DiscountService strategy implementation as the concrete example.
Document PaymentService provider strategy as future work.
Keep IdentityService local JWT while documenting future OIDC/SSO direction.
```

## Day 36 Code Hardening

`DiscountStrategyFactory` now indexes strategies by `DiscountType` and fails fast if more than one strategy is registered for the same type.

This keeps runtime selection explicit and prevents ambiguous discount calculation behavior.
