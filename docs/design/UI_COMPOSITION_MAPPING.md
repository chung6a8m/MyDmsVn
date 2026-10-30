# UI Composition Mapping

**Scope:** CMP-T03 của [active plan](../plans/20261030-001-business-compact-composition-adoption.md), ngày 30/10/2026. Đây là mapping source/API; product pilot CMP-T04 và routing review CMP-T05 còn PLANNED. [Business Compact](BUSINESS_COMPACT.md) sở hữu theme, geometry và action hierarchy; [evidence app](../evidence/ui-labs/20261030-ui-composition-adoption.md) sở hữu checkpoint và giới hạn.

## Source và runtime độc lập

- Recipe source được Owner duyệt: toolkit `1420f252ca198cd4409e0e945fa6433b30b943b3`.
- Handoff `d439cbdb043e1b798d1f991f0266350d9f61441e`: [guide](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/d439cbdb043e1b798d1f991f0266350d9f61441e/docs/guides/UI_COMPOSITION_RECIPES.md), [plan/status owner CMP-T01–T02](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/d439cbdb043e1b798d1f991f0266350d9f61441e/docs/plans/20261030-001-ui-composition-recipes.md), [toolkit evidence](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/d439cbdb043e1b798d1f991f0266350d9f61441e/docs/verification/20261030-ui-composition-recipes.md).
- Runtime app giữ gitlink `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`. Đọc recipe không cần nâng pin, reference ControlGallery hoặc kế thừa `DemoFormBase`.
- Đối chiếu source tại hai SHA xác nhận TextBox, ComboBox, Button, DataGridView, CheckBox, Label, validation enum và theme manager không đổi. Các public capabilities bên dưới có tại runtime pin; demo class/helper/snapshot không phải API UI core. Đây là kiểm tra source, không là app runtime verification.

## UIC-01 — Filter Results → Security catalog

| Trường | Mapping |
|---|---|
| Recipe source | [CompositionListDemoForm.cs@1420f25](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/1420f252ca198cd4409e0e945fa6433b30b943b3/samples/ControlGallery/CompositionListDemoForm.cs); guide/plan/evidence tại handoff ở trên |
| Runtime pin / public API | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`: `BootstrapTextBox.Text/TextChanged/KeyDown`; `BootstrapComboBox.Items/SelectedIndex`; `BootstrapButton.Click/Variant/Outline`; `BootstrapDataGridView.ReadOnly/EmptyStateText/Loading/LoadingText` và native rows/columns; `BootstrapThemeManager.ThemeChanged`; native layout primitives. Recipe DataSource là khả năng có sẵn, không yêu cầu đổi cách catalog bind rows. |
| Product surface | [SecurityPermissionCatalogView](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityPermissionCatalogView.cs), production control trong Security workspace; áp BC-01–BC-06, BC-08, BC-09 và BC-11–BC-12. |
| Giữ và chuyển thể | Giữ hai pane nhóm/catalog 23/77, filters wrap, grid chỉ đọc, details chọn/copy, refresh phụ và tìm theo friendly name/key/description. Giữ canonical Module selector và group-list sync, lifecycle Active/Deprecated, selection key, native row identity và scroll anchor khi cùng owner. Không biến catalog thành list một pane chỉ để giống demo. |
| State / ownership | Giữ `Catalog.IsCurrent(OwnerVersion)`, state loading/empty/no-match/unavailable và count chưa biết; không map trạng thái chưa biết thành 0. App giữ authorization, fake ports, session generations, cancellation, refresh và clipboard. Không thêm assignment/mutation hay yêu cầu `Security.Mutate` cho flow đọc. |
| Deviations | Bỏ demo scenario controls/dataset, `DemoDataGridRowMetrics` và Gallery profiles/metrics. Product dùng Business Compact theme/bootstrap và row metrics hiện hữu; không chuyển query trim hoặc state machine demo thành contract app. Không thêm pagination/count contract. |
| Lab / test owner | [Security Lab runbook](../development/UI_LAB.md), launcher `MyDmsVn.Desktop.UiLab.Security`; production catalog qua fake ports hiện hữu. `MyDmsVn.Desktop.Ui.Tests`: [SecurityWorkspaceControlTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs) sở hữu read-only/filter/selection/copy/geometry; [SecurityPaneStateTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityPaneStateTests.cs) sở hữu state; [SecurityProductHostParityTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityProductHostParityTests.cs) sở hữu Lab/product theme và identity parity. Chọn method theo diff và plan §5.2. |
| Evidence / giới hạn | [CMP-T03 checkpoint](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t03--checkpoint) mở đầu vào CMP-T04. [Consumer evidence 29/10](../evidence/ui-labs/20261029-input-popup-consumer-acceptance.md) chỉ giữ giá trị đúng candidate/phạm vi cũ. CMP-T04 phải xác định evidence còn áp dụng, quan sát geometry/behavior còn thiếu và gap trước khi quyết định sửa source. Chưa có app candidate/native decision mới. |

## UIC-02 — Compact Edit Form → user-edit reference

| Trường | Mapping |
|---|---|
| Recipe source | [CompositionEditDemoForm.cs@1420f25](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/1420f252ca198cd4409e0e945fa6433b30b943b3/samples/ControlGallery/CompositionEditDemoForm.cs); guide/plan/evidence tại cùng handoff ở trên |
| Runtime pin / public API | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`: `BootstrapLabel.Text/MaximumSize/TextRole`; `BootstrapTextBox.Text/Focus/ValidationState/Multiline/WordWrap/ScrollBars`; `BootstrapValidationState.Invalid`; `BootstrapComboBox.Items/SelectedIndex`; `BootstrapCheckBox.Checked`; `BootstrapButton.Click/Variant/Outline`; native scroll/layout và theme manager. Chỉ chuyển thể khả năng cần thiết, không thêm fields demo vào dialog. |
| Product surface | [SecurityWorkspaceEditorBindings](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceEditorBindings.cs): “Sửa thông tin…” → `IdentityDialogAsync(true, false)` → `ShowIdentityDialogAsync(true, false)`. **Compatibility reference only**, không sửa dialog trong adoption scope. Áp BC-01–BC-06, BC-08–BC-09 và BC-11–BC-12. |
| Giữ và chuyển thể | Tham khảo field/error wrapping, focus field lỗi đầu, Save chính/Cancel phụ và commands truy cập được ở viewport nhỏ. Product giữ `MutationDialog : BootstrapModal`, dữ liệu user, validation/server feedback, busy/close admission, recovery và originating-owner/session guards hiện hữu. |
| Deviations / ownership | Demo Cancel phục hồi snapshot cục bộ; app Cancel/dirty-close và mutation response tuân contract app, không thay bằng snapshot demo. Không reference demo assembly, field schema/validation hay Gallery metrics. Builder chung còn phục vụ create-user, create-role và edit-role; mọi gap chỉ ghi follow-up để Owner chọn, kể cả ảnh hưởng secret clearing ở create-user. |
| Lab / test owner | Security Lab với production dialog/fake ports; `MyDmsVn.Desktop.Ui.Tests`, [SecurityWorkspaceControlTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs) cho modal/validation/lifetime và [SecurityProductHostParityTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityProductHostParityTests.cs) cho host parity. Chỉ chọn tests nếu follow-up thật sự thay boundary. |
| Evidence / giới hạn | [Exception evidence](../evidence/ui-labs/20261028-business-compact-exceptions.md) giữ approval modal cũ trong phạm vi cũ. CMP-T03 chỉ đối chiếu source/API; CMP-T05 sẽ đánh giá compatibility/gap theo mapping này. **NOT RUN** app GUI/native mới; không tuyên bố dialog đạt recipe hoặc Owner visual acceptance mới. |

Nếu không có recipe phù hợp, tiếp tục theo Business Compact và pinned capability map. Chỉ đề xuất recipe sau nhu cầu lặp lại hoặc lỗi phối hợp đáng kể có bằng chứng; không tạo framework chỉ vì thiếu mẫu.
