# Security provisioning runbook

Status: P3.1 deployment workflow, 2026-10-14. Các lệnh trong tài liệu này chỉ chạy từ gói quản trị `MyDmsVn.Server.DbMigrator`; desktop startup và Login không chạy migration, permission sync hoặc bootstrap.

## 1. Điều kiện và danh tính

- Dừng toàn bộ client trong maintenance window, xác nhận backup/restore point và artifact hash.
- Dùng migration configuration và migration principal có `CONTROL` trên đúng application database. Không dùng runtime principal, `sa` trong desktop, hoặc đưa connection string lên command line.
- `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING[_FILE]` và `%ProgramData%\MyDmsVn\migration.connection` có precedence như tài liệu cấu hình SQL.
- Mọi output chỉ chứa server/database đã sanitize, `DatabaseIdentity`, release/plan ID, key và mã kết quả. Không chứa connection string, password/hash/salt hoặc raw exception chain.

## 2. Thứ tự fresh install

1. Preflight, backup/restore evidence, dừng client.
2. Chạy DbUp để áp dụng immutable scripts `001`–`008`; chạy lại migrator để xác nhận no-op.
3. Tạo approval JSON được release owner và security owner review theo từng key.
4. Chạy permission dry-run, lưu JSON output và duyệt đúng `planId`, target, changes/conflicts.
5. Chạy apply với chính approval artifact và `planId` đã duyệt. Mọi drift/expiry/mismatch yêu cầu dry-run và review mới; không có `--force`.
6. Chạy bootstrap dry-run. Chỉ khi status `Ready`, nhập `DatabaseIdentity` vào apply và nhập password qua prompt masked hai lần.
7. Kiểm tra marker, role ownership, approved grants, Login Application và direct-deny behavior trước khi mở client.

```powershell
dotnet MyDmsVn.Server.DbMigrator.dll

dotnet MyDmsVn.Server.DbMigrator.dll permissions sync --dry-run `
  --approvals C:\SecureDeployment\permission-approvals.json

dotnet MyDmsVn.Server.DbMigrator.dll permissions sync --apply `
  --approvals C:\SecureDeployment\permission-approvals.json `
  --plan-id <64-character-plan-id>

dotnet MyDmsVn.Server.DbMigrator.dll security bootstrap-admin --dry-run

dotnet MyDmsVn.Server.DbMigrator.dll security bootstrap-admin --apply `
  --confirm-database <database-identity-guid> `
  --username <initial-admin-username> `
  --display-name <display-name> `
  --actor <deployment-operator-id> `
  --release-id 2026.10.13-p3.1
```

`--non-interactive` có cùng tác dụng kiểm tra như bootstrap `--dry-run` và không tạo user. Bootstrap apply từ stdin redirected/noninteractive trả lỗi ngay; password không được truyền bằng argument, environment, file approval hoặc log. Marker `InitialAdministrator` làm mọi lần bootstrap sau thành report/no-op, kể cả user ban đầu đã bị disable hoặc xóa.

## 3. Approval artifact

Artifact là JSON admin-only, hết hạn, khớp chính xác release manifest và có đúng một entry cho mỗi key:

```json
{
  "releaseId": "2026.10.13-p3.1",
  "approvedBy": "release-operator-id",
  "expiresAtUtc": "2026-10-14T08:00:00Z",
  "permissions": [
    {
      "permissionKey": "Catalog.Products.Read",
      "catalogDecision": "Approved",
      "administratorGrantDecision": "Approved"
    },
    {
      "permissionKey": "Security.Users.Manage",
      "catalogDecision": "Approved",
      "administratorGrantDecision": "Pending"
    }
  ]
}
```

Ví dụ rút gọn trên phải được mở rộng đủ manifest thực tế. `CatalogDecision` và `AdministratorGrantDecision` là hai phê duyệt độc lập. `Eligible` chỉ cho phép đề xuất; vẫn cần decision `Approved`. `Sensitive` luôn review theo key. `NotEligible` không được auto-grant dù artifact ghi Approved. Approval hết hạn, thiếu/thừa key hoặc khác release bị từ chối.

Manifest hiện tại đặt toàn bộ `Catalog.*` và `Inventory.*` là `Eligible`; `Security.Users.Manage` và `Security.Roles.Manage` là `Sensitive`. Không key nào được grant chỉ vì policy này: release owner duyệt manifest/catalog, security owner duyệt Administrator decision theo từng key, và operator áp dụng đúng artifact/plan đã được hai vai trò phê duyệt. Trước approval, review user có role Administrator, mọi direct deny hiện hữu và blast radius của key; direct deny luôn giữ ưu tiên.

## 4. Upgrade và permission mới

Upgrade bỏ qua bootstrap. Sau migration, thực hiện dry-run/review/apply cho manifest mới. Client `001`–`007` thấy DB `008` phải dừng với `Schema.TooNew`; client mới gặp DB thiếu `008` hoặc registry chưa sync phải fail closed. Không hỗ trợ rolling/mixed-version hay downgrade schema bằng cách hạ client.

Checklist PR có permission mới:

- thêm constant và `PermissionDefinition` tại module owner, rồi đăng ký qua composition deterministic;
- chọn lifecycle và `AdministratorGrantPolicy` theo key, không chỉ theo module;
- dùng key trong server-side Application authorization; registry validation phải thấy mọi key được tham chiếu;
- cập nhật fixtures/snapshot/schema/package docs nếu cần;
- test undeclared/pending/unknown deny, activation độc lập với Administrator approval, direct deny vẫn thắng;
- test Administrator chỉ nhận grant đã duyệt; ordinary/manual roles không đổi;
- chuẩn bị approval artifact, dry-run diff và rollback/forward-fix note cho rollout.

Rename key không phải edit metadata thông thường; cần mapping/ADR và migration riêng. Sync không xóa definition, manual assignment hoặc direct-user allow/deny.

## 5. Revoke, restore và drift

Lấy target identity từ bootstrap dry-run, sau đó dùng workflow riêng:

```powershell
dotnet MyDmsVn.Server.DbMigrator.dll permissions administrator-grant --revoke `
  --confirm-database <database-identity-guid> `
  --permission-key <exact-key> --release-id <release-id> `
  --actor <security-operator-id> --reason "<reviewed change reference>"

dotnet MyDmsVn.Server.DbMigrator.dll permissions administrator-grant --restore `
  --confirm-database <database-identity-guid> `
  --permission-key <exact-key> --release-id <release-id> `
  --actor <security-operator-id> --reason "<reviewed restore reference>"
```

Revoke/restore cập nhật assignment và managed tombstone trong cùng serialized transaction và ghi audit. Restore chỉ thành công khi key active và có Administrator approval hiện hành, chưa hết hạn, đúng release. Routine sync không hồi sinh `Revoked`. Assignment bị xóa ngoài luồng được chuyển thành `Revoked`; assignment xuất hiện lại khi tombstone không Granted tạo conflict và phải điều tra.

## 6. Lost-admin recovery

Bootstrap không phải công cụ recovery và không có flag bỏ marker. Khi mất mọi application Administrator:

1. Dừng client, mở incident/change được security owner và data owner phê duyệt; lưu backup identifier và audit ngoài hệ thống.
2. Xác nhận marker, managed role/grants và nguyên nhân disable/delete. Không xóa/sửa marker hoặc gán first arbitrary user.
3. Ưu tiên restore backup đã test nếu RPO được chấp nhận. Nếu cần forward recovery, tạo change riêng có peer review để khôi phục đúng identity đã được owner xác nhận và membership vào managed `Administrator`; dùng BCrypt/password policy qua tool recovery được release riêng, không dùng SQL plaintext/hash tự chế. Baseline này không cung cấp bypass forward-recovery chung.
4. Ghi một audit maintenance đã redaction, chạy permission dry-run, Login smoke, least-privilege/direct-deny checks rồi mới mở client.

Nếu chưa có tool recovery được duyệt và backup restore không được owner chấp nhận, dừng rollout và escalation; không biến bootstrap hoặc Login thành backdoor.

## 7. Kết quả và recoverability

Exit code chung: `0` thành công/no-op, `1` lỗi không phân loại an toàn, `2` argument/configuration, `3` conflict/stale/policy rejection, `4` thiếu privilege (migration thuần còn dùng các exit code chi tiết trong schema runbook). DbUp là transaction-per-script và chỉ forward-fix/restore; permission apply, grant change và bootstrap mỗi operation là transaction riêng, rollback toàn operation khi lỗi và audit attempt thất bại tách biệt.
