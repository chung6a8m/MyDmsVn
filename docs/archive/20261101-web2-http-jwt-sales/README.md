# WEB2 net48 HTTP/JWT/Sales server companion — archive (2026-11-01)

**Decision: COMPLETED WITH OWNER_ACCEPTED_EXCEPTION — 2026-11-01.** The Owner accepted the single final **full-IIS acceptance BLOCKED** gate as a scoped exception. **WEB2 execution closes**, but **full IIS and production are NOT APPROVED/NOT RUN**. This is a documentation decision, not new test evidence.

Read [frozen WEB2 server companion plan](plans/20261031-003-web2-http-jwt-sales-server.md), [original backend evidence](../../evidence/web2/20261031-http-jwt-sales.md#2026-11-01--owner-closeout-and-full-iis-exception), [current execution index](../../plans/README.md), and [frontend companion closeout](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/archive/20261101-web2-http-jwt-sales/README.md).

## Inspected revisions and accepted scope

- Backend `main@6a62a98993e698c3baf146e30200f754db3cb37f` ([PR #72](https://github.com/chung6a8m/MyDmsVn/pull/72)); Web `main@1d75917f7a3f7e4ae7a891bd242b2d9bb70c9b11` ([PR #10](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/10)). Final WEB2-10 tested source/test candidates: backend `9480d1b79c012b50aff725612989c7dc861b2973`, web `36c1bca80e02ea3f42ecf08578072d68ddd6e6de`.
- WEB2-01–05 are **DONE (bounded)** for host/preflight/DbUp refresh sessions/JWT/Sales and Catalog HTTP, with WEB2-06–09 **DONE (bounded)** in Web companion.
- WEB2-10 is **DONE for IIS Express HTTPS + real restricted SQL + real React Chromium/Edge pilot**, with **full IIS = OWNER_ACCEPTED_EXCEPTION / NOT RUN**. Whole WEB2 roadmap disposition: **COMPLETED WITH OWNER-ACCEPTED EXCEPTION**.

**Recorded results remain tied to the original candidates:** **325/325 selected .NET rows PASS** (264 HTTP, 45 focused security incl. 36 real SQL, 13 migration, 1 System.Web smoke, 2 browser wrapper rows); **16/16 actual live browser cases PASS** (8 Chromium + 8 Edge) are separately counted. Web has 245/245 unit/component, 91/91 Chromium regression and 1/1 production artifact. The independently inspected SQL oracle verifies Posted/stock/value/COGS/ledger under IIS Express and disposable schema11. No full IIS tests ran in this scope.

## Boundaries deliberately not promoted to PASS

- **Exception:** absent W3SVC/WAS/appcmd prevented full IIS execution; the Owner waived this roadmap gate **only**, not IIS suitability or a production-release sign-off.
- IIS app-pool identity/ACL/binding/TLS, audit destination/retention, multiworker rate limiting, operations/recovery and candidate-specific attended IME/accessibility remain **NOT VERIFIED/DEFERRED**, not retroactively approved. JWT in browser localStorage retains XSS risk; no durable frontend drafts, backend idempotency journal, or mixed desktop/schema11 writer acceptance.
- CEX-A01–A05 WinForms composition roadmap remains **active separately** and unchanged. Historical desktop/Local/SQL/GUI test and Owner approvals are not part of WEB2 closeout. Production release prerequisites remain UNACCEPTED/UNSCHEDULED.

## Archive routing and preservation

The [archived plan](plans/20261031-003-web2-http-jwt-sales-server.md) preserves the original instructions and dated status as historical. Its `WEB2-10 IN PROGRESS` references describe 2026-10-31, superseded by the Owner's 2026-11-01 **roadmap-disposition** decision only. The [former plan path](../../plans/20261031-003-web2-http-jwt-sales-server.md) remains a compatibility page with heading anchors.

The [HTTP/JWT/wire design](../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md), [host runbook](../../../src/MyDmsVn.Server.HttpHost/README.md) and original [evidence](../../evidence/web2/20261031-http-jwt-sales.md) remain current technical references; tests/source/SQL/auth code remain unchanged. Further rollout/hardening or another Web module requires an independently selected roadmap. Documentation-only archive requires link/diff/encoding verification, not .NET/IIS/SQL reruns, and claims no new runtime PASS.
