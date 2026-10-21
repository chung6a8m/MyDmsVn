# Cấu hình và quyền SQL cho Local Mode v1

**Trạng thái:** Hiện trạng `v1a-pro`, cập nhật 2026-10-20. Desktop.App và DbMigrator là `net48`/x86; AppCore `net8.0-windows` parked. Đây là thiết kế quyền/configuration, chưa phải bằng chứng production clean install, credential rotation hay least-privilege acceptance. Các [điều kiện phát hành](DEFERRED_WORK.md#release-prerequisites) vẫn **UNACCEPTED / UNSCHEDULED**.

[RESP](plans/20261018-002-respawn-fixture-reuse.md) và [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) đều đã hoàn thành plan-specific acceptance. Catalog/Inventory hiện dùng LINQ/native; Identity/Security/operations dùng typed native SqlClient ([T05](L2DB_VERIFICATION.md#l2db-t05--identity-security-and-operations-conversion)), test-only oracles cũng đã chuyển sang native tại T06. T07 correctness/package và limited authenticated Local smoke đã hoàn tất ([evidence](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke)); [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) đã được nghiệm thu theo phạm vi và ngoại lệ đã ghi. BCI đã triển khai/nghiệm thu trong phạm vi UI lịch sử. [BCF](plans/20261021-001-business-compact-lookup-and-visual-parity.md) đã COMPLETE / OWNER ACCEPTED trong [phạm vi và ngoại lệ cuối](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) và không thay runtime/API/schema. SQL/VM/recovery release checks chưa được nghiệm thu; thay UI hoặc DAL không cho phép thêm quyền, rò maintenance credential, sửa schema/binding hay thử trên production.

## 1. Hai danh tính SQL bắt buộc

Không dùng chung credential giữa desktop và gói quản trị.

| Danh tính | Nơi sử dụng hiện tại | Quyền được phê duyệt |
|---|---|---|
| Runtime principal | Desktop.App net48, Local Mode | DML theo object/cột và EXECUTE trên đúng ba security procedure ở mục 2; đọc migration journal và metadata; không DDL hoặc sửa journal/binding. |
| Migration/maintenance principal | Gói quản trị DbMigrator net48 | `CONTROL` trong đúng application database để triển khai schema và thực hiện maintenance được chỉ định. |

Hai database user phải khác nhau. Runtime không được là `sa`, thành viên fixed role quản trị/đọc-ghi rộng như `sysadmin`, `db_owner`, `db_ddladmin`, hoặc nhận quyền tương đương qua role khác. Không cấp quyền impersonate hay kiểm soát principal khác để chuyển sang migration identity. Migration credential không được đóng gói cùng desktop, ghi vào source control, shortcut, command line hoặc installer property có thể xuất hiện trong log.

Nguồn provisioning thực tế là [deployment/sql/Provision-MyDmsVnPrincipals.sql](../deployment/sql/Provision-MyDmsVnPrincipals.sql). Script yêu cầu application database và hai database user đã tồn tại, từ chối system database/cùng identity, kiểm tra quyền runtime hiện có, rồi thiết lập `MyDmsVnRuntimeRole` và `MyDmsVnMigrationRole`. Nó không tạo login, database hoặc business schema; các object của supported schema và `dbo.SchemaVersions` phải sẵn sàng trước khi áp dụng các GRANT.

DBA tạo login/database user riêng, cấp maintenance principal quyền `CONTROL` trong đúng application database để chạy migrator, rồi triển khai supported schema bằng identity này. Chỉ sau khi đủ schema/journal 001–009 mới áp dụng script bên dưới để thiết lập managed roles và runtime object grants. Script này không phải bước tạo quyền ban đầu cho một migrator chưa thể kết nối/triển khai schema:

```powershell
sqlcmd -S '<server>' -d '<application-database>' -E `
  -v RuntimeUser="<runtime-database-user>" MigrationUser="<migration-database-user>" `
  -i deployment\sql\Provision-MyDmsVnPrincipals.sql
```

Không đưa password vào lệnh. Với SQL Authentication, DBA tạo/đổi password qua kênh quản trị riêng. Khi cập nhật provisioning, script chủ động REVOKE `INSERT` và `UPDATE` toàn bảng `dbo.Users` khỏi managed runtime role trước khi cấp lại đúng năm cột password/audit. Không coi việc chạy lại script là cơ chế tự động xóa mọi grant tùy ý đã được cấp ngoài thiết kế.

## 2. Ma trận quyền runtime hiện tại

### Quyền dữ liệu và sequence

| Object trong `dbo` | SELECT | INSERT | UPDATE | DELETE |
|---|---:|---:|---:|---:|
| `Users` | Có | Không | Chỉ `PasswordHash`, `PasswordSalt`, `PasswordAlgorithm`, `UpdatedAtUtc`, `UpdatedByUserId` | Không |
| `Roles`, `UserRoles`, `RolePermissions`, `UserPermissions` | Có | Không | Không | Không |
| `SystemSecuritySettings` | Có | Không | Không | Không |
| `Products`, `Warehouses`, `Employees`, `Customers` | Có | Có | Có | Không |
| `GoodsReceipts` | Có | Có | Có | Không |
| `GoodsReceiptLines` | Có | Có | Có | Có |
| `GoodsReceiptNumberSequence` | — | — | Có, để dùng `NEXT VALUE FOR` | — |
| `StockLedger` | Có | Có | Không | Không |
| `StockBalances` | Có | Có | Có | Không |
| `SchemaVersions` | Có | DENY | DENY | DENY |
| `SecurityAuditEvents` | **Không** | Không | Không | Không |

Runtime không có direct `UPDATE IsActive` hay generic user/RBAC administration. Các thao tác quản trị hợp lệ đi qua Application authorization rồi gọi procedure được cấp quyền; procedure kiểm tra actor/target, cập nhật revision và ghi audit trong cùng transaction. Quyền EXECUTE procedure không tương đương quyền đọc/ghi trực tiếp `SecurityAuditEvents`.

### Allowlist EXECUTE

Script hiện tại cấp đúng ba quyền EXECUTE sau cho `MyDmsVnRuntimeRole`:

| Procedure | Mục đích |
|---|---|
| `dbo.ApplySecurityAdministration` | Thay đổi user/role/membership/assignment theo thao tác hỗ trợ, có revision và audit. |
| `dbo.ReadSecurityPermissionImportTarget` | Đọc target/assignments phục vụ preview sau khi kiểm tra actor. |
| `dbo.ApplySecurityPermissionImport` | Áp dụng additive import với kiểm tra target/revision, actor và audit. |

Không cấp EXECUTE ở database/schema scope, blanket EXECUTE hoặc quyền chạy maintenance ngoài allowlist này. Không cấp SELECT/DML toàn schema hoặc quyền dữ liệu trên object ngoài ma trận. Bootstrap/recovery/rename superuser thuộc admin package và maintenance connection; chúng không trở thành runtime procedure chỉ vì ứng dụng có tài khoản superuser.

### Metadata và phạm vi kiểm tra hiện có

Runtime role có `VIEW DEFINITION` ở database scope; script còn ghi rõ một số object-level `VIEW DEFINITION`, gồm `dbo.CatalogTextContainsNul`. Quyền này phục vụ schema preflight đọc định nghĩa object, constraint, index, function và trigger. Nó không cấp SELECT dữ liệu, EXECUTE, DML, DDL hoặc quyền chỉnh sửa lịch sử. Mọi object/quyền mới phải được xem xét rõ ràng cùng script và validator.

Provisioning kiểm tra effective schema/object/column DML, quyền quản trị database/schema/object, fixed database roles và quyền kiểm soát/impersonate database principals. [SqlRuntimePrincipalValidator](../src/MyDmsVn.Server.Infrastructure/Operations/SqlRuntimePrincipalValidator.cs) thực hiện các kiểm tra tương ứng khi desktop khởi động và kiểm tra thêm server roles, CONTROL SERVER cùng quyền trên login/server-role khi dùng login thực. Phát hiện quyền ngoài các kiểm tra này trả về `Security.RuntimePrincipalOverprivileged`.

**Giới hạn source hiện tại:** Các phép kiểm tra schema/object trong provisioner và startup validator liệt kê DML/quyền quản trị, chưa đối chiếu đầy đủ EXECUTE tại schema/object scope với allowlist ba procedure. Ma trận trên là quyền được script cấp và được phê duyệt; kết quả startup thành công không chứng minh tự động rằng không có EXECUTE được cấp thêm bên ngoài script. Khi kiểm tra principal triển khai, đối chiếu các grant EXECUTE thực tế với đúng ba object trên. Tài liệu này không bổ sung hoặc tuyên bố đã kiểm thử một validator mới.

## 3. Nguồn cấu hình và precedence

[SqlConnectionConfigurationLoader](../src/MyDmsVn.Server.Infrastructure/Operations/SqlConnectionConfiguration.cs) tách cấu hình runtime và migration. Mỗi file triển khai chứa một connection string; không commit file thật hoặc giá trị secret vào repo.

| Mức ưu tiên | Desktop runtime | Admin migrator/maintenance |
|---|---|---|
| 1 | `MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING` | `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING` |
| 2 | File tại `MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING_FILE` | File tại `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE` |
| 3 | `%ProgramData%\MyDmsVn\runtime.connection` | `%ProgramData%\MyDmsVn\migration.connection` |

Giá trị environment không rỗng được chọn trước; tiếp theo là environment-file path không rỗng, rồi installation file. Một nguồn đã được chọn nhưng không đọc được hoặc không hợp lệ gây lỗi; không tự lùi xuống nguồn khác để che cấu hình hỏng. Không có fallback giữa runtime và migration. Desktop không đọc migration setting, và migrator không đọc runtime setting.

Loader kiểm tra connection-string syntax và yêu cầu database cụ thể, từ chối `master`, `model`, `msdb`, `tempdb`. Thiếu file/cấu hình, không đọc được, sai cú pháp hoặc database không hợp lệ trả về mã lỗi an toàn; không xuất connection string. Những kiểm tra này không thay thế cấu hình TLS, network và quyền SQL được phê duyệt.

### Cấu hình superuser và binding

`Security__SuperuserUsername` là biến môi trường bắt buộc cho desktop và các lệnh security maintenance sử dụng cấu hình này. Đây là cách source hiện tại nạp setting logic `Security:SuperuserUsername`, qua [SuperuserConfigurationLoader](../src/MyDmsVn.Server.Infrastructure/Operations/SuperuserConfigurationLoader.cs); không có file JSON fallback được triển khai cho setting này.

Username được trim/normalize bằng `UsernameNormalizer`. Sau kiểm tra quyền và compatible schema, startup kiểm tra setting khớp với `dbo.SystemSecuritySettings` singleton và tài khoản được bind trong `Users`. Setting do operator quản lý bên ngoài database; **bound normalized username và UserId được lưu trong SQL** để đối chiếu identity. Không nhầm setting bên ngoài với dữ liệu binding hợp lệ này.

Bootstrap dùng maintenance connection qua lệnh `security bootstrap-superuser --target <application-database>`, yêu cầu xác nhận target và đọc password qua prompt masked. Nó chỉ chấp nhận user store rỗng và binding chưa có; không tự nhận một tài khoản có tên tương tự. Recovery/rename dùng quy trình maintenance riêng, expected user ID và cấu hình tương ứng. Không đổi binding trực tiếp qua runtime API hoặc đưa password lên command line. Xem [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md).

## 4. Credential và triển khai

Ưu tiên Windows Authentication với group/service identity giới hạn và kết nối từ workstation được phép. Đây là chính sách triển khai; tài liệu không tự cấu hình login hay ACL.

File trong `%ProgramData%\MyDmsVn` phải thuộc Administrators/nhóm triển khai. Identity chạy desktop chỉ được đọc runtime file; migration file chỉ dành cho DBA/deployment. Không cho `Users`/`Everyone` ghi và không kế thừa ACL rộng từ thư mục tạm hoặc share. Hai loại file phải được phân quyền riêng.

Nếu cần SQL Authentication, provision credential ngoài source/installer defaults. Dùng file được bảo vệ bằng ACL hoặc secret delivery của tổ chức; environment override dành cho quy trình kiểm soát. DPAPI có thể là một thiết kế bổ sung, nhưng loader hiện tại không triển khai mã hóa/giải mã DPAPI và không được mô tả như đã có bảo vệ đó. ACL/DPAPI không ngăn process desktop vốn được cấp quyền sử dụng runtime credential.

Khi rotate: tạo/đổi credential tại SQL, cập nhật đúng runtime hoặc maintenance secret qua kênh quản trị, dừng client liên quan, thay file/environment atomically, khởi động lại rồi chạy startup/schema preflight và smoke phù hợp. Không đưa credential cũ/mới vào ticket, log hay ảnh chụp. Xem [migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) trước mọi thao tác thay schema hoặc recovery.

## 5. Mạng và TLS

- Chỉ mở SQL Server port từ subnet/workstation được phê duyệt; không public Internet.
- Production dùng `Encrypt=true` và xác thực certificate với `TrustServerCertificate=false`.
- Không đổi thành `Encrypt=false` hoặc `TrustServerCertificate=true` để chữa lỗi certificate khi chưa có exception được owner phê duyệt và ghi nhận.
- Dùng connection timeout hữu hạn, phù hợp độ trễ môi trường; không cấu hình vô hạn.
- SQL instance, application database và authentication mode phải đúng ma trận triển khai; mọi product/migration connection phải chỉ rõ application database.

Đây là yêu cầu triển khai giữ nguyên trong RESP/L2DB, không phải tuyên bố loader đã tự kiểm chứng mọi thiết lập TLS/timeout. Integration Test dùng riêng `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`, trỏ rõ vào `master` của SQL Server non-production do người vận hành chọn. Helper cục bộ tạo database disposable và xác minh instance/định danh/marker trước khi xóa; không còn approval file hoặc SQL runner. Không thay application/migration connection bằng connection `master` của test. [TEST_POLICY](TEST_POLICY.md) quy định phạm vi kiểm thử và tài nguyên test.

## 6. Startup, redaction và diagnostics

[DesktopDatabaseStartupCoordinator](../src/MyDmsVn.Server.Infrastructure/Operations/DesktopDatabaseStartupCoordinator.cs) nạp runtime configuration, kiểm tra runtime principal, kiểm tra schema, rồi kiểm tra superuser binding khi schema tương thích. Startup không tự migration, cấp thêm quyền, sync permission hay bootstrap tài khoản. Lỗi SQL trong preflight được chuyển thành thông báo database-unavailable an toàn.

Không ghi/export password, PasswordHash, PasswordSalt, bearer/auth artifact, decrypted secret, connection string đầy đủ, SQL với parameter nhạy cảm hoặc exception chain thô. Diagnostics chỉ giữ loại nguồn `Environment`/`EnvironmentFile`/`InstallationFile`, mã lỗi ổn định, correlation ID và metadata không nhạy cảm được duyệt. Correlation ID không phải permission hay credential.

Các mã startup hiện có liên quan:

| Mã | Ý nghĩa |
|---|---|
| `Startup.DatabaseConfigurationMissing` | Không có cấu hình mặc định khả dụng. |
| `Startup.DatabaseConfigurationNotFound` | File được chỉ định không tồn tại. |
| `Startup.DatabaseConfigurationUnreadable` | File không thể đọc bằng quyền/cấu hình hiện tại. |
| `Startup.DatabaseConfigurationInvalid` | Connection string/database không hợp lệ. |
| `Startup.DatabaseUnavailable` | SQL preflight không thực hiện được. |
| `Startup.SuperuserConfigurationMissing` | Thiếu configured superuser username. |
| `Security.SuperuserBindingMismatch` | Setting/binding/account không khớp. |
| `Security.RuntimePrincipalOverprivileged` | Validator phát hiện quyền runtime ngoài phạm vi kiểm tra được cho phép. |
| `Schema.*` | Trạng thái schema không tương thích; theo migration runbook. |

## 7. Giới hạn trust và công việc được hoãn

Local v1 chạy Application trong process desktop và là triển khai two-tier. Người kiểm soát process hoặc runtime credential có thể gọi SQL trong phạm vi quyền được cấp, vượt qua Application authorization/business workflow. Thu hẹp quyền và procedure giảm phạm vi tác động; chúng không tạo một trusted server boundary hay biến actor ID từ Local client thành bằng chứng mật mã.

HTTP v2/P7 và modern product runtime là các [hướng ngoài phạm vi](DEFERRED_WORK.md#out-of-scope-directions). Các [điều kiện phát hành](DEFERRED_WORK.md#release-prerequisites) chưa được nghiệm thu, chưa xếp lịch và không được coi là hoàn thành/waived khi chọn BCF làm lộ trình duy nhất. Runbook này mô tả khả năng hiện có, giới hạn đã thấy trong source và yêu cầu triển khai; nó không thay thế bằng chứng acceptance thực tế.

