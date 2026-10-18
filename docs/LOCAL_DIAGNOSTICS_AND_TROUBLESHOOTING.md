# Diagnostics, security audit và troubleshooting cho Local Mode v1

**Hiện trạng:** `v1a`, cập nhật 2026-10-18. Host active là `MyDmsVn.Desktop.App` (`net48`/x86). AppCore (`net8.0-windows`) parked ngoài solution/release; runner `net8.0` không phải product host. SEC-REBUILD đã được chấp nhận trong PR #34, còn P5/P6 operational/release evidence được giữ **OPEN / DEFERRED** tại [DEFERRED_WORK](DEFERRED_WORK.md). Xem [HISTORY](HISTORY.md) cho bằng chứng lịch sử.

## Log và retention

Host active ghi JSON Lines bằng Serilog vào `%LocalAppData%\MyDmsVn\Logs\MyDmsVn.Desktop.App`, theo [DesktopDiagnosticsOptions](../src/MyDmsVn.Server.Infrastructure/Diagnostics/DesktopDiagnosticsOptions.cs) và [DesktopProductionDiagnostics](../src/MyDmsVn.Server.Infrastructure/Diagnostics/DesktopProductionDiagnostics.cs):

- `application-YYYYMMDD.json`: operation, safe error code và diagnostics kỹ thuật đã redaction;
- `audit-YYYYMMDD.json`: security audit có cấu trúc;
- rolling theo ngày, giữ tối đa 14 file cho mỗi sink.

Thư mục nằm trong profile người dùng nên desktop không cần quyền Administrator. Không nới ACL cho `Everyone`, không chuyển log vào thư mục cài đặt chỉ đọc. File log workstation chỉ phục vụ support; nó không phải audit tập trung, chống sửa/xóa hoặc bằng chứng bất biến.

Nếu không tạo/ghi được file sink khi khởi tạo, composition vẫn dùng non-modal Trace fallback và công bố `Diagnostics.FileSinkUnavailable`. Security audit dùng định dạng JSON có cấu trúc của Serilog với ranh giới append và flush-to-disk đồng bộ để audit writer nhận được lỗi I/O trong runtime.

Phân biệt hai loại lỗi ghi audit trong runtime:

- **Contention tạm thời:** sharing/lock violation được ghi vào Trace với mã `Diagnostics.AuditFileTemporarilyUnavailable` và được chuyển đến audit failure reporter, nhưng không đổi `FileLoggingEnabled` hoặc `SafeErrorCode`. Entry đang bị contention không được ghi vào file và không được backfill tự động. Sau khi reader/tool nhả file, event audit kế tiếp sẽ thử mở lại và có thể ghi bình thường. Operator phải tra Trace/failure report, xác định khoảng thời gian và entry có thể thiếu, kiểm tra tính đầy đủ của audit rồi xử lý theo quy trình sự cố của tổ chức; không được suy ra rằng recovery của event sau đã phục hồi event bị thiếu.
- **I/O degradation không tự phục hồi:** lỗi ACL, đường dẫn hoặc storage làm audit sink không còn ghi được sẽ chuyển health sang `Diagnostics.AuditFileSinkUnavailable`, ghi safe Trace fallback và gọi audit failure reporter. Operator phải khắc phục quyền/dung lượng/đường dẫn, restart process và xác nhận file audit mới trước khi coi sink healthy trở lại.

Trong cả hai trường hợp lỗi **audit file workstation**, nếu nghiệp vụ đã commit thì UI vẫn nhận kết quả nghiệp vụ thành công; không được mô tả là transaction rollback. Entry bị mất không có outbox/backfill tự động.

### Audit file và audit SQL của security administration

`audit-*.json` là telemetry của desktop, gồm login và các hành động nghiệp vụ được ghi bởi `ISecurityAuditWriter`. SEC-REBUILD còn có `dbo.SecurityAuditEvents` cho security administration/permission import: mutation và SQL audit của nó nằm trong cùng transaction; lỗi ghi audit SQL làm mutation rollback. Cơ chế này được thực hiện bởi [security store](../src/MyDmsVn.Server.Infrastructure/Security/SqlSecurityAdministrationStore.cs) và procedures của scripts 008/009. Không áp dụng quy tắc “file audit lỗi sau commit” để kết luận một security mutation có SQL audit thất bại vẫn thành công. Cũng không coi file workstation là bản sao đầy đủ hoặc bất biến của bảng audit SQL.

## Trường cấu trúc

Operation log có `ApplicationVersion`, `Host`, `Runtime`, `OperationName`, `CorrelationId` và `ErrorCode` (`None` khi thành công). `Runtime` lấy từ `Environment.Version`, không phải tên TFM; khi ghi support evidence cần kèm package/target net48 riêng. Mỗi Local API call tạo correlation ID 32 ký tự hex trong scope riêng; scope được dispose khi success, error hoặc cancellation để không rò sang request kế tiếp.

Audit tối thiểu có:

- `Action`, `Outcome`, `ActorUserId`, `OccurredAtUtc`;
- `DocumentType`, `DocumentId` cho hành động tài liệu;
- `CorrelationId` khi audit chạy trong Local request scope.

Login ghi các outcome success, failed, disabled và rehash-failed. Post Goods Receipt ghi `Denied` tại authorization boundary và chỉ ghi `Succeeded` sau `IUnitOfWork.Commit()`. Validation/conflict/cancellation không tạo false-success audit.

## Redaction và support export

Không ghi/export password, `PasswordHash`, `PasswordSalt`, token/Bearer/Authorization, full connection string, SQL parameter nhạy cảm hoặc raw exception/stack trace. `SensitiveDataRedactor` xử lý message và nested exception; exception reporter chỉ ghi tên exception và diagnostic đã redaction.

Khi gửi log cho support:

1. ghi lại support correlation ID hiển thị cạnh thông báo lỗi;
2. sao chép đúng khoảng thời gian từ `application-*` và `audit-*`;
3. kiểm tra lại secret trước khi chuyển;
4. gửi qua kênh được phê duyệt và xóa bản export theo retention của tổ chức.

## Troubleshooting

| Tình huống | Kiểm tra an toàn | Hành động |
|---|---|---|
| Configuration failure | Mã `Startup.*`, source loại cấu hình; không thu connection string | Sửa file/env theo [configuration runbook](CONFIGURATION_AND_SQL_SECURITY.md), restart |
| SQL offline/timeout | Correlation ID, `Startup.DatabaseUnavailable`, network/TLS | Kiểm tra SQL service/firewall/certificate; không blind-retry command |
| Schema incompatible | Mã `Schema.*`, runtime scripts 001–009, journal read-only | Dừng client, dùng [migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md); prefix cũ bị migrator hiện tại từ chối, không blind-retry upgrade |
| Package ghi schema 8 trong khi runtime yêu cầu 009 | Package/commit/hash và `schemaSupport` | Giữ release blocked theo `RELEASE-MANIFEST-01` trong [deferred work](DEFERRED_WORK.md); không sửa journal theo package metadata lỗi thời |
| Authorization denied | Audit `Denied`, actor/action/document, current key/assignment/revision | Dùng [security administration](SECURITY_ADMINISTRATION_RUNBOOK.md), kiểm tra default deny/direct Deny và binding; không sync hoặc tự cấp grant |
| Slow query | Operation/correlation/timestamp, không SQL parameter | Đối chiếu SQL health và query plan bằng DBA tooling |
| Post failed | Error code, document ID, ledger/balance reconciliation | Reload receipt; không retry khi commit outcome chưa rõ |
| Audit file contention tạm thời | Trace/failure report có `Diagnostics.AuditFileTemporarilyUnavailable`; tool đang đọc giữ file không share write | Đóng hoặc cấu hình lại tool đọc log; xác nhận event kế tiếp được ghi; kiểm tra và xử lý entry bị thiếu vì không có backfill tự động |
| Security administration/import rollback | Safe error code, target/revision và audit SQL qua read-only review | Xác minh mutation/audit transaction; re-preview khi revision/catalog thay đổi, không áp lại preview cũ hoặc sửa quyền trực tiếp |
| Logging/audit degradation | `Diagnostics.FileSinkUnavailable` hoặc `Diagnostics.AuditFileSinkUnavailable`, ACL/disk quota/path | Khôi phục quyền/dung lượng/đường dẫn, restart process, xác nhận file mới trước khi coi sink healthy |
| Crash recovery | Trace fallback và log cuối cùng | Ghi correlation/time, restart; kiểm tra schema và trạng thái Post trước retry |

Không mở modal từ logging/audit failure. Modal startup hiện hữu chỉ chứa safe code/message; active executable nhận `--smoke-test` để ghi lỗi startup vào stderr thay cho modal. Kiểm tra unattended vẫn phải giữ process watchdog theo [TEST_POLICY](TEST_POLICY.md) và [tests/README](../tests/README.md); switch này không thay thế bằng chứng packaged login/GUI.

Log-path/full-disk, recovery và installed-runtime checks còn mở trong [P6 deferred work](DEFERRED_WORK.md). Support evidence phải ghi đúng revision/package, thời gian, safe code/correlation và kết quả quan sát; không dùng bằng chứng AppCore lịch sử như một lần chạy net48 hiện tại.
