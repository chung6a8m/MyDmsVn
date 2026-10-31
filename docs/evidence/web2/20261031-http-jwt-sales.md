# Backend WEB2 — HTTP/JWT/Sales execution evidence

**Date created:** 2026-10-31. **Status:** WEB2-01–04 DONE within bounded contract/host/SQL-session/auth scope; WEB2-05 onward PLANNED. Focused HTTP/SQL and published IIS Express smoke PASS; full IIS/browser/Sales pilot acceptance remains open. Cross-repo plan: [server companion](../../plans/20261031-003-web2-http-jwt-sales-server.md); [contract](../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md); [React roadmap](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261031-002-http-jwt-sales-vertical-slice.md).

## Planning baseline

- Backend inspected main `5b9f960ee4048581f107e9ee6350e1dfad462b53`, web inspected main `90ef55c7408c25a6107310199890d809bc329adb`; implementation PRs must recheck current source/branches.
- Current application exposes Local in-process handlers and migration scripts001–010. WEB2 source/runtime does **not** yet exist.
- Owner decisions: net48/IIS thin host; short JWT Bearer access in browser localStorage; GUID refresh cookie HttpOnly Secure Strict; hash and rotate in SQL; separate HTTP DTO v1 to protect int64/decimal precision.
- Legacy release GUI and existing SQL test acceptance are retained under their own scope and **not inherited**.

## Task register

| Task | Server PR / commit | Actual tests and environment | Result | Unresolved risks |
|---|---|---|---|---|
| WEB2-01 | `350358ae9f8519efd54d46d61b80d3b7887c1dcd` | Windows/net48 24 probe rows; Node 59 corpus tests; docs checks | PASS / DONE (bounded) | No production host/auth/mapper, IIS, SQL or browser acceptance |
| WEB2-02 | `55ea4fd8b4aa567bdef0c723fd08b552b7b11269` | 63 HTTP/DI/JWT rows + 1 real published IIS Express/SQL smoke | PASS / DONE (bounded) | Full IIS and browser/auth/Sales endpoints pending |
| WEB2-03 | `36c528acc932b73c8d93e2fd54089c0856fd919b` | 25 store rows (16 SQL/9 pure), 25 migration, 5 principal, 15 reset rows | PASS / DONE (bounded) | Controlled nonproduction schema11 only; no desktop coexistence or production rollout |
| WEB2-04 | `a0770ac082505fec1f1db52a2469a5aa392a69f4` | 147 HTTP + 33 session/SQL + 1 published IIS Express auth smoke | PASS / DONE (bounded) | Full IIS/browser/Sales acceptance remains open |
| WEB2-05 | — | NOT RUN | PLANNED | Sales/Catalog HTTP golden |
| WEB2-08/09 | — | NOT RUN | PLANNED | Browser integration/real write |
| WEB2-10 | — | NOT RUN | PLANNED | Full candidate acceptance |

## Per-task evidence instructions

Append candidate date and SHA, focused changed files, real Windows/IIS/.NET Framework build/runtime, SQL Server DB schema version (nonproduction only), restricted principal and grant verification, binding redirects/secret provenance (never secret value), test project commands/count/results, HTTP golden status/JSON traces, cookie flags with **redacted values**, JWT negative cases, SQL concurrency/rollback/replay, 2 actors, Sales Post stock/value/COGS. Separate automated MSW/unit, live HTTP, live SQL and attended browser results. Mark missing local infrastructure **BLOCKED / NOT RUN**, never inherit PASS from Local/WinForms or plan documentation. Production deployment intentionally excluded.

## WEB2-01 — 2026-10-31 contract/dependency candidate

Inspected backend base `e657d8ddfc6dd4cf8a146ea04325a4dc9f8bd02d`, web base `3faa0a54c97de6b42cf80fd693fb05a68640c331`. Artifact/probe commit **`350358ae9f8519efd54d46d61b80d3b7887c1dcd`**, followed only by status/evidence documentation; local branch `codex/web2-01-contract`. Web companion has documentation/source inventory only; see its [WEB2 evidence](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/web2-01-contract/docs/evidence/20261031-web2-http-jwt-sales.md). No push, PR, merge or deployment performed. Historical evidence, shipped graph/global pins and scripts001–010 remain unchanged.

Deliverables: [ADR 0012](../../adr/0012-web2-net48-host-preflight.md), [canonical wire v1 corpus](../../contracts/web2-v1/README.md), [isolated package probe](../../../tools/Web2.HostPreflight/README.md). Owner selected `https://web2.mydmsvn.test` as a local origin template, not a deployed endpoint. No real keys, credentials or connections were used; no SQL allocation was created.

Environment: Windows 11 Home Single Language x64 / `10.0.26300`; Node `22.14.0`, npm `11.6.0`; SDK `8.0.427`, MSBuild `17.11.69`; .NET Framework registry `4.8.10008`, Release `534296`. IIS/SQL/browser: **NOT RUN**. Selected package versions/assets and official references are owned by ADR 0012, not duplicated here.

| Executed command/check | Observed result |
|---|---|
| `dotnet restore tools/Web2.HostPreflight/Web2.HostPreflight.csproj --locked-mode` | PASS; candidate lock unchanged |
| `dotnet build tools/Web2.HostPreflight/Web2.HostPreflight.csproj -c Release -f net48 --no-restore` | PASS, 0 warnings/errors |
| `dotnet test tools/Web2.HostPreflight/Web2.HostPreflight.csproj -c Release -f net48 --no-restore --no-build --logger 'trx;LogFileName=host-preflight-final.trx' --results-directory TestResults/web2-01-preflight` | **24 executed / 24 passed / 0 failed / 0 notExecuted**, checked actual TRX |
| `node --test scripts/check-web2-wire-fixtures.mjs` | **59 tests / 59 passed / 0 failed / 0 skipped** |
| `git diff --check`; changed-file encoding/EOL/JSON/local and companion Markdown paths; source/global dependency/historical SQL diff | PASS; no product or existing test/solution/global-pin changes |
| Real HTTP host/auth/mapper, IIS HTTPS/redirect/Web.config/cookie/CSP, SQL schema/grants/rotation/stock/reset, browser/attended | **NOT RUN**, future task gates |

The 24 probe rows comprise 14 package/in-memory route/JWT/cookie cases and 10 invariant CLR/JSON long/decimal vectors. JWT policy negatives are dependency tests, not an authenticated production pipeline or required-claims/session/account validation. System.Web cookie properties and OWIN headers are in-memory observations. The 59 Node checks validate a hand-authored corpus, not real endpoint rejection or backend/frontend mapper parity. Future WEB2-05/08 must run the same fixtures through implemented mappers/codecs.

Exploratory history is retained in ignored local TRX: `host-preflight.trx` and `host-preflight-corrected.trx` each executed 14 with 11 passed/3 failed (test-key kid mutation and same-response cookie append/delete lookup). Independent fixtures corrected the harness, yielding 14/14 in `host-preflight-harness-fixed.trx` and `host-preflight-aligned.trx`, then 24/24 in `host-preflight-wire.trx` and the root's final run above. No product fix, SQL fallback or skip-to-pass occurred. Corpus review corrected impossible cursors on a one-item max-ID/pageSize25 page; the terminal page and independent opaque cursor vectors now have an explicit semantic assertion. Sales label examples match the source's code/name projection.

Independent review inspected source, lock/assets, final TRX and status docs; no unresolved finding. ADR now matches the generated config: no IdentityModel/JWT redirect is emitted by this aligned probe; integrated-host redirect verification remains WEB2-02.

**Decision:** GO for WEB2-02/03 implementation under ADR/schema gates (and frontend WEB2-06). NO-GO for runtime integration acceptance, migration/deployment, desktop/schema11 coexistence or production readiness. Katana's missing Max-Age requires the planned single host cookie writer and IIS/browser proof; schema11 needs explicit exact10 admission, separate web principal/profile, reset expansion and legacy writer drain/fail-closed checks. No next task is implemented by this preflight.

## WEB2-02/03 — 2026-10-31 host and refresh persistence

**Source candidate:** WEB2-03 `36c528acc932b73c8d93e2fd54089c0856fd919b`, followed by WEB2-02 `55ea4fd8b4aa567bdef0c723fd08b552b7b11269`; documentation/status changes follow separately. Branch `codex/web2-02-03`, base `1c6514ee4976a8217d1666618dfda75c1c8aee85`. Web status-only companion starts from `d3a6b0b58ed3ad1f693c07d4f3f6912a7b8ffe87` on `codex/web2-02-03-status`. This work is stacked on the contract candidate from the Owner-requested [backend PR68](https://github.com/chung6a8m/MyDmsVn/pull/68) and [web PR5](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/5); no new push, PR or merge is part of this checkpoint. The earlier no-push statement above describes the earlier preflight checkpoint.

Implementation: [host/runbook](../../../src/MyDmsVn.Server.HttpHost/README.md), [web SQL operations](../../CONFIGURATION_AND_SQL_SECURITY.md#web2-schema11-và-principal-riêng), [schema011](../../../src/MyDmsVn.Server.DbMigrator/Scripts/011_WebAuthSessions.sql), [store](../../../src/MyDmsVn.Server.Infrastructure/Identity/SqlWebAuthSessionStore.cs). HTTP health is real; login/refresh/logout/me/capabilities and Sales/Catalog endpoints remain WEB2-04/05. `wwwroot/index.html` is only a publish marker, not React UI. No production fake or credential bypass was added.

Environment: Windows11 Home Single Language x64 `10.0.26300`, SDK8.0.427/MSBuild17.11.69, .NET Framework4.8.10008 (Release534296), VSTest17.11.1 x64/net48. SQL Server **14.0.2140.2 Developer Edition (64-bit)**, compatibility140, SQL login enabled. Existing explicit test connection targets operator-designated nonproduction master; it is never printed or substituted. IIS Express **10.0.26013.1000**, CLR4 Integrated, actual `https://localhost:44321` binding and `http://localhost:18081` negative check. The smoke pins the existing localhost TLS certificate only inside its test; browser trust/DNS for Owner template `https://web2.mydmsvn.test` was not tested or provisioned. Full IIS is not installed on this machine; browser/attended/frontend build were NOT RUN.

### Executed scope

All selected projects restored with `dotnet restore <project> --locked-mode` before build/test. Final TRX counters were inspected: **134 executed, 134 passed, 0 failed, 0 notExecuted** across the disjoint rows below. This is a focused scope, not a solution-wide regression.

| Project/selector | Final command options / local ignored TRX | Actual result |
|---|---|---|
| `tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj` | `dotnet test <project> -c Release --no-restore -p:BuildProjectReferences=false --logger 'trx;LogFileName=host-final.trx' --results-directory TestResults/web2-host-final` | 63/63 PASS; SQL-free OWIN pipeline, JWT/transport negatives, real Application authorization, overlapping A/B actor scopes, cancellation/disposal and sanitized audit |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` / `FullyQualifiedName~WebAuthSession` | `dotnet test <project> -c Release -f net48 --no-restore --filter '<selector>' --logger 'trx;LogFileName=web2-store-final.trx' --results-directory TestResults/web2-store-final` | 25/25 PASS, 1m01s; 16 real SQL + 9 pure; all16 fresh owned allocations drained/dropped |
| Same Security project / `FullyQualifiedName~WebPrincipalProvisioningTests` | Same Release/net48 pattern; `TestResults/web-principals/web-principals.trx` | 5/5 PASS, 23s; effective grants, rehash/sequence/auth DML, privilege negatives and desktop auth denial |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests` / `FullyQualifiedName~WebAuthResetTests\|FullyQualifiedName~ReusableSqlFixtureIntegrationTests` | Same Release/net48 pattern; `TestResults/web-reset/web-reset.trx` | 15/15 PASS, 3m30s; web auth inventory/trigger/journal/ownership + 14 legacy reset/poison/failure-boundary cases |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests` / expanded selector below | Same Release/net48 pattern; `TestResults/web-migrations-final/web-migrations-final.trx` | 25/25 PASS, 2m30s; fresh11, exact10→11, no-op, drift/partial/future/9 refusal, rollback and preserved historical admission |
| Security project / `FullyQualifiedName~WebHostIisExpressTests` | `dotnet test <project> -c Release -f net48 --no-restore --filter '<selector>' --logger 'trx;LogFileName=iis-express-final.trx' --results-directory TestResults/web2-iis-final` | 1/1 PASS, 20s; real SystemWeb HTTPS + restricted SQL startup and session revocation |

Final migration selector:

```text
FullyQualifiedName~WebAuthMigrationTests|FullyQualifiedName~Exact9_runtime_refuses_and_restricted_admin_upgrade_preserves_mode_and_noop|FullyQualifiedName~Every_old_prefix_remains_recovery_required_without_automatic_upgrade|FullyQualifiedName~Clean_migration_applies_once_and_repeat_is_a_no_op|FullyQualifiedName~Interrupted_old_security_baseline_is_rejected_without_mutating_existing_data|FullyQualifiedName~Losing_the_migration_lock_session_stops_schema_preflight
```

Publish executed with `./src/MyDmsVn.Server.HttpHost/Publish-Web2Host.ps1 -OutputPath TestResults/web2-02-03/publish-iis`; it performed locked restore and full referenced Release build with **0 warnings/errors**. The smoke set `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY` to that absolute output and `MYDMSVN_WEB_TEST_TLS_THUMBPRINT` to the existing localhost binding certificate; no key/password was committed. Actual deployed Web.config contains **13 generated binding redirects** (OWIN/Newtonsoft/BCL/DI/logging/Serilog/System libraries), no IdentityModel/JWT redirect in the aligned8.23 graph. Published bin contains **0 Desktop DLLs**. Publisher requires an empty destination and never recursively deletes an existing deployment.

HTTP observations: health200 `{"data":{"status":"ok"}}` with correlation; missing/invalid Bearer401 JSON and no Location; independently signed valid JWT + real SQL session reaches JSON404 for unknown API; real revoke causes the same JWT to receive401. HTTP transport400, cross-origin403, index/client-route same HTML marker, missing `.js`404. API never falls through to SPA. IIS native filtering/TLS/startup failures remain native errors outside the managed JSON contract; actual browser/cookie behavior is not claimed.

Persistence observations: hash-only GUIDv4, absolute30d/idle7d, rotation/reuse, expired-ancestor replay, family isolation, logout/refresh race, disable→reenable without intervening request, boundary expiry, child/initial insert rollback, retention and audit failure after committed revoke. Three contention tests record independent SPIDs, hold a session-key U lock and verify DMV WAIT/CONVERT on that same key plus Users S lock before release; the clock advances only after confirmed blocking. Shared cleanup helper drains workers before disposal and quarantines on timeout.

All ordinary SQL allocations, including the two IIS attempts, were positively owned and cleaned. IIS worker was launched hidden with isolated config, ephemeral signing key and random SID-tracked restricted login; test-admin MYDMSVN variables were removed from its environment. Cleanup stops/drains that exact process, verifies login instance/SID/no sessions and then drops the database through its ownership helper. No listener remained on44321/18081 after final smoke. Expected reset-failure probes passed their refusal assertions; no unexpected cleanup failure or retained resource was observed. New schema tests aggregate scenario+cleanup errors; that final error-path-only review correction changed no SQL/assertions/happy path and was statically reviewed, not used to claim a new dual-failure runtime scenario.

### Failure history and review

RED was retained: host anonymous case1FAIL; first integrated52 rows49PASS/3FAIL exposed WebAPI404 pass-through, audit lifetime and a test Host-authority fixture; audit correlation regression1FAIL reproduced real DI creation before actor admission. These are fixed; final63 host rows pass. Store input8FAIL→8PASS, Issue SQL1FAIL, maintenance input1FAIL, then lifecycle24 rows12PASS/12 expected unimplemented-rotation failures preceded final25GREEN. Migration exploration12 rows11PASS/1FAIL exposed a fault-injection THROW placed inside the trigger batch; a GO separator fixed the fixture. Temporary compile blockers and fingerprint-derivation probes were resolved before final runs and are not acceptance evidence.

First IIS smoke1FAIL was a harness assertion expecting `isSuccess`; actual ApiResponse marks that property JsonIgnore and correctly emits only `data`. Source/contract inspection corrected the assertion; final smoke1PASS covers all steps. Both attempts cleaned their owned process/login/database; no host product change was needed for that correction.

Independent review found no remaining P1/P2 product issue after correlation/audit, deterministic SQL handshake, bounded IIS worker shutdown and aggregate cleanup-error corrections. Static diff/encoding/local-link checks, unchanged global pins/scripts001–010/desktop grants and frontend source boundary are checked with the documentation checkpoint. Raw synthetic TRX/publish/config remain ignored under TestResults and are not committed.

**Decision:** WEB2-02/03 DONE for the implemented host/session scope; next planned dependency is WEB2-04 (not started here). Full IIS deployment, DNS/browser trust for the pilot origin, actual login/refresh cookie/CSRF/rate limits, Sales HTTP, browser storage/UI and Gate A–D/production rollout remain NOT RUN / pending. Desktop remains exact10 and cannot coexist as an admitted writer on schema11. TraceSource `MyDmsVn.Web.Security` uses a fixed sanitized allowlist; operator-provisioned destination/retention is still a deployment responsibility.

## WEB2-04 — 2026-10-31 auth endpoints

**Source/test candidate: `a0770ac082505fec1f1db52a2469a5aa392a69f4`; DONE within bounded WEB2-04 scope.** Baseline backend `4290220`, web `60e5210`, isolated branches `codex/web2-04-auth` / `codex/web2-04-status`. The implementation checklist is in the [owning companion plan](../../plans/20261031-003-web2-http-jwt-sales-server.md#web2-04-implementation-checklist--2026-10-31). Historical WEB2-01–03 and fake WEB-01–08 results remain unchanged.

Web status-only commit: `bb83abbc282cd741fcfac21cf58cd61c33715dd7`; frontend source is unchanged from its merged baseline. Backend documentation checkpoint follows the source/test candidate; no runtime source changed after the final checks.

Scope: existing LoginCommand/password policy and authoritative request actor; five auth routes; HS256 access JWT, GUID cookie-only refresh/logout, strict Origin/custom header/body guards, bounded throttling and sanitized audit. Refresh defaults stay7d/30d; bounded configuration permits shorter lifetimes. No Sales/Catalog endpoints, schema/grant change, Desktop/Local behavior or frontend runtime is included.

Baseline HTTP verification: locked restore and Release/net48 HttpHost.Tests, **63 executed/63 passed/0 failed/0 skipped**, local `TestResults/web2-04-http-baseline/baseline.trx`. Initial auth endpoint RED: **49 executed/8 passed/41 failed**, `TestResults/web2-04-auth-red/auth-red.trx`; missing cookie routes reject with401, and some dependent tests fail after unsuccessful login. Configuration RED was a compile failure for the new missing types, not an executed test result; subsequent focused configuration run **13/13 PASS**, `TestResults/web2-04-ttl/ttl-green.trx`.

### Final verification

Environment rechecked: Windows11 `10.0.26300.0`, .NET SDK8.0.427, Framework4.8.10008/Release534296, VSTest17.11.1 x64/net48; designated nonproduction SQL Server14.0.2140.2 Developer64; IIS Express10.0.26013.1000. No production connection, schema migration, DNS/TLS provisioning or desktop deployment was performed. The existing localhost44321 TLS binding is pinned only by the test. Browser: **NOT RUN**; full IIS is not installed on this machine.

Final disjoint counters verified from actual TRX: **181 executed / 181 passed / 0 failed / 0 notExecuted**. This is a focused task scope, not a solution regression.

| Project/scope | Executed command | Actual result |
|---|---|---|
| HTTP/DI/auth (SQL-free) | `dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=http-final.trx' --results-directory TestResults/web2-04-http-final` | 147/147 PASS: 63 retained host rows + 13 refresh config + 71 auth/access config; locked restore preceded execution |
| Session/HTTP SQL | `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebAuthSession\|FullyQualifiedName~WebAuthHttpSql' --logger 'trx;LogFileName=web2-04-auth-sql-final.trx' --results-directory TestResults/web2-04-auth-sql-final` | 33/33 PASS,110s: 17 session SQL + 7 HTTP SQL + 9 SQL-free store rows; locked restore preceded execution |
| Publish | `./src/MyDmsVn.Server.HttpHost/Publish-Web2Host.ps1 -OutputPath TestResults/web2-04-iis/publish-final` | Locked restore/full referenced Release build PASS,0 warnings/errors;13 generated redirects,0 Desktop DLLs |
| Published HTTPS auth | `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-build --no-restore --filter FullyQualifiedName~WebHostIisExpressTests --logger 'trx;LogFileName=iis-final.trx' --results-directory TestResults/web2-04-iis-final` | 1/1 PASS,28s; actual System.Web + restricted SQL LOGIN, login/me/refresh/logout and secure cookie headers |

The SQL selector above contains a literal `|` (Markdown table escapes it). Pure HTTP tests use deterministic ports/clock but real production routes and Application policies. The seven HTTP SQL cases use a fresh owned database and a provisioned restricted database user via `EXECUTE AS USER` on each production-store connection; independent administrator oracles arrange and inspect state. The published IIS case instead uses the existing SID-tracked restricted SQL LOGIN and hidden IIS worker. These are distinct from a real browser's cookie enforcement.

SQL observations: actual BCrypt login and lower-work-factor rehash through the existing LoginCommand; independent live Read/Write/Post capability changes for overlapping actors; issue rollback on injected insert failure; inactive-account denial; concurrent refresh yields one child then replay revokes only that family; logout invalidates both issued unexpired JWTs. Custom idle2d/absolute5d issue/rotation clamps at day5 and rejects expiry, while retained default7d/30d cases pass. A throwing audit sink after committed refresh still permits its200 response; intentionally reusing the old cookie (lost-cookie response model) revokes the family and denies the child JWT. No refresh plaintext appears in JSON/audit; SQL stores32-byte hashes.

IIS smoke explicitly set `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY` to the new publish output and `MYDMSVN_WEB_TEST_TLS_THUMBPRINT` from the already configured local binding, then restored both process variables. Login/rotation cookie observed with **redacted value**: `__Host-mydmsvn-rt=<redacted>; Max-Age=<remaining>; Path=/; Secure; HttpOnly; SameSite=Strict`, no Domain; logout has empty value and `Max-Age=0`. Missing Origin/cross-origin403, invalid/revoked Bearer401 JSON without redirect, unknown API404, HTTP400 and marker SPA behavior also pass. All owned SQL allocations, the temporary login and exact worker drained/cleaned; no quarantine/cleanup failure, and ports44321/18081 had no listeners afterward.

### Retained failure history and review

The initial SQL RED executed7 with7 failures: six missing auth-route cases and the configured2-day TTL returning7days. The first eight-case SQL GREEN attempt had7PASS/1FAIL because a test oracle named nonexistent `CreatedAtUtc`; the oracle was corrected to read a token hash and rerun8/8, then the final33/33 above. Intermediate HTTP runs exposed four missing auth-audit events; a fixture parsed UTC JSON strings as Date tokens and was corrected to inspect wire strings. These failures are retained locally, not counted as final PASS.

Independent review identified two P2 issues before completion: route classification differed from Web API's case/trailing-slash semantics, and rejected account/IP churn could consume the bounded limiter cache. Focused RED reproduced six route variants, two cache-exhaustion cases and lowercase-POST CSRF bypass. Fixes unify method/path admission and reject exhausted counterparts before allocating cache entries. Fourteen config/route RED failures and three capacity/method RED failures were followed by final147GREEN. Review of the corrected source/tests found **no unresolved actionable finding**; the reviewer did not run tests. Diff/local-link/encoding checks and preserved historical schema/grants/fake-evidence boundaries are verified in the documentation checkpoint.

Documentation checks: `git diff --check 4290220` (backend) and `git diff --check 60e5210` (web) PASS. The ignored local `node TestResults/web2-04-doc-check.mjs <backend-worktree> <web-worktree>` checked393 backend and58 web relative Markdown targets, UTF-8/EOL rules and changed C# BOM/CRLF with0 errors. Diff inventories confirm no historical SQL scripts, global package pins, vendor, deployment grants, web runtime/tests/package lock or WEB-01–08 evidence changed. Both original main checkouts remain untouched; work is preserved in the isolated task branches.

**Decision:** WEB2-04 DONE for backend auth endpoints, focused SQL/HTTP and published IIS Express scope. Per-worker throttling resets on recycle; operator audit destination/retention and a multi-worker abuse policy require deployment validation. Full IIS, browser cookie enforcement/localStorage/multi-tab lifecycle, physical network-loss injection, Sales/Catalog HTTP, Gate A–D and production rollout remain **NOT RUN / pending**. No push, PR or merge was performed; WEB2-05–10 remain planned.
