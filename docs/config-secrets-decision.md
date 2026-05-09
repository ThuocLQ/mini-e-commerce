# Config and Secrets Decision

## Why externalize configuration?

We externalize configuration so services can run in different environments without code changes.

## Current Configuration Sources

- appsettings.json: default non-sensitive settings.
- appsettings.Development.json: local development settings.
- User Secrets: local sensitive settings.
- Environment Variables: runtime overrides and deployment settings.

## Current Rules

- Do not hardcode URLs, ports, connection strings or secrets in code.
- Do not commit real secrets to Git.
- Use options classes for structured configuration.
- Use environment variables for container/Kubernetes deployment later.

## Local Development

- Rider/host apps call Redis via localhost:6379.
- Containerized apps call Redis via redis:6379.

## Future Production Direction

- Kubernetes ConfigMap for non-sensitive config.
- Kubernetes Secret or Azure Key Vault for secrets.
- Centralized configuration may be introduced later if needed.