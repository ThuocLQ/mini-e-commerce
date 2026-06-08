# Specification Pattern Lite / Query Criteria

## Goal

Query rules should be easy to name, reuse, and test without scattering filter logic across endpoints and repositories.

## What To Use In MicroShop Today

Use a lightweight query criteria object for simple Dapper queries.

Example:

```csharp
public sealed record ProductQueryCriteria(
    string? SearchTerm = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null);
```

## When To Use

```text
Multiple query routes share filter rules.
Endpoint bodies start accumulating filter logic.
Repository methods multiply with similar query combinations.
The query intent needs a clear name.
```

## When Not To Use

```text
The query is a simple GetById.
The abstraction becomes more complex than direct SQL.
There is no duplicated query behavior yet.
```

## Dapper Rule

For Dapper repositories, the criteria object expresses intent. The repository still owns SQL translation.

Do not introduce expression-tree specifications or a generic framework unless the project has enough query complexity to justify it.
