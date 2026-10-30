# Codex handoff — Composition expansion

**Selected work:** [active app roadmap](../plans/20261030-002-editable-lines-sal-adoption.md) và [toolkit companion](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/main/docs/plans/20261030-002-editable-lines-master-detail-recipes.md). Đây là hướng dẫn thực thi, không phải evidence đã chạy. Task owners giữ trạng thái; không cập nhật tiến độ riêng ở trang này.

## 1. Thứ tự đọc và bắt đầu

1. `AGENTS.md` → `AI_CONTEXT.md` → `docs/plans/README.md` → active task.
2. [Selected design](../design/EDITABLE_LINES_SAL_ADOPTION.md), [Business Compact](../design/BUSINESS_COMPACT.md), module contracts, [UI composition mapping](../design/UI_COMPOSITION_MAPPING.md).
3. Actual source/gitlinks → exact recipe revision khi có → public API ở runtime pin.
4. [Test policy](../TEST_POLICY.md), [tests README](../../tests/README.md), [UI Lab runbook](UI_LAB.md), relevant source tests.

Trình tự ưu tiên: A01 song song toolkit K01 → SAL A02 → master–detail K02/A03 → A04 helper decision → A05 handoff. K03 bàn giao riêng mỗi recipe. UIC-04 không chặn SAL; GUI trong cùng môi trường luôn chạy tuần tự. Public API/pin gap ghi follow-up riêng, tiếp tục phần không bị chặn.

Không cần chọn lại roadmap hoặc hỏi lại Owner cho từng bước đã được cho phép. Native visual candidate mới vẫn cần decision tại gate của task; thiếu quyết định đó phải ghi PENDING và tiếp tục những phần độc lập.

## 2. Prompt giao việc theo repository

### MyDmsVn

```text
Triển khai roadmap đang ACTIVE tại docs/plans/20261030-002-editable-lines-sal-adoption.md.
Đọc AGENTS.md, AI_CONTEXT.md, execution index, selected design và sole TEST_POLICY trước.
Bắt đầu CEX-A01: ghi baseline/gitlinks, map production SAL/Users/Roles, public APIs tại runtime pin và helper inventory.
Sau khi có frozen UIC-03 source từ toolkit, thực hiện CEX-A02 trên Sales Lab dùng production SalesReceiptControl.
Giữ runtime pin, seven-column/manual-price/duplicate-row/Gift contracts, keyset, pending buffers, Save/Post và recovery.
Chỉ sửa gap có reproduction; NO-CHANGE là kết quả hợp lệ. UIC-04 và A03 là follow-on riêng.
A04 quyết định NO EXTRACTION hoặc internal field wrapper đúng scope; không tạo generic framework/public toolkit API.
Dùng selectors đã đối chiếu source trong handoff, verify actual nonempty discovery và chạy native trên Windows/STA tuần tự.
Commit nhỏ theo task, ghi exact source/test/recipe/runtime revisions và limitations vào evidence mới.
Không kế thừa PASS/Owner approval cũ, không mở lại historical SAL/SEC/SQL/release queues.
Nếu một dependency/native gate còn thiếu, báo chính xác phần blocked và tiếp tục các phần độc lập; không ghi COMPLETE cho task còn thiếu evidence.
```

### MyDmsVn.WinFormsKit

```text
Triển khai docs/plans/20261030-002-editable-lines-master-detail-recipes.md sau khi đọc AGENTS/AI_CONTEXT/ROADMAP và selected toolkit design.
Ưu tiên CEX-K01 UIC-03 Editable Lines trong GridGallery bằng public registry/adapters hiện có.
Ghi sample helpers/models là demo-only; giữ ownership/lifetime, pending-vs-committed, stable row identity, keyboard và validation semantics.
Không dùng Product/Sales DTO hoặc copy Save/Post/recovery vào toolkit; không đổi public API/runtime chỉ để thêm sample.
Dùng existing grid test owner đã reference GridGallery. Native tests tuần tự trên net48 và net8.0-windows.
Thực hiện K03 theo từng recipe: có thể bàn giao UIC-03 trước khi UIC-04 xong; không bắt app chờ toàn bộ K03.
Tiếp theo K02 UIC-04 Master–Detail read-only trong ControlGallery, dùng existing host/test owner.
Ghi exact source/test/framework/physical-DPI/candidate/evidence/Owner decision và giới hạn; không chuyển approval lịch sử sang candidate mới.
Commit theo deliverable; giữ scope TAB/BUS/input-popup và historical evidence không đổi.
```

## 3. Prompt cho clean-context routing review ở CEX-A05

Giao riêng prompt dưới đây cho agent/context mới. Không kèm tên UIC, đường recipe, nội dung plan hoặc danh sách source cần tìm; agent chỉ có repository instructions bình thường. Root ghi lại route thực tế agent tìm được vào evidence.

```text
Đánh giá cách bố trí và phối hợp control của màn hình Phiếu bán hàng theo Business Compact.
Xác định những gì có thể dùng lại, các ràng buộc nghiệp vụ cần giữ và phạm vi kiểm chứng nhỏ nhất nếu cần sửa.
Đồng thời chỉ ra điểm tham khảo phù hợp cho bố cục danh sách–chi tiết Người dùng/Vai trò.
Chưa sửa code; trả lời dựa trên source và instructions hiện tại.
```

Đạt khi agent tự tìm active plan/design/mapping → đúng recipe/source revision → pin/public APIs → production control/Lab → focused tests; nhận ra demo APIs khác production helpers, SAL List/Detail mode và helper extraction có điều kiện. Không dùng số test/toolkit PASS làm product acceptance.

## 4. Source-verified selectors tại activation baseline

Các method dưới đây đã được đọc ở app `014e82d0013b0a16d73da0b35287d79d5848cc10`; **chưa được discovery hoặc chạy trong commit kích hoạt**. Re-read source nếu tests đổi tên. Namespace thường là `MyDmsVn.Desktop.Tests` dù project là `MyDmsVn.Desktop.Ui.Tests` hoặc `MyDmsVn.Desktop.Session.Tests`.

### 4.1. A02 — SAL native baseline

Project: `tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj`. Chín method dùng native `ScenarioControlHost`/STA/message pump/guard.

```powershell
$salNativeFilter = @(
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S01_duplicate_product_lines_keep_ids_and_save_the_submitted_edits'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S02_invalid_native_quantity_keeps_raw_text_and_prevents_save_or_post'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S02_server_field_error_attaches_to_submitted_line_after_local_input_is_valid'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S03_newer_lookup_result_wins_even_when_canceled_older_result_arrives_late'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S03_session_replacement_fences_late_lookup_and_mutations'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S05_dirty_close_cancel_discard_and_save_are_decided_by_coordinator'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S05_pending_native_buffer_survives_cancel_and_rejected_close_save'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S05_back_rejects_invalid_native_buffer_without_losing_document_or_dispatching_mutation'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesScenarioBehaviorTests.S06_unknown_post_stays_locked_until_same_id_authoritative_reload'
) -join '|'
```

Nếu thay header/date wrapper, xem thêm `S02_invalid_native_sale_date_is_the_only_save_blocker` và `S02_invalid_native_sale_date_is_the_only_post_blocker` trong cùng class. Nếu thay native grid/paste, chọn affected methods của `SalesReceiptGridTests` ở Desktop.Tests, gồm behavior đang đổi; `Compact_944_grid_keeps_all_seven_columns_visible` chỉ là fixture geometry, không thay module-specific viewport measurement.

### 4.2. A02 — Session/VM guards khi chạm boundary tương ứng

Project: `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj`. Đây là tests ViewModel/fake ports, không native STA; vẫn target net48.

```powershell
$salSessionFilter = @(
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesReceiptListViewModelTests.Pending_criteria_failure_retains_applied_page_and_disables_old_cursor_navigation'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesReceiptListViewModelTests.Empty_later_page_recovers_once_per_generation_and_failed_recovery_retains_snapshot'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesReceiptEditorViewModelTests.Raw_buffers_commit_before_save_snapshot_and_before_post_dirty_decision'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesReceiptEditorViewModelTests.Unknown_or_conflicted_post_stays_locked_until_successful_same_id_reload'
) -join '|'
```

### 4.3. A03 — Security master–detail native baseline

Project: `tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj`. Năm method native STA; `SecurityPaneStateTests` cũng native, không xếp nhầm vào headless VM tests.

```powershell
$securityMasterDetailFilter = @(
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests.Toolkit_master_tiles_preserve_owner_native_selection_and_theme'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests.Toolkit_master_render_keeps_native_item_focus_scroll_and_full_identity'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests.Filters_preserve_selected_owner_and_show_catalog_lifecycle'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests.Master_and_identity_views_show_separate_hierarchy'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SecurityWorkspaceControlTests.Member_navigation_resolves_role_draft_before_selecting_requested_user'
) -join '|'
```

### 4.4. A04 — Guards cho hai field-wrapper consumers nếu extraction

Project: `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj`; native STA. Đây là guards semantics; nếu layout/tab order đổi vẫn cần meaningful coverage riêng cho thay đổi đó.

```powershell
$fieldConsumerFilter = @(
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.GoodsReceiptControlTests.Mode_switch_preserves_list_and_document_identity'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.GoodsReceiptControlTests.Back_mouse_down_does_not_commit_highlighted_lookup'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesReceiptControlTests.Pending_header_query_blocks_save_and_post_without_committing_text_to_identity'
  'FullyQualifiedName~MyDmsVn.Desktop.Tests.SalesReceiptControlTests.Pending_sales_date_filter_survives_unrelated_state_projection_and_rejected_apply'
) -join '|'
```

### 4.5. Discovery/execution commands

Chạy từ repository root trên Windows, SDK theo `global.json`, locked restore. Ví dụ cho A02 native; chọn project/filter tương ứng khi chạy task khác. Không chạy mọi ví dụ chỉ vì chúng nằm trong handoff.

```powershell
$compositionProject = 'tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj'
dotnet restore $compositionProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
dotnet test $compositionProject -c Release --framework net48 --no-restore `
  -p:PlatformTarget=x86 --filter $salNativeFilter --list-tests
if ($LASTEXITCODE -ne 0) { throw 'Discovery failed.' }
# Inspect output: each selected method exists; reconcile actual Theory rows before execution.
$compositionResults = 'TestResults/composition-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet test $compositionProject -c Release --framework net48 --no-restore `
  -p:PlatformTarget=x86 --filter $salNativeFilter `
  --logger 'trx;LogFileName=composition.trx' --results-directory $compositionResults `
  -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'Selected native tests failed.' }
```

Record actual discovered/executed/pass/fail/skip counts và exact command. Success exit với zero tests/missing mandatory cases/skip không là PASS. Không dùng `--no-build` trừ khi current candidate đã được build bằng cấu hình tương ứng. Không restore approval file, runner, lease hoặc test-gates manifest. Với session-only selector, dùng đúng Session.Tests project/filter; native target-platform requirement không phải bằng chứng session tests là GUI.

## 5. Evidence cần ghi khi làm việc

- Task và scope; app source/test commit, recipe source/test commit, runtime gitlink; source-only/compile/native/Owner decision tách biệt.
- Windows/.NET/SDK/TFM, physical scale và DeviceDpi riêng; product shell → workspace → control/grid content bounds thực đo cho từng module.
- Scenario/fixture/input từng phím và expected/actual; journal count/payload được sanitize; raw proprietary TRX giữ local theo policy.
- Các gap đã sửa, tests tương ứng và evidence validity khi source thay đổi; NO-CHANGE / NO EXTRACTION có lý do.
- Native candidate mới có dated Owner decision hoặc PENDING; không giả lập quyết định đó bằng completion code/test.

Xem [evidence skeleton](../evidence/ui-labs/20261030-composition-expansion.md). Source change sau native review phải đánh giá phần bằng chứng còn áp dụng, không chuyển approval sang candidate khác mặc định.
