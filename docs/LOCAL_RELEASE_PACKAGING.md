# Đóng gói và triển khai Local Mode v1

**Hiện trạng:** Nhánh `v1a-pro`, cập nhật 2026-10-21 tại baseline merge PR #41 `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. Package hiện chỉ có desktop và admin migrator `net48`/x86. SEC-REBUILD, RESP, L2DB, [GUI-P5](archive/20261023-pre-ui-next/plans/20261019-001-full-winforms-gui-p5-acceptance.md) và [BCI](archive/20261023-pre-ui-next/plans/20261020-001-business-compact-inline-goods-receipt.md) đã hoàn tất trong phạm vi được nghiệm thu, giữ nguyên ngoại lệ và provenance đã ghi. [BCF](archive/20261023-pre-ui-next/plans/20261021-001-business-compact-lookup-and-visual-parity.md) đã COMPLETE / OWNER ACCEPTED trong [phạm vi cuối](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21), giữ nguyên ngoại lệ và nguồn bằng chứng. BCF không đổi runtime/API/schema; [điều kiện phát hành](DEFERRED_WORK.md#release-prerequisites) vẫn **UNACCEPTED / UNSCHEDULED**. Tài liệu này mô tả cách đóng gói, không chứng nhận clean install, upgrade hay production release.

**RELEASE-MANIFEST-01:** Script đã sửa nguồn metadata: đọc `SchemaCompatibilityPolicy.CurrentScripts` từ DLL admin đã đóng gói qua helper PowerShell x86, thay cho pin `8..8`. Bốn test packaging x86 và đối chiếu artifact từ commit sạch `d8013030` đã đạt: schema9, danh sách001–009, 224 hash file. [L2DB evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed) đóng riêng lỗi metadata này. Chưa có release acceptance.

**L2DB-T06/T07:** Product net48 đã chuyển sang LINQ to DB/native SqlClient và gỡ dependency RepoDb/Dapper active; AppCore vẫn parked. T07 đã hoàn thành correctness/package, limited authenticated Local smoke và kiểm tra cleanup trên frozen revision (xem [evidence](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke)). **139 invocations Desktop GUI chưa chạy tại T07**; GUI-P5 đã có nghiệm thu riêng sau đó; clean VM, phục hồi dữ liệu và G1–G5 vẫn là điều kiện phát hành chưa được nghiệm thu, chưa xếp lịch. Không dùng artifact T02/T07 của revision cũ để suy ra release acceptance.

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

`MyDmsVn.Desktop.AppCore` / `net8.0-windows` nằm ngoài solution/release và được ghi **PARKED / NOT RUN / NOT SHIPPED**. SDK .NET 8 dùng để build, không phải product package. Modern product runtime thuộc [hướng ngoài phạm vi](DEFERRED_WORK.md#out-of-scope-directions), cần quyết định riêng trước khi lập kế hoạch mới.

Native/transitive DLL và cấu hình executable phải đi cùng publish directory. Máy đích cần .NET Framework 4.8, không cần SDK/Visual Studio theo mô hình triển khai dự kiến; clean-machine evidence vẫn phải chứng minh điều đó. Chỉ công bố OS/architecture/runtime được xác nhận trong ma trận phát hành được Owner duyệt, không suy diễn support từ build trên developer machine.

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

## Điều kiện phát hành chưa nghiệm thu

- đối chiếu metadata runtime/package thực tế; `RELEASE-MANIFEST-01` đã đóng riêng phần chênh lệch nguồn;
- clean install active desktop/admin package trên Windows VM không SDK/Visual Studio;
- verify prerequisite `.NET Framework 4.8`, x86/native dependencies và failure UX;
- review đường upgrade được hỗ trợ, preserve config/shared SQL, restart/repair/uninstall; không coi prefix cũ là upgrade path hiện có;
- limited-user execution và writable log folder;
- optional signature/trust chain theo owner policy;
- bằng chứng GUI trên đúng candidate phát hành: GUI-P5/BCI/BCF giữ nguyên phạm vi nghiệm thu lịch sử và candidate riêng; SAL cần bằng chứng mới theo [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md), và production release vẫn cần G1–G5 trên host net48 thực sự phát hành.

Các mục trên là điều kiện **UNACCEPTED / UNSCHEDULED** theo [release prerequisites](DEFERRED_WORK.md#release-prerequisites), không phải một kế hoạch thực thi thứ hai. Khi có phạm vi được duyệt, [TEST_POLICY](TEST_POLICY.md) yêu cầu ghi revision/artifact, Pass/Fail/Blocked/Not run, OS build, architecture, runtime, SQL version, tester và timestamp. Publish-directory output không thay thế VM, GUI, migration/recovery hay owner release evidence; việc chọn SAL làm lộ trình duy nhất không đóng hoặc miễn các yêu cầu này.

## Candidate SAL sau triển khai — chưa có package nghiệm thu

[SAL](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) là roadmap đang hoạt động từ PR #41 `35ba2e8a3f37fb07095701eb94f4b56f7941c491`; tài liệu hiện không tạo package hoặc đổi runtime/schema. Candidate tương lai vẫn gồm Desktop.App và admin DbMigrator net48/x86, cùng vendor pins và metadata lấy từ policy đã đóng gói.

Sau khi script010 và mọi writer/reconciliation/grant/reset được triển khai đồng bộ, manifest phải phản ánh schema10/001–010 từ DLL thực tế; không sửa tay metadata để che một package cũ. T08 phải ghi source/test/package hashes, kiểm chứng preflight old/new, upgrade exact9 và bootstrap/rollback, rồi chạy scoped real-Login receipt/sale workflow với kiểm tra độc lập Quantity/InventoryValue/CostAmount và cleanup đúng ownership. Không ship checkpoint có schema10 nhưng còn writer cũ.

[SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) hiện NOT RUN. BCF và RELEASE-MANIFEST-01 giữ nguyên evidence lịch sử; không dùng các package schema9 đó làm bằng chứng tính giá vốn hoặc hỗ trợ upgrade của SAL. Nghiệm thu feature không đồng nghĩa production release sign-off.
