# Portfolio public-release audit

**Audit date:** 2026-08-26  
**Scope:** `compose.portfolio.yml`, `compose.local-prod.yml`, Cloudflare tunnel scripts, Caddy, Storefront and Operations BFF/session handling, API Gateway routes, and the available tests.  
**Decision:** **do not make the current portfolio public until PR-01 and PR-02 are fixed.** Once fixed, it can support a controlled portfolio demo; it is not ready for real-customer traffic until the `Required before real customer` items are closed.

## P0 remediation status (2026-08-26)

- **PR-01 implemented, pending an end-to-end public-script run:** BFF origin checks are now fail-closed for an absent `Origin` and accept only normalized exact values from `MICROSHOP_ALLOWED_ORIGINS` or the backward-compatible `MICROSHOP_PUBLIC_ORIGIN`. The portfolio override no longer enables the Quick Tunnel wildcard. `portfolio-public-up.ps1` receives each generated URL, recreates only the two BFF containers with that exact HTTPS origin, then asserts that the configured origin reaches authentication while an untrusted origin receives 403.
- **PR-02 implemented, pending a fresh Compose rollout:** the host proxy binding is now loopback-only. Docker-run cloudflared joins the private `microshop-local-prod_microshop-network` and sends traffic to `reverse-proxy:8080`, so it does not depend on a LAN-reachable host port. Public tunnel reconfiguration sets the two BFF cookie settings to `Secure=true`; ordinary local portfolio startup retains its explicit HTTP/`Secure=false` developer setting.

## Public topology observed

```text
Internet
  -> Cloudflare Quick Tunnel (two temporary public URLs)
  -> host port 5027 / Caddy
  -> Storefront or Operations BFF
  -> API Gateway (allow-listed paths only)
  -> private Compose services and infrastructure
```

The running portfolio profile publishes only Caddy on the host. `docker ps` showed `0.0.0.0:5027->8080/tcp` for `microshop-local-prod-reverse-proxy-1`; Storefront, Operations, API Gateway, databases, queues, and service containers had only container ports. This is the intended service-boundary shape, subject to the host-port/TLS blocker below.

## Release gaps

### Do not make public

| ID | Finding and evidence | Impact | Required remediation |
| --- | --- | --- | --- |
| PR-01 | **Quick Tunnel origin validation accepts every `*.trycloudflare.com` origin.** `compose.portfolio.yml:97,103` enables `MICROSHOP_ALLOW_TRYCLOUDFLARE_ORIGIN`; both BFF helpers return true for any HTTPS hostname ending in `.trycloudflare.com` (`Frontend/apps/storefront/src/lib/http/same-origin.ts:8-11`, same Operations file). Runtime control: an invalid login with `Origin: https://attacker.trycloudflare.com` returned **401** (accepted through origin validation) for both BFFs; the same request from `https://attacker.example` returned **403**. | All Quick Tunnel subdomains share the registrable `trycloudflare.com` site. A malicious Quick Tunnel can issue credentialed same-site requests to the victim tunnel. The Operations BFF then exposes state changes such as catalog/stock changes and supplier/purchase-order/receipt actions; Storefront exposes cart, checkout, and payment initiation. `SameSite=Lax` does not compensate for a sibling-site attack. | Remove the wildcard bypass. Use stable, owned named-tunnel hostnames and exact `MICROSHOP_PUBLIC_ORIGIN` values. If temporary tunnels must remain, add a per-session CSRF token required on every unsafe BFF route and reject absent `Origin`; test both malicious sibling-tunnel and unrelated origins. Do not treat a suffix match as origin authentication. |
| PR-02 | **The reverse proxy is plain HTTP on all host interfaces and portfolio cookies are explicitly non-secure.** Caddy is configured as `:8080` (`docker/caddy/Caddyfile.local-prod:1`); Compose publishes `5027:8080` (`compose.local-prod.yml:46-47`) rather than loopback-only; both web apps set `MICROSHOP_COOKIE_SECURE: 'false'` (`compose.local-prod.yml:571,593`). | A LAN user, or any network path exposing port 5027, can reach the app over HTTP. A user who signs in through that route receives a bearer-token session cookie without the `Secure` attribute. This bypasses the HTTPS property provided by Cloudflare Tunnel. | Make the origin listener private (at minimum bind explicitly to loopback and verify the tunnel can still reach it, or attach cloudflared to the private Compose network). Set `MICROSHOP_COOKIE_SECURE=true` for every public profile. Require HTTPS at the edge and verify the HTTP endpoint is unreachable externally. |

### Demo-only acceptable (after PR-01 and PR-02)

| ID | Finding and evidence | Demo boundary |
| --- | --- | --- |
| PR-03 | The script creates account-less Quick Tunnels and, in Docker mode, pulls `cloudflare/cloudflared:latest` (`scripts/portfolio-public-up.ps1:121`). Runtime tunnel logs explicitly state that Quick Tunnels have no uptime guarantee and recommend a pre-created named tunnel for production. | Fine for a short, supervised portfolio demo with synthetic data. Do not rely on it for a stable URL, access policy, incident response, or supply-chain repeatability. Pin the image and use a named tunnel for anything longer lived. |
| PR-04 | Operations is exposed through its own public Quick Tunnel; protection begins at the application login, with no edge access policy or device/MFA control. The bootstrap administrator is enabled in the portfolio Compose profile (`compose.local-prod.yml:367-377`). | A demo administrator account may be used only with a unique, temporary password and no customer data. Prefer a Cloudflare Access policy even for shared demos; never share the operation URL and credentials in a public portfolio post. |
| PR-05 | `/alive` and `/health` are allow-listed through Caddy (`docker/caddy/Caddyfile.local-prod:24-29`). The gateway has reasonable debug/internal guards (`Services/ApiGateway/SecurityMiddlewareExtensions.cs:35-57`), and Caddy's allow-list does not include `_internal` or `/debug`, but health remains externally observable. | Liveness disclosure is acceptable for a demo. For a real deployment, keep public liveness minimal and move readiness/dependency details behind authenticated operations access. |

### Required before real customer

| ID | Finding and evidence | Required remediation |
| --- | --- | --- |
| PR-06 | Logout only expires the BFF cookie (`storefront .../api/session/route.ts:106-115`; same Operations route), while the session is a raw JWT and the portfolio sets Identity token lifetime to 120 minutes (`compose.local-prod.yml:371`). The public gateway also accepts bearer tokens for protected routes. | Introduce server-side, revocable sessions or token revocation/short-lived access tokens with refresh-token rotation. Test logout, password reset, admin removal, and suspected-token-compromise invalidation. |
| PR-07 | Gateway rate-limit partitioning uses `context.Connection.RemoteIpAddress` (`Services/ApiGateway/Program.cs:42-65,144-146`), but neither the gateway nor shared defaults configure forwarded headers. Behind Caddy/cloudflared, requests can therefore collapse to the proxy's address, letting one client exhaust the shared limit or making attribution unreliable. | Configure forwarded headers with explicit trusted proxy/network boundaries before rate limiting, or rate-limit at the trusted edge. Add a deployed-topology test that proves clients receive independent limits and spoofed forwarding headers cannot choose a partition. |
| PR-08 | Public response headers include useful anti-framing/nosniff/referrer settings, but no Content-Security-Policy or HSTS policy is set in `docker/caddy/Caddyfile.local-prod:4-10`; BFF auth makes XSS defense especially important. | Deploy a nonce/hash-compatible CSP, verify it against Next assets, set HSTS only on the real HTTPS hostname, and add browser tests for expected headers. |
| PR-09 | There are no frontend unit, integration, or browser tests: `Frontend/apps/storefront` and `Frontend/apps/operations` contain no `test`/`spec` files and their `package.json` files have only dev/build/start/lint scripts. Existing .NET integration tests cover domain/persistence behavior, not BFF or proxy security. | Add route-handler tests for session/origin/role/path behavior and Playwright (or equivalent) tests for the customer and operations critical paths below. Run them in CI against the composed topology. |
| PR-10 | The public API surface is broad by design: Caddy forwards `/auth`, `/catalog`, `/cart`, `/orders`, `/payments`, `/inventory`, `/suppliers`, and `/procurement` to the gateway (`docker/caddy/Caddyfile.local-prod:24-29`). Gateway policies and downstream endpoint authorization are present, and internal/debug routes have two layers of protection, but there is no executable route-surface contract. | Keep a single explicit public-route inventory; add integration tests asserting every non-inventory route is 404 at Caddy and that public routes enforce authentication/roles. Retain the signed, idempotent payment webhook as the only intentionally anonymous mutation surface. |

## Test matrix to add

### Customer journey

| Flow | Essential assertions | Level |
| --- | --- | --- |
| Registration and sign-in | Exact-origin registration/login succeeds; malformed credentials fail; BFF never returns the access token; cookie is `HttpOnly`, `Secure`, `SameSite`, scoped correctly, and expires at the intended time. | Route handler + browser |
| Catalog | Anonymous browse works; filtering/query encoding cannot change upstream target; catalog mutations through the customer BFF are impossible. | Route handler + browser |
| Basket | Customer A may only read/change `/api/cart/{A}/...`; customer B and an admin token cannot use A's customer path through the Storefront BFF. Test add/update/delete and stale basket version. | Route handler + API integration |
| Checkout | Requires a valid customer session and valid origin/CSRF proof; same idempotency key returns the same outcome; duplicate, stale, malformed, and cross-user basket attempts are safe. | API integration + browser |
| Orders and payment | Customer sees only own orders/payments; payment initiation belongs to the order owner; signed webhook drives the expected state once, unsigned/replayed/out-of-order messages do not. | API integration |

### Operations journey

| Flow | Essential assertions | Level |
| --- | --- | --- |
| Access control | Anonymous and non-admin users cannot fetch Operations data or mutate it. A forged cookie must not pass because the API routes revalidate the token and role. | Route handler + browser |
| Catalog and stock | Admin create/edit/stock actions succeed; non-admin fails; concurrent updates and invalid numbers do not corrupt stock. Cross-origin/CSRF attempts fail. | Browser + API integration |
| Inventory, orders, payments | Admin views work; sensitive customer/order/payment data is not rendered before authorization; limits/pagination are enforced. | Browser + API integration |
| Suppliers and procurement | Admin-only create supplier, draft order, submit, and receive work; duplicate receipt is idempotent; Cross-origin requests cannot create supplier/order or receive stock. | Browser + API integration |
| Sign-out | Exact-origin logout expires cookie; a token captured before logout is rejected after revocation (once PR-06 is implemented). | Browser + API integration |

### Deployment and security checks

| Check | Pass condition |
| --- | --- |
| Origin/CSRF | `attacker.example` and a separate `attacker.trycloudflare.com` both receive 403 for every unsafe BFF method. Missing Origin is rejected or requires the CSRF proof. |
| Cookie transport | Public profile sets `Secure`; no HTTP listener is reachable from a second LAN namespace; HTTPS redirects/headers are correct. |
| Tunnel | Exact, owned hostnames only; pinned cloudflared image; tunnel process/container stops cleanly; operations hostname is protected by edge access policy. |
| Gateway route surface | Caddy rejects `_internal/*`, `/debug/*`, service health/details not intentionally public, and arbitrary paths. Protected routes return 401/403 without a valid bearer token and enforce role ownership. |
| Forwarded headers/rate limits | Trusted proxy configuration is tested; spoofed `X-Forwarded-For` is ignored; distinct real client IPs do not share one quota; login and webhook limits have appropriate independent limits. |
| Headers/XSS | CSP, HSTS (owned HTTPS domain only), frame protection, nosniff, referrer, and permissions policies are present and do not break Next rendering. |
| Secrets/observability | No token, authorization header, password, or webhook secret appears in logs; monitoring captures security failures without high-cardinality PII. |

## Checks performed in this audit

| Check | Result |
| --- | --- |
| Runtime origin control | **Failed as designed by PR-01:** malicious `*.trycloudflare.com` Origin reached authentication (401); unrelated origin was rejected (403), on both BFFs. No account or order was created. |
| Running Compose boundary inspection | **Pass:** only reverse proxy published host port 5027; application and infrastructure ports remained private to Docker. Note that port 5027 is nevertheless bound to all host interfaces (PR-02). |
| `scripts/local-prod-smoke.ps1 -SkipAuth -SkipReadModel -VerifyPortfolioFrontends` | **Pass:** `/alive`, `/health`, catalog, discount lookup, Storefront UI, and Operations UI. |
| .NET test subset | **Pass:** 18 tests (`CheckoutIdempotencyTests`, `BasketCatalogFailureTests`, `PaymentSagaCompensationTests`), 0 failures, 120 ms. |
| Full .NET integration suite | **Not accepted as a release result:** it starts many Testcontainers concurrently and did not complete within this audit environment's execution window. Temporary containers self-cleaned; rerun in CI with sufficient Docker capacity and capture the final report. |
| Frontend tests | **Absent:** no test/spec files or test runner scripts were found in either Next application. |

## Release exit criteria

1. PR-01 and PR-02 are fixed and their negative security tests pass.
2. A full clean Compose smoke covers an actual signed-in customer journey and an admin journey, not only landing pages.
3. Route-handler and browser security suites from the matrix run in CI.
4. For real customers, PR-06 through PR-10 are closed, a named tunnel/custom domain is used, and an operational owner approves the production secret, backup, monitoring, and incident-response setup.
