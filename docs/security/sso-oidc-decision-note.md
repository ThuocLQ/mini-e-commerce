# SSO / OIDC Decision Note

## Current Decision

MicroShop currently keeps `IdentityService` with local JWT authentication for learning and service-boundary practice.

## Production Direction

Production enterprise systems commonly delegate authentication to an external Identity Provider:

```text
Keycloak
Microsoft Entra ID
Auth0
Okta
```

Future direction:

```text
Use OIDC with an external Identity Provider.
Let the gateway validate external tokens where appropriate.
Let services enforce authorization policies for their own protected actions.
Keep claims small and explicit.
```

## Not Implemented Today

```text
Keycloak integration.
Microsoft Entra ID integration.
Auth0 or Okta integration.
Refresh token production flow.
Full RBAC/ABAC implementation.
```
