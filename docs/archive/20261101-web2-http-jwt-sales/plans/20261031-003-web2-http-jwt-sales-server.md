# WEB2 companion — HTTP/JWT/Sales server vertical slice (net48)
> **ARCHIVED — historical WEB2 execution plan, 2026-11-01.** Original 2026-10-31 task status and acceptance text below is preserved for provenance, including the then-`IN_PROGRESS / full IIS BLOCKED` gate. **Owner closed the bounded WEB2 roadmap on 2026-11-01 as COMPLETED WITH OWNER_ACCEPTED_EXCEPTION** for **full IIS only**, without claiming full IIS or production was tested. See the [archive decision](../README.md), [current execution index](../../../plans/README.md) and [original evidence](../../../evidence/web2/20261031-http-jwt-sales.md). Original relative Markdown links have been rebased. This archive is **not an active task queue**.


**Ngày:** 2026-10-31. **Owner chọn:** giữ `net48`, thin HTTP host, Bearer JWT short-lived trong frontend localStorage, refresh GUID trong HttpOnly cookie. **Trạng thái:** ACTIVE / WEB2-01–05 DONE (bounded contract/host/SQL session/auth/Sales/Catalog HTTP); WEB2-06–09 completed in the web companion; WEB2-10 IN PROGRESS / full IIS BLOCKED. **Repo:** `chung6a8m/MyDmsVn`; **phối hợp:** [ReactWebUI Roadmap 02](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261031-002-http-jwt-sales-vertical-slice.md). Các task HTTP ở đây **song song, độc lập** với CEX-A01–A05 (WinForms); không thay thế active composition roadmap.

**Bắt buộc đọc:** [web2 contract](../../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md), [architecture](../../../ARCHITECTURE.md), [security](../../../SECURITY.md), [contracts](../../../CONTRACTS.md), [SQL security](../../../CONFIGURATION_AND_SQL_SECURITY.md), [test policy](../../../TEST_POLICY.md), [direct sales](../../../DIRECT_SALES.md), `src/MyDmsVn.Server.Application/Identity`, `Sales`, `src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs` và `src/MyDmsVn.Server.DbMigrator`. Base: `main@5b9f960`; recheck fresh HEAD trước implementation. Không sao chép Runtime/desktop acceptance sang API.

## Deliverable / task map

| Task (globally tracked in Web UI roadmap) | Owner | Depends | Output / gate | State |
|---|---|---|---|---|
| WEB2-01 — contract & host preflight | Both | — | ADR/HTTP examples/locked compatible dependencies, pilot HTTPS origin, exact schema admission | DONE (bounded) |
| WEB2-02 — host + scoped actor | Server | WEB2-01 | Web API 2 `net48` host, DI, bearer validate per request, HTTPS/correlation/health, no Desktop DI | DONE (bounded) |
| WEB2-03 — refresh persistence | Server | WEB2-01 | DbUp 011, restricted SQL permissions, hashed token store, atomic session/rotation/replay tests | DONE (bounded) |
| WEB2-04 — login/refresh/logout/capabilities | Server | WEB2-02,03 | JWT issue/validate, cookies+CSRF, active actor, revocation, auth negative suite | DONE (bounded) |
| WEB2-05 — Sales + Catalog HTTP | Server | WEB2-02,04 | v1 routes, mapping, lossless golden fixtures, permission + SQL integration tests | DONE (bounded) |
| WEB2-08/09 — integrated read/write | Server assists Web | WEB2-05 | Fault injection/read/write contract parity and defect fixes, not new business rules | DONE (bounded frontend HTTP harness; web companion) |
| WEB2-10 — pilot acceptance | Both | all | Verified IIS/SQL/browser candidate, evidence and handoff | IN PROGRESS / full IIS BLOCKED |

**PR policy:** mỗi task hoặc subtask độc lập thành PR nhỏ, base latest main, chỉ merge sau kiểm chứng; PR title kèm mã WEB2. Dừng sau từng gate, không tự chuyển sang production hoặc thay CEX. Server task hoàn thành không có nghĩa frontend/Web2 toàn scope hoàn tất.

## WEB2-01 — khảo sát, chốt kỹ thuật và golden contracts

1. Khảo sát `.csproj`/`Directory.Packages.props`/`packages.lock.json` và SDK, lựa chọn Web API 2, OWIN/SystemWeb, JWT signing libraries **thật sự tương thích net48**. Xác minh assembly redirects, IIS integrated pipeline, cookie writer. Không thêm package pin phiên bản chưa xác thực; không nâng toàn solution.
2. Chốt pilot same-origin HTTPS: public URL, reverse proxy/trusted forwarding allowlist, IIS static SPA + API route/fallback, config signing key/DB connection/issuer/audience/expiry và nguồn secret ngoài repo. Không đưa secret vào docs or logs.
3. Chốt web DTO v1: int64/decimal là string, int32 numeric, SaleDate date-only, post ack; lập golden JSON cho success 400/401/403/404/409, User/Capability, Sales create/update/get/list/post, Catalog lookup. Ghi status mapping, legacy Local unaffected.
4. Xác minh schema10 hiện tại, DbUp preflight (Empty/exact10/exact9 case), privilege SQL; mô tả rõ 011 chỉ chạy trên supported state, không tác động historical schema9 package hoặc unsupported prefixes.
5. Nghiệm thu: ADR ghi trade-off localStorage XSS; contract fixtures/roundtrips thử được, target/test matrix cụ thể, không code thực thi nếu dependency/hosting compatibility chưa xác minh. Tách OPEN/blocked thực sự thay vì tự chọn ASP.NET Core.

**WEB2-01 result:** [ADR 0012](../../../adr/0012-web2-net48-host-preflight.md), [wire v1 corpus](../../../contracts/web2-v1/README.md), [isolated net48 probe](../../../../tools/Web2.HostPreflight/README.md), [actual evidence](../../../evidence/web2/20261031-http-jwt-sales.md). GO cho implementation kế tiếp; NO-GO cho runtime/deployment acceptance.

## WEB2-02 — web host, request DI, bearer authorization

1. Tạo project `src/MyDmsVn.Server.HttpHost` `net48` + `tests/MyDmsVn.Server.HttpHost.Tests`; đưa vào `MyDmsVn.sln` (không thay UiLab solution nếu không cần). Dependency chỉ tới Contracts/Application/Infrastructure cho composition; controllers không tham chiếu Desktop.Local/WinForms.
2. IIS/SystemWeb Web API 2 pipeline nhận `/api/v1/*`; register per-request MediatR scope và HTTP `ICurrentUserAccessor` instance immutable sau khi xác thực. Anonymous login scope phải là `CurrentUser.Anonymous`. Không dùng `LocalDesktopSession` hoặc session singleton.
3. Xác thực Bearer token ở middleware/HTTP filter: alg whitelist, signature/issuer/audience/lifetime/`kid`, required claims/positive userId, `sid` tồn tại chưa revoke, active account. Khởi tạo CurrentUser từ trusted SQL account; authorizer của Application vẫn chạy sau middleware; không dùng JWT grant-list.
4. Reuse `ApiResponse<T>` mapping và `ApiJson` conventions; return actual HTTP status, correlation ID, no stack/SQL/token. API 401 là JSON không redirect; Content-Type/Cache-Control/no-sniff, request limits, HTTPS-only, health check không chứa sensitive data.
5. Test: anonymous/forged/expired/revoked/wrong audience/wrong key/wrong alg/active-disabled; concurrent requests với actor A/B, no cross-request contamination; DI scope disposal, no Desktop registration, protected endpoints reject prehandler; IIS warm-up/static fallback.

## WEB2-03 — SQL refresh session lifecycle

1. Add `011_WebAuthSessions.sql` (recheck next script number) có `WebAuthSessions` + `WebRefreshTokens`, unique 32-byte SHA256 hashes, indexes, FK, expiry/revocation, family/lineage, UTC timestamps, no plaintext. New migration additive only; update schema checker, DbUp admission, test baselines/Respawn reset scripts **có kiểm tra supported schema**, không tự sửa script cũ.
2. Interface application sở hữu issue/rotate/revoke/getSession (không reference SQL). Infrastructure dùng typed SqlClient trong transaction riêng, lock session+token row, consumed parent + child inserted atomic; per-session absolute/idle expiry, user-active check. Parent reused => revoke family/session (not silently reissue child); unknown hash => 401.
3. Extend provisioning & `SqlRuntimePrincipalValidator` with separate restricted web principal/grants on auth tables/procs; keep desktop runtime grant unchanged, no maintenance credentials. Add SQL privilege-negative tests (no DDL, no broad table access), principal roles and migration refusal on incomplete/old/too-new DB.
4. Fail closed on unexpected DB/network errors; no partial rotation. Diagnostics audit only user/session ID pseudonymous + error category, never raw token/hash/password/JWT. Cleanup expired rows using explicit maintenance with retention, not request-path deletion affecting replay evidence.
5. Tests real SQL: concurrent refresh same parent => at most one child; replay revokes active family; concurrent logout; disabled user, expiry, startup schema change, unique token hash, nonproduction allocation guard, cleanup/rollback.

## WEB2-04 — auth endpoints & security hardening

1. Reuse `LoginCommandHandler` password verification and rehash. On success create session+refresh atomically before returning access JWT; no auth token if persistence fails. Generic invalid credential response, rate-limit by normalized account/IP; no password echo.
2. Cookie must match `__Host-mydmsvn-rt; HttpOnly; Secure; SameSite=Strict; Path=/; no Domain`, no token in JSON/body/query. `POST refresh` rotates, issues new JWT and resets cookie, `POST logout` revokes session and clears with same scope. All auth cookie writes require exact Origin + browser fetch metadata/custom CSRF header; same-origin only. Use TLS/strong cache controls.
3. JWT defaults access 10min; refresh idle 7d/absolute 30d; configurable bounded values and test clock; HS256 256-bit secret from secure deployment config (not committed). `kid` rotation; test unknown kid/wrong audience/exp/nbf/alg none, malformed signature, session revoked even if JWT valid.
4. `GET me` uses authoritative current user; `GET capabilities?feature=sales` checks three existing permission keys via `IPermissionAuthorizationService`, no new grants or superuser bypass.
5. Tests browser-like cookie attributes, CSRF cross-origin/missing Origin, double refresh, different users; signout revokes unexpired access JWT, SQL failure safe, no credential/token in logs.

### WEB2-04 implementation checklist — 2026-10-31

Baseline: backend `4290220` (WEB2-02/03 merged), web `60e5210`; isolated branches `codex/web2-04-auth` / `codex/web2-04-status`. The approved contract above remains authoritative; this checklist records implementation seams, not a second protocol specification.

- [x] HTTP auth: `src/MyDmsVn.Server.HttpHost` owns narrow anonymous cookie routes, `AuthController`, signing/cookie/body/throttle helpers and request-scoped Application calls. `tests/MyDmsVn.Server.HttpHost.Tests` first demonstrates missing routes, then checks successful lifecycle and negative admission. Baseline: 63/63 PASS.
- [x] Lifetime configuration: `Server.Application/Identity/WebAuthSessionOptions.cs`, `Server.Infrastructure/Identity/SqlWebAuthSessionStore.cs`, host configuration/Startup; defaults remain idle7d/absolute30d, operator may shorten within idle1–7d/absolute1–30d with idle <= absolute. Pure configuration tests and a real SQL issue/rotate/absolute-expiry regression prove enforcement. No schema/grant change.
- [x] SQL/host integration: extend the existing Security test owner and owned allocation/IIS Express fixture. Exercise actual LoginCommand/password verification, independently evaluated Read/Write/Post permissions, rotation/replay/concurrency/logout/disabled account. Run SQL owners sequentially; inspect nonempty TRX and cleanup.
- [x] Final verification: focused HttpHost suite, affected Security cases, published IIS Express HTTPS smoke where available; independent code/security review and encoding/link/diff checks. Record actual source/test SHA, runtime, commands, counts, failures and limitations in WEB2 evidence in both repos. No automatic merge.

Review focus: malformed/duplicate credential or cookie input must not issue a session; an expired optional Bearer must not block cookie-only refresh/logout; throttling must stay bounded under changing accounts/tokens/IPs; response/audit failures after rotation must never replay a token; logout and account disable must deny an otherwise valid unexpired JWT on the next request.

## WEB2-05 — Sales/Catalog HTTP without duplicating business

1. Implement routes from [contract](../../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md) with HTTP DTO v1 mapping. JSON `long` IDs + `decimal` **strings**; checked conversions/range; date-only/date/timestamp logic; reject duplicate/ambiguous/malformed body; preserve existing C# request classes and Local adapters.
2. Sales List/Get/Create/Update/Post call existing `ListSalesReceiptsQuery`, `GetSalesReceiptByIdQuery`, `CreateSalesReceiptDraftCommand`, `UpdateSalesReceiptDraftCommand`, `PostSalesReceiptCommand`. Do not invoke SQL directly or wrap command twice; Post only returns ack (no eager Get).
3. Catalog lookup for product/customer/warehouse/employee uses existing feature Application queries with their own Read permissions; active-only choices for editing, existing inactive reference labels come from Sales/Get.
4. Enforce JSON/map invariants: list PageSize=25 (client-specified), opaque cursor, filters Product Any; Create/Update rows ordered, saleLineId new=`"0"`; server rowversion opaque. No browser-provided actor or synthetic permission.
5. Tests: golden two-way JSON >2^53/int64 max and decimal boundary without Number, duplicate product mapping, 1–200 rows, 401/403/409, stale version, Post ack + Get denied, posted COGS only from SQL; compare handler/Local/HTTP business outcomes and error taxonomy.

### WEB2-05 implementation checklist — 2026-10-31

Baseline backend `f563d32` (local verified WEB2-04, not merged into main), web `bb83abb`; branches `codex/web2-05-sales` / `codex/web2-05-status`. Scope follows the canonical wire corpus; no new business/schema/grant contract.

- [x] HttpHost request parser/response mapper and Sales/Catalog controllers: strict wire scalars, dates, unknown/duplicate fields, query limits; existing MediatR handlers and independent permissions; Post acknowledgement without Get.
- [x] HttpHost.Tests RED/GREEN and golden corpus consumption: exact int64/decimal, inactive labels, nullable costs, duplicate/200 rows, validation field paths, HTTP status/error taxonomy.
- [x] Restricted SQL HTTP tests: actual login, lookup rights, Draft create/update/list/get, filter-bound keyset, Post/stock/cost, stale409 and Post-only acknowledgement with Get403; owned allocation cleanup.
- [x] Independent review, focused final HTTP/SQL and published IIS smoke; source/test candidate `3cd853df4b6d9aa6716533d891229debe5746289`, actual commands/counts/environment in [WEB2 evidence](../../../evidence/web2/20261031-http-jwt-sales.md#web2-05--2026-10-31-salescatalog-endpoints). Browser integration/full IIS remain later gates.

## WEB2-08/09 — integration assistance

- Supply SQL-backed fault injection **at HTTP boundary**, never fake a successful Sales commit by reimplementing SQL. Test network loss after commit, response corrupt/missing, concurrent edits, authorization revocation, Post ack returned but Get fails.
- Read denied after Write/Post is not a mutation failure. Never grant Read implicitly. Side effects committed before response are authoritative; client retry only allowed for safe reads; no backend idempotency contract in this roadmap.
- Backend changes required by frontend only in mapper/transport/auth seam, not new pricing/tax/returns/stock protocols.

## WEB2-10 — acceptance & handoff

Gate A: authenticated current user/capabilities, real lookup/list/Get through IIS with 2 distinct actors. Gate B: Create/Update authoritative JSON, exact decimals/int64, rowversion/conflict. Gate C: Post creates correct stock/COGS server-side; lost-response/refresh-replay/logout/Read denial scenarios are explicit. Gate D: SSL/cookie/CSRF/CSP, SQL restricted principal, nonproduction migration/reset and reproducible evidence.

Record at `docs/evidence/web2/20261031-http-jwt-sales.md` exact server/client SHAs, Node/.NET/IIS/OS/SQL, command/count, security negatives, schema version, origin, screenshots sanitized, PASS/FAIL/BLOCKED, known risks. Windows/SQL tests not run -> BLOCKED / NOT RUN. Separate feature acceptance from production rollout; deployment of schema11 and legacy desktop coexistence remains controlled pilot only.

### WEB2-10 execution checkpoint — 2026-10-31

- [x] Published React + net48 host on existing trusted same-origin localhost HTTPS with disposable schema11/restricted login; Chromium and Edge exercise real auth, independent rights, Save/Post and response-loss recovery with independent SQL stock/value/COGS oracles.
- [x] Reproduce and fix native static asset404 and CSP compatibility: managed asset routing, fresh style nonce for approved component styles, frontend Zod JIT disabled; no relaxed script policy or auth bypass.
- [x] Preserve owned worker/SQL cleanup and publish sanitized candidate-specific [evidence](../../../evidence/web2/20261031-http-jwt-sales.md#web2-10--2026-10-31-integrated-pilot-and-handoff).
- [ ] Full IIS deployment acceptance: BLOCKED, this Windows11 Home machine has no full IIS/W3SVC/WAS/appcmd. IIS Express is not a substitute.
- [ ] Operator audit destination/retention and full IIS app-pool/TLS/binding/ACL acceptance; attended candidate-specific IME/accessibility remains separate. No production release or mixed desktop/schema11 writer approval.

The task stays **IN PROGRESS / full IIS acceptance BLOCKED**. Backend source/test `9480d1b`; web source/test `36c1bca`. No schema/grant/business-handler changes or production deployment were required.
