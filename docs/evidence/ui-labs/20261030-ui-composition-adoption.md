# UI Composition Adoption — app evidence

## Baseline và scope

- Ngày 30/10/2026; task CMP-T03, [selected plan](../../plans/20261030-001-business-compact-composition-adoption.md); app inspected source/test `53b951c5974283c47b5f0bc2484d037ab312af47`, branch `codex/cmp-t03`.
- Product/target: Windows, net48, bitness hiện hữu. Task chỉ thay Markdown/skill routing; smallest verification scope là diff, links/anchors, source/API/status và UTF-8 không BOM/CRLF.
- Recipe source `1420f252ca198cd4409e0e945fa6433b30b943b3`; handoff docs `d439cbdb043e1b798d1f991f0266350d9f61441e`; runtime gitlink `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` giữ nguyên. [Mapping](../../design/UI_COMPOSITION_MAPPING.md) chứa exact source/guide/plan/evidence links và product ownership.

## Source/API reconciliation

Đã đọc guide và evidence ở handoff, recipe forms tại source candidate, catalog và identity dialog tại app baseline. `git ls-tree HEAD vendor/MyDmsVn.WinFormsKit` xác nhận pin runtime nêu trên. Đối chiếu `git diff --name-only 433e5b5… 1420f25… -- src` tại repository toolkit: delta thuộc tabs/busy; các file TextBox, ComboBox, Button, DataGridView, CheckBox, Label, validation enum và theme manager dùng bởi recipes không đổi. Các API public trong mapping có tại pin; không đề xuất API production mới hoặc pin upgrade.

UIC-01 map vào hai-pane catalog hiện hữu; giữ module/lifecycle/search, read-only, selection/scroll, details/copy và current-owner state. Demo fixtures, scenario controls và metrics không chuyển sang app. UIC-02 map đúng nhánh user-edit `ShowIdentityDialogAsync(true, false)` làm tham chiếu tương thích; shared builder và các nhánh create-user/create-role/edit-role không đổi.

Toolkit evidence tại handoff ghi focused automation hai TFM, bounded physical100/125, Owner approval candidate `1420f25` và representative attended matrix closure. Đây là kết quả của toolkit owner, **không phải PASS mới của app** và không khẳng định mọi state ở mọi tổ hợp đã được quan sát.

## CMP-T03 — checkpoint

**COMPLETE — docs/skill routing only.** Đã có mapping UIC-01/UIC-02, exact source và handoff links, public API reconciliation với runtime pin, deviations/ownership, Lab/test owners và đường skill → mapping → source. Checkpoint UIC-01 đủ đầu vào tài liệu cho CMP-T04; không chứng minh product pilot đã thực hiện.

**NO product source/runtime change. NOT RUN app GUI/native verification.** Không chạy build, GUI, SQL, package hoặc actual Login cho Markdown-only diff. Không có app candidate visual mới và **không tuyên bố quyết định Owner visual**. Historical evidence giữ đúng candidate/phạm vi; không chuyển approval toolkit thành approval app.

Docs verification: `git diff --check`; kiểm local links/anchors và exact toolkit object paths tại SHA được dẫn; kiểm status CMP-T03 COMPLETE, CMP-T04/CMP-T05 PLANNED và plan acceptance OPEN; kiểm UTF-8 không BOM, CRLF cho các file thay đổi. Skill clean-context behavior review chưa chạy trong task này và thuộc CMP-T05; static routing review không thay kết quả đó.

## CMP-T04 — product pilot

**COMPLETE — no source/runtime change, 30/10/2026.** Đã đi theo skill → mapping → exact UIC-01 source/guide → production `SecurityPermissionCatalogView` trong Security Lab. Bố cục hiện tại phù hợp với recipe đã chuyển thể; kiểm chứng dưới đây không chỉ ra gap cụ thể cần sửa layout. Không sửa production, samples hoặc tests, không thêm CRUD/assignment, không reference ControlGallery và không nâng runtime pin. TDD RED/fix không áp dụng vì không có behavior change; không tạo test trùng lặp để có diff.

### Candidate, evidence reuse và môi trường

| Item | Candidate / observation |
|---|---|
| App source/test tại lượt chạy | `cc498975f49c0b42f87b6e4c52c10a3c5c635cf9`, worktree `cmp-t04-security-catalog`, detached HEAD |
| Production `src` tree | `a5083a939408501cdf86e40fc6548e2b4323e5a4` |
| `samples` tree | `53f7fe35de808c5c6d732791a3da753e31ae2bea` |
| `tests` tree | `900e01fa440760c563edad23820ad88763562735` |
| Runtime toolkit / SourceGrid | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` / `f4e457b43582bf01892f50bdc74aa480531e5944`, unchanged |
| Recipe / handoff | `1420f252ca198cd4409e0e945fa6433b30b943b3` / `d439cbdb043e1b798d1f991f0266350d9f61441e`, source references only |
| Platform | Interactive Windows NT `10.0.26300.0`, SDK `8.0.427`, net48, Release, `PlatformTarget=x86`, test host `TargetPlatform=x86` |
| Display diagnostics | DeviceDpi `96`; Lab work area `1920×1032`; physical scale **unverified in this supplemental pass** |

`git diff 95916e33650648ef9753223605b5e679d6944d2a HEAD -- src samples tests` rỗng; cả ba tree hashes và runtime pin trùng [consumer candidate 29/10](20261029-input-popup-consumer-acceptance.md). Do đó giữ evidence physical100/125, Light/Dark và Owner approval cũ đúng candidate/phạm vi đó. Không biến chúng thành kết quả mới của task này. Evidence CMP-T03 ở trên vẫn là checkpoint docs-only tại thời điểm của nó.

### Focused discovery và execution

Restore locked trực tiếp UI test project và Security launcher; build hai projects Release/net48/x86 đều exit 0, **0 warning / 0 error**. Chọn 11 methods hiện hữu cho các boundary cần xác nhận; không chạy toàn lớp/solution, SQL hoặc archived matrix. Nhóm filter geometry thay cho `Normal_and_minimum_hosts_keep_catalog_actions_reachable` vì kiểm chính xác filter HWND/height/wrap bằng Business Compact; product geometry và actions được bổ sung qua parity test và quan sát Lab. Hai rows native typing bảo đảm per-character/caret/Enter, không dùng whole-string assignment làm bằng chứng per-key.

| Method-qualified selector (`FullyQualifiedName~`) | Discovered / executed / passed |
|---|---:|
| `SecurityWorkspaceControlTests.Role_catalog_render_preserves_selected_definition_native_row_and_filter_identity` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Role_catalog_toolkit_surfaces_keep_read_only_theme_focus_and_copy` | 2 / 2 / 2 |
| `SecurityWorkspaceControlTests.Role_catalog_same_owner_render_keeps_native_member_and_catalog_rows_scroll_and_identity` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Filters_preserve_selected_owner_and_show_catalog_lifecycle` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Catalog_has_no_assignment_actions` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Catalog_search_matches_the_displayed_friendly_name` | 1 / 1 / 1 |
| `SecurityPaneStateTests.Revoked_or_failed_catalog_clears_old_keys_without_clearing_accounts` | 2 / 2 / 2 |
| `SecurityPaneStateTests.Held_catalog_read_exposes_loading_then_successful_empty_independently` | 1 / 1 / 1 |
| `SecurityProductHostParityTests.Sidebar_security_matches_direct_lab_content_and_retains_editor_on_theme_toggle` | 4 / 4 / 4 |
| `SecurityNativeAndResetTests.Role_catalog_native_filter_retains_caret_range_key_and_enter_never_writes` | 2 / 2 / 2 |
| `InputPopupConsumerAcceptanceTests.Catalog_filter_keeps_equal_native_input_heights_without_clipping_when_refresh_wraps` | 4 / 4 / 4 |
| **Total** | **20 / 20 / 20; fail 0, skip 0** |

Exact method names above are joined by `|` into `$filter`, each prefixed `FullyQualifiedName~`. Commands executed from the isolated worktree:

```powershell
git submodule update --init --recursive
dotnet --version
dotnet restore tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj --locked-mode
dotnet restore samples/MyDmsVn.Desktop.UiLab.Security/MyDmsVn.Desktop.UiLab.Security.csproj --locked-mode
dotnet build tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 -v minimal
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --list-tests --filter $filter -- RunConfiguration.TargetPlatform=x86
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --filter $filter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=cmp-t04-catalog.trx' --results-directory TestResults/cmp-t04-catalog -- RunConfiguration.TargetPlatform=x86
dotnet build samples/MyDmsVn.Desktop.UiLab.Security/MyDmsVn.Desktop.UiLab.Security.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 -v minimal
```

Discovery exit 0, 20 actual Fact/Theory rows. Run exit 0, duration `52 s`; TRX counters total/executed/passed `20`, failed/error/timeout/aborted/notExecuted `0`. Raw TRX remains ignored/local at `TestResults/cmp-t04-catalog/cmp-t04-catalog.trx`; no raw result or screenshots committed. Assembly disables test parallelization; native rows use existing STA/pump/guard/finite waits. GUI work was serialized, with computer-use Lab actions only after test process completion; no remaining Lab/testhost process after close.

SHA-256 of actual outputs: UI test DLL `7487594810a85be5e4582d0c653f2b2b9919fdad6383f49bbf1f77e4f72ad599`; Security launcher `a483fbc1b56b6e9829d2d4e0f3bd0a97643b8b65fcaf3bf878eecc58bca97c7d`; production WinForms DLL `16b240f2aed38d05830249110bbb80f3c784535bc77370a057459be4338313ea`. These are development build outputs, not a release package.

### BC-04/05/09/11 và native observations

| Policy | Evidence mới và disposition |
|---|---|
| BC-04 | Product-host TRX: normal outer `1200×760`, shell client `1184×721`, Security content `952×657`; minimum `900×600`, `884×561`, `652×497`; cả hai themes PASS, DeviceDpi=96. Lab launcher outer `1250×850`, client `1234×811`; fixed content lần lượt `952×657` / `652×497`. Diagnostics ban đầu báo normal viewport clipped khi tools/journal hiện; F11 giữ content `952×657` và báo visible, không tăng target viewport. Minimum visible với tools. |
| BC-05 | Filter test mới đo Search/Lifecycle/Refresh cao `32` tại DeviceDpi=96 ở Light/Dark. Host `1200`: filter width `884`, Refresh `(365,3,110,32)`; host `550`: width `384`, Refresh `(3,41,110,32)`, mọi filter trong client bounds. Source row metric vẫn `max(Scale(30), Font.Height+8)`, copy height `34`, details height `116`, padding `8`; không đổi metrics. Tại fixed minimum, Refresh wrap và Copy/Details/count còn hiện, grid cuộn cục bộ. Long friendly name/canonical key ellipsis trong grid nhưng full accessible cell/details vẫn có; chọn group `UiLab.ReviewOnly` cho `1 / 8` và details wrap key dài. Không thấy gap cần ép chiều cao/font hoặc horizontal workspace scroll. |
| BC-09 | Hai lệnh catalog là `Làm mới` và `Sao chép mã quyền`, secondary outline; không có Save/Create/Edit/Delete hoặc assignment. Native per-character `Sales.` giữ raw text, caret/range, focus và selected `Sales.Receipts.Post`; Enter không tạo `Security.Mutate`. Copy test đọc clipboard đúng selected key rồi restore clipboard. |
| BC-11 | Focused tests xác nhận read-only grid/details, selected key/native row/scroll identity, friendly-name/module/lifecycle/no-match filtering, loading→empty và failed/forbidden scrubbing mà không xóa accounts. Source giữ count “chưa khả dụng” khi dataset không current; empty dataset khác no-match. Test assertions ghi nhận read-only flows không có `Security.Mutate`; Lab journal ban đầu chỉ Users/Roles/Catalog và các read results. Light→Dark tại minimum giữ `Sales.Receipts.Read`, selected row/details; group long-key trả đúng selected definition. |

Launched exact `samples/MyDmsVn.Desktop.UiLab.Security/bin/Release/net48/MyDmsVn.Desktop.UiLab.Security.exe`, fixture SEC_L01, tab **Danh mục quyền**, sản phẩm dùng `BusinessCompactAppearance` qua bootstrap hiện hữu. Quan sát bằng computer-use, không bản sao/demo. Native OS ComboBox/scrollbar chrome còn theo hợp đồng pin; không thêm native/custom exception. Khi index tab từ UI snapshot bị stale, input bị từ chối; reobserve rồi dùng điểm trên ảnh mới thành công. Đây là tool targeting retry, không phải test failure hoặc lỗi catalog. Các captures chỉ xem cục bộ trong lượt kiểm tra, không tạo board/approval mới.

**Giới hạn:** physical100/125 không chạy lại trong CMP-T04; lượt bổ sung không xác minh độc lập scale và không phải attended Owner review. Không có candidate visual mới, không ghi approval mới. Không chạy SQL, actual Login, package/release, Security full matrix, dialog rewrite hoặc CMP-T05 fresh-context review. No-gap disposition chỉ áp dụng UIC-01 catalog trong scope đã chọn; không đóng source-focus confirmation `3b5f95e2` hay acceptance lịch sử.

## Remaining work

- CMP-T05 **PLANNED**: fresh-context routing review, đối chiếu UIC-02 compatibility/gap và handoff.
- Plan completion/acceptance **OPEN**. Không mở SQL, release, dialog rewrite hoặc archived acceptance.
