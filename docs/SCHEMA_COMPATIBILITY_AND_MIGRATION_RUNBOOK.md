# Schema compatibility và migration runbook

**Hiện trạng:** `v1a-pro`, cập nhật 2026-10-21. SEC-REBUILD/RESP/L2DB và GUI-P5 đã hoàn thành theo phạm vi được nghiệm thu; runtime yêu cầu chính xác scripts 001–009. Desktop và DbMigrator phát hành đều `net48`/x86, AppCore parked. DbMigrator là gói admin riêng; desktop không tự chạy DbUp hoặc security bootstrap. BCI đã triển khai/nghiệm thu trong phạm vi UI lịch sử. [BCF](plans/20261021-001-business-compact-lookup-and-visual-parity.md) là lộ trình duy nhất, đang PLANNED; implementation NOT STARTED, nghĩa vụ runtime mới NOT RUN và không thay schema. Các [điều kiện schema-compatibility, recovery và phát hành](DEFERRED_WORK.md#release-prerequisites) vẫn **UNACCEPTED / UNSCHEDULED**.

## 1. Runtime schema contract hiện tại

[SchemaCompatibilityPolicy.CurrentScripts](../src/MyDmsVn.Server.Infrastructure/Operations/SchemaCompatibility.cs) yêu cầu đủ chuỗi sau, theo thứ tự bất biến:

1. `001_PersistenceFoundation.sql`
2. `002_Identity.sql`
3. `003_Catalog.sql`
4. `004_Catalog_Nul_Constraints.sql`
5. `005_GoodsReceipt.sql`
6. `006_GoodsReceipt_Number_Sequence.sql`
7. `007_Inventory_Stock.sql`
8. `008_Security_Administration.sql`
9. `009_Security_Permission_Import.sql`

Journal giữ nguyên `dbo.SchemaVersions`. Script đã phát hành là immutable; không sửa nội dung, đổi tên, xóa hoặc thêm entry thủ công để hợp thức hóa một schema khác. [SqlSchemaCompatibilityChecker](../src/MyDmsVn.Server.Infrastructure/Operations/SqlSchemaCompatibilityChecker.cs) kiểm tra history cùng các artifact/invariant được khai báo: tables/functions/sequence, CHECK/PK/unique index/FK, enabled/trusted state và trigger append-only. Baseline hiện tại còn gồm `SystemSecuritySettings`, `SecurityAuditEvents`, `ApplySecurityAdministration`, `ReadSecurityPermissionImportTarget` và `ApplySecurityPermissionImport`. Chỉ thấy entry `009` không chứng minh các artifact đều đúng.

Sequence cấp số phiếu phải giữ đúng `bigint`, start/increment/min bằng 1, max bằng 9999999999 và `NO CYCLE`; preflight không so `current_value`, `last_used_value`, cache hoặc exhaustion state vì chúng thay đổi hợp lệ khi vận hành. Preflight kiểm kê user-defined schema-scoped object cấp cao nhất, DML trigger trên mọi schema, synonym, database-scoped DDL trigger và user-defined type trong `sys.types`; allowlist khớp schema, tên, object type và parent của trigger. Baseline chưa có managed user-defined type, nên alias/table type ngoài allowlist làm database bị coi là unmanaged. Object ngoài contract hoặc trùng tên nhưng sai type không được coi là database empty. Constraint/index con được kiểm tra bằng các invariant riêng.

Các prefix hợp lệ nhưng chưa đủ, từ `001` đến tối đa `008`, là `Schema.TooOld` và bị desktop chặn. **Migrator hiện tại cũng từ chối TooOld** bằng `Migration.UnsupportedExistingDatabase`; nó không cung cấp auto-upgrade cho dữ liệu của baseline permission cũ hoặc tự hoàn tất một prefix đã commit dở. [DatabaseMigrationRunner](../src/MyDmsVn.Server.DbMigrator/DatabaseMigrationRunner.cs) chỉ nhận `Empty` hoặc `Compatible` trước khi chạy DbUp. Không có prior production baseline/mixed-client upgrade path đã được chấp nhận trong tài liệu hiện tại.

[Publish script](../deployment/release/Publish-LocalRelease.ps1) hiện lấy schemaSupport và requiredScripts từ runtime policy trong DLL admin của package, qua helper x86. RELEASE-MANIFEST-01 đã đóng riêng phần metadata với package commit sạch `d8013030`: schema9 và danh sách001–009 ([evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed)). Package cũ ghi schema8 là artifact lịch sử; không downgrade schema hoặc thay journal theo metadata cũ. Metadata đúng chưa chứng minh startup hay release acceptance.

## 2. Desktop read-only preflight

Host active `MyDmsVn.Desktop.App.exe` chạy `DesktopDatabaseStartupCoordinator` trước khi đăng ký persistence nghiệp vụ hoặc tạo shell. Preflight chỉ mở connection, đọc journal và metadata; không chạy migration hoặc sửa business data. AppCore không phải target đang chạy/phát hành.

| Trạng thái | Mã | Desktop | Hành động operator |
|---|---|---|---|
| Không history/artifact nghiệp vụ, database empty | `Schema.Empty` | Chặn | DBA tạo/chọn đúng application database empty, rồi chạy clean install bằng migrator |
| Prefix hợp lệ 001–008 nhưng chưa đủ 009 | `Schema.TooOld` | Chặn | Giữ state; migrator hiện tại trả `Migration.UnsupportedExistingDatabase`. Chọn recovery/forward-fix được review, hoặc tạo lại target disposable đã xác minh; không auto-upgrade |
| Đúng manifest 001–009 và đủ artifact, bao gồm `SystemSecuritySettings` và security administration/import procedures | `Schema.Compatible` | Cho phép preflight tiếp | Kiểm tra superuser binding/configuration kế tiếp |
| Đủ prefix 001–009 và journal có thêm script sau đó | `Schema.TooNew` | Chặn | Dùng client/schema contract tương thích đã được duyệt; không downgrade schema |
| Sai/thừa/thiếu giữa chuỗi hoặc artifact ngoài allowlist | `Schema.UnknownHistory` | Chặn | Dừng rollout, điều tra provenance; không sửa journal thủ công |
| Journal và managed artifact không khớp, hoặc có managed artifact nhưng không journal | `Schema.PartialMigration` | Chặn | Migrator từ chối `Migration.PartialSchema`; giữ client đóng và chọn forward-fix hoặc restore đã kiểm chứng |

Không có mixed-version combination được phê duyệt ở baseline này. Nếu release sau muốn hỗ trợ rolling rollout, migration phải theo expand/contract và cả hai client manifest phải được kiểm thử rõ ràng. Nếu không, yêu cầu downtime cho tới khi DB và toàn bộ client được nâng cấp khớp nhau.

## 3. Preflight triển khai

Trước migration, operator phải ghi nhận:

1. đúng server/instance/application database, không phải system database;
2. SQL Server version/edition và bằng chứng support tương ứng; matrix hiện còn mở, không suy diễn support từ một lần kết nối thành công;
3. migration principal riêng có quyền cần thiết, runtime principal không có DDL;
4. dung lượng data/log/tempdb đủ và không có lỗi storage;
5. `dbo.SchemaVersions` và manifest hiện tại;
6. backup/recovery point đã được tạo và restore-test vào database riêng theo yêu cầu P6 recovery còn mở;
7. client đã dừng/drain, không còn transaction Post đang chạy;
8. target release/commit/artifact hash đã duyệt.

Chỉ sau đó đặt một maintenance owner và chạy migrator với [migration configuration](CONFIGURATION_AND_SQL_SECURITY.md). Migrator không tạo/drop database và từ chối system database. Nó xác nhận principal có `CONTROL` trên đúng application database trước khi lấy lock hoặc gọi DbUp; runtime principal bị từ chối bằng `Migration.InsufficientPrivileges`, kể cả khi migration sẽ là no-op. `DatabaseMigrationRunner` lấy SQL application lock exclusive `MyDmsVn.SchemaMigration`, chạy preflight và DbUp transaction-per-script trên cùng một SQL session. Mất session đồng thời giải phóng lock và dừng/rollback script đang chạy, tránh một DbUp session mồ côi tiếp tục sau khi lock biến mất. Lần gọi cạnh tranh nhận `Migration.AlreadyRunning`; không retry mù hoặc chạy migrator thứ hai.

## 4. Thực thi

Từ thư mục package active `admin-migrator-net48-win-x86`, dùng executable .NET Framework và file admin-only:

```powershell
$env:MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE = 'C:\ProgramData\MyDmsVn\migration.connection'
& .\MyDmsVn.Server.DbMigrator.exe
```

[DatabaseMigratorCommand](../src/MyDmsVn.Server.DbMigrator/DatabaseMigratorCommand.cs) trả exit code: `0` thành công/no-op, `1` migration failure, `2` configuration failure, `3` competing migrator, `4` schema preflight từ chối, `5` migration principal thiếu quyền. Kiểm tra `$LASTEXITCODE` trước bước tiếp theo. Không dùng lệnh `dotnet ...dll` của package net8 đã parked. Output không in connection string hoặc exception chain.

DbUp dùng `WithTransactionPerScript()`: mỗi script thành công có thể commit trước khi script sau lỗi. Toàn release không phải một transaction all-or-nothing. Nếu lần chạy dừng sau một prefix đã commit, lần chạy tiếp theo có thể bị chặn bởi `TooOld`/`PartialMigration`; xem recovery decision, không giả định retry sẽ tiếp tục. Không chạy migrator ở desktop login/startup và không sửa/xóa journal để ép no-op.

## 5. Recovery decision

Sau lỗi:

1. Giữ toàn bộ client đóng; preflight phải tiếp tục chặn trạng thái không tương thích.
2. Lưu safe log, journal, schema inventory, SQL health và backup identifier; không export secret.
3. Xác định script cuối commit và artifact thực tế.
4. Đánh giá forward-fix đã review nếu có thể bảo toàn dữ liệu; thay đổi đó phải cung cấp một đường chuyển schema được hỗ trợ. Current migrator không tự nâng prefix 001–008, nên “chạy lại executable” không phải forward-fix.
5. Chỉ restore khi owner chấp nhận recovery point/RPO và backup đã restore-test vào database riêng. Restore có thể mất write sau recovery point.
6. Không viết lại `SchemaVersions` thủ công như cách sửa thông thường.
7. Sau recovery, xác nhận target thuộc trạng thái được công cụ đã duyệt hỗ trợ. Với current baseline hoàn chỉnh, chạy migrator một owner để xác nhận no-op, rồi desktop preflight và reconciliation/smoke trước khi mở lại truy cập. Với target empty/disposable được xác nhận rõ, clean install phải hoàn tất đủ 001–009 rồi no-op lần hai.

## 6. Rollout order

Fresh install hoặc một đường triển khai đã được review dùng stop/drain maintenance. Danh sách dưới đây không công bố hỗ trợ upgrade tự động từ prefix cũ:

1. Dừng client cũ.
2. Backup, restore-test và kiểm tra preflight quản trị; xử lý release metadata blocker trước khi phê duyệt artifact.
3. Chạy công cụ migration/forward-fix được duyệt cho đúng trạng thái target, bằng admin package và maintenance principal.
4. Xác nhận journal/artifact và migrator no-op.
5. Với fresh install có user store trống, cấu hình `Security__SuperuserUsername` và chạy `security bootstrap-superuser` theo [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md). Không chạy bootstrap trên database đã có user.
6. Triển khai host phù hợp manifest 001–009 và cùng cấu hình superuser đã được review.
7. Chạy schema preflight, xác nhận binding/configuration, functional smoke và reconciliation.
8. Mở lại truy cập.

Không chạy client cũ sau khi schema vượt contract của nó; không chạy client mới khi DB còn prefix cũ/partial. Downgrade client không đồng nghĩa downgrade database. Ghi revision/package/SQL version, preflight, exit code, journal/artifact và recovery outcome theo [TEST_POLICY](TEST_POLICY.md); các bài diễn tập recovery và owner release sign-off vẫn là [điều kiện chưa nghiệm thu, chưa xếp lịch](DEFERRED_WORK.md#release-prerequisites).


