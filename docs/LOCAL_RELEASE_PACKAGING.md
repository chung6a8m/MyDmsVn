# Đóng gói và triển khai Local Mode v1

**Hiện trạng:** Nhánh `v1a`, cập nhật 2026-10-18. Script đóng gói hiện có chỉ tạo desktop và admin migrator `net48`/x86. SEC-REBUILD đã được chấp nhận trong PR #34; P5 interactive và P6 operational/release vẫn **OPEN / DEFERRED** trong [DEFERRED_WORK](DEFERRED_WORK.md). Lịch sử bằng chứng nằm ở [HISTORY](HISTORY.md); tài liệu này không chứng nhận clean install, upgrade hay production release.

**RELEASE-MANIFEST-01:** Script đã sửa nguồn metadata: đọc `SchemaCompatibilityPolicy.CurrentScripts` từ DLL admin đã đóng gói qua helper PowerShell x86, thay cho pin `8..8`. Test packaging x86 đã đạt; đối chiếu artifact từ commit sạch còn chờ trong [L2DB evidence](L2DB_VERIFICATION.md#l2db-t02-startup-follow-up--runtime-identity-and-schema-manifest). Chưa có release acceptance.

## Release artifacts

[global.json](../global.json) khai báo SDK nền `8.0.400`, `rollForward: latestFeature`, `allowPrerelease: false`. Máy build cần SDK được resolver chọn theo cấu hình đó, targeting pack .NET Framework 4.8 và dependencies/submodules của solution. Ghi lại SDK thực tế được dùng.

Ví dụ tạo candidate từ repository sạch trên Windows; thay version và thư mục staging bằng giá trị đã duyệt:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File deployment\release\Publish-LocalRelease.ps1 `
  -Version '1.0.0' `
  -OutputRoot 'C:\ApprovedReleaseStaging\MyDmsVn-1.0.0'
```

[Script hiện tại](../deployment/release/Publish-LocalRelease.ps1) locked-restore solution rồi publish tuần tự hai project trên cùng TFM `net48`:

- source tree phải sạch; script từ chối build release từ thay đổi chưa commit;
- `OutputRoot` phải mới hoặc rỗng; script không xóa/ghi đè một thư mục có nội dung;
- manifest ghi `sourceCommit`, `sourceTreeDirty`, version và thời điểm sinh;
- desktop package được kiểm tra để từ chối migration/admin artifacts và dấu hiệu credential/connection string trong các file văn bản được script kiểm tra.

| Thư mục | Host | Kiến trúc | Deployment | Prerequisite |
|---|---|---|---|---|
| `desktop-net48-win-x86` | `MyDmsVn.Desktop.App.exe` | x86 | publish directory | .NET Framework 4.8 |
| `admin-migrator-net48-win-x86` | `MyDmsVn.Server.DbMigrator.exe` | x86 | publish directory, admin only | .NET Framework 4.8 |

`MyDmsVn.Desktop.AppCore` / `net8.0-windows` nằm ngoài solution/release và được ghi **PARKED / NOT RUN / NOT SHIPPED**. SDK .NET 8 dùng để build, không phải product package. Việc khôi phục host hiện đại đi qua [REACTIVATE_NET8](REACTIVATE_NET8.md).

Native/transitive DLL và cấu hình executable phải đi cùng publish directory. Máy đích cần .NET Framework 4.8, không cần SDK/Visual Studio theo mô hình triển khai dự kiến; clean-machine evidence vẫn phải chứng minh điều đó. Chỉ công bố OS/architecture/runtime được xác nhận trong release matrix P6, không suy diễn support từ build trên developer machine.

`release-manifest.json` ghi kiến trúc/runtime, deployment mode và SHA-256/length của từng file. Phần `schemaSupport` lấy `minimum`, `maximum` và `requiredScripts` theo runtime policy trong DLL admin của package; hiện là schema 9 với scripts `001`–`009`. Helper x86 đọc được DLL net48/x86 cả khi script chạy từ PowerShell 64-bit, có thời hạn và từ chối policy thiếu/rỗng. Đối chiếu [schema contract](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) và artifact sinh ra; không sửa manifest bằng tay.

Danh sách SQL versions đã xác minh vẫn rỗng; SQL support/recovery validation còn mở. Script hiện không thực hiện signing và manifest ghi trạng thái unsigned cần owner acceptance; quyết định trust-chain/signing thuộc release gate. `-SkipPublish` chỉ xử lý các thư mục artifact đã có và tái tạo manifest cho kiểm tra, có thể ghi `sourceTreeDirty: true`; nó không chứng minh đã publish từ revision sạch hoặc hoàn thành acceptance.

## Cài đặt và upgrade

Baseline dùng directory deployment, chưa dùng MSI/Onova/auto-update.

1. Xử lý release blocker và xác minh package/commit/hash, runtime schema contract cùng các prerequisite đã duyệt.
2. DBA chuẩn bị backup/recovery point, dừng/drain client và áp dụng [migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) trong maintenance window. Migrator hiện chỉ nhận database empty hoặc đúng current baseline; prefix cũ không có đường auto-upgrade được hỗ trợ.
3. Machine install: Administrator copy package vào versioned folder dưới `%ProgramFiles%\MyDmsVn\<version>` và tạo shortcut. User install chỉ được dùng thư mục tổ chức phê duyệt mà user không thể thay binary cho người khác.
4. Giữ runtime configuration tại `%ProgramData%\MyDmsVn\runtime.connection`; không đặt secret trong publish directory.
5. Upgrade theo side-by-side folder, chuyển shortcut atomically sau smoke; rollback binary chỉ khi schema matrix cho phép. Không downgrade schema/journal thủ công.
6. Log active host ở `%LocalAppData%\MyDmsVn\Logs\MyDmsVn.Desktop.App` và không bị overwrite bởi upgrade. Fresh install cần bootstrap có chủ đích theo [security administration](SECURITY_ADMINISTRATION_RUNBOOK.md); restart giữ nguyên binding/configuration đã được duyệt.

Uninstall xóa shortcut và đúng versioned application folder sau khi xác nhận không còn process. Mặc định giữ `%ProgramData%\MyDmsVn`, log theo retention và toàn bộ shared SQL database. Ordinary desktop install/update không chạy DbUp, không provision SQL principal và không chứa migration credential.

Từ thư mục `admin-migrator-net48-win-x86`, lệnh migration active là `& .\MyDmsVn.Server.DbMigrator.exe`, dùng migration configuration được bảo vệ. Không chạy DLL bằng `dotnet` như ví dụ của package net8 đã parked.

## Verification bắt buộc còn lại

- giải quyết `RELEASE-MANIFEST-01` và kiểm tra package/schema metadata thực tế;
- clean install active desktop/admin package trên Windows VM không SDK/Visual Studio;
- verify prerequisite `.NET Framework 4.8`, x86/native dependencies và failure UX;
- review đường upgrade được hỗ trợ, preserve config/shared SQL, restart/repair/uninstall; không coi prefix cũ là upgrade path hiện có;
- limited-user execution và writable log folder;
- optional signature/trust chain theo owner policy;
- packaged P5 smoke và các release scenarios còn mở trên host net48 thực sự được phát hành.

Các mục trên theo [TEST_POLICY](TEST_POLICY.md) và [DEFERRED_WORK](DEFERRED_WORK.md), ghi revision/artifact, Pass/Fail/Blocked/Not run, OS build, architecture, runtime, SQL version, tester và timestamp. Publish-directory output không thay thế VM, GUI, migration/recovery hay owner release evidence. Việc chọn RESP và L2DB làm hai kế hoạch active không đóng các yêu cầu này.
