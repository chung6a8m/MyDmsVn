# Cấu hình và quyền SQL cho Local Mode v1

Status: P6-T01 implementation baseline, 2026-10-12. Tài liệu này áp dụng cho hai host `net48` và `net8.0-windows`, cùng gói quản trị `MyDmsVn.Server.DbMigrator`.

## 1. Hai danh tính SQL bắt buộc

Không dùng chung credential giữa desktop và migrator.

| Danh tính | Nơi sử dụng | Quyền |
|---|---|---|
| Runtime principal | Hai desktop host | DML tối thiểu cho P3/P5; đọc journal để schema preflight; không DDL và không sửa journal |
| Migration principal | Chỉ gói triển khai/quản trị | `CONTROL` trong đúng application database để DbUp tạo/thay đổi schema |

Runtime principal không được là `sa`, thành viên `db_owner`, `db_ddladmin`, `sysadmin`, hoặc nhận quyền tương đương qua role khác. Migration credential không được đóng gói cùng desktop, ghi vào source control, shortcut, command line hoặc installer property có thể log ra ngoài.

Hai user phải khác nhau. Script provisioning từ chối cùng một identity và từ chối runtime user đã có fixed-role, database/schema/object administration hoặc database-wide permission ngoài tập mặc định an toàn. Khi khởi động, cả hai desktop host kiểm tra lại effective server/database/schema/object permissions và fail closed bằng `Security.RuntimePrincipalOverprivileged`; vì vậy cấu hình nhầm migration credential hoặc một `GRANT ALTER/CONTROL/TAKE OWNERSHIP` trực tiếp trên object không thể mở shell nghiệp vụ.

DBA tạo login/user theo chính sách của môi trường, sau đó chạy script idempotent:

```powershell
sqlcmd -S <server> -d <application-database> -E `
  -v RuntimeUser="<runtime-database-user>" MigrationUser="<migration-database-user>" `
  -i deployment\sql\Provision-MyDmsVnPrincipals.sql
```

Không đưa password vào lệnh trên. Với SQL Authentication, DBA tạo/đổi password qua kênh quản trị riêng trước khi tạo database user.

Khi nâng cấp từ bản provisioning cũ, chạy lại script hiện tại bằng principal quản trị. Script chủ động `REVOKE` quyền `INSERT` và `UPDATE` toàn bảng `dbo.Users` trước khi cấp lại đúng năm cột password/audit; không chỉ dựa vào `GRANT` mới để thu hẹp quyền cũ.

## 2. Ma trận quyền runtime

| Object | SELECT | INSERT | UPDATE | DELETE |
|---|---:|---:|---:|---:|
| `Users` | Có | Không | Chỉ `PasswordHash`, `PasswordSalt`, `PasswordAlgorithm`, `UpdatedAtUtc`, `UpdatedByUserId` | Không |
| `Roles`, `UserRoles`, `RolePermissions`, `UserPermissions` | Có | Không | Không | Không |
| `Products`, `Warehouses`, `Employees`, `Customers` | Có | Có | Có | Không |
| `GoodsReceipts` | Có | Có | Có | Không |
| `GoodsReceiptLines` | Có | Có | Có | Có |
| `GoodsReceiptNumberSequence` | — | — | Có (`NEXT VALUE FOR`) | — |
| `StockLedger` | Có | Có | Không | Không |
| `StockBalances` | Có | Có | Có | Không |
| `SchemaVersions` | Có | Không | Không | Không |

Runtime role còn có `VIEW DEFINITION` ở database scope để schema preflight kiểm kê nhất quán mọi user object, kể cả object unmanaged chưa được cấp quyền dữ liệu, đồng thời xác nhận function, constraint, index và trigger trong manifest. Đây là quyền chỉ đọc metadata/định nghĩa; nó không cấp `SELECT`, `EXECUTE`, DML hay DDL trên các object đó. Runtime validator chỉ allowlist quyền database read-only này và vẫn từ chối các quyền quản trị, bảo mật hoặc thay đổi schema.

Mọi object mới phải được xem xét và thêm rõ ràng vào script; không cấp `SELECT/EXECUTE` toàn schema. Runtime không được đọc object chưa liệt kê, `ALTER/DROP`, tự cấp quyền, hoặc thay đổi migration history.

## 3. Nguồn cấu hình và precedence

Mỗi file cấu hình chứa đúng một connection string trên một dòng. Không commit file thật vào repo.

### Desktop runtime

Theo thứ tự ưu tiên:

1. `MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING`.
2. File được chỉ bởi `MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING_FILE`.
3. `%ProgramData%\MyDmsVn\runtime.connection`.

### Admin migrator

Theo thứ tự ưu tiên:

1. `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING`.
2. File được chỉ bởi `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE`.
3. `%ProgramData%\MyDmsVn\migration.connection`.

Không có fallback giữa hai nhóm. Desktop không đọc migration setting và migrator không đọc runtime setting. Cấu hình thiếu, không đọc được, sai cú pháp, thiếu database hoặc trỏ vào `master`, `model`, `msdb`, `tempdb` đều fail closed bằng mã an toàn; connection string không xuất hiện trong thông báo.

## 4. Chiến lược credential

Ưu tiên Windows Authentication với group/service identity bị giới hạn và chỉ được kết nối từ workstation được phép. File trong `%ProgramData%\MyDmsVn` phải:

- thuộc Administrators/nhóm triển khai;
- runtime file chỉ cho identity chạy desktop đọc;
- migration file chỉ cho nhóm DBA/deployment đọc;
- không cho `Users`/`Everyone` ghi;
- không kế thừa ACL rộng từ thư mục tạm hoặc share.

Nếu bắt buộc SQL Authentication, credential được provision ngoài source/installer defaults. Dùng file ACL-protected hoặc secret delivery của tổ chức; environment override chỉ dành cho quy trình triển khai được kiểm soát. DPAPI có thể bổ sung theo machine/user binding của topology, nhưng chưa được triển khai trong P6-T01: không được ghi nhận như một bảo vệ hiện có. DPAPI/ACL không ngăn process desktop đã được cấp quyền, malware hoặc người dùng chiếm được runtime credential sử dụng quyền SQL đó.

Rotation: tạo/đổi credential ở SQL, cập nhật runtime hoặc migration secret qua kênh quản trị, dừng client liên quan, thay file/environment atomically, khởi động lại và chạy schema preflight/smoke. Không ghi credential cũ/mới vào ticket, log hoặc ảnh chụp.

## 5. Mạng và TLS

- Chỉ mở SQL Server port từ subnet/workstation được phê duyệt; không public Internet.
- Dùng `Encrypt=true` và xác thực certificate (`TrustServerCertificate=false`) trong production.
- Không sửa thành `Encrypt=false` hoặc `TrustServerCertificate=true` để chữa lỗi certificate mà không có exception được owner phê duyệt và ghi nhận.
- Connection timeout mặc định phải hữu hạn; môi trường có latency cần đặt giá trị cụ thể, không vô hạn.
- SQL Server instance, database name và authentication mode phải khớp ma trận triển khai. Mọi connection đều phải chỉ rõ application database.

## 6. Redaction và diagnostics

Không ghi hoặc export: password, `PasswordHash`, `PasswordSalt`, bearer/auth artifact, decrypted secret, connection string đầy đủ, SQL command kèm parameter nhạy cảm hoặc exception chain thô. Chỉ ghi source loại `Environment`, `EnvironmentFile` hoặc `InstallationFile`, mã lỗi ổn định, correlation ID ở P6-T03, và metadata không nhạy cảm đã duyệt.

Mã startup hiện có:

- `Startup.DatabaseConfigurationMissing`
- `Startup.DatabaseConfigurationNotFound`
- `Startup.DatabaseConfigurationUnreadable`
- `Startup.DatabaseConfigurationInvalid`
- `Startup.DatabaseUnavailable`
- `Security.RuntimePrincipalOverprivileged`
- các mã `Schema.*` trong runbook migration

## 7. Giới hạn trust còn lại

Local Mode v1 là kiến trúc two-tier. Người dùng có thể trích xuất hoặc sử dụng runtime credential có khả năng bypass Application authorization và business workflow trong phạm vi quyền SQL được cấp. Least privilege chỉ giảm blast radius, không tạo trusted server boundary. P7 HTTP service là hướng khắc phục kiến trúc; P6-T01 không được mô tả như đã loại bỏ rủi ro này.

