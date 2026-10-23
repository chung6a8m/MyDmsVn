# Schema compatibility và migration runbook

**Hiện trạng:** SAL-T01 development checkpoint, cập nhật 2026-10-22. Runtime candidate yêu cầu exact schema10; checkpoint này chưa thể triển khai độc lập vì receipt/sale/reconciliation cutover và SAL-T08 package/Owner evidence chưa hoàn tất. SEC-REBUILD/RESP/L2DB và GUI-P5 đã hoàn thành theo phạm vi được nghiệm thu; Baseline lịch sử trước SAL yêu cầu scripts001–009. Desktop và DbMigrator phát hành đều `net48`/x86, AppCore parked. DbMigrator là gói admin riêng; desktop không tự chạy DbUp hoặc security bootstrap. BCI đã triển khai/nghiệm thu trong phạm vi UI lịch sử. [BCF](plans/20261021-001-business-compact-lookup-and-visual-parity.md) đã COMPLETE / OWNER ACCEPTED trong [phạm vi và ngoại lệ cuối](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) và không thay schema. Các [điều kiện schema-compatibility, recovery và phát hành](DEFERRED_WORK.md#release-prerequisites) vẫn **UNACCEPTED / UNSCHEDULED**.

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
10. `010_DirectSalesAndValuation.sql`

Journal giữ nguyên `dbo.SchemaVersions`. Script đã phát hành là immutable; không sửa nội dung, đổi tên, xóa hoặc thêm entry thủ công để hợp thức hóa một schema khác. [SqlSchemaCompatibilityChecker](../src/MyDmsVn.Server.Infrastructure/Operations/SqlSchemaCompatibilityChecker.cs) kiểm tra history cùng các artifact/invariant được khai báo: tables/functions/sequence, CHECK/PK/unique index/FK, enabled/trusted state và trigger append-only. Baseline hiện tại còn gồm `SystemSecuritySettings`, `SecurityAuditEvents`, `ApplySecurityAdministration`, `ReadSecurityPermissionImportTarget` và `ApplySecurityPermissionImport`. Chỉ thấy entry `009`/`010` không chứng minh các artifact đều đúng. Runtime10 giữ các invariant baseline9 và kiểm tra toàn bộ definition inventory10 (columns/defaults/checks/keys/indexes/FKs/modules/triggers/sequences); dữ liệu, current identity/sequence values và grants không được trộn vào definition fingerprint. Grants được kiểm tra riêng.

Sequence cấp số phiếu phải giữ đúng `bigint`, start/increment/min bằng 1, max bằng 9999999999 và `NO CYCLE`; preflight không so `current_value`, `last_used_value`, cache hoặc exhaustion state vì chúng thay đổi hợp lệ khi vận hành. Preflight kiểm kê user-defined schema-scoped object cấp cao nhất, DML trigger trên mọi schema, synonym, database-scoped DDL trigger và user-defined type trong `sys.types`; allowlist khớp schema, tên, object type và parent của trigger. Baseline chưa có managed user-defined type, nên alias/table type ngoài allowlist làm database bị coi là unmanaged. Object ngoài contract hoặc trùng tên nhưng sai type không được coi là database empty. Constraint/index con được kiểm tra bằng các invariant riêng.

Các prefix hợp lệ nhưng chưa đủ, từ `001` đến tối đa `008`, là `Schema.TooOld` và bị desktop chặn. **Migrator tiếp tục từ chối prefixes001–008** bằng `Migration.UnsupportedExistingDatabase`; nó không tự hoàn tất một prefix đã commit dở. [DatabaseMigrationRunner](../src/MyDmsVn.Server.DbMigrator/DatabaseMigrationRunner.cs) nhận Empty, exact10 no-op, hoặc đường exact9 riêng với frozen source manifest/artifact validation và maintenance admission dưới đây. New runtime từ chối exact9; old schema9 policy từ chối10. Không có prior production baseline/mixed-client upgrade path đã được chấp nhận trong tài liệu hiện tại.

[Publish script](../deployment/release/Publish-LocalRelease.ps1) hiện lấy schemaSupport và requiredScripts từ runtime policy trong DLL admin của package, qua helper x86. RELEASE-MANIFEST-01 đã đóng riêng phần metadata với package commit sạch `d8013030`: schema9 và danh sách001–009 ([evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed)). Package cũ ghi schema8 là artifact lịch sử; không downgrade schema hoặc thay journal theo metadata cũ. Metadata đúng chưa chứng minh startup hay release acceptance.

## 2. Desktop read-only preflight

Host active `MyDmsVn.Desktop.App.exe` chạy `DesktopDatabaseStartupCoordinator` trước khi đăng ký persistence nghiệp vụ hoặc tạo shell. Preflight chỉ mở connection, đọc journal và metadata; không chạy migration hoặc sửa business data. AppCore không phải target đang chạy/phát hành.

| Trạng thái | Mã | Desktop | Hành động operator |
|---|---|---|---|
| Không history/artifact nghiệp vụ, database empty | `Schema.Empty` | Chặn | DBA tạo/chọn đúng application database empty, rồi chạy clean install bằng migrator |
| Prefix hợp lệ 001–008 | `Schema.TooOld` | Chặn | Giữ state; migrator hiện tại trả `Migration.UnsupportedExistingDatabase`. Chọn recovery/forward-fix được review, hoặc tạo lại target disposable đã xác minh; không auto-upgrade |
| Exact9 đã validate đầy đủ | `Schema.TooOld` | Chặn | Chỉ separate admin migrator có thể admission theo mục3.1; không mở old writers trên10 |
| Đúng manifest001–010 và đầy đủ artifact, gồm Sales/valuation và value balance | `Schema.Compatible` | Cho phép preflight tiếp | Kiểm tra superuser binding/configuration kế tiếp |
| Đủ prefix001–010 và journal có thêm script sau đó | `Schema.TooNew` | Chặn | Dùng client/schema contract tương thích đã được duyệt; không downgrade schema |
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
6. backup/recovery point đã được tạo và restore-test vào database riêng theo release prerequisites còn mở;
7. client đã dừng/drain, không còn transaction Post đang chạy;
8. target release/commit/artifact hash đã duyệt.

Chỉ sau đó đặt một maintenance owner và chạy migrator với [migration configuration](CONFIGURATION_AND_SQL_SECURITY.md). Migrator không tạo/drop database và từ chối system database. Nó xác nhận principal có `CONTROL` trên đúng application database trước khi lấy lock hoặc gọi DbUp; runtime principal bị từ chối bằng `Migration.InsufficientPrivileges`, kể cả khi migration sẽ là no-op. `DatabaseMigrationRunner` lấy SQL application lock exclusive `MyDmsVn.SchemaMigration`, chạy preflight và DbUp transaction-per-script trên cùng một SQL session. Mất session đồng thời giải phóng lock và dừng/rollback script đang chạy, tránh một DbUp session mồ côi tiếp tục sau khi lock biến mất. Lần gọi cạnh tranh nhận `Migration.AlreadyRunning`; không retry mù hoặc chạy migrator thứ hai.

## 3.1 Exact9 maintenance admission — narrow supported identity model

Đường upgrade chỉ nhận database ONLINE/read-write/noncontained đã ở `RESTRICTED_USER`. Operator phải đóng/drain clients và thiết lập mode trước; migrator không ALTER mode, KILL, rotate/disable credentials hoặc tự mở lại MULTI_USER. Giữ restriction qua cả thành công lẫn lỗi010 cho đến khi matching receipt/sale/reconciliation runtime, grants và package đã được kiểm chứng và operator chủ động cho mở lại. Một Session applock không đủ chặn old binaries.

`Schema9MaintenanceValidator` đọc trên chính nonpooled session sở hữu migration lock, ngay trước handoff cho DbUp. Admin phải có CONTROL database cùng VIEW SERVER STATE, VIEW ANY DEFINITION và (SQL Server2022+) VIEW SERVER PERFORMANCE STATE; thiếu visibility không được hiểu là zero writers. Guard từ chối session/request khác tại target, user transactions hoặc locks tại target kể cả session đang ở master, và tất cả original-login sessions của mapped SQL writers trên instance. Own implicit observation transaction và system-only transactions không bị nhầm là application writers. Script010 kiểm tra restriction lần nữa và lấy transaction-held exclusive table locks trên historical source/ledger/balances trước data proof.

**Ruling:** admission chỉ hỗ trợ identifiable SQL-login database users; từ chối Windows/group/external/contained/application-role/certificate/key users, guest CONNECT, TRUSTWORTHY hoặc cross-database ownership chaining, unresolved login mappings và mapped elevated writers. Migration identity riêng hiện tại và privileged administrators là trusted maintenance boundary. Đây là cố ý thu hẹp đường exact9 upgrade, không phải bằng chứng universal identity closure cho mọi Windows/group/alternate credential path. Nếu deployment nằm ngoài model này, dừng và thiết kế admission có evidence riêng; không dùng boolean attestation hay bỏ guard để vượt qua. Trusted admin có thể thay mode/permissions hoặc can thiệp database; guard không tuyên bố chống lại admin đó.

Trong transaction010, bootstrap chứng minh ledger↔Posted receipt line và ledger keys↔balance keys theo cả hai chiều; từ chối unsupported movements/audit/quantity/cardinality/cost/overflow, kể cả extra zero balance. Mỗi source line được round4 riêng rồi sum rộng; không gán missing values bằng zero và không rewrite ledger/audit/document tokens. DDL/backfill/checks và010 journal share transaction-per-script. Lỗi010 rollback về intact validated9 và giữ restriction; điều này không biến failure sau prefix001–008 của fresh install thành auto-retry path.

SAL-T01 evidence chỉ là checkpoint schema/bootstrap/grants/reset. Actual packaged upgrade, matched runtime startup, frozen integration và Owner disposition vẫn thuộc SAL-T08; operational release prerequisites vẫn unaccepted/unscheduled.

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
7. Sau recovery, xác nhận target thuộc trạng thái được công cụ đã duyệt hỗ trợ. Với current baseline hoàn chỉnh, chạy migrator một owner để xác nhận no-op, rồi desktop preflight và reconciliation/smoke trước khi mở lại truy cập. Với target empty/disposable được xác nhận rõ, clean install phải hoàn tất đủ001–010 rồi no-op lần hai.

## 6. Rollout order

Fresh install hoặc một đường triển khai đã được review dùng stop/drain maintenance. Danh sách dưới đây không công bố hỗ trợ upgrade tự động từ prefix cũ:

1. Dừng client cũ.
2. Backup, restore-test và kiểm tra preflight quản trị; xử lý release metadata blocker trước khi phê duyệt artifact.
3. Chạy công cụ migration/forward-fix được duyệt cho đúng trạng thái target, bằng admin package và maintenance principal.
4. Xác nhận journal/artifact và migrator no-op.
5. Với fresh install có user store trống, cấu hình `Security__SuperuserUsername` và chạy `security bootstrap-superuser` theo [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md). Không chạy bootstrap trên database đã có user.
6. Chỉ triển khai host phù hợp target manifest đã được review/nghiệm thu và cấu hình superuser tương ứng. SAL-T01 checkpoint chưa thỏa bước triển khai này.
7. Chạy schema preflight, xác nhận binding/configuration, functional smoke và reconciliation.
8. Mở lại truy cập.

Không chạy client cũ sau khi schema vượt contract của nó; không chạy client mới khi DB còn prefix cũ/partial. Downgrade client không đồng nghĩa downgrade database. Ghi revision/package/SQL version, preflight, exit code, journal/artifact và recovery outcome theo [TEST_POLICY](TEST_POLICY.md); các bài diễn tập recovery và owner release sign-off vẫn là [điều kiện chưa nghiệm thu, chưa xếp lịch](DEFERRED_WORK.md#release-prerequisites).

## SAL schema10 — source checkpoint và evidence boundary

[SAL](DIRECT_SALES.md), [đặc tả giá vốn](DIRECT_SALES_COSTING.md) và [ADR0011](adr/0011-direct-sales-and-moving-average-costing.md) kiểm soát đường chuyển. SAL-T01 source candidate thêm010, exact9 maintenance admission, runtime exact10 preflight, mappings, grants và reset metadata. Các gói/acceptance schema9 lịch sử ở trên giữ đúng revision gốc; không được diễn giải chúng thành package evidence10.

Script010 kiểm chứng hai chiều Posted Goods Receipt ↔ StockLedger ↔ balance keys và quantity/audit; round từng source line rồi sum rộng; tạo valuation companions và InventoryValue mà không sửa ledger lịch sử hay tự bù0 cho nguồn thiếu/sai. Transaction-per-script gắn DDL/backfill/final constraints với010 journal. Fresh-install failure trước010 có thể để lại prefix đã commit, vẫn cần recovery; không suy atomicity cho toàn001–010.

[Ruling maintenance](#31-exact9-maintenance-admission--narrow-supported-identity-model) cố ý thu hẹp exact9 upgrade vào model đã kiểm chứng. Giữ mọi old clients/writers đóng và restriction cho đến khi receipt Post, sale Post và reconciliation cùng dùng giao thức khóa/value mới và matching runtime/grants/package đã được kiểm tra. Checkpoint T01 không độc lập deploy được.

[T00 focused foundation evidence](evidence/sal/20261022-t00-foundation.md) đã có; [acceptance ledger](DIRECT_SALES_ACCEPTANCE.md) phân biệt task checkpoint với SAL-T08 frozen SQL/UI/package evidence và Owner disposition còn thiếu. Các kiểm thử feature không thay thế [nghiệm thu vận hành/phát hành](DEFERRED_WORK.md#release-prerequisites), vẫn UNACCEPTED / UNSCHEDULED.
