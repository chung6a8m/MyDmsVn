# Bằng chứng candidate khắc phục các ngoại lệ Business Compact — 2026-10-28

**Phạm vi được chọn:** Owner chọn [kế hoạch khắc phục ba ngoại lệ](../../plans/20261028-001-business-compact-exceptions.md) và [thiết kế](../../design/BUSINESS_COMPACT_EXCEPTION_CLOSURE.md) tương ứng: action disabled trong Dark, khung hộp thoại Security và văn bản nhiều dòng chỉ đọc. Các thay đổi source đã được triển khai trong các tree source/test của candidate ghi dưới đây. **Focused Windows/native tests PASS; review Light/Dark × normal/minimum ở physical 100%/125% PASS; Owner APPROVED ngày 2026-10-28. Cả ba ngoại lệ CLOSED trong phạm vi này.**

Báo cáo này chỉ ghi candidate mới. [Evidence UIL gốc](20261027-business-compact-and-independent-ui-labs.md), các quyết định có ngày và phạm vi giới hạn của nó, cùng xác nhận Owner tập trung đang chờ riêng cho sửa focus `3b5f95e2d7d1414f0ffe97f47a3bdef58f33d786` được giữ nguyên. Việc chọn task này hoặc kết quả compiler của nó không phê duyệt sửa đổi trước đó và không mở lại ma trận UIL lịch sử.

## Checkpoint Windows/native EXC-T05

### Candidate và môi trường

| Mục | Giá trị Windows cuối |
|---|---|
| App code/test revision | `f492a7f87ac3cff5d6eac91bf9430899dc48e925`; source tree `a5083a939408501cdf86e40fc6548e2b4323e5a4`, samples tree `53f7fe35de808c5c6d732791a3da753e31ae2bea`, tests tree `bbb6f3d5af953d6ee9db3a27ef4889fa3b32f55c` |
| Toolkit final evidence/approval pin | `a5dd3cec6daba71943549afac628e13727c00da5`; approved presented candidate `e06ba1e31f63e60d4a88fd65122f044e27dc7e56`; code/test execution revision `a71d64bd82eb40123eafef4c5a6832e53d7ef9ca`; source tree `d2edfe81dd4d4949889fa5cae69bc6129a8a8837`, tests tree `7dfba0532f79b3052a3039b94b64446b91f797ec`. Commit cuối chỉ sửa tài liệu review; source/test trees không đổi |
| Nested SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944`; không đổi |
| Host | Windows 11 Home Single Language 10.0.26300 x64; interactive desktop 1920×1080, work area 1920×1032 |
| Toolchain | SDK `8.0.427`; MSBuild `17.11.69`; .NET Framework 4.8; .NET Desktop Runtime 8.0.32/8.0.33 |
| Build | Toolkit `Debug` all-scope PASS cả hai TFM, 0 warning/error, 12,61 giây. App `MyDmsVn.UiLab.sln` locked restore/build `Debug`, `net48`, x86 PASS, 0 warning/error |
| Executable review | `samples/MyDmsVn.Desktop.UiLab.Security/bin/Debug/net48/MyDmsVn.Desktop.UiLab.Security.exe` từ worktree candidate; SQL-free Security Lab |
| Physical 100% | Registry monitor setting tương ứng `DpiValue=-1`, `AppliedDPI=96`; Lab `DeviceDpi=96`; không dùng expected-scale hoặc resize ảnh làm DPI evidence |
| Physical 125% | **PASS** — Windows Display scale 125%; registry monitor `DpiValue=0`; thread `PER_MONITOR_AWARE_V2` gọi `GetDpiForMonitor` trả 120×120, `HRESULT=0`; logical work area 1536×816 trên desktop 1920×1080. Lab net48 báo `DeviceDpi=96`, được ghi riêng và không dùng làm physical-scale proof |

### Focused tests và lỗi đã xử lý

Discovery được chạy trên đúng project/TFM/filter trước execution. Raw TRX nằm dưới `.superpowers/sdd/20261028-001-business-compact-exceptions/results/`, bị ignore và không commit. Mọi GUI run tuần tự; lượt full consumer cuối xác nhận không còn `testhost` hoặc cửa sổ do test sở hữu.

Các lệnh consumer Windows thực tế dưới đây được chạy từ root app bằng PowerShell. VSTest launcher `17.11.1` báo process x64; `PlatformTarget=x86` và `RunConfiguration.TargetPlatform=x86` đặt assembly/test target x86. `--blame-hang-timeout 5m` là timeout chẩn đoán. Discovery và full rerun cuối dùng cùng exact filter; TRX cuối ánh xạ trực tiếp tới hàng 163/163 bên dưới. Exact commands/filters cho bốn lượt toolkit 75/75 và 40/40 được ghi tại [toolkit evidence](../../../vendor/MyDmsVn.WinFormsKit/docs/verification/20261028-business-compact-exceptions.md#checkpoint-windowsnative-exc-t05).

```powershell
$consumerFilter = 'FullyQualifiedName~SecurityToolkitExceptionTests|FullyQualifiedName~SecurityWorkspaceControlTests|FullyQualifiedName~SecurityReviewScenarioTests|FullyQualifiedName~UiLabModalOwnershipTests|FullyQualifiedName~SecurityProductHostParityTests'
$resultsRoot = Join-Path (Get-Location) '.superpowers\sdd\20261028-001-business-compact-exceptions\results'

dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Debug -f net48 --no-restore --no-build --list-tests --filter $consumerFilter --blame-hang --blame-hang-timeout 5m -p:PlatformTarget=x86 -- RunConfiguration.TargetPlatform=x86
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Debug -f net48 --no-restore --no-build -p:PlatformTarget=x86 --filter $consumerFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-net48-x86.trx' --results-directory $resultsRoot\consumer-net48-x86-rerun -- RunConfiguration.TargetPlatform=x86
```

| Owner / target | Discovered | Executed | Pass / fail / skip | Thời gian TRX |
|---|---:|---:|---:|---:|
| Toolkit controls / `net48` | 75 | 75 | **75 / 0 / 0** | 2,89 s |
| Toolkit controls / `net8.0-windows` | 75 | 75 | **75 / 0 / 0** | 1,79 s |
| Toolkit SourceGrid adapters / `net48` | 40 | 40 | **40 / 0 / 0** | 5,14 s |
| Toolkit SourceGrid adapters / `net8.0-windows` | 40 | 40 | **40 / 0 / 0** | 6,74 s |
| Consumer `MyDmsVn.Desktop.Ui.Tests` / `net48` x86 | 163 | 163 | **163 / 0 / 0** | 8 phút 13,09 s |

Controls selection gồm Button, modal theme/DPI/accessibility, TextBox/multiline/accessory/validation, Select accessibility, formatted input và `Phase16PublicApiBaselineTests`. Adapter dùng project/filter riêng cho `BootstrapTextBoxEditorTests` và các adapter liên quan. Consumer selection gồm `SecurityToolkitExceptionTests`, `SecurityWorkspaceControlTests`, `SecurityReviewScenarioTests`, `UiLabModalOwnershipTests`, `SecurityProductHostParityTests` cùng các regression close/recovery/session/secret thuộc owner đó.

Hai lỗi fixture/assertion được giữ và phân loại trước GREEN cuối:

- Toolkit `net48` đầu đạt 74/75. Fixture fallback-font tạo Font từ family đã resolve; trên Windows `Font.Equals` còn xét `OriginalFontName`, nên setup không thể vào nhánh equal-value. Commit `a71d64b` tái tạo cùng unavailable-family request, vẫn kiểm tra fallback name khác request; focused 2/2 rồi full 75/75 PASS. Production toolkit không đổi.
- Consumer đầu đạt 158/163. Năm lỗi do assertion coi `BootstrapTextBox.Focus()` phải trả `true` dù composite chuyển focus vào native EDIT, và đếm hidden clear button nội bộ như catalog command. Commit `f492a7f` kiểm `ContainsFocus` + native editor `Focused`, chỉ đếm action `Visible`; focused 5/5 rồi full 163/163 PASS. Production app không đổi.

Một thao tác CUA gửi Enter/Escape vào main HWND đang disabled thay vì owned-modal HWND, làm hiện decision dialog nhưng không ghi `Security.Mutate`; native driver trong process gửi key vào `GetFocus()` và regression Enter/secret cleanup PASS. Đây là giới hạn công cụ quan sát, không được ghi thành lỗi production hoặc bằng chứng keyboard. Keyboard/close semantics lấy từ native tests; screenshot chỉ dùng cho appearance.

### Review physical 100%

Kích thước normal dùng content 952×657 trong host 1200×760; minimum dùng content 652×497 trong host 900×600. Cả hai được kiểm ở Light và Dark trên cùng executable/DeviceDpi 96.

| Ngoại lệ | Light normal/minimum | Dark normal/minimum | Kết quả thao tác native ở physical 100% |
|---|---|---|---|
| Action disabled | PASS | PASS | Tài khoản bảo vệ hiển thị Edit/Reset/State neutral-disabled, phân biệt Create Secondary outline enabled; UIA báo disabled; click không mở dialog/dispatch |
| Toolkit modal | PASS | PASS | Owner/backdrop, initial focus, Tab/Shift+Tab, Enter/Escape, X, pending-close/recovery/session/secret cleanup và repeated disposal PASS trong focused native tests; title đo/vẽ và đổi theme lặp không dùng Font disposed; footer/validation còn truy cập được ở minimum |
| Multiline read-only | PASS | PASS | Explanation và Catalog details chọn/copy/cuộn/wrap được; nhập ký tự không đổi nội dung; accessible name đúng; theme switch giữ nội dung và tests giữ focus/selection/caret; không có clipping sai ngoài truncation/scroll có chủ ý |

Ảnh đã rà soát, chỉ chứa dữ liệu giả của Lab:

- [physical 100% Dark / normal / Catalog long read-only](screenshots/20261028-exc-t05/physical100-dark-normal-catalog-long-readonly.png)
- [physical 100% Light / minimum / Catalog](screenshots/20261028-exc-t05/physical100-light-minimum-catalog.png)

### Review physical 125%

Display scale thực sự là 125% được xác nhận trước và sau review bằng process độc lập đặt thread `PER_MONITOR_AWARE_V2`: `GetDpiForMonitor=120×120`, `HRESULT=0`; logical work area là 1536×816 trên desktop 1920×1080. UI Lab net48 vẫn báo `DeviceDpi=96`; đây là giá trị host được ghi riêng, không phủ định probe physical scale và không bị thay bằng DPI giả lập.

| Ngoại lệ | Light normal/minimum | Dark normal/minimum | Kết quả thao tác native ở physical 125% |
|---|---|---|---|
| Action disabled | PASS | PASS | Protected account giữ Edit/Reset/State neutral-disabled, khác Create Secondary outline enabled; UIA tiếp tục báo disabled ở Light/Dark normal/minimum; focused native tests sở hữu inertness/no-dispatch semantics |
| Toolkit modal | PASS | PASS | Password modal được quan sát ở Light normal/minimum và Dark normal/minimum; title, initial focus, backdrop, footer và validation surface vẫn trong work area. Native tests sở hữu theme-switch/keyboard/pending-close/secret-cleanup semantics |
| Multiline read-only | PASS | PASS | Catalog long detail wrap và scroll đúng ở normal/minimum; key dài, mô tả tiếng Việt và nút copy còn truy cập được; không có clipping sai ngoài truncation/scroll có chủ ý |

Ảnh 125% đã rà soát, chỉ chứa dữ liệu giả của Lab:

- [physical 125% Dark / normal / protected disabled actions](screenshots/20261028-exc-t05/physical125-dark-normal-protected-disabled.png)
- [physical 125% Light / minimum / Catalog long read-only](screenshots/20261028-exc-t05/physical125-light-minimum-catalog.png)
- [physical 125% Dark / minimum / password modal](screenshots/20261028-exc-t05/physical125-dark-minimum-password-modal.png)

## Quyết định Owner

Ngày 2026-10-28, sau khi nhận bộ kết quả cụ thể cho app evidence candidate `8efe9bff806aefa49e6ab6ee90efeb34d06fafc0` và toolkit evidence candidate `e06ba1e31f63e60d4a88fd65122f044e27dc7e56`, Owner trả lời rõ **“APPROVED”** cho ba ngoại lệ EXC-T01–T03. Quyết định đóng action disabled, khung modal Security và multiline read-only trên đúng candidate đã trình. Commit toolkit `a5dd3cec6daba71943549afac628e13727c00da5` và commit app chứa đoạn này chỉ ghi quyết định/cập nhật review/routing, không đổi immutable source/test trees hoặc mở rộng approval sang SQL, Login thực tế, Designer, package, deployment hay release.

Theo yêu cầu tách riêng trong prompt và evidence lịch sử, câu trả lời này không tự phê duyệt source focus correction `3b5f95e2d7d1414f0ffe97f47a3bdef58f33d786`; focused Owner confirmation đó vẫn PENDING.

## Định danh candidate và môi trường

| Mục | Giá trị đã ghi |
|---|---|
| Baseline source app | `5386a6d8efe23099c436eb7ef62141df8d3896b8` |
| Candidate app | Code/test revision Windows `f492a7f87ac3cff5d6eac91bf9430899dc48e925`; commit chứa report này chỉ bổ sung pin/evidence/screenshots |
| Tree `src` của app | `a5083a939408501cdf86e40fc6548e2b4323e5a4` |
| Tree `samples` của app | `53f7fe35de808c5c6d732791a3da753e31ae2bea` |
| Tree `tests` của app | `bbb6f3d5af953d6ee9db3a27ef4889fa3b32f55c` |
| Gitlink toolkit trước sửa | `c232a4c09dc0386a5bd8bb484a5c991acae827d5` |
| Baseline toolkit upstream | `efe2e7212fd36e32c91b914bdc5e4081947124fc`; cùng tree với pin consumer trước sửa |
| Candidate/pin toolkit cuối | `a5dd3cec6daba71943549afac628e13727c00da5` theo [PR #2](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/pull/2); Owner approved presented pin `e06ba1e31f63e60d4a88fd65122f044e27dc7e56`; code/test revision native `a71d64bd82eb40123eafef4c5a6832e53d7ef9ca`; consumer checkout sạch. Các commit sau execution chỉ cập nhật tài liệu/evidence/review; source/test trees chính xác không đổi và native results tiếp tục áp dụng |
| SourceGrid lồng bên trong | `f4e457b43582bf01892f50bdc74aa480531e5944`; không đổi |
| Host/toolchain | Checkpoint native: Windows 11 10.0.26300 x64, SDK `8.0.427`, MSBuild `17.11.69`, .NET Framework 4.8 và Desktop Runtime 8.0.32/8.0.33. Linux cross-build lịch sử được giữ riêng bên dưới |
| Target sản phẩm/test | `net48`, Debug, x86 cho app/consumer; toolkit `net48` và `net8.0-windows` |
| Điều kiện cache | Cache không được xóa; build cuối dùng restored assets và warm cache. Không có clean-restore claim |
| Screenshot/quan sát native mới | Physical 100%/DeviceDpi 96 PASS; physical 125% với probe 120×120 và Lab `DeviceDpi=96` PASS; năm ảnh selected ở `screenshots/20261028-exc-t05/` |

Candidate app được định danh bằng commit chứa report này, ba tree source/samples/tests ở trên và pin toolkit chính xác. Kết quả build cuối bên dưới dùng đúng pin và các tree đó. Các lần restore/compile trước đó được giữ riêng để phân biệt lỗi đã sửa, build trên bản mirror source và build cuối theo gitlink.

## Source đã triển khai trong phạm vi

| Ngoại lệ | Thay đổi source của candidate | Trạng thái hiện tại |
|---|---|---|
| Action disabled trong Dark | Palette disabled của toolkit dùng secondary surface và màu chữ bình thường, giữ hành vi/border disabled; fixture UI tập trung có assertion và kiểm thử vẽ bằng palette Business Compact Light/Dark thực tế | **CLOSED** — tests và physical-100/125 appearance/inertness PASS; Owner APPROVED 2026-10-28 |
| Khung hộp thoại Security native | Các hộp thoại tài khoản/password/ghi dữ liệu, quyết định và review import không dùng SQL hiện có được bố trí qua `BootstrapModal.BodyPanel`/`FooterPanel` public; bộ duyệt theme của app và công cụ Lab giải phóng mutation đang giữ đi qua các vùng public đó; vòng đời Font tiêu đề của toolkit được sửa | **CLOSED** — focused lifecycle/keyboard/cleanup tests và physical-100/125 appearance PASS; Owner APPROVED 2026-10-28 |
| Văn bản nhiều dòng chỉ đọc native | `SecurityUserDetailView.Explanation` và `SecurityPermissionCatalogView.Details` dùng hợp đồng multiline/wrap/scrollbar bổ sung của `BootstrapTextBox`, giữ văn bản, chiều cao và accessible name hiện có | **CLOSED** — controls/adapter/consumer tests và physical-100/125 native selection/copy/scroll/accessibility PASS; Owner APPROVED 2026-10-28 |

Mã nguồn app liên quan: [BusinessCompactAppearance](../../../src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs), [SecurityWorkspaceEditorBindings](../../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceEditorBindings.cs), [SecurityDecisionDialog](../../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityDecisionDialog.cs), [SecurityImportReviewDialog](../../../samples/MyDmsVn.Desktop.UiLab/SecurityImportReviewDialog.cs), [SecurityLabForm](../../../samples/MyDmsVn.Desktop.UiLab/Security/SecurityLabForm.cs), [SecurityUserDetailView](../../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityUserDetailView.cs) và [SecurityPermissionCatalogView](../../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityPermissionCatalogView.cs).

Fixture mới trong source là [SecurityToolkitExceptionTests](../../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityToolkitExceptionTests.cs). Các kiểm thử modal/workspace/review/parity hiện có được giữ và điều chỉnh tại đúng owner. Việc chúng tồn tại và compile thành công không chứng minh native RED/GREEN hoặc Windows discovery không rỗng.

## Bằng chứng restore và compiler

| Thao tác | Kết quả thực tế đã ghi | Giới hạn |
|---|---|---|
| Locked restore/cross-build Debug toàn bộ solution toolkit, chạy lại sau cập nhật API baseline | **PASS — cả hai TFM và các toolkit test owner compile, 0 cảnh báo, 0 lỗi; 12.77 giây**, SDK `8.0.427` | Chỉ là bằng chứng compiler/reference; compiled API delta đã review; build Windows theo commit cuối được ghi bên dưới |
| Locked restore app ban đầu trên Linux | **FAILED: NU1004**, graph reference-assembly/RID không khớp | Giữ kết quả lần thất bại; đây không phải lỗi native runtime và không bị thay âm thầm bằng một kết quả thành công |
| Locked restore app với overlay Linux tạm thời | **PASS**, không đổi project/lockfile được quản lý trong Git | Kết quả chuẩn bị; chưa phải app compilation hoặc native execution |
| Lần compile app đầu tiên sau restore | **FAILED: 2 lỗi CS0103** trong fixture mới do tham chiếu helper từ `MyDmsVn.Desktop.Tests` nhưng thiếu namespace import; đã bổ sung import | Lượt này không báo lỗi compiler của production code; kết quả compile thất bại được giữ trong report |
| Cross-build app `MyDmsVn.UiLab.sln` Debug net48/x86 sau khi sửa import của test | **PASS — 0 cảnh báo, 0 lỗi; 10.64 giây** | Bản mirror cục bộ của đúng production source upstream; đây là lượt trước khi pin gitlink cuối |
| Locked restore app theo pin implementation | **PASS**, dùng cùng lệnh overlay đã ghi | Consumer checkout sạch tại `1b9c345aa2afd85e38a72d575485c3abdf559451`; final evidence/approval/review pin `a5dd3cec6daba71943549afac628e13727c00da5` không đổi source/test/project/lockfile; không claim thêm một lượt restore cho commit Markdown-only |
| Build app Linux lịch sử `MyDmsVn.UiLab.sln` Debug net48/x86, gồm Theory palette thực tế | **PASS — 0 cảnh báo, 0 lỗi; 11.65 giây** | Provenance chính xác: app `9a620e10fd7d8d3246c1f180d28a62e26a84eeec`, source tree `a5083a939408501cdf86e40fc6548e2b4323e5a4`, samples tree `53f7fe35de808c5c6d732791a3da753e31ae2bea`, tests tree `6ceb7954df45971ec0bd10209c1572304d0757ea`, toolkit pin `1b9c345aa2afd85e38a72d575485c3abdf559451`. Không gán thời gian này cho tests tree hiện tại `bbb6f3d5af953d6ee9db3a27ef4889fa3b32f55c` |
| Build/native candidate Windows hiện tại | **PASS — app code/test `f492a7f87ac3cff5d6eac91bf9430899dc48e925`, tests tree `bbb6f3d5af953d6ee9db3a27ef4889fa3b32f55c`; toolkit code/test `a71d64bd82eb40123eafef4c5a6832e53d7ef9ca`** | App `MyDmsVn.UiLab.sln` Debug net48/x86 và toolkit all-scope hai TFM đều 0 warning/error; Windows focused totals/TRX là bằng chứng riêng ở checkpoint, không kế thừa thời gian 11.65 giây lịch sử |
| Toolkit CI Release trên `windows-2022`, cả hai TFM | **PASS — build-only** tại [run 40710094747](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40710094747) | `head_sha` đúng `1b9c345aa2afd85e38a72d575485c3abdf559451`; không chạy native tests |
| Toolkit CI exact approved evidence pin | **PASS — build-only** tại [run 40727842682](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40727842682) | `head_sha` đúng `e06ba1e31f63e60d4a88fd65122f044e27dc7e56`; final `a5dd3ce` chỉ ghi approval/routing/review, không đổi source/test trees; focused native tests vẫn là evidence local Windows ở checkpoint |
| Toolkit CI intermediate review pin | **PASS — build-only** tại [run 40785846939](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40785846939) | `head_sha` đúng `4eb0172a1986f8918eef096efb968478df31e084`; workflow Windows 2022 hoàn tất trước correction đường dẫn TRX, không thay native evidence local |
| Toolkit CI exact final review pin | **PASS — build-only** tại [run 40786442922](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40786442922) | `head_sha` đúng `a5dd3cec6daba71943549afac628e13727c00da5`; workflow Windows 2022 build cả hai TFM và xác nhận committed dependency locks không đổi |
| Windows discovery/execution | **PASS** | Toolkit controls 75/75/TFM, adapters 40/40/TFM; consumer 163/163 net48/x86; 0 fail/skip; TRX local ignored |
| Owner review native tập trung | **APPROVED — 2026-10-28** | Owner trả lời rõ “APPROVED” sau khi nhận bộ kết quả cho exact app/toolkit candidate; ba ngoại lệ CLOSED trong phạm vi này |

Các lệnh toolkit được người thực hiện ghi lại, chạy từ checkout toolkit upstream:

```sh
/tmp/mydmsvn-dotnet/dotnet restore MyDmsVn.WinFormsKit.sln --locked-mode -p:EnableWindowsTargeting=true
/tmp/mydmsvn-dotnet/dotnet build MyDmsVn.WinFormsKit.sln -c Debug --no-restore -p:EnableWindowsTargeting=true -v:minimal
```

Người thực hiện đã review compiled API metadata trên cả hai target toolkit: baseline `c232a4c` tái tạo fingerprint `5016acec10c7efdafe03387de2c8f182a2e96c49f473f1bcb8bc6e5932dfa6cb`; fingerprint của candidate là `e6735faae42a8eb41c06cdd5d4fd27a87e56dccaee73f579cd4460ef0693db6e`, gồm 182 exported types và 1,748 dòng surface. Delta chỉ gồm `BootstrapTextBox.Multiline`, `WordWrap` và `ScrollBars`. Tài liệu baseline và hằng số test được duyệt đã được cập nhật sau review. `Phase16PublicApiBaselineTests` PASS trong selection controls Windows trên cả hai TFM.

Lệnh locked restore app ban đầu trên Linux, thất bại với NU1004:

```sh
dotnet restore MyDmsVn.UiLab.sln --locked-mode -p:EnableWindowsTargeting=true
```

Lệnh restore app thành công, chạy từ checkout app:

```sh
/tmp/mydmsvn-dotnet/dotnet restore MyDmsVn.UiLab.sln --locked-mode -p:EnableWindowsTargeting=true -p:AutomaticallyUseReferenceAssemblyPackages=false -p:TargetFrameworkRootPath=/root/.nuget/packages/microsoft.netframework.referenceassemblies.net48/1.0.3/build/ -p:CustomAfterMicrosoftCommonTargets=/tmp/mydmsvn-crossbuild.targets
```

Overlay tạm thời đặt `RuntimeIdentifier=win7-x86` chỉ cho project `net48` có `OutputType` là `Exe`/`WinExe` và identifier đang rỗng. Lệnh chỉ rõ bộ reference assemblies có sẵn và ngăn package reference-assembly ngầm định bổ sung làm thay đổi locked graph. Đây là điều chỉnh chuẩn bị compiler trên Linux, không thay project/runtime/lockfile được quản lý trong Git. Build app thành công dùng cùng tham số reference-assembly/overlay sau khi sửa namespace import của test. Kết quả trên bản mirror source cục bộ được ghi ở trên. Cache không được xóa; build cuối dùng restored assets và warm cache, không có clean-restore claim. Sau đó consumer checkout sạch tại pin toolkit cuối và cùng lệnh locked restore đã PASS. Lệnh Windows native/build cuối theo pin chính xác được ghi trong checkpoint ở đầu report.

```sh
/tmp/mydmsvn-dotnet/dotnet build MyDmsVn.UiLab.sln -c Debug --no-restore -p:EnableWindowsTargeting=true -p:AutomaticallyUseReferenceAssemblyPackages=false -p:TargetFrameworkRootPath=/root/.nuget/packages/microsoft.netframework.referenceassemblies.net48/1.0.3/build/ -p:CustomAfterMicrosoftCommonTargets=/tmp/mydmsvn-crossbuild.targets -p:PlatformTarget=x86 -v:minimal
```

Sandbox ban đầu chặn SDK/build truy cập thông tin process qua `/proc` (`System.ComponentModel.Win32Exception`). Người thực hiện chạy lại các lệnh toolkit ngoài sandbox thành công. Lượt kiểm tài liệu tái hiện giới hạn môi trường này bằng `/tmp/mydmsvn-dotnet/dotnet --version` (exit 134); kết quả đó không chứng minh lỗi source và không phủ nhận lượt cross-build thành công được ghi riêng.

Toolkit `.github/workflows/build.yml` định nghĩa workflow chỉ build trên `windows-2022`, không chạy native tests. [Run 40710094747](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40710094747) có `status=completed`, `conclusion=success`, `head_sha=1b9c345aa2afd85e38a72d575485c3abdf559451`; bắt đầu `2026-10-28T14:51:32Z`, cập nhật `2026-10-28T14:53:23Z`. Đây là build Release trên Windows cho implementation handoff trước native fixture/evidence, không phải native test execution. App không có `.github/workflows`.

## Hoàn tất Windows/native

Theo [TEST_POLICY](../../TEST_POLICY.md), [tests/README](../../../tests/README.md) và [UI Lab runbook](../../development/UI_LAB.md), discovery/execution focused đã hoàn tất tuần tự với STA/finite pumping/cleanup như checkpoint. Toolkit đã bao phủ cả hai target và adapter consumer bị ảnh hưởng.

Physical 100%/125% Light/Dark × normal/minimum đã PASS. Lượt 125% dùng probe per-monitor-aware xác nhận effective DPI 120×120 và ghi riêng `DeviceDpi=96` của Lab net48. Owner APPROVED đúng candidate ngày 2026-10-28. Không chạy lại SQL/Login/package/release cho thay đổi docs-only ghi quyết định.

## Trạng thái kiểm chứng và quyết định

- **RECORDED:** API test gate, compiled public API delta/fingerprint, pin chính xác, build và focused Windows discovery/execution totals.
- **RECORDED:** physical-100/125 native observations, independent DPI probe và selected screenshots.
- **RECORDED:** quyết định Owner APPROVED ngày 2026-10-28 cho ba ngoại lệ trên exact candidate đã trình.

## Mục review riêng — source focus correction `3b5f95e2`

Đối chiếu [review findings follow-up của evidence UIL](20261027-business-compact-and-independent-ui-labs.md#2026-10-28-review-findings-follow-up) và các mục Issue #59/#60 sau đó không tìm thấy quyết định Owner mới áp dụng cho candidate `3b5f95e2d7d1414f0ffe97f47a3bdef58f33d786`. Phạm vi cần xác nhận vẫn chỉ là selected/focused native-keyboard correction đã được focused verification ghi tại đó; không mở lại full visual matrix, không chuyển approval của candidate trước và không gồm SQL/Login/package/release.

**Focused Owner confirmation cho `3b5f95e2`: PENDING.** Nghiệm thu ba ngoại lệ EXC-T01–T03 không tự phê duyệt mục này.

Cả ba ngoại lệ đã **CLOSED** theo quyết định Owner ngày 2026-10-28. Xác nhận Owner tập trung riêng cho `3b5f95e2` vẫn **PENDING**. SQL, Login thực tế, Designer, package, deployment và release acceptance nằm ngoài phạm vi được chọn; không chuyển acceptance lịch sử sang candidate này.
