# WEB2 — HTTP host, JWT, refresh sessions và Sales wire contract

**Chọn bởi Owner:** 2026-10-31. **Current disposition (Owner 2026-11-01): COMPLETED WITH OWNER_ACCEPTED_EXCEPTION.** WEB2-01–09 DONE (bounded); WEB2-10 DONE in proven IIS Express HTTPS + SQL Server + Chromium/Edge scope; **full IIS acceptance NOT RUN / Owner-accepted exception**, not PASS or production approval. [Archive](../archive/20261101-web2-http-jwt-sales/README.md), [backend evidence](../evidence/web2/20261031-http-jwt-sales.md#2026-11-01--owner-closeout-and-full-iis-exception). The HTTP/auth/wire contract remains implemented technical guidance: keep `net48`, Windows/IIS, ASP.NET Web API 2, JWT short access in browser localStorage, GUID HttpOnly Secure Strict rotating refresh; historical implementation instructions are not active.

## 1. Bất biến và phạm vi

- **Giữ** Domain/Server.Application, MediatR + authorization/validation pipeline, ErrorOr -> `ApiResponse<T>`, SQL UoW/transaction/stock locks, DbUp, `net48`. Không thay đổi flow Sales, schema 001–010, desktop Local adapter, desktop UI, hoặc chuyển ASP.NET Core.
- HTTP host là **trusted server** mới. Mỗi request có DI scope, `ICurrentUserAccessor` scoped immutable; KHÔNG dùng singleton `LocalDesktopSession`, không lấy `actorKey`/capabilities/JWT permission claim làm quyền. Gọi cùng handlers trong `MyDmsVn.Server.Application`. Account đang active + session chưa revoke được kiểm tra trên request; Application tự kiểm tra `Sales.Receipts.Read/Write/Post` và catalog permissions.
- Mục tiêu chỉ Sales Draft/Create/Update/List/Get/Post, bốn catalog lookups, login/refresh/logout/current user/capabilities, test trên SQL **non-production**. Không Security Administration API, desktop cutover, user provisioning, payments, autosave offline, print hay deploy production.
- UI giữ Nova/Base UI, AG Grid Community, Dockview và session-owned raw buffers/barrier; RHF không được trở thành canonical owner của Sales.
- Source/business DTO hiện có không được đổi chỉ để HTTP dùng số JS an toàn. Thêm versioned *wire DTO* ở host/mapping, kiểm chứng đối xứng.

## 2. Host và composition

**Ứng viên được chốt cho plan:** project `src/MyDmsVn.Server.HttpHost` targeting `net48`, Web API 2 trên IIS/System.Web; dùng OWIN v4 middleware nếu kiểm chứng phiên bản package và integration thực tế. Host chỉ chứa routes, auth middleware/filters, binding, wire mapping, HTTP/DI composition và safe diagnostics. Tên folder/class có thể điều chỉnh hẹp theo convention hiện có, cập nhật handoff và tests trong cùng PR.

- Tái sử dụng `AddServerApplication`, `AddSqlPersistence` và services scoped hiện có, nhưng **không gọi** `AddLocalDesktopAdapter` hoặc đăng ký các desktop session/Vm services. Dùng `IRequestCurrentUserAccessor` của HTTP scope; login anonymous, Bearer-protected requests lấy danh tính từ JWT đã thẩm định và SQL session.
- Token/session store nằm trong `Server.Application.Identity` abstraction và `Server.Infrastructure.Identity` SQL adapter, không đặt SQL access trong Web API controller. Auth response/cookie transport thuộc host. Không tạo một `CurrentUser` mutable dùng chung nhiều request.
- Chọn single **same-origin HTTPS**: `https://<pilot-host>/` phục vụ SPA, `/api/v1/*` phục vụ HTTP API. Production-like pilot không cấu hình credentialed cross-origin CORS; Vite dev proxy cùng-origin (đồng nhất cookie scope). Khả năng dùng IIS static files/route fallback phải có smoke test ở WEB2-02.
- Host xử lý HTTP status/correlation một lần. JSON success `{"data":...}`, failure `{"error":{"code","message","details","correlationId"}}` theo `ApiJson`; status từ transport, không thêm `IsSuccess` vào wire. Cấm stack trace/SQL exception/password/token vào response/log.
- Không dùng desktop `ApiStatusCode` như bằng chứng auth browser. Request validation, request body max, query parsing và type-limit phải độc lập với serializer/model binder mặc định; reject unknown/malformed body trước mutation.
- JWT middleware kiểm tra chữ ký, alg cố định, `kid` allowlist, issuer, audience, `nbf/iat/exp`, clock skew <= 30 giây, `sub` userId dương, `sid` + `jti`. Không nhận bearer token từ query/cookie; chỉ `Authorization: Bearer <token>`. Không nhét effective permission grants vào JWT. User active, session active được truy vấn server-side mỗi protected request để logout/revoke có hiệu lực ngay.

## 3. Auth flow — quyết định Owner và an toàn

### 3.1 Storage/định dạng cố định

| Nội dung | Chính sách |
|---|---|
| Access token | JWT **Bearer**, đề xuất lifetime 10 phút; **client lưu trong localStorage** theo lựa chọn Owner; không lưu password/refresh/session secret tại đó. Client kiểm chứng `expiresAtUtc` để lập lịch refresh, server luôn tự xác thực JWT. |
| JWT ký | HS256 với key >=256 bit random do operator provision ngoài repo/package, `kid`, active + previous validation keys để rotate; bắt buộc whitelist algorithm, không nhận `none` hoặc header key URL. Check package API tương thích net48/strong-name/binding redirects. |
| Refresh token | GUID v4 opaque do CSPRNG tạo, canonical `D` string, ngẫu nhiên mỗi lần login/refresh; **không** đưa vào JSON response, URL, JS hoặc localStorage. Lưu **SHA-256 của UTF-8 canonical GUID** (`binary(32)`) và session lineage trong SQL; raw token chỉ xuất hiện tại Set-Cookie/request cookie và ngắn hạn trong server memory. |
| Cookie | `__Host-mydmsvn-rt=<GUID>; HttpOnly; Secure; SameSite=Strict; Path=/`; **không** có Domain; `Max-Age` tới hết hiệu lực refresh. HTTPS kể cả môi trường pilot; mọi cách xóa cookie dùng **đúng** name/path/attributes. |
| Refresh lifetime | Mặc định **7 ngày idle** và **30 ngày absolute** tính từ login; hạn mới là min(now+7d, original absolute). TTL cấu hình server theo giới hạn đã test; không tin clock client. |
| Session | Login tạo một AuthSession/browse-device family; logout/replay/account disable revoke session đó, chặn cả JWT chưa hết hạn. Không đồng bộ quyền qua JWT; quyền server đánh giá theo DB mỗi operation. |

**Rủi ro được ghi rõ:** access token trong localStorage bị JavaScript cùng origin đọc được; XSS có thể đánh cắp bearer token. Phương án này do Owner chọn nhưng **không tương đương cookie HttpOnly/BFF về XSS resilience**. Bắt buộc security gate: CSP production không có `unsafe-inline` cho script, không inject raw HTML, sanitize untrusted content, dependency review, không third-party script cùng origin, HTTPS + HSTS + secure headers, token TTL thấp, không log/token trong telemetry/URL, browser XSS tests. Nếu gate không đạt: không gọi production-ready; đề xuất thay token storage cần Owner quyết định riêng.

WEB2-10 triển khai CSP style nonce theo [host runbook](../../src/MyDmsVn.Server.HttpHost/README.md): nonce riêng mỗi HTML được bind với metadata cho AG Grid/Dockview; script policy không có unsafe-inline/unsafe-eval. Đây là hardening của candidate, không thay lựa chọn localStorage hoặc đóng full IIS acceptance.

### 3.2 Endpoints phiên

| Route / method | Authorization | Data / side effects |
|---|---|---|
| `POST /api/v1/auth/login` | anonymous; rate limit | JSON `{username,password}` -> `data:{accessToken,expiresAtUtc,user}`; set refresh HttpOnly cookie; giữ `LoginCommand`/current password algorithm, generic invalid-credential error. |
| `POST /api/v1/auth/refresh` | **refresh cookie only**, no Bearer required | Empty body; verify+rotate atomically, active account, CSRF/origin guard; JSON access token/user+expiry, new cookie. Không chấp nhận refresh GUID từ header/body/query. |
| `POST /api/v1/auth/logout` | refresh cookie, Bearer optional | Revoke session/family server-side nếu còn hợp lệ; clear cookie; safe idempotent response; CSRF/origin guard. Không gọi logout để reset remote session khác. |
| `GET /api/v1/auth/me` | Bearer | CurrentUserDto từ authenticated, active actor/session (not JWT display-name claim). |
| `GET /api/v1/auth/capabilities?feature=sales` | Bearer | `{read,write,post}` qua `IPermissionAuthorizationService` (từng code key); chỉ UI hint, server kiểm tra lại từng operation. |

- Thêm `Cache-Control: no-store` cho auth responses (kể cả lỗi), `X-Content-Type-Options: nosniff`; không cho redirect 302 đến login HTML ở API.
- `POST login/refresh/logout` là cookie-changing endpoints: reject Origin không match exact configured public origin (bao gồm missing Origin trên browser path); check `Sec-Fetch-Site` và bắt buộc header `X-Requested-With: XMLHttpRequest` cho browser cookie-changing endpoints. `Origin` do browser tạo, client không tự gán; missing/cross-origin đều bị từ chối. Pilot template origin là `https://web2.mydmsvn.test`; đây là cấu hình mục tiêu, chưa là bằng chứng DNS/certificate/IIS đã chạy. SameSite=Strict **không thay** đầy đủ CSRF validation; CORS không mở wildcard. Host trusted proxy chỉ nhận Forwarded/Host từ allowlisted proxy, không suy cookie Secure/redirect từ header khách gửi trực tiếp.
- Rate-limit login theo account normalize + IP với bounded attempts/backoff, generic errors, không tạo user enumeration; refresh rate-limit theo cookie/session and IP. Không đưa password/refresh/access vào diagnostics.

### 3.3 Rotation, replay, multi-tab

- SQL **transaction** khóa session + token row, verify hash, `ConsumedAtUtc/RevokedAtUtc/ExpiresAtUtc` và User active; compare-and-swap consume parent, insert child token hash, advance idle expiry; trả **chỉ** GUID child một lần. Unique constraint/token lineage; không có race cấp hai child. Không lưu child plaintext để tái-phát.
- Token parent đã consumed được gửi lại => **reuse**: revoke cả family/session, audit sanitized và trả 401; request không tìm được hash trả 401 mà không lộ trạng thái. Expired/revoked token => 401; hành vi revoke family chỉ khi chứng minh được reuse thuộc family active.
- Browser serialize refresh trong cùng tab (single-flight), phối hợp nhiều tab với Web Locks/BroadcastChannel có feature detection; không coi localStorage là mutex atomic. Nếu thiếu bảo đảm cross-tab thì fail closed/reauth; không thêm server grace mơ hồ để trả token plaintext. Refresh response mất sau commit => cookie cũ gây 401/reuse, buộc login; ghi rõ đây là hành vi đã chấp nhận.
- Access 401 chỉ được auto-refresh cho **GET/HEAD** khi chưa refresh; tối đa 1 retry read có kiểm soát, abort/actor generation guard. **Không auto retry Create/Update/Post**, dù sau refresh đã có JWT mới. Preflight refresh trước write nếu JWT gần hết hạn; 401/network timeout/malformed response sau khi write được gửi vẫn qua existing unknown/recovery policy; client không coi abort là rollback.

### 3.4 Session identity và sign-out

- `sid` và account status được validate server-side trên mỗi protected request; current actor lấy user details từ server, không từ Bearer claim về username/displayName.
- Frontend `actorGeneration` đổi khi refresh failure, logout, different-login hoặc same-user new auth session; đang pending/unknown phải giữ private state của owner cũ nhưng **không** cho mutate hay hiển thị dưới actor mới.
- Voluntary logout đi qua document close admission trước khi request revoke; khi forced expiry thì khóa toàn bộ protected view/actions, không tự Discard, không auto-persist raw sensitive drafts. Same-owner reauth mới có thể xem xét khôi phục trong RAM theo policy; cross-owner data phải được cô lập.

## 4. Wire contract v1 — mapping rõ, không mất precision

Chọn HTTP-specific DTO v1 tại host; **không sửa** `MyDmsVn.Contracts.SalesContracts` của Local. Serializer áp dụng envelope/error/camelCase hiện có, dùng dedicated mapper + golden fixtures. [Canonical WEB2 v1 field tables/corpus](../contracts/web2-v1/README.md) và [manifest](../contracts/web2-v1/manifest.json) sở hữu toàn bộ field/route/status examples; frontend chỉ liên kết, không sao chép spec. WEB2-01 corpus checks không phải runtime HTTP mapper/client codec acceptance.

- **Sales `long`:** trên JSON wire **string số thập phân** (`saleId`, `saleLineId`), request từ URL/body parse server-side bằng `long.TryParse` invariant, range > 0 (new saleLineId = **"0"**; Create bắt buộc mọi line = **"0"**, Update mới cho phép existing positive line IDs). Không dùng JS Number trên ID `long`.
- **Catalog `int`:** JSON numeric integer, nằm trong int32; TypeScript parser kiểm số nguyên an toàn trong 32 bit và normalize thành string nội bộ. `lineNo`, `pageSize`, `lineCount` cũng int32.
- **Tất cả `decimal` kể cả nullable cost/profit:** JSON **string invariant decimal**, không exponent, chính xác theo precision của C#; scale trường khác nhau được giữ đúng (không ép postedUnitCost thành 4 decimals). Omitted null theo `ApiJson` được frontend normalize về null, không giả số 0.
- **Ngày:** `saleDate/fromDate/toDate` đúng `yyyy-MM-dd`, không timezone; `postedAtUtc` UTC timestamp với `Z`; `version` opaque Base64 rowversion, `cursor` opaque string.
- **Create:** body từ `SalesSaveSnapshot` chỉ có `saleDate,customerId,warehouseId,employeeId,note,lines`; line gồm `saleLineId:"0",productId:int32,qty:string,unitPrice:string,isGift:boolean`; **không** truyền `clientLineId`, lineNo, unit, cost, status, server number, actor. POST response là **full detail** được server materialize trước commit.
- **Update:** `PUT /api/v1/sales-receipts/{saleId}/draft`, body chứa `expectedVersion` và field Create; `saleId` chỉ ở route, body không có `saleId`; future host reject unknown body fields trước mutation. Result full authoritative detail; concurrency mismatch -> 409.
- **Post:** `POST /api/v1/sales-receipts/{saleId}/post`, body chỉ `{expectedVersion}`; response **ack** `{saleId,status,postedAtUtc,version}`, **không** fake full detail hoặc post-commit Get nội bộ. Frontend đánh dấu Posted/read-only khi ack thành công; Get sau đó có thể fail/403, không biến ack thành failed Post.
- **List:** `GET /api/v1/sales-receipts`, query `fromDate,toDate,customerId,warehouseId,employeeId,status,productId,search,pageSize=25,cursor`. Giữ filter-bound keyset, `saleId DESC`, Product Any, tổng toàn phiếu; giữ next/previous cursor.
- **Get:** `GET /api/v1/sales-receipts/{saleId}` trả full detail với labels, Unit, line ordering + cost khi Posted; không phụ thuộc lookup active list để dựng nhãn cũ.
- **Lookups:** `GET /api/v1/catalog/{products|customers|warehouses|employees}/lookup?search=...&limit=...`; tái dùng Application queries/authorization của từng catalog; không mặc định `Sales.Receipts.Read` cho lookup. Không tạo truy vấn SQL trong controller.
- **Error:** 400 ValidationError, 401 `Auth.Unauthorized` (giữ riêng single `Auth.InvalidCredentials` từ Login), 403 Auth.Forbidden, 404 NotFound, 409 `SalesReceipt.ConcurrencyConflict`/`SalesReceipt.AlreadyPosted` (giữ business code từ `ApiResponseMapper`), 5xx InternalError/Unavailable. Envelope `ApiError` giữ correlation. Validation `lines[<index>].field` phải map dựa **ordered snapshot gửi đi** về `clientLineId`; fieldless issues vẫn hiển thị. Malformed JSON/data không được coi như thành công.

### Contract golden fixtures bắt buộc

Canonical fixtures và lệnh `node --test scripts/check-web2-wire-fixtures.mjs` ở [WEB2 v1 corpus](../contracts/web2-v1/README.md). Node/BigInt checks chỉ xác nhận dữ liệu preflight; các auth/SQL/HTTP/browser scenarios dưới đây vẫn thuộc task runtime tương ứng.

- `saleId = 9007199254740993` và `9223372036854775807` round-trip string; `saleLineId = "0"` khi tạo mới; reject overflow và noncanonical/negative ID.
- Qty `99999999999999.9999`, UnitPrice `999999999999999.9999`, line amount cap, decimal carry/tie; PostedUnitCost có precision riêng; nullable omitted.
- Duplicate product rows 1–200, stable client mapping (ordered request + server positive line ID), dates leap/timezone, opaque cursor/version, inactive label.
- Login/sign/verify, cookie flag/xóa cookie, expiry, rotation/reuse/session revoke, forged JWT, revoked permission, Origin fail, timeout-after-commit và ack-Get-denied.

## 5. SQL/schema và least privilege

- Baseline migration scripts **001–010**; new auth tables chỉ qua **011_WebAuthSessions.sql** (hoặc số tiếp theo nếu main đổi) bằng DbUp. Không viết lại 001–010; chỉ cho migrate từ exact supported schema10 hoặc fresh state qua explicit gate; kiểm tra `SqlSchemaCompatibilityChecker`, `DatabaseMigrationRunner`, test fixture, schema journal, sql grants và launcher compatibility trước khi chốt. Không tự deploy vào schema9 package/production.
- Gợi ý schema tối thiểu: `WebAuthSessions(SessionId uniqueidentifier PK, UserId int FK, CreatedAtUtc, AbsoluteExpiresAtUtc, RevokedAtUtc, ...)`; `WebRefreshTokens(TokenHash binary(32) UNIQUE, SessionId FK, IssuedAtUtc, ExpiresAtUtc, ConsumedAtUtc, ReplacedByTokenHash nullable, RevokedAtUtc, ...)`. Constraints, indexes theo token lookup, session/family, retention; không lưu plaintext, JWT hoặc password.
- SQL operations phát hành/rotate/revoke phải atomic trong một owner transaction, row locks/CAS; tách khỏi Sales transaction. Nếu audit sink fail sau auth commit, không làm lộ/replay token.
- **Đừng cấp db_owner/DDL/blanket DML**. Bổ sung quyền table/procedure tối thiểu cho riêng web runtime principal, cập nhật `deployment/sql/Provision-MyDmsVnPrincipals.sql`/validator/grant tests rõ ràng; không ngầm tăng quyền desktop principal. Maintenance principal vẫn là user độc lập. SQL test chỉ dùng designated non-production SQL theo `docs/TEST_POLICY.md`.
- Không coi schema11 là desktop release-ready. Trước pilot kiểm thử coexistence và fail-closed trên version không hỗ trợ; mọi thay đổi cho desktop startup compatibility được đánh giá trong plan task, không thay đổi WinForms UI.

## 6. Verification, operational handoff và giới hạn

- Host test: in-memory handler/HTTP route, IIS HTTPS same-origin smoke, 2 browser actors song song, JWT negative suite, replay/concurrent refresh, SQL unique/CAS/grant, Auth 401/403/500, session disabled, request limits/Origin/proxy.
- Sales test: contract fixtures request/response 200 dòng, stale rowversion conflict, denied Read separate Write/Post, loss response after Create/Update/Post, status Posted ack -> Get 403/error, no auto-write retry; compare same business outcome through Local and HTTP.
- Recording: exact backend/frontend SHAs, OS/Windows runtime, .NET Framework/IIS/SQL versions, test command/count/pass/fail/skips, HTTPS/cookie/SameSite, candidate origin, CORS/CSRF/CSP and residual localStorage risk. Không báo production-ready do migration/SQL/HTTP chưa nghiệm thu.
- Standard references (source, not implied implemented behavior): [ASP.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/aspnet), [RFC 9700 `4.14 refresh rotation](https://www.rfc-editor.org/rfc/rfc9700.html), [OWASP Session Management](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html), [MDN cookie prefixes](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Set-Cookie).
