# Schema compatibility và migration runbook

Status: Current SEC-REBUILD T01 schema baseline. Migrator là gói admin độc lập; desktop không chứa hoặc tự chạy DbUp hay security bootstrap.

## 1. Manifest hiện tại

Client v1 hiện tại hỗ trợ đúng journal prefix sau, theo thứ tự bất biến:

1. `001_PersistenceFoundation.sql`
2. `002_Identity.sql`
3. `003_Catalog.sql`
4. `004_Catalog_Nul_Constraints.sql`
5. `005_GoodsReceipt.sql`
6. `006_GoodsReceipt_Number_Sequence.sql`
7. `007_Inventory_Stock.sql`

Journal giữ nguyên `dbo.SchemaVersions` từ P1. Script đã phát hành là immutable; sửa nội dung hoặc đổi tên script cũ tạo fork không được hỗ trợ. Manifest kiểm tra cả history, table/function/sequence, toàn bộ nhóm CHECK constraints của 002/003/004/005/007 ở trạng thái enabled/trusted, primary key theo đúng bảng/tên/thứ tự cột, unique index theo đúng key/filter, foreign key theo đúng bảng/cột/action ở trạng thái enabled/trusted và trigger append-only đang enabled, vì chỉ thấy entry `007` không chứng minh schema hoàn chỉnh. Sequence cấp số phiếu phải giữ đúng `bigint`, start/increment/min bằng 1, max bằng 9999999999 và `NO CYCLE`; preflight không so `current_value`, `last_used_value`, cache hoặc exhaustion state vì chúng thay đổi hợp lệ khi vận hành. Preflight kiểm kê đầy đủ user-defined schema-scoped object cấp cao nhất, DML trigger trên mọi schema, synonym, database-scoped DDL trigger và user-defined type trong `sys.types`; allowlist khớp đồng thời schema, tên, object type và parent của trigger. P6-T01/T02 chưa có managed user-defined type, nên alias/table type bất kỳ đều làm database bị coi là unmanaged. Object ngoài manifest hoặc object trùng tên nhưng sai type khiến database bị coi là unmanaged thay vì “empty”. Constraint/index con được kiểm tra bằng các invariant riêng thay vì coi là object độc lập trong inventory.

Đây là release schema baseline đầu tiên; chưa có prior production release baseline nào được hỗ trợ cho mixed-client rollout. Database ở prefix `001`–`006` chỉ là pre-release/interrupted state và bị desktop chặn.

## 2. Desktop read-only preflight

Cả hai host chạy cùng `DesktopDatabaseStartupCoordinator` trước khi đăng ký persistence nghiệp vụ hoặc tạo shell. Preflight chỉ mở connection, đọc journal và metadata; không chạy migration hoặc sửa business data.

| Trạng thái | Mã | Desktop | Hành động operator |
|---|---|---|---|
| Không journal, không artifact | `Schema.Empty` | Chặn | Chạy clean install bằng migrator |
| Prefix hợp lệ nhưng thiếu script hiện tại | `Schema.TooOld` | Chặn | Backup/maintenance window, chạy migrator, preflight lại |
| Đúng manifest 001–009 và đủ artifact, bao gồm `SystemSecuritySettings` và security administration/import procedures | `Schema.Compatible` | Cho phép preflight tiếp | Kiểm tra superuser binding/configuration kế tiếp |
| Thiếu script/artifact hoặc có prefix migration chưa hoàn tất | `Schema.TooOld` / `Schema.PartialMigration` | Chặn | Chạy migrator đã review rồi preflight lại |
| Có script sau 007 | `Schema.TooNew` | Chặn | Cài client tương thích; không downgrade schema |
| Sai/thừa/thiếu giữa chuỗi | `Schema.UnknownHistory` | Chặn | Dừng rollout, điều tra provenance; không sửa journal thủ công |
| Journal và artifact không khớp | `Schema.PartialMigration` | Chặn | Giữ client đóng; chọn forward-fix hoặc restore đã kiểm chứng |

Không có mixed-version combination được phê duyệt ở baseline này. Nếu release sau muốn hỗ trợ rolling rollout, migration phải theo expand/contract và cả hai client manifest phải được kiểm thử rõ ràng. Nếu không, yêu cầu downtime cho tới khi DB và toàn bộ client được nâng cấp khớp nhau.

## 3. Preflight triển khai

Trước migration, operator phải ghi nhận:

1. đúng server/instance/application database, không phải system database;
2. SQL Server version/edition nằm trong support matrix của release;
3. migration principal riêng có quyền cần thiết, runtime principal không có DDL;
4. dung lượng data/log/tempdb đủ và không có lỗi storage;
5. `dbo.SchemaVersions` và manifest hiện tại;
6. backup/recovery point đã được tạo và restore-test theo P6-T06;
7. client đã dừng/drain, không còn transaction Post đang chạy;
8. target release/commit/artifact hash đã duyệt.

Chỉ sau đó đặt một maintenance owner và chạy migrator với migration configuration. Migrator xác nhận principal có `CONTROL` trên đúng database trước khi lấy lock hoặc gọi DbUp; runtime principal bị từ chối bằng `Migration.InsufficientPrivileges`, kể cả khi database hiện tại khiến migration là no-op. `DatabaseMigrationRunner` lấy SQL application lock exclusive `MyDmsVn.SchemaMigration`, chạy preflight và toàn bộ DbUp transaction-per-script trên cùng một SQL session. Mất session đồng thời giải phóng lock và dừng/rollback script đang chạy, nên không còn một DbUp session mồ côi tiếp tục sau khi lock biến mất. Lần gọi cạnh tranh nhận `Migration.AlreadyRunning`; không retry mù hoặc chạy migrator thứ hai.

## 4. Thực thi

Ví dụ dùng file admin-only:

```powershell
$env:MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE = 'C:\ProgramData\MyDmsVn\migration.connection'
dotnet MyDmsVn.Server.DbMigrator.dll
```

Hoặc chạy executable `net48` tương ứng. Exit code: `0` thành công/no-op, `1` migration failure, `2` configuration failure, `3` competing migrator, `4` schema preflight từ chối, `5` migration principal thiếu quyền. Output không in connection string hoặc exception chain.

DbUp dùng `WithTransactionPerScript()`: mỗi script thành công có thể commit trước khi script sau lỗi. Toàn release không phải một transaction all-or-nothing. Không chạy migrator ở desktop login/startup và không sửa/xóa journal để ép no-op.

## 5. Recovery decision

Sau lỗi:

1. Giữ toàn bộ client đóng; preflight phải tiếp tục chặn trạng thái không tương thích.
2. Lưu safe log, journal, schema inventory, SQL health và backup identifier; không export secret.
3. Xác định script cuối commit và artifact thực tế.
4. Ưu tiên forward-fix đã review nếu các script đã commit đúng và lỗi có thể sửa additive.
5. Chỉ restore khi owner chấp nhận recovery point/RPO và backup đã restore-test vào database riêng. Restore có thể mất write sau recovery point.
6. Không viết lại `SchemaVersions` thủ công như cách sửa thông thường.
7. Sau recovery, chạy migrator một owner, xác nhận no-op lần hai, chạy desktop preflight và P5 reconciliation/smoke trước khi mở lại truy cập.

## 6. Rollout order

Baseline này yêu cầu downtime:

1. Dừng client cũ.
2. Backup và kiểm tra preflight quản trị.
3. Chạy migration bằng admin package.
4. Xác nhận journal/artifact và migrator no-op.
5. Với fresh install có user store trống, cấu hình `Security__SuperuserUsername` và chạy `security bootstrap-superuser` theo [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md). Không chạy bootstrap trên database đã có user.
6. Triển khai host phù hợp manifest 001–009 và cùng cấu hình superuser đã được review.
7. Chạy schema preflight, xác nhận binding/configuration, functional smoke và reconciliation.
8. Mở lại truy cập.

Không chạy client cũ sau khi schema vượt manifest của nó; không chạy client mới khi DB còn prefix cũ/partial. Downgrade client không đồng nghĩa downgrade database.

