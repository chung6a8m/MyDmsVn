# Backend WEB2 — HTTP/JWT/Sales execution evidence

**Date created:** 2026-10-31. **Status:** PLANNED / DOCUMENTATION ONLY / NO HTTP, IIS, SQL, JWT OR TESTS RUN by this PR. Cross-repo plan: [server companion](../../plans/20261031-003-web2-http-jwt-sales-server.md); [contract](../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md); [React roadmap](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261031-002-http-jwt-sales-vertical-slice.md).

## Planning baseline

- Backend inspected main `5b9f960ee4048581f107e9ee6350e1dfad462b53`, web inspected main `90ef55c7408c25a6107310199890d809bc329adb`; implementation PRs must recheck current source/branches.
- Current application exposes Local in-process handlers and migration scripts001–010. WEB2 source/runtime does **not** yet exist.
- Owner decisions: net48/IIS thin host; short JWT Bearer access in browser localStorage; GUID refresh cookie HttpOnly Secure Strict; hash and rotate in SQL; separate HTTP DTO v1 to protect int64/decimal precision.
- Legacy release GUI and existing SQL test acceptance are retained under their own scope and **not inherited**.

## Task register

| Task | Server PR / commit | Actual tests and environment | Result | Unresolved risks |
|---|---|---|---|---|
| WEB2-01 | — | NOT RUN | PLANNED | Contract/dependency compatibility |
| WEB2-02 | — | NOT RUN | PLANNED | IIS/actor-scoped DI |
| WEB2-03 | — | NOT RUN | PLANNED | DbUp/SQL principal/session rotation |
| WEB2-04 | — | NOT RUN | PLANNED | JWT/cookie/CSRF/rotation |
| WEB2-05 | — | NOT RUN | PLANNED | Sales/Catalog HTTP golden |
| WEB2-08/09 | — | NOT RUN | PLANNED | Browser integration/real write |
| WEB2-10 | — | NOT RUN | PLANNED | Full candidate acceptance |

## Per-task evidence instructions

Append candidate date and SHA, focused changed files, real Windows/IIS/.NET Framework build/runtime, SQL Server DB schema version (nonproduction only), restricted principal and grant verification, binding redirects/secret provenance (never secret value), test project commands/count/results, HTTP golden status/JSON traces, cookie flags with **redacted values**, JWT negative cases, SQL concurrency/rollback/replay, 2 actors, Sales Post stock/value/COGS. Separate automated MSW/unit, live HTTP, live SQL and attended browser results. Mark missing local infrastructure **BLOCKED / NOT RUN**, never inherit PASS from Local/WinForms or plan documentation. Production deployment intentionally excluded.
