# Đóng gói và triển khai Local Mode v1

Status: P6-T04 publish-directory baseline, 2026-10-13. Đây chưa phải bằng chứng clean-install/upgrade VM; các gate đó còn Blocked cho tới khi P6-T05/P6-T07 thực thi trên máy không có SDK/Visual Studio.

## Release artifacts

Chạy từ repository sạch có .NET SDK `8.0.425` theo `global.json`:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File deployment\release\Publish-LocalRelease.ps1 `
  -Version 1.0.0 `
  -OutputRoot C:\ApprovedReleaseStaging\MyDmsVn-1.0.0
```

Script restore locked và publish tuần tự, không chạy hai TFM song song:

- source tree phải sạch; script từ chối build release từ thay đổi chưa commit;
- `OutputRoot` phải mới hoặc rỗng; script không xóa/ghi đè một thư mục có nội dung;
- manifest ghi cả `sourceCommit` và `sourceTreeDirty` để phân biệt fixture kiểm thử với release thật.

| Thư mục | Host | Kiến trúc | Deployment | Prerequisite |
|---|---|---|---|---|
| `desktop-net48-win-x86` | `MyDmsVn.Desktop.App.exe` | x86 | publish directory | .NET Framework 4.8 |
| `desktop-net8-win-x64` | `MyDmsVn.Desktop.AppCore.exe` | x64 | framework-dependent | .NET 8 Desktop Runtime x64 |
| `admin-migrator-net8-win-x64` | `MyDmsVn.Server.DbMigrator.exe` | x64 | framework-dependent, admin only | .NET 8 Runtime x64 |

Candidate Windows scope là Windows 10/11 x64; host `net48` chạy x86 qua WoW64. Chỉ các OS/runtime được ghi Pass trong release matrix P6-T08 mới được công bố là supported. Native/transitive DLL được giữ cạnh executable trong publish directory; không yêu cầu Visual Studio hay developer SDK trên máy đích.

`release-manifest.json` ghi product version, commit/trạng thái source tree, exact schema range `8..8` cho baseline P3.1, kiến trúc/runtime, deployment mode và SHA-256/length của từng file. SQL version validation vẫn là P6-T06; manifest để danh sách verified SQL version rỗng thay vì suy diễn support. Binary hiện unsigned; release owner phải chấp nhận rõ hoặc cung cấp signing policy/trust-chain gate trước release.

## Cài đặt và upgrade

Baseline dùng directory deployment, chưa dùng MSI/Onova/auto-update.

1. DBA backup/verify restore point và chạy migrator từ package admin bằng migration principal trong maintenance window.
2. Kiểm tra manifest hash và schema compatibility trước khi copy desktop package.
3. Machine install: Administrator copy package vào versioned folder dưới `%ProgramFiles%\MyDmsVn\<version>` và tạo shortcut. User install chỉ được dùng thư mục tổ chức phê duyệt mà user không thể thay binary cho người khác.
4. Giữ runtime configuration tại `%ProgramData%\MyDmsVn\runtime.connection`; không đặt secret trong publish directory.
5. Upgrade theo side-by-side folder, chuyển shortcut atomically sau smoke; rollback binary chỉ khi schema matrix cho phép. Không downgrade schema/journal thủ công.
6. Log vẫn ở `%LocalAppData%\MyDmsVn\Logs\<host>` và không bị overwrite bởi upgrade.

Uninstall xóa shortcut và đúng versioned application folder sau khi xác nhận không còn process. Mặc định giữ `%ProgramData%\MyDmsVn`, log theo retention và toàn bộ shared SQL database. Ordinary desktop install/update không chạy DbUp, không provision SQL principal và không chứa migration credential.

## Verification bắt buộc còn lại

- clean install từng host trên Windows VM không SDK/Visual Studio;
- verify `.NET Framework 4.8` và `.NET 8 Desktop Runtime x64` prerequisite/failure UX;
- upgrade từ release trước, preserve config/shared SQL, restart/repair/uninstall;
- limited-user execution và writable log folder;
- optional signature/trust chain theo owner policy;
- packaged P5 smoke riêng cho từng host.

Các mục trên phải ghi Pass/Fail/Blocked, OS build, architecture, runtime, SQL version, tester và timestamp; build/publish trên developer machine không thay thế VM evidence.
