# UI Composition Mapping

**Active expansion:** [CEX roadmap](../plans/20261030-002-editable-lines-sal-adoption.md) and [selected design](EDITABLE_LINES_SAL_ADOPTION.md) add planned UIC-03/UIC-04 routes below. Existing UIC-01/UIC-02 mappings and historical evidence retain their scope. Recipe/runtime/app revisions remain separate.

**Completed mapping scope:** CMP-T03–CMP-T05 của [completed bounded plan](../plans/20261030-001-business-compact-composition-adoption.md), ngày 30/10/2026. Mapping source/API đã được xác nhận trên product catalog ở CMP-T04, không cần sửa source/runtime; [clean-context routing/handoff CMP-T05](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t05--routing-review-and-handoff) tìm đúng recipe, production view và focused test owners, không cần sửa routing. [Business Compact](BUSINESS_COMPACT.md) sở hữu theme, geometry và action hierarchy; [evidence app](../evidence/ui-labs/20261030-ui-composition-adoption.md) sở hữu checkpoint và giới hạn.

## Source và runtime độc lập

- Recipe source accessibility correction: toolkit `bdac29bf4aca20db6618570ecd9a354630bc2e5f`; nó chỉ bỏ `AccessibleName` tĩnh của các label thông báo động để `AccessibilityObject.Name` cung cấp `Text` hiện tại.
- Handoff correction `078eb348ac40b7a7e762a9b3f8b4da3fb19f314c`: [guide](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/078eb348ac40b7a7e762a9b3f8b4da3fb19f314c/docs/guides/UI_COMPOSITION_RECIPES.md), [plan/status owner CMP-T01–T02](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/078eb348ac40b7a7e762a9b3f8b4da3fb19f314c/docs/plans/20261030-001-ui-composition-recipes.md), [toolkit evidence](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/078eb348ac40b7a7e762a9b3f8b4da3fb19f314c/docs/verification/20261030-ui-composition-recipes.md). Owner visual approval source `1420f252ca198cd4409e0e945fa6433b30b943b3` được giữ riêng như quyết định lịch sử, không chuyển sang correction/AT.
- Runtime app giữ gitlink `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`. Đọc recipe không cần nâng pin, reference ControlGallery hoặc kế thừa `DemoFormBase`.
- Đối chiếu source tại hai SHA xác nhận TextBox, ComboBox, Button, DataGridView, CheckBox, Label, validation enum và theme manager không đổi. Các public capabilities bên dưới có tại runtime pin; demo class/helper/snapshot không phải API UI core. Đây là kiểm tra source, không là app runtime verification.

## UIC-01 — Filter Results → Security catalog

| Trường | Mapping |
|---|---|
| Recipe source | [CompositionListDemoForm.cs@bdac29b](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/bdac29bf4aca20db6618570ecd9a354630bc2e5f/samples/ControlGallery/CompositionListDemoForm.cs); guide/plan/evidence correction tại handoff ở trên; visual approval `1420f25` là lịch sử riêng |
| Runtime pin / public API | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`: `BootstrapTextBox.Text/TextChanged/KeyDown`; `BootstrapComboBox.Items/SelectedIndex`; `BootstrapButton.Click/Variant/Outline`; `BootstrapDataGridView.ReadOnly/EmptyStateText/Loading/LoadingText` và native rows/columns; `BootstrapThemeManager.ThemeChanged`; native layout primitives. Recipe DataSource là khả năng có sẵn, không yêu cầu đổi cách catalog bind rows. |
| Product surface | [SecurityPermissionCatalogView](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityPermissionCatalogView.cs), production control trong Security workspace; áp BC-01–BC-06, BC-08, BC-09 và BC-11–BC-12. |
| Giữ và chuyển thể | Giữ hai pane nhóm/catalog 23/77, filters wrap, grid chỉ đọc, details chọn/copy, refresh phụ và tìm theo friendly name/key/description. Giữ canonical Module selector và group-list sync, lifecycle Active/Deprecated, selection key, native row identity và scroll anchor khi cùng owner. Không biến catalog thành list một pane chỉ để giống demo. |
| State / ownership | Giữ `Catalog.IsCurrent(OwnerVersion)`, state loading/empty/no-match/unavailable và count chưa biết; không map trạng thái chưa biết thành 0. App giữ authorization, fake ports, session generations, cancellation, refresh và clipboard. Không thêm assignment/mutation hay yêu cầu `Security.Mutate` cho flow đọc. |
| Deviations | Bỏ demo scenario controls/dataset, `DemoDataGridRowMetrics` và Gallery profiles/metrics. Product dùng Business Compact theme/bootstrap và row metrics hiện hữu; không chuyển query trim hoặc state machine demo thành contract app. Không thêm pagination/count contract. |
| Lab / test owner | [Security Lab runbook](../development/UI_LAB.md), launcher `MyDmsVn.Desktop.UiLab.Security`; production catalog qua fake ports hiện hữu. `MyDmsVn.Desktop.Ui.Tests`: [SecurityWorkspaceControlTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs) sở hữu read-only/filter/selection/copy/geometry; [SecurityPaneStateTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityPaneStateTests.cs) sở hữu state; [SecurityProductHostParityTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityProductHostParityTests.cs) sở hữu Lab/product theme và identity parity. Chọn method theo diff và plan §5.2. |
| Evidence / giới hạn | [CMP-T04 pilot](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t04--product-pilot): production catalog SEC_L01, normal/minimum content 952×657 / 652×497, focused GUI 20/20 và long-key/details/theme quan sát bổ sung; không phát hiện gap cần sửa layout. Source/sample/test trees và runtime pin trùng [consumer candidate 29/10](../evidence/ui-labs/20261029-input-popup-consumer-acceptance.md), nên evidence physical100/125 còn áp dụng đúng phạm vi cũ. Lượt bổ sung báo DeviceDpi=96, physical scale unverified; không là physical100/125 repeat hoặc Owner decision mới. |

## UIC-02 — Compact Edit Form → user-edit reference

| Trường | Mapping |
|---|---|
| Recipe source | [CompositionEditDemoForm.cs@bdac29b](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/bdac29bf4aca20db6618570ecd9a354630bc2e5f/samples/ControlGallery/CompositionEditDemoForm.cs); guide/plan/evidence correction tại cùng handoff ở trên; visual approval `1420f25` là lịch sử riêng |
| Runtime pin / public API | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`: `BootstrapLabel.Text/MaximumSize/TextRole`; `BootstrapTextBox.Text/Focus/ValidationState/Multiline/WordWrap/ScrollBars`; `BootstrapValidationState.Invalid`; `BootstrapComboBox.Items/SelectedIndex`; `BootstrapCheckBox.Checked`; `BootstrapButton.Click/Variant/Outline`; native scroll/layout và theme manager. Chỉ chuyển thể khả năng cần thiết, không thêm fields demo vào dialog. |
| Product surface | [SecurityWorkspaceEditorBindings](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceEditorBindings.cs): “Sửa thông tin…” → `IdentityDialogAsync(true, false)` → `ShowIdentityDialogAsync(true, false)`. **Compatibility reference only**, không sửa dialog trong adoption scope. Áp BC-01–BC-06, BC-08–BC-09 và BC-11–BC-12. |
| Giữ và chuyển thể | Tham khảo field/error wrapping, focus field lỗi đầu, Save chính/Cancel phụ và commands truy cập được ở viewport nhỏ. Product giữ `MutationDialog : BootstrapModal`, dữ liệu user, validation/server feedback, busy/close admission, recovery và originating-owner/session guards hiện hữu. |
| Deviations / ownership | Demo Cancel phục hồi snapshot cục bộ; app Cancel/dirty-close và mutation response tuân contract app, không thay bằng snapshot demo. Không reference demo assembly, field schema/validation hay Gallery metrics. Builder chung còn phục vụ create-user, create-role và edit-role; mọi gap chỉ ghi follow-up để Owner chọn, kể cả ảnh hưởng secret clearing ở create-user. |
| Lab / test owner | Security Lab với production dialog/fake ports; `MyDmsVn.Desktop.Ui.Tests`, [SecurityWorkspaceControlTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs) cho modal/validation/lifetime và [SecurityProductHostParityTests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityProductHostParityTests.cs) cho host parity. Chỉ chọn tests nếu follow-up thật sự thay boundary. |
| Evidence / giới hạn | [Exception evidence](../evidence/ui-labs/20261028-business-compact-exceptions.md) giữ approval modal cũ trong phạm vi cũ. CMP-T05 đối chiếu source tại app `4c05c7598d86b45dbd1bb90c2098733394c063e8`: toolkit fields/validation + focus lỗi đầu, Save Primary/Cancel Secondary, modal/footer và owner/session/recovery guards phù hợp để tham chiếu. **NO-CHANGE**: chưa có gap được chứng minh để chọn follow-up. `MaximumSize=400` của validation và Cancel đóng/admission là khác biệt cần hiểu theo contract app, không tự là lỗi. Long-error wrapping ở mọi viewport/DPI chưa được chứng minh. **NOT RUN** app GUI/native mới; không tuyên bố dialog đạt recipe hoặc Owner visual acceptance mới. |

Nếu không có recipe phù hợp, tiếp tục theo Business Compact và pinned capability map. Chỉ đề xuất recipe sau nhu cầu lặp lại hoặc lỗi phối hợp đáng kể có bằng chứng; không tạo framework chỉ vì thiếu mẫu.


## UIC-03 — Editable Lines → SAL (PLANNED)

| Field | Planned mapping / current constraint |
|---|---|
| Owner | CEX-K01/K03 toolkit; CEX-A01/A02 app |
| Recipe | GridGallery recipe selected by the companion plan; source/test candidate not implemented at activation. Record immutable SHA after K01. |
| Public API | Source-confirmed at runtime pin: grid-owned `BootstrapEditors.CreateLookupBox(Type)` / `CreateDecimalInput(Type, DecimalInputProfile)` and typed `BootstrapControl`. Reconcile every API actually used by frozen recipe before marking compatible. |
| Product surface | [SalesReceiptControl](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs), [SalesReceiptLinesGridAdapter](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs), production instance in [SalesLabForm](../../samples/MyDmsVn.Desktop.UiLab/Sales/SalesLabForm.cs). |
| Preserve | Seven columns/manual price/duplicate rows/Gift; pending versus committed, stable line identity, existing numeric snapshot/patch; keyset/Pending-Applied criteria; List/Detail control identity; Save/Post and unknown-outcome recovery. |
| Deviations | Demo Apply/Restore, fake reference data and sample numeric/derived rules do not replace application contracts. Use existing app session/feedback/validation helpers; do not import Gallery types. |
| Verification route | CEX-A02; native `SalesScenarioBehaviorTests` selectors in [handoff](../development/COMPOSITION_EXPANSION_CODEX_HANDOFF.md); add focused grid/session/host cases only for affected boundaries. |
| State | PLANNED — no new recipe compatibility, runtime/native or Owner acceptance claimed; [new evidence](../evidence/ui-labs/20261030-composition-expansion.md). |

## UIC-04 — Master–Detail → Security Users/Roles (PLANNED)

| Field | Planned mapping / current constraint |
|---|---|
| Owner | CEX-K02/K03 toolkit; CEX-A01/A03 app |
| Recipe | Neutral read-only flat-list/detail in ControlGallery; candidate source not implemented at activation. |
| Product surface | [SecurityWorkspaceControl](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs), existing `Master/Compose` for Users/Roles and their detail views. |
| Preserve | Current layout/identity/filter behavior, native selection/focus/scroll, dirty navigation admission, editor revision and recovery. Product owns hidden-selected-owner policy; sample filter policy does not discard app drafts. |
| Deviations | App role/user detail includes permissions and mutations outside read-only recipe. No broad SEC/identity-dialog redesign; SAL keeps existing List/Detail modes. |
| Verification route | CEX-A03; selected `SecurityWorkspaceControlTests` native methods in handoff; module-specific measured client geometry and evidence. |
| State | PLANNED — NO-CHANGE is a valid pilot outcome after verification; no new app approval at activation. |

## Existing production helpers and CEX decision route

Start with `BusinessCompactAppearance`, `WorkspaceFeedbackHost`, `WorkspaceBusyBinder`, `FieldValidationBinder`, `InlineEditSession<TLine>` and numeric paste helpers, plus Security-local header/master composition. CEX-A04 records concrete consumers and USE AS IS / NOT APPLICABLE / CANDIDATE; NO EXTRACTION is valid. Only the bounded internal caption/editor wrapper in [design §7](EDITABLE_LINES_SAL_ADOPTION.md#7-điều-kiện-helper-và-phạm-vi-đã-cho-phép) is conditionally authorized for extraction. A shared-looking footer does not make permission/session/document/recovery logic generic.
