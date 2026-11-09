# Thiết kế WEB6 B05 — Candidate ghép cặp

**Trạng thái:** đề xuất để Owner duyệt. Owner đã chọn prerequisite audit B05/F04 và miễn điều kiện chặn B03 cho đúng một candidate nonproduction có giới hạn. Waiver không đổi lỗi hoặc hosted proof chưa chạy của B03 thành PASS, không cho phép dọn quarantine lịch sử, không chấp nhận staging và không phê duyệt production.

## Ý định

Tạo một candidate backend/frontend có thể lặp lại, pin cả source lẫn artifact, chạy ranh giới manual same-key resend WEB6 trên tài nguyên IIS Express và SQL Server thuộc quyền sở hữu đã kiểm chứng; sau đó đo compatibility, operational safety, workload và service recovery trung thực. Candidate là công cụ nghiệm thu, không phải gói triển khai.

## Mục tiêu

- Hoàn thành B05a trước khi cấp phát candidate sống: kiểm toán dependency, source pin, limiter một worker, đích security audit/chính sách lỗi, redaction và owned-resource admission.
- Build backend Release và React candidate artifact bất biến; kiểm hash file và đóng dấu candidate manifest trước live execution.
- Chạy sáu lệnh Sales/Receipt Create, Update, Post; explicit same-key consent; tranh chấp hai tab; operator-first/original-first; oracle vật lý cho journal, business, stock, ledger, valuation và COGS.
- Kiểm tra direct same-origin HTTPS, JWT/refresh/cookie/CSRF/Origin/CSP, forwarded-header giả, runtime/operator principal giới hạn và PR #95 `VerifyHost -> Admit`.
- Kiểm cutover mà không downgrade schema: WEB4 client cache với compatible new host; new client với old exact12 host dùng database tương thích riêng; UNKNOWN qua restart; revoked grant; stale epoch.
- Chạy smoke và measurement profile có tên; báo count theo lớp, p50/p95/p99, throughput, error/timeout, worker CPU/RAM, SQL lock/deadlock/pool và journal growth. Số đo không phải SLA target.
- Diễn tập backup/restore candidate sang database owned khác, rotate epoch trước writer admission, từ chối stale writer, chạy full authenticated HTTP/business oracle và đo service RPO/RTO.

## Ngoài phạm vi và anti-goals

- Không full IIS, Windows Server, reverse proxy, tuyên bố multiworker limiter, production capacity, staging acceptance hoặc production GO.
- Không automatic mutation retry, background replay, operation ID mới cho same-key, suy 404 thành NOT_EXECUTED hoặc settle từ support reconciliation.
- Không tái sử dụng, nhận quyền, kiểm tra hoặc dọn database/manifest/probe quarantine lịch sử.
- Không broad SQL grant; không nâng quyền web runtime hoặc B02 operator thường. Compaction ngoài candidate trừ khi có maintenance identity được duyệt riêng.
- Không bịa SLA; không gắn PASS cho case thiếu, skip, bị retry che đi hoặc không đủ mẫu.

## Các cách đã cân nhắc

### Đã chọn: bốn checkpoint có gate trên hai repository

1. B05a kiểm toán và harden backend; F04a chỉ thêm candidate-bound frontend boot/configuration và live acceptance seam.
2. Review gate đóng băng SHA, dependency lock, contract hash, environment preflight và manifest format.
3. B05b sở hữu orchestration, SQL/IIS admission, workload, recovery; F04b sở hữu Chromium/Edge và browser evidence trên đúng host.
4. Chỉ reconcile evidence sau khi process, HTTP queue và SQL resource mới có disposition đã kiểm chứng.

Cách này khớp PR roadmap, chỉ cấp phát sau fail-closed audit và tách source defect khỏi environment failure.

### Đã loại: một PR candidate liên repository

Cách này giảm branch choreography nhưng trộn source, live allocation và performance evidence; lỗi môi trường sẽ làm mờ source đã review và provenance của rollback/evidence.

### Đã loại: dùng lại artifact WEB5/WEB6 cũ

Run cũ không chứa toàn bộ eligibility, durable claim và confirmation hiện tại; chúng chỉ là baseline.

## Quyền sở hữu và interface

### Backend sở hữu

- Candidate preflight, sealed manifest schema và đối chiếu source/artifact hash.
- Release publish, private IIS Express site/config/certificate pin, PR #95 admission và bounded shutdown.
- Fresh schema13 SQL, migration, restricted principal riêng, epoch và physical oracle độc lập.
- HTTP security negative, operator race, audit/limiter probe, workload metric, backup/restore và cleanup/quarantine decision.
- Sanitized machine-readable handoff gồm candidate ID, origin, public artifact hash, scenario ID và outcome; không chứa credential hoặc raw business payload.

### Frontend sở hữu

- Build-time candidate ID mặc định vắng mặt; khi có phải strict validate rồi truyền vào `createProductionRuntime({ manualResend: ... })`.
- Artifact assertion chứng minh build thường OFF và candidate build chỉ ON cho backend candidate ID khớp.
- Live Playwright Chromium/Edge cho sáu kind, accessible confirmation, double-tab/claim/reload, no-auto-retry và fail-closed authority/cutover.
- Browser trace/screenshot redaction và count gắn với backend manifest.

Backend/frontend candidate ID phải khớp tuyệt đối. Context thiếu/sai hoặc old host không tạo eligibility dùng được và UI giữ disabled. UI flag không phải security boundary; GET/mutation vẫn kiểm actor/session/grant/epoch và SQL journal fence.

Compatibility pin lịch sử dùng đúng archive merge đã được Owner chấp nhận: frontend WEB4 PR #22 merge `ea104582a5aa4bcdf0d04999296a30a3c43e450e` cho cached client và backend WEB4 PR #85 merge `a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2` cho exact12 host. Manifest phải build lại từ đúng tree và ghi artifact hash mới; SHA lịch sử không thay artifact proof.

## B05a/F04a — Prerequisite audit và hardening

Audit ghi merged heads hiện tại và từ chối dirty/unpinned input. Mỗi B03 exception được phân loại: B05 chạy lại độc lập, giữ `OWNER_ACCEPTED_EXCEPTION`, hoặc không liên quan. Không được trích run 342/344 thất bại thành PASS.

Backend test characterise `AuthAttemptLimiter` chỉ cho một verified worker và phủ denial/reset/clock/concurrency. Candidate phải chứng minh đúng một admitted worker; worker thứ hai hoặc shared-capacity claim làm dừng run. `TraceSecurityAuditSink` được cấu hình tới đích owned có ACL current-user/approved-operator, rotation/retention giới hạn, redaction và correlation ID. Nếu source/config hiện tại không đạt, B05a thêm implementation nhỏ nhất và test trước candidate allocation.

Owner chọn chính sách outage theo thời điểm commit. Authentication/authorization admission chưa được phép tiếp tục nếu required audit event không persist được; request phải fail closed. Một authentication/authorization vốn đã bị từ chối vẫn giữ kết quả từ chối khi audit lỗi và đánh dấu audit degraded. Khi business transaction đã commit, audit sink failure không được giả rollback, đổi thành UNKNOWN hoặc che kết quả COMMITTED: host giữ đúng business outcome, phát degraded signal độc lập và candidate readiness phải FAIL cho tới khi audit destination phục hồi và được xác minh. Test phải phân biệt pre-admission, denied path và post-commit path.

Frontend thêm đúng một build seam strict cho candidate ID. Không query parameter, localStorage hoặc user-editable toggle. Default và unsupported host giữ OFF.

## B05b/F04b — Candidate execution

### Manifest và setup

Trước live test, commit manifest template và tạo sealed local run record gồm backend/frontend SHA/tree, lock hash, contract hash WEB4/5/6, DLL/Web.config hash, toàn bộ `dist` hash, schema13 fingerprint, mode, candidate ID, origin/certificate/IIS Express config hash, browser, SQL ownership identity, selector, time budget và stop rule. Giá trị có secret ở ngoài Git.

Chỉ cấp phát fresh owned SQL và private publish root. Thứ tự admission: restore/build, migrate/provision, IIS Express config, kiểm certificate/queue/process/controller/worker, rồi mới health/authenticated traffic. Failure trước admission chỉ cleanup khi ownership chắc chắn; ownership/quiescence không chắc thì quarantine run và chặn destructive action tiếp theo.

### Functional/browser matrix

Với sáu command, tạo original immutable operation mới và phủ retained COMMITTED, response lost, absent + valid receipt ALLOW, explicit consent, hai tab đồng thời, operator-first NOT_EXECUTED, result unavailable/expired, sai fingerprint/actor/grant/epoch/candidate và SQL/HTTP failure. Assert tối đa một browser mutation transmission cho mỗi consent và đúng một physical business effect. Receipt Post vẫn không trả version. UI chỉ settle từ decoded original result; support reconciliation không unlock.

Fault injection chỉ nằm trong Playwright/orchestrator: trường hợp not-observed abort route trước `route.fetch`, còn COMMITTED-response-lost dùng `route.fetch({ maxRetries: 0 })` chờ HTTPS response thật rồi dispose/abort response về browser. Không thêm runtime admin endpoint hoặc production fault seam. SQL oracle xác nhận cut point trước khi browser được phép resend.

Chromium chạy trước, Edge sau trên cùng admitted pair. Telex/VNI, 100%/125% và Narrator là attended evidence riêng; Narrator không chạy là `NOT RUN / OWNER_ACCEPTED_EXCEPTION`, không phải PASS.

### Cutover

Dùng database/artifact owned tương thích riêng cho old/new. Không trỏ exact12 binary vào schema13. Kiểm WEB4 cached asset với compatible new host, current asset với old host không có resend, DISPATCHED/UNKNOWN qua restart không rotate epoch, revoked grant/stale epoch và matched rollback. Lifecycle không dispatch mutation.

### Workload

Chạy low-volume smoke rồi concurrency 1/4/8/16 khi one-worker environment ổn định. Sáu critical class là Sales Create/Update/Post và Receipt Create/Update/Post, mỗi class có percentile và mục tiêu 1.000 observation riêng nếu time/resource budget cho phép. Auth refresh, protected status/eligibility và stock read là supporting class, báo riêng và không gộp vào sáu mutation class. Không đủ mẫu thì `INSUFFICIENT_SAMPLE / NOT MEASURED`. Tính percentile từ monotonic duration riêng từng class bằng nearest-rank đã ghi.

### Recovery

Interrupt candidate, backup owned source DB, restore sang owned DB khác, rotate epoch trước writer admission, provision lại principal và restart pinned host. Đo từ failure detection đến trusted authenticated service, không dùng riêng backup seconds. Từ chối old epoch/writer/receipt; chạy fresh current-epoch command và kiểm full journal/document/stock/ledger/value/COGS. Restore/admission/cleanup không chắc chắn giữ quarantine và FAIL/BLOCKED.

## Evidence và stop rules

- Ghi expected/discovered/executed/pass/fail/skip/retry, duration, source/artifact hash, browser/Playwright/OS/SQL, principal/mode/schema, oracle, process/queue drain và cleanup disposition.
- Giữ mọi failure đầu và lý do rerun; rerun xanh không xóa failed attempt hoặc resource disposition.
- Dừng ngay khi host identity mơ hồ, candidate mismatch, principal sai, duplicate effect không giải thích được, source/artifact drift, secret leak, mandatory skip hoặc cleanup không chắc chắn.
- Same-key resend giữ OFF trong artifact thường. Candidate chỉ bật khi build/config khớp chính xác và chỉ trong admitted nonproduction run.
- B05/F04 thành công chỉ hỗ trợ quyết định nonproduction pilot riêng sau này; không cấp B06/F05 staging/production approval.
- Không commit Playwright trace/HAR của live authenticated flow. Chỉ giữ screenshot đã kiểm redaction và generated network summary theo allowlist field. Raw diagnostic tạm thời, nếu cần cho failure, nằm trong protected private run root, không đưa vào evidence Git và bị xóa chỉ sau khi đã xác minh process/queue drain; nếu không xác minh được thì quarantine cùng run.

## Chiến lược kiểm chứng

Thực hiện TDD. B05a/F04a bắt đầu bằng test fail cho default-off binding, audit persistence/redaction/failure, single-worker limiter và manifest rejection. B05b/F04b thêm fail-first orchestrator/live spec trên controlled owned fixtures.

Chạy focused trước complete affected project. SQL/IIS/browser chạy tuần tự và có nonzero discovery guard. Gate cuối gồm backend SQL-free/owned-SQL, frontend unit/typecheck/build, Chromium/Edge live, manifest reconciliation và `git diff --check` ở cả hai repo.

## Quyết định để người triển khai tự chọn

- Class/file split theo convention Security.Tests và Playwright hiện có.
- Cơ chế protected audit destination sau khi test xác định config hiện tại có đủ hay cần focused replacement.
- Duration/count dưới 1.000/class khi máy không đáp ứng; bắt buộc gắn insufficient thay vì PASS.

