# Bằng chứng candidate khắc phục các ngoại lệ Business Compact — 2026-10-28

**Phạm vi được chọn:** Owner chọn [kế hoạch khắc phục ba ngoại lệ](../../plans/20261028-001-business-compact-exceptions.md) và [thiết kế](../../design/BUSINESS_COMPACT_EXCEPTION_CLOSURE.md) tương ứng: action disabled trong Dark, khung hộp thoại Security và văn bản nhiều dòng chỉ đọc. Các thay đổi source đã được triển khai trong các tree source/test của candidate ghi dưới đây. **Kiểm chứng Windows/native là NOT RUN / BLOCKED; phê duyệt native tập trung của Owner là PENDING. Cả ba ngoại lệ vẫn OPEN.**

Báo cáo này chỉ ghi candidate mới. [Evidence UIL gốc](20261027-business-compact-and-independent-ui-labs.md), các quyết định có ngày và phạm vi giới hạn của nó, cùng xác nhận Owner tập trung đang chờ riêng cho sửa focus `3b5f95e2d7d1414f0ffe97f47a3bdef58f33d786` được giữ nguyên. Việc chọn task này hoặc kết quả compiler của nó không phê duyệt sửa đổi trước đó và không mở lại ma trận UIL lịch sử.

## Định danh candidate và môi trường

| Mục | Giá trị đã ghi |
|---|---|
| Baseline source app | `5386a6d8efe23099c436eb7ef62141df8d3896b8` |
| Candidate app | Commit chứa report này; các tree được Git chuẩn hóa của source/samples/tests được ghi ngay dưới đây |
| Tree `src` của app | `a5083a939408501cdf86e40fc6548e2b4323e5a4` |
| Tree `samples` của app | `53f7fe35de808c5c6d732791a3da753e31ae2bea` |
| Tree `tests` của app | `6ceb7954df45971ec0bd10209c1572304d0757ea` |
| Gitlink toolkit trước sửa | `c232a4c09dc0386a5bd8bb484a5c991acae827d5` |
| Baseline toolkit upstream | `efe2e7212fd36e32c91b914bdc5e4081947124fc`; cùng tree với pin consumer trước sửa |
| Candidate/pin toolkit cuối | `1b9c345aa2afd85e38a72d575485c3abdf559451` theo [PR #2](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/pull/2); consumer checkout sạch. Locked restore/build theo pin chính xác PASS; compiled API review được ghi bên dưới |
| SourceGrid lồng bên trong | `f4e457b43582bf01892f50bdc74aa480531e5944`; không đổi |
| Host/toolchain | Linux `6.18.55` x86_64; SDK `8.0.427` theo lượt chạy thành công ngoài sandbox của người thực hiện |
| Target sản phẩm/test | `net48`, Debug, target build x86. Chưa ghi lượt chạy process Windows/native runtime |
| Điều kiện cache | Cache không được xóa; build cuối dùng restored assets và warm cache. Không có clean-restore claim |
| Screenshot/quan sát native mới | **NOT RUN** |

Candidate app được định danh bằng commit chứa report này, ba tree source/samples/tests ở trên và pin toolkit chính xác. Kết quả build cuối bên dưới dùng đúng pin và các tree đó. Các lần restore/compile trước đó được giữ riêng để phân biệt lỗi đã sửa, build trên bản mirror source và build cuối theo gitlink.

## Source đã triển khai trong phạm vi

| Ngoại lệ | Thay đổi source của candidate | Trạng thái hiện tại |
|---|---|---|
| Action disabled trong Dark | Palette disabled của toolkit dùng secondary surface và màu chữ bình thường, giữ hành vi/border disabled; fixture UI tập trung có assertion và kiểm thử vẽ bằng palette Business Compact Light/Dark thực tế | **OPEN** — source đã triển khai; evidence appearance/bất hoạt native mới và quyết định Owner đang chờ |
| Khung hộp thoại Security native | Các hộp thoại tài khoản/password/ghi dữ liệu, quyết định và review import không dùng SQL hiện có được bố trí qua `BootstrapModal.BodyPanel`/`FooterPanel` public; bộ duyệt theme của app và công cụ Lab giải phóng mutation đang giữ đi qua các vùng public đó; vòng đời Font tiêu đề của toolkit được sửa | **OPEN** — source đã triển khai; modal owner, chặn đóng khi thao tác đang chờ/phục hồi, xóa dữ liệu bí mật và đổi theme khi hộp thoại mở cần native execution |
| Văn bản nhiều dòng chỉ đọc native | `SecurityUserDetailView.Explanation` và `SecurityPermissionCatalogView.Details` dùng hợp đồng multiline/wrap/scrollbar bổ sung của `BootstrapTextBox`, giữ văn bản, chiều cao và accessible name hiện có | **OPEN** — source đã triển khai, pin chính xác và compiled API delta đã review; evidence native selection/copy/scroll/accessibility đang chờ |

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
| Locked restore app theo pin toolkit cuối | **PASS**, dùng cùng lệnh overlay đã ghi | Consumer checkout sạch tại `1b9c345aa2afd85e38a72d575485c3abdf559451`; không đổi project/lockfile |
| Build app cuối `MyDmsVn.UiLab.sln` Debug net48/x86 theo pin chính xác, gồm Theory palette thực tế | **PASS — 0 cảnh báo, 0 lỗi; 11.65 giây** | Dùng đúng pin và tree source/samples/tests ghi ở trên; không chạy native tests |
| Toolkit CI Release trên `windows-2022`, cả hai TFM | **PASS — build-only** tại [run 40710094747](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40710094747) | `head_sha` đúng `1b9c345aa2afd85e38a72d575485c3abdf559451`; không chạy native tests |
| Windows discovery/execution | **NOT RUN / BLOCKED; 0 đã chạy** | Chưa có số discovered/pass/fail/skip hoặc TRX |
| Owner review native tập trung | **PENDING** | Việc người dùng cho phép triển khai không phải phê duyệt native của candidate |

Các lệnh toolkit được người thực hiện ghi lại, chạy từ checkout toolkit upstream:

```sh
/tmp/mydmsvn-dotnet/dotnet restore MyDmsVn.WinFormsKit.sln --locked-mode -p:EnableWindowsTargeting=true
/tmp/mydmsvn-dotnet/dotnet build MyDmsVn.WinFormsKit.sln -c Debug --no-restore -p:EnableWindowsTargeting=true -v:minimal
```

Người thực hiện đã review compiled API metadata trên cả hai target toolkit: baseline `c232a4c` tái tạo fingerprint `5016acec10c7efdafe03387de2c8f182a2e96c49f473f1bcb8bc6e5932dfa6cb`; fingerprint của candidate là `e6735faae42a8eb41c06cdd5d4fd27a87e56dccaee73f579cd4460ef0693db6e`, gồm 182 exported types và 1,748 dòng surface. Delta chỉ gồm `BootstrapTextBox.Multiline`, `WordWrap` và `ScrollBars`. Tài liệu baseline và hằng số test được duyệt đã được cập nhật sau review. Việc chạy API test gate trên Windows vẫn NOT RUN.

Lệnh locked restore app ban đầu trên Linux, thất bại với NU1004:

```sh
dotnet restore MyDmsVn.UiLab.sln --locked-mode -p:EnableWindowsTargeting=true
```

Lệnh restore app thành công, chạy từ checkout app:

```sh
/tmp/mydmsvn-dotnet/dotnet restore MyDmsVn.UiLab.sln --locked-mode -p:EnableWindowsTargeting=true -p:AutomaticallyUseReferenceAssemblyPackages=false -p:TargetFrameworkRootPath=/root/.nuget/packages/microsoft.netframework.referenceassemblies.net48/1.0.3/build/ -p:CustomAfterMicrosoftCommonTargets=/tmp/mydmsvn-crossbuild.targets
```

Overlay tạm thời đặt `RuntimeIdentifier=win7-x86` chỉ cho project `net48` có `OutputType` là `Exe`/`WinExe` và identifier đang rỗng. Lệnh chỉ rõ bộ reference assemblies có sẵn và ngăn package reference-assembly ngầm định bổ sung làm thay đổi locked graph. Đây là điều chỉnh chuẩn bị compiler trên Linux, không thay project/runtime/lockfile được quản lý trong Git. Build app thành công dùng cùng tham số reference-assembly/overlay sau khi sửa namespace import của test. Kết quả trên bản mirror source cục bộ được ghi ở trên. Cache không được xóa; build cuối dùng restored assets và warm cache, không có clean-restore claim. Sau đó consumer checkout sạch tại pin toolkit cuối và cùng lệnh locked restore đã PASS. Lệnh build cuối theo pin chính xác bên dưới đã PASS, gồm Theory palette Business Compact Light/Dark thực tế; native execution vẫn chưa chạy.

```sh
/tmp/mydmsvn-dotnet/dotnet build MyDmsVn.UiLab.sln -c Debug --no-restore -p:EnableWindowsTargeting=true -p:AutomaticallyUseReferenceAssemblyPackages=false -p:TargetFrameworkRootPath=/root/.nuget/packages/microsoft.netframework.referenceassemblies.net48/1.0.3/build/ -p:CustomAfterMicrosoftCommonTargets=/tmp/mydmsvn-crossbuild.targets -p:PlatformTarget=x86 -v:minimal
```

Sandbox ban đầu chặn SDK/build truy cập thông tin process qua `/proc` (`System.ComponentModel.Win32Exception`). Người thực hiện chạy lại các lệnh toolkit ngoài sandbox thành công. Lượt kiểm tài liệu tái hiện giới hạn môi trường này bằng `/tmp/mydmsvn-dotnet/dotnet --version` (exit 134); kết quả đó không chứng minh lỗi source và không phủ nhận lượt cross-build thành công được ghi riêng.

Toolkit `.github/workflows/build.yml` định nghĩa workflow chỉ build trên `windows-2022`, không chạy native tests. [Run 40710094747](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/actions/runs/40710094747) có `status=completed`, `conclusion=success`, `head_sha=1b9c345aa2afd85e38a72d575485c3abdf559451`; bắt đầu `2026-10-28T14:51:32Z`, cập nhật `2026-10-28T14:53:23Z`. Đây là build Release trên Windows cho cả hai TFM tại đúng commit toolkit cuối, không phải native test execution. App không có `.github/workflows`.

## Kiểm chứng Windows/native còn cần thực hiện

Theo [TEST_POLICY](../../TEST_POLICY.md), [tests/README](../../../tests/README.md) và [UI Lab runbook](../../development/UI_LAB.md). Discovery các fixture thực tế trong phạm vi trên candidate cuối, rồi chạy STA tuần tự với WinFormsTestGuard, thời gian chờ hữu hạn và cleanup hiện có. Selector trong source gồm `SecurityToolkitExceptionTests`, `SecurityWorkspaceControlTests`, `SecurityReviewScenarioTests`, `UiLabModalOwnershipTests` và `SecurityProductHostParityTests`; tên trong source không phải evidence discovery. Kiểm chứng toolkit phải bao phủ cả hai target và các adapter consumer bị ảnh hưởng.

Ghi Light/Dark ở physical 100%/125%, kích thước nội dung thông thường/tối thiểu, nhãn/trạng thái bất hoạt của action disabled, modal owner/Accept/Cancel/chặn đóng khi thao tác đang chờ/xử lý dữ liệu bí mật, đổi theme khi modal vẫn mở và multiline selection/copy/scroll/caret/accessibility. Ghi physical scale riêng với `DeviceDpi`, đồng thời giữ chính xác revision source/test/toolkit. Tại checkpoint này chưa có kết quả thị giác hoặc native của candidate hiện tại để phê duyệt.

## Các mục kiểm chứng còn cần hoàn tất

- **PENDING:** chạy API test gate trên Windows; compiled public API delta/fingerprint review, pin chính xác và các kết quả build đã được ghi ở trên.
- **PENDING:** lệnh kiểm thử Windows thực tế, số discovered/executed/pass/fail/skip và tóm tắt TRX đã loại dữ liệu nhạy cảm.
- **PENDING:** quan sát native có ngày, tham chiếu screenshot khi đã thu thập và quyết định Owner tập trung rõ ràng cho candidate này.

Cả ba ngoại lệ vẫn **OPEN**. Xác nhận Owner tập trung riêng cho `3b5f95e2` cũng vẫn **PENDING**. SQL, Login thực tế, package, deployment và release acceptance nằm ngoài phạm vi được chọn; không chuyển acceptance lịch sử sang candidate này.
