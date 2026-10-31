# Backend WEB2 — HTTP/JWT/Sales execution evidence

**Date created:** 2026-10-31. **Status:** WEB2-01 DONE (contract/dependency preflight only); later tasks PLANNED. Isolated net48/Node checks PASS; live HTTP/IIS/SQL/browser acceptance NOT RUN. Cross-repo plan: [server companion](../../plans/20261031-003-web2-http-jwt-sales-server.md); [contract](../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md); [React roadmap](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261031-002-http-jwt-sales-vertical-slice.md).

## Planning baseline

- Backend inspected main `5b9f960ee4048581f107e9ee6350e1dfad462b53`, web inspected main `90ef55c7408c25a6107310199890d809bc329adb`; implementation PRs must recheck current source/branches.
- Current application exposes Local in-process handlers and migration scripts001–010. WEB2 source/runtime does **not** yet exist.
- Owner decisions: net48/IIS thin host; short JWT Bearer access in browser localStorage; GUID refresh cookie HttpOnly Secure Strict; hash and rotate in SQL; separate HTTP DTO v1 to protect int64/decimal precision.
- Legacy release GUI and existing SQL test acceptance are retained under their own scope and **not inherited**.

## Task register

| Task | Server PR / commit | Actual tests and environment | Result | Unresolved risks |
|---|---|---|---|---|
| WEB2-01 | `350358ae9f8519efd54d46d61b80d3b7887c1dcd` | Windows/net48 24 probe rows; Node 59 corpus tests; docs checks | PASS / DONE (bounded) | No production host/auth/mapper, IIS, SQL or browser acceptance |
| WEB2-02 | — | NOT RUN | PLANNED | IIS/actor-scoped DI |
| WEB2-03 | — | NOT RUN | PLANNED | DbUp/SQL principal/session rotation |
| WEB2-04 | — | NOT RUN | PLANNED | JWT/cookie/CSRF/rotation |
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
