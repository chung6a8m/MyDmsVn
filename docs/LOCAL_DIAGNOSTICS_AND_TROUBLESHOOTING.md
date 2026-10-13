# Diagnostics, security audit và troubleshooting cho Local Mode v1

Status: P6-T03 implementation baseline, 2026-10-13. Áp dụng cho `MyDmsVn.Desktop.App` (`net48`) và `MyDmsVn.Desktop.AppCore` (`net8.0-windows`).

## Log và retention

Mỗi host ghi JSON Lines bằng Serilog vào `%LocalAppData%\MyDmsVn\Logs\<host>`:

- `application-YYYYMMDD.json`: operation, safe error code và diagnostics kỹ thuật đã redaction;
- `audit-YYYYMMDD.json`: security audit có cấu trúc;
- rolling theo ngày, giữ tối đa 14 file cho mỗi sink.

Thư mục nằm trong profile người dùng nên desktop không cần quyền Administrator. Không nới ACL cho `Everyone`, không chuyển log vào thư mục cài đặt chỉ đọc. File log workstation chỉ phục vụ support; nó không phải audit tập trung, chống sửa/xóa hoặc bằng chứng bất biến.

Nếu không tạo/ghi được file sink khi khởi tạo, composition vẫn dùng non-modal Trace fallback và công bố `Diagnostics.FileSinkUnavailable`. Security audit dùng định dạng JSON có cấu trúc của Serilog với ranh giới append và flush-to-disk đồng bộ để audit writer nhận được lỗi I/O trong runtime.

Phân biệt hai loại lỗi ghi audit trong runtime:

- **Contention tạm thời:** sharing/lock violation được ghi vào Trace với mã `Diagnostics.AuditFileTemporarilyUnavailable` và được chuyển đến audit failure reporter, nhưng không đổi `FileLoggingEnabled` hoặc `SafeErrorCode`. Entry đang bị contention không được ghi vào file và không được backfill tự động. Sau khi reader/tool nhả file, event audit kế tiếp sẽ thử mở lại và có thể ghi bình thường. Operator phải tra Trace/failure report, xác định khoảng thời gian và entry có thể thiếu, kiểm tra tính đầy đủ của audit rồi xử lý theo quy trình sự cố của tổ chức; không được suy ra rằng recovery của event sau đã phục hồi event bị thiếu.
- **I/O degradation không tự phục hồi:** lỗi ACL, đường dẫn hoặc storage làm audit sink không còn ghi được sẽ chuyển health sang `Diagnostics.AuditFileSinkUnavailable`, ghi safe Trace fallback và gọi audit failure reporter. Operator phải khắc phục quyền/dung lượng/đường dẫn, restart process và xác nhận file audit mới trước khi coi sink healthy trở lại.

Trong cả hai trường hợp, nếu nghiệp vụ đã commit thì UI vẫn nhận kết quả nghiệp vụ thành công; không được mô tả là transaction rollback. Thiết kế outbox/audit transactionally durable nằm ngoài P6-T03 và cần thay đổi được phê duyệt riêng.

## Trường cấu trúc

Mọi operation log có `ApplicationVersion`, `Host`, `Runtime`, `OperationName`, `CorrelationId` và `ErrorCode` (`None` khi thành công). Mỗi Local API call tạo correlation ID 32 ký tự hex trong scope riêng; scope được dispose khi success, error hoặc cancellation để không rò sang request kế tiếp.

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
| Configuration failure | Mã `Startup.*`, source loại cấu hình; không thu connection string | Sửa file/env theo `CONFIGURATION_AND_SQL_SECURITY.md`, restart |
| SQL offline/timeout | Correlation ID, `Startup.DatabaseUnavailable`, network/TLS | Kiểm tra SQL service/firewall/certificate; không blind-retry command |
| Schema incompatible | Mã `Schema.*`, manifest/journal read-only | Dừng client, dùng migration runbook; desktop không tự migrate |
| Authorization denied | Audit `Denied`, actor/action/document | Xem role/direct deny; không sửa DB trực tiếp để bypass |
| Slow query | Operation/correlation/timestamp, không SQL parameter | Đối chiếu SQL health và query plan bằng DBA tooling |
| Post failed | Error code, document ID, ledger/balance reconciliation | Reload receipt; không retry khi commit outcome chưa rõ |
| Audit file contention tạm thời | Trace/failure report có `Diagnostics.AuditFileTemporarilyUnavailable`; tool đang đọc giữ file không share write | Đóng hoặc cấu hình lại tool đọc log; xác nhận event kế tiếp được ghi; kiểm tra và xử lý entry bị thiếu vì không có backfill tự động |
| Logging/audit degradation | `Diagnostics.FileSinkUnavailable` hoặc `Diagnostics.AuditFileSinkUnavailable`, ACL/disk quota/path | Khôi phục quyền/dung lượng/đường dẫn, restart process, xác nhận file mới trước khi coi sink healthy |
| Crash recovery | Trace fallback và log cuối cùng | Ghi correlation/time, restart; kiểm tra schema và trạng thái Post trước retry |

Không mở modal từ logging/audit failure. Modal startup hiện hữu chỉ chứa safe code/message; automated smoke dùng `--smoke-test` để ghi stderr và thoát hữu hạn.
