# WEB2 IIS/System.Web host, auth and Sales/Catalog endpoints

This net48 host serves `/api/v1/health` anonymously and implements WEB2-04 auth and WEB2-05 Sales/Catalog routes. `POST /api/v1/auth/login` calls the existing LoginCommand/password policy, persists a refresh session and returns access JWT/user/expiry. `POST refresh` and `POST logout` use only the refresh cookie; an optional stale Bearer does not block them. `GET me` and `GET capabilities?feature=sales` require a validated Bearer and authoritative active SQL session/account. Capabilities evaluate Read/Write/Post independently through the existing permission service. No production fake, Desktop session, cookie/query access token, automatic write retry or cross-origin CORS is registered.

Production entry point is `Startup.Configuration`. It validates secrets/configuration, exact web schema11, the separate restricted web SQL principal and the protected superuser binding before installing the pipeline. Startup refuses schema10/desktop principals, inaccessible SQL and missing/invalid configuration. Health therefore proves successful host startup, not a bypass around SQL checks. Runtime failures expose a generic JSON 500; disabled/revoked/missing sessions expose JSON 401. Application handlers retain their existing authorization pipeline.

The app-local Web.config extends the CLR4 Integrated extensionless handler's existing GET/HEAD/POST/DEBUG verb set with PUT for Draft Update. Without that mapping the installed IIS Express template sends PUT to its native StaticFile handler before OWIN runs. The override stays in the published application; it does not change machine IIS configuration or enable unrelated verbs. In-memory TestServer does not exercise native IIS handler selection, so the published HTTPS smoke explicitly performs PUT.

Sales List/Get/Create/Update/Post dispatch the existing Application queries/commands through MediatR. Create and Update return their authoritative full detail without a separate Read check; Post returns only its acknowledgement, so a later Get may independently return403. Read/Write/Post remain separate permissions. Four active-only catalog lookup routes use each catalog's own Read permission; Sales detail retains historical inactive labels. Existing `AddSqlPersistence` supplies the query services, typed repositories and transaction owner; the host adds no business SQL or alternate stock/pricing logic.

The [canonical v1 field tables and corpus](../../docs/contracts/web2-v1/README.md) own routes and exact payloads. Dedicated HTTP mapping keeps int64 IDs and decimal values as invariant strings, int32 values as JSON integers, document dates as `yyyy-MM-dd` and versions/cursors opaque. Strict UTF-8 JSON/query parsing rejects duplicate/unknown members, numeric int64/decimal wire values, coercion and invalid dates/ranges before mutation. Sales list defaults to25; lookups require explicit `limit`. HTTP wire mapping does not change Local DTO serialization or its default page size.

## Operator configuration

Use a CLR v4 Integrated IIS app pool with ASP.NET 4.8 enabled; disable Windows/Forms auth for this application and allow anonymous IIS admission so the application can authenticate Bearer requests. Restrict the TLS binding to the configured host. Operator-provision process environment, readable only to the app-pool identity:

| Variable | Value/constraint |
|---|---|
| `MYDMSVN_WEB_ORIGIN` | Exact HTTPS authority, e.g. `https://web2.mydmsvn.test`; no trailing slash/path |
| `MYDMSVN_WEB_JWT_ISSUER` | HTTPS issuer; pilot uses the same origin |
| `MYDMSVN_WEB_JWT_AUDIENCE` | Nonempty audience, e.g. `mydmsvn-web2` |
| `MYDMSVN_WEB_JWT_ACTIVE_KID` | Active key ID in the validation allowlist |
| `MYDMSVN_WEB_JWT_KEYS` | Comma-separated `kid:base64` entries, at most five; each cryptographically random key at least 32 bytes; no committed example secret |
| `MYDMSVN_WEB_ACCESS_LIFETIME_MINUTES` | Optional integer 1–15; default10 |
| `MYDMSVN_WEB_REFRESH_IDLE_DAYS` | Optional integer 1–7; default7 |
| `MYDMSVN_WEB_REFRESH_ABSOLUTE_DAYS` | Optional integer 1–30; default30; must be >= idle |
| `MYDMSVN_WEB_SQL_CONNECTION_STRING` | Separate provisioned restricted web runtime principal, never maintenance/Desktop credentials |
| `MYDMSVN_WEB_SUPERUSER_USERNAME` | Existing protected database binding; no user bootstrap |

Access lifetime defaults to 10 minutes (supported 1–15), skew 30 seconds (supported 0–30); API request body cap 1 MiB. Options are validated and copied at startup. Provision secrets outside source, SPA and deployment output. Do not log tokens, passwords, connection strings, key material or raw exception text. Trusted proxy allowlist is empty: any Forwarded/X-Forwarded-* API or SPA request is rejected, even over HTTPS. Proxy deployment needs a separately implemented/tested allowlist. Unsafe API requests require exact Origin; supplied Fetch Metadata must say same-origin. Login/refresh/logout additionally require exactly `X-Requested-With: XMLHttpRequest` before any auth side effect. Missing or cross-origin Origin is denied.

Login accepts only UTF-8 `application/json` with string `username` and `password`; unknown/duplicate fields, malformed JSON, token headers and query parameters are rejected. Refresh/logout require an empty body and do not accept a token in JSON, query or alternate headers. `__Host-mydmsvn-rt` is a canonical GUIDv4 cookie with `Max-Age`, `Path=/`, `Secure`, `HttpOnly`, `SameSite=Strict` and no Domain; logout clears the identical scope with `Max-Age=0`. A single host writer supplies the header, including Max-Age that Katana's cookie API lacks. No refresh token is serialized in JSON. Browser access JWT storage remains localStorage by Owner choice; the frontend lifecycle is WEB2-07.

Each worker bounds auth admission to4096 hashed subject/IP entries in5-minute windows: login10 attempts per normalized account and30 per IP; refresh30 per cookie and60 per IP. Requests above either limit or cache capacity return429 with Retry-After. Account/token and IP limits are independent. This is a per-worker guard and resets on recycle; a multi-worker/distributed abuse policy needs separate deployment validation. Do not treat the limiter as a refresh lock: SQL rotation/replay remains the authority. Authentication responses, including errors, are no-store and audit output uses a fixed sanitized allowlist. Operators own TraceSource destination/retention.

## Build and publish

From repository root, run locked restore and the focused test project:

```powershell
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=host.trx' --results-directory TestResults/web2-host
./src/MyDmsVn.Server.HttpHost/Publish-Web2Host.ps1 -OutputPath C:/OperatorChosenEmptyDirectory/Web2Host
```

Publish requires an empty destination and does not delete existing files. `-SkipBuild` is only for an already built current candidate. It copies runtime/native dependencies and imports actual generated binding redirects into deployed Web.config; do not deploy source Web.config alone. Host-local JWT family is aligned 8.23.0, OWIN 4.2.3, WebAPI Core/Owin 5.3.0, Client 6.0.0. Existing global pins/legacy lockfiles remain separate. No Desktop references exist in the host graph.

Set IIS physical path to published root, bind the trusted TLS certificate/DNS and recycle the pool after environment changes. `wwwroot/index.html` is a deployment marker, not the React application: replace wwwroot with the separately verified frontend build. Existing allowed assets are served directly; extensionless paths fall back to index.html. Missing asset names and every `/api` failure are excluded from SPA fallback. CSP permits self-hosted scripts with no unsafe-inline. Confirm actual frontend compatibility separately.

For IIS Express, use an isolated applicationhost.config based on the installed IISExpress template, a CLR 4 Integrated pool, physical path above and HTTPS binding matching `MYDMSVN_WEB_ORIGIN` (e.g. trusted `https://localhost:44321` for a local owned fixture). Launch installed iisexpress.exe with `/config:<isolated-config>` and `/site:<site-name>` through a hidden process inheriting only the test/operator settings. Drain and stop that exact process before disposing an owned SQL fixture. IIS Express is a pipeline smoke, not full IIS deployment acceptance.

Smoke: GET health -> 200 minimal JSON; GET protected/missing route without Bearer -> 401 JSON, WWW-Authenticate Bearer and no Location; valid token+active SQL session unknown route -> 404 JSON; bad signature/revoked/disabled -> 401; wrong origin/forwarded headers -> 403/400; index/client route ->same marker; missing `.js` -> 404, never HTML. Confirm no exception/key/SQL details in errors, actual deployed redirects and two overlapping request actors.

The application buffers at most 1 MiB before dispatch including chunked bodies; IIS/ASP.NET retain a separate 2 MiB hard ceiling. Requests rejected before the managed pipeline (invalid HTTP, IIS filtering ceiling, TLS, startup refusal) can use native IIS error pages/statuses and do not constitute the API JSON contract. The focused auth/Sales/SQL/System.Web results belong to [WEB2 evidence](../../docs/evidence/web2/20261031-http-jwt-sales.md). Full IIS, actual browser cookie enforcement/storage/multi-tab, frontend Sales integration and production deployment remain separate gates.
