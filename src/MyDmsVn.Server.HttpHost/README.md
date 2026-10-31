# WEB2-02 IIS/System.Web host

This net48 host serves `/api/v1/health` anonymously. All other `/api` routes require one validated Bearer header and an authoritative active SQL session/account before a request actor is admitted. Login/refresh/logout/capabilities and Sales/Catalog routes belong to WEB2-04/05 and are not implemented here. No production fake, Desktop session, cookie/query access token, automatic write retry or cross-origin CORS is registered.

Production entry point is `Startup.Configuration`. It validates secrets/configuration, exact web schema11, the separate restricted web SQL principal and the protected superuser binding before installing the pipeline. Startup refuses schema10/desktop principals, inaccessible SQL and missing/invalid configuration. Health therefore proves successful host startup, not a bypass around SQL checks. Runtime failures expose a generic JSON 500; disabled/revoked/missing sessions expose JSON 401. Application handlers retain their existing authorization pipeline.

## Operator configuration

Use a CLR v4 Integrated IIS app pool with ASP.NET 4.8 enabled; disable Windows/Forms auth for this application and allow anonymous IIS admission so the application can authenticate Bearer requests. Restrict the TLS binding to the configured host. Operator-provision process environment, readable only to the app-pool identity:

| Variable | Value/constraint |
|---|---|
| `MYDMSVN_WEB_ORIGIN` | Exact HTTPS authority, e.g. `https://web2.mydmsvn.test`; no trailing slash/path |
| `MYDMSVN_WEB_JWT_ISSUER` | HTTPS issuer; pilot uses the same origin |
| `MYDMSVN_WEB_JWT_AUDIENCE` | Nonempty audience, e.g. `mydmsvn-web2` |
| `MYDMSVN_WEB_JWT_ACTIVE_KID` | Active key ID in the validation allowlist |
| `MYDMSVN_WEB_JWT_KEYS` | Comma-separated `kid:base64` entries, at most five; each cryptographically random key at least 32 bytes; no committed example secret |
| `MYDMSVN_WEB_SQL_CONNECTION_STRING` | Separate provisioned restricted web runtime principal, never maintenance/Desktop credentials |
| `MYDMSVN_WEB_SUPERUSER_USERNAME` | Existing protected database binding; no user bootstrap |

Access lifetime defaults to 10 minutes (supported 1–15), skew 30 seconds (supported 0–30); API request body cap 1 MiB. Options are validated and copied at startup. Provision secrets outside source, SPA and deployment output. Do not log tokens, passwords, connection strings, key material or raw exception text. Trusted proxy allowlist is empty: any Forwarded/X-Forwarded-* API or SPA request is rejected, even over HTTPS. Proxy deployment needs a separately implemented/tested allowlist. Unsafe API requests require exact Origin; supplied Fetch Metadata must say same-origin. Cookie-specific CSRF/header/cookie writing remains WEB2-04.

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

The application buffers at most 1 MiB before dispatch including chunked bodies; IIS/ASP.NET retain a separate 2 MiB hard ceiling. Requests rejected before the managed pipeline (invalid HTTP, IIS filtering ceiling, TLS, startup refusal) can use native IIS error pages/statuses and do not constitute the API JSON contract. Full IIS, real browser/Sales integration, refresh cookie/CSRF, and production deployment remain separate gates.
