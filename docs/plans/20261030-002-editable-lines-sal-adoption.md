# Editable Lines Composition and SAL Adoption — Active Roadmap

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Preserve the Owner-selected scope and repository policy.

**Goal:** Tạo đường áp dụng recipe SourceGrid nhập nhiều dòng vào SAL, bổ sung master–detail reference cho Security và chỉ trích helper production khi có contract chung đã được chứng minh.

**Architecture:** WinFormsKit sở hữu UIC-03/UIC-04 trong Gallery với dữ liệu trung tính và public APIs hiện hữu. MyDmsVn sở hữu Business Compact, mapping, SAL/SEC production pilots, nghiệp vụ/session/validation/recovery và quyết định helper. Recipe source SHA độc lập với app runtime gitlink; không reference demo assemblies.

**Tech Stack:** C# / WinForms; MyDmsVn và active tests `net48`; WinFormsKit `net48;net8.0-windows`; SourceGrid adapters/registry hiện có; xUnit ở app, NUnit ở toolkit.

**Spec:** [Selected design](../design/EDITABLE_LINES_SAL_ADOPTION.md), [Business Compact](../design/BUSINESS_COMPACT.md), [Independent UI Labs](../design/INDEPENDENT_UI_LABS.md), [toolkit companion plan](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/main/docs/plans/20261030-002-editable-lines-master-detail-recipes.md).

**Status:** ACTIVE — được Owner chọn ngày **30/10/2026**; các task runtime dưới đây bắt đầu ở PLANNED. Commit kích hoạt chỉ thay Markdown, không có source implementation, GUI/SQL PASS hoặc native approval mới. [Evidence mới](../evidence/ui-labs/20261030-composition-expansion.md) ghi riêng kết quả sau này. CMP-T01–CMP-T05 và các acceptance lịch sử không đổi.

## Global Constraints

- Baseline app `014e82d0013b0a16d73da0b35287d79d5848cc10`; toolkit main `25b6c77a4d95d0df112aeba389d61a58ad0ee6db`; runtime gitlink app `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`. Re-read then-current source/delta trước thực thi.
- Giữ product `net48`, WinFormsKit `net48;net8.0-windows`, namespaces/assembly identities và recursive vendor pins. Không nâng dependency chỉ để đọc recipe.
- Không đổi DTO/API, query/keyset, pricing/costing, permissions, session/draft, Save/Post hoặc recovery contract. Không chọn SQL/migration, actual Login, package/release hoặc broad SEC redesign.
- UIC-03 Editable Lines dùng GridGallery; UIC-04 Master–Detail dùng ControlGallery. Proposed names/tests chưa tồn tại không được mô tả là public API hay evidence đã chạy.
- Dùng sourcegrid editor registry, public capabilities và product helpers hiện hữu; không tạo per-cell editors, cross-grid shared adapter, private-child theme walker, schema renderer hoặc generic document controller.
- Giữ List/Detail trên cùng SAL control/editor/adapter; giữ Users/Roles composition/navigation của Security. Native layout primitives hợp lệ.
- Helper assessment bắt buộc; extraction chỉ theo design §7, giới hạn app-internal caption/editor wrapper của Goods Receipt/SAL nếu chứng minh cùng contract. Không có quota phải tạo helper.
- UI Labs khởi chạy độc lập và dùng production controls/fake ports. Source review/fake ports không chứng minh Server/SQL hoặc production release.
- GUI chạy Windows desktop tương tác, STA tuần tự, message loop/guard/finite cleanup hiện hữu. Dùng [sole test policy](../TEST_POLICY.md); không thêm manifest/runner/approval files.
- Markdown UTF-8 không BOM; working tree CRLF và Git text LF theo attributes. Giữ tiếng Việt có dấu, historical evidence và các quyết định đang PENDING.

## Review Focus

1. Invalid/pending text khi click Save/Post hoặc Back phải còn nguyên và không dispatch trái phép — CEX-A02.
2. Duplicate Product, ID-zero rows và late lookup phải gắn đúng line/document/session — CEX-A02.
3. Filter criteria/cursor khác snapshot hiện tại không được điều hướng sai hoặc biến lỗi thành empty-success — CEX-A02.
4. Viewport nhỏ/125%, theme switch và Users/Roles filter phải giữ nhận diện, focus, command accessibility và control identity — CEX-A02/A03.
5. Dirty/unknown-Post/close/dispose và helper migration không được bỏ qua ownership/admission hoặc tái tạo editor — CEX-A02/A04.

---

## 0. Ownership, dependencies và trạng thái

| Task | Status owner | Trạng thái | Dependency / đầu ra |
|---|---|---|---|
| CEX-K01 — UIC-03 Editable Lines + focused tests | Toolkit companion | Xem toolkit owner | Source mẫu/guide/APIs; source preparation độc lập với app |
| CEX-K02 — UIC-04 Master–Detail + focused tests | Toolkit companion | Xem toolkit owner | Mẫu read-only; dùng format recipe của K01 |
| CEX-K03 — Native review/handoff theo từng recipe | Toolkit companion | Xem toolkit owner | Handoff UIC-03 và UIC-04 riêng, không chờ chung mới bàn giao |
| CEX-A01 — Inventory và API reconciliation | Plan này | PLANNED | Có thể bắt đầu ngay; mapping dự kiến/source capabilities và boundaries |
| CEX-A02 — SAL pilot và sửa gap hẹp | Plan này | PLANNED | A01 + frozen K01 source; completion dùng handoff UIC-03 của K03 |
| CEX-A03 — Security master–detail reference | Plan này | PLANNED | A01 + K02 source/handoff UIC-04; ưu tiên sau pilot SAL đầu tiên |
| CEX-A04 — Helper assessment và extraction có điều kiện | Plan này | PLANNED | A02; dùng A03 nếu candidate liên quan master–detail; không chặn K01/A02 |
| CEX-A05 — Routing review và completion handoff | Plan này | PLANNED | A02/A03/A04 + hai recipe handoffs K03 |

K01 source/focused results cho phép A02 chuẩn bị mapping và native pilot. A02 không chờ UIC-04 hoặc aggregate K03 completion; khi đóng A02 phải dẫn đúng per-UIC-03 handoff và limits. K03 Owner decision pending không được ghi thành approved recipe. A03 chuẩn bị source inventory song song, nhưng GUI trên cùng desktop luôn tuần tự.

Mỗi task owner cập nhật một status record. Entry pages chỉ dẫn tới owner. Nếu môi trường thiếu Windows, tiếp tục docs/source reconciliation không phụ thuộc native, ghi BLOCKED / NOT RUN cho phần cần native và giữ task tương ứng chưa COMPLETE.

## 1. File map

| Vai trò | Files |
|---|---|
| Plan/spec/handoff | `docs/plans/20261030-002-editable-lines-sal-adoption.md`; `docs/design/EDITABLE_LINES_SAL_ADOPTION.md`; `docs/development/COMPOSITION_EXPANSION_CODEX_HANDOFF.md` |
| Mapping/evidence | `docs/design/UI_COMPOSITION_MAPPING.md`; `docs/evidence/ui-labs/20261030-composition-expansion.md` |
| Routing | `AGENTS.md`, `AI_CONTEXT.md`, `README.md`, `docs/README.md`, `docs/plans/README.md`; selection paragraph của `docs/TEST_POLICY.md` |
| Existing skill | `.agents/skills/mydmsvn-winforms-ui/SKILL.md` — chỉ sửa nếu A05 chứng minh route còn thiếu; mapping hiện có là entry point |
| SAL production seams | `src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs`, `SalesReceiptLinesGrid.cs`, `SalesReceiptLinesGridAdapter.cs`; source ViewModels là contract reference, không mặc định cần sửa |
| SAL Lab | `samples/MyDmsVn.Desktop.UiLab/Sales/SalesLabForm.cs`; `samples/MyDmsVn.Desktop.UiLab.Scenarios/` chỉ bổ sung fixture/scenario nếu selected observation chưa có |
| Security seams | `src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs`, `SecurityPresentation.cs`, `SecurityHeaderLayout.cs`, `SecurityUserDetailView.cs`, `SecurityRoleDetailView.cs`; navigation/recovery là contract reference |
| Conditional helper | `GoodsReceiptControl.cs:CompactField`, `SalesReceiptControl.cs:Field`; helper mới chỉ tạo sau A04 decision record |
| Existing test owners | `tests/MyDmsVn.Desktop.Ui.Tests/`, `tests/MyDmsVn.Desktop.Tests/`, `tests/MyDmsVn.Desktop.Session.Tests/`; exact selectors tại handoff §4 |

File map là khu vực cần đọc/được sửa theo gap; không yêu cầu chạm mọi file. Không tạo artifact/test class chỉ để có diff hoặc lặp lại assertions đã đủ.

## 2. CEX-A01 — Inventory và compatibility checkpoint

**Files:** Modify mapping/evidence; đọc design, actual gitlinks, SAL/SEC seams, pinned toolkit adapters và helper call sites.

**Consumes:** baseline/dependency tree thực tế; K01/K02 có thể chưa được implement; public APIs tại runtime pin.

**Produces:** bảng mapping cho UIC-03/UIC-04, phân biệt `PLANNED`, `SOURCE CONFIRMED`, `PILOT VERIFIED`; source/runtime/source-test revisions; candidate helper inventory. Không sản xuất production API mới.

- [ ] **Step 1 — Ghi baseline thực.** Đọc instructions/index/test policy; lấy app HEAD và recursive gitlinks. So sánh delta với §Global Constraints. Ghi toolchain/target dự kiến và phần source đang làm việc; không dùng main floating URL như candidate SHA.
- [ ] **Step 2 — Đối chiếu API tại pin.** Xác nhận registry `CreateLookupBox(Type)`, `CreateDecimalInput(Type, DecimalInputProfile)`, `BootstrapControl` và ownership. Đọc constructors/access modifiers/profile edit guard. Ghi public capability confirmed by source; recipe mới chỉ marked compatible sau khi đọc chính frozen recipe và đối chiếu mọi API nó dùng.
- [ ] **Step 3 — Map SAL.** Vẽ bảng vùng filter/list/cursor, metadata, inline grid, summaries, command/feedback. Ghi production controls, application owner, Lab và exact test owner. Inventory `StatusFilter`, `PageSizeEditor`, `OutcomeSaleIdEditor` theo BC-08; native control trong source không tự là defect hoặc lệnh thay thế.
- [ ] **Step 4 — Map master–detail.** Đọc Users/Roles `Master/Compose` cùng `AdmitNavigationAsync`; phân biệt list–detail và header–lines. Ghi lựa chọn filter ẩn selected owner và width rule theo source. Không áp số geometry của Security vào SAL.
- [ ] **Step 5 — Lập helper inventory.** Theo design §7, liệt kê consumer/contract; ghi rõ những phần đã dùng chung và những khác biệt có chủ đích của hai field wrappers.
- [ ] **Step 6 — Verify/commit checkpoint.** Kiểm local/remote links, planned/existing/API labels, immutable source refs, runtime pin không đổi, status và encoding. Commit docs checkpoint. Không chạy GUI/SQL cho A01 docs-only.

**Exit:** implementer có thể chỉ ra exact source/template/API/runtime/surface/test owner mà không đoán. Pending K01/K02 được ghi như dependency, không là FAIL của capability inventory và không được coi là sample đã tồn tại.

## 3. CEX-A02 — SAL pilot và gap fixes có căn cứ

**Files:** Mapping/evidence; `SalesReceiptControl.cs`, `SalesReceiptLinesGridAdapter.cs` chỉ khi gap; `SalesScenarioBehaviorTests.cs`, `SalesReceiptGridTests.cs`, `SalesReceiptControlAdmissionTests.cs`, `SalesReceiptControlTests.cs` cho boundary bị đổi. Session VM tests đọc/chạy khi list/command contract cần chứng minh; không thay business logic để làm sample giống app.

**Consumes:** A01 inventory; K01 frozen recipe source/guide/public API map; UIC-03 K03 evidence theo từng candidate. Registry và `SalesReceiptControl` là APIs/source đã tồn tại, không tạo wrapper control mới.

**Produces:** mapping UIC-03 đã xác nhận trên production SAL, gap disposition cụ thể, native evidence và dated Owner decision nếu có candidate visual mới; NO-CHANGE là terminal hợp lệ.

- [ ] **Step 1 — Đọc recipe và lập deviations trước code.** Ghi những phần giữ từ mẫu (layout/editor coordination), những phần sản phẩm sở hữu (Gift, manual price, fixed-point totals, permissions, draft, Save/Post/recovery). Không chuyển auto-price/quantity-normalization/demo Apply/Restore vào SAL.
- [ ] **Step 2 — Thiết lập môi trường Lab thật.** Chạy launcher Sales độc lập, xác nhận `SalesLabForm` tạo production `SalesReceiptControl` với fake ports. Dùng đúng product theme/bootstrap. Đo outer shell → module client → control/grid usable area cho SAL ở normal/minimum và physical100/125; đưa giá trị đo vào fixed Lab viewport. `952×657` là Lab default, `652×497` là SEC baseline, `944` là grid test host: không tự dùng làm số nghiệm thu SAL.
- [ ] **Step 3 — Discovery và baseline native hẹp.** Dùng chín exact existing methods ở handoff §4.1 cho S01/S02/S03/S05/S06; ghi discovered method/Theory rows thực tế rồi chạy cùng selector trên Windows/STA tuần tự. Các tests này là native, không gọi là headless unit tests. Dùng session methods ở §4.2 khi chứng minh list/command projection; chỉ mở rộng theo boundary.
- [ ] **Step 4 — Quan sát interaction/visual.** Chạy chuỗi hai duplicate rows → edit numeric/lookup → invalid Save → sửa hợp lệ → Save → dirty Back/Close → unknown Post → authoritative reload. Check raw text/caret/range/focus, line identity, request count/payload và feedback owner; kiểm Gift/summary bằng calculator sẵn có. Theme switch lúc editor/popup còn mở; caption/error dài tại constrained viewport. Nếu chạm paste, dùng `TryPasteNumeric`/snapshot/patch hiện hữu và quan sát all-or-nothing.
- [ ] **Step 5 — Chọn nhánh NO-CHANGE hoặc GAP.** Không có gap: ghi source/runtime không đổi, những observations đã thực hiện và evidence cũ còn áp dụng đúng boundary. Có gap: ghi thao tác tái hiện, expected/actual, owner seam và regression case trước khi sửa. Không dùng screenshot/source suspicion đơn lẻ làm kết luận lỗi.
- [ ] **Step 6 — Với GAP, pin regression và sửa tối thiểu.** Chọn existing failing case nếu đã đủ; nếu thiếu, thêm test hành vi ở đúng owner, chứng minh FAIL do gap rồi sửa layout/binding tại seam đã ghi. Assertions phải giữ raw input/line identity/dispatch count hoặc geometry cụ thể; không thêm test chỉ kiểm tên control/cấu trúc code. Giữ pending criteria/cursor, buffer-before-dirty và unknown outcome contracts. Không tạo public toolkit API hoặc nâng pin trong nhánh này.
- [ ] **Step 7 — Verify changed boundaries.** Chạy lại selectors bị ảnh hưởng và case mới, verify TRX không rỗng/không skip required cases. Nếu sửa app shared presentation thì bổ sung consumer/parity affected; nếu chỉ một grid layout thì không chạy toàn SQL/historical suites. Re-observe normal/constrained, Light/Dark, physical100/125 cho candidate thay đổi. Khi sửa source sau capture, ghi validity delta hoặc capture lại phần bị ảnh hưởng.
- [ ] **Step 8 — Chốt evidence và commit.** Ghi source/test/runtime/recipe SHAs, environment/geometry, observations, outcome, exceptions. Candidate visual mới: trình evidence cho dated Owner decision; trước decision ghi PENDING, không dừng các task docs/source độc lập. NO-CHANGE: không phát minh approval mới. Chỉ đóng A02 khi evidence và per-UIC-03 handoff đủ cho bounded scope.

**Acceptance assertions:** local invalid input không dispatch Save/Post; valid buffer có trong Save payload; duplicate rows độc lập; stale lookup/session không thay data; denied navigation giữ draft/raw input; unknown Post không replay; visible summaries/commands/identity đúng owner. Read-only/Posted/busy/access-denied vẫn dùng command guards thật.

## 4. CEX-A03 — Master–detail mapping trên Security

**Files:** Mapping/evidence; Security workspace/presentation/header/user/role files chỉ khi gap. Tests: selected `SecurityWorkspaceControlTests` methods trong handoff §4.3; pane-state/parity only if changed boundary.

**Consumes:** K02 UIC-04 source và per-recipe K03 handoff; A01 Users/Roles inventory; BC policy và Security module contract.

**Produces:** mapping UIC-04 → Users/Roles với deviations, source/geometry/keyboard/selection evidence; không mở rộng sang permission/identity dialog redesign.

- [ ] **Step 1 — Đọc sample và current product composition.** Đối chiếu native layout primitives, search/activity/status, master identity và detail header. Giữ `Master/Compose` hiện có nếu đáp ứng; không tạo base class cho hai call sites trong một module.
- [ ] **Step 2 — Chốt selection/admission mapping.** Ghi source owner của selected UserId/RoleId, behavior khi filter ẩn owner, dirty role draft và `AdmitNavigationAsync`. Chính sách lọc của sample không được thay product draft policy.
- [ ] **Step 3 — Chạy focused discovery/execution.** Chọn năm exact methods §4.3; đây là native STA trong Ui.Tests. Quan sát hierarchy dài, selection/focus/scroll/theme, thay User/Role và rejected navigation trên production Security Lab. Đo geometry module riêng.
- [ ] **Step 4 — NO-CHANGE hoặc regression/fix.** NO-CHANGE ghi evidence phù hợp. Nếu gap, dùng failing behavior test rồi sửa riêng composition; giữ draft/save/recovery và native master identity. Candidate visual mới cần Owner decision trong evidence; không đóng các acceptance lịch sử khác.
- [ ] **Step 5 — Commit mapping/handoff.** Cập nhật recipe source/runtime/app test SHA, scope/limits và A03 status. Không chờ hoặc mở lại SQL/permission integration để đóng một mapping/visual scope.

**Exit:** recipe tìm được và áp đúng cho Users/Roles; product policy selection/dirty/admission vẫn có owner rõ; mẫu không biến thành framework hoặc yêu cầu đổi SAL mode.

## 5. CEX-A04 — Helper decision và extraction có điều kiện

**Files:** Evidence/design inventory; conditional `GoodsReceiptControl.cs`, `SalesReceiptControl.cs` và một internal helper/test file chỉ nếu decision EXTRACT. Public API/helper name mới ghi như proposed trước code.

**Consumes:** A02 observations, existing helper call sites; A03 nếu đánh giá master–detail candidate. `CompactField(string, Control, int)` và `Field(string, Control)` là exact current methods.

**Produces:** decision `NO EXTRACTION` hoặc `EXTRACT INTERNAL FIELD WRAPPER` với contract/call sites/evidence. Cả hai là đầu ra hợp lệ; task không tạo public toolkit API.

- [ ] **Step 1 — So sánh contract thực.** Ghi Goods Receipt TableLayoutPanel/Dock/TabIndex/margin và SAL FlowLayoutPanel/width/height/caption khác nhau. Đánh giá những khác biệt đã được pilot chứng minh có chủ đích hay cần thống nhất. Đọc helper đã tồn tại trước khi đề xuất mới.
- [ ] **Step 2 — Ghi quyết định kiểm chứng được.** Không có common contract nhỏ: giữ local helpers, ghi lý do và consumer mapping, đi Step 5. Nếu có: ghi exact proposed filename/type/method signature, inputs và ownership trong evidence trước code; helper chỉ dựng caption/editor wrapper, giữ editor instance và semantics, không chứa binding/permissions/document state. Scope cho phép đúng hai consumer Goods Receipt/SAL.
- [ ] **Step 3 — Nếu EXTRACT, thêm meaningful behavior coverage.** Dùng hai production consumers để assert editor identity, caption/Tab order, focus sau navigation từ chối, scaling/reflow và disposal không làm mất editor sớm. Add tests trong `tests/MyDmsVn.Desktop.Tests/` chỉ cho behavior chưa có coverage; existing four guards tại §4.4 bảo vệ header/filter semantics nhưng không thay test layout/tab order nếu thay đổi.
- [ ] **Step 4 — Implement và verify hai consumers.** Trích nhỏ, migrate đúng call sites, chạy focused new/layout tests và four guards §4.4 trên Windows/STA. Kiểm theme/physical100/125 cho phần geometry bị tác động; không đổi common palette hoặc private composite internals. Nếu interface cần module flags/ViewModel/transaction ownership, quay về NO EXTRACTION và giữ contract local.
- [ ] **Step 5 — Commit decision hoặc implementation.** Record actual tests/observations nếu có source change; NO EXTRACTION chỉ cần docs diff/link/status/encoding. Nếu có candidate visual mới, cập nhật Owner decision scope cùng candidate app; A02/A03 evidence trước helper phải được đánh giá validity lại cho vùng bị tác động.

**Exit:** helper reuse rõ, không có abstraction hình thức. Native/generic defect cần upstream/pin được ghi follow-up, không làm âm thầm tăng scope hoặc chặn các kết quả không phụ thuộc.

## 6. CEX-A05 — Clean routing và final handoff

**Files:** Mapping/evidence/plan/index; `.agents/skills/mydmsvn-winforms-ui/SKILL.md` chỉ nếu route gap được chứng minh; runbook/handoff khi commands/source changed.

**Consumes:** A02/A03/A04 outcomes và toolkit handoffs theo từng recipe. No-source-change và NO EXTRACTION được giữ đúng nghĩa, không đổi thành implementation claim.

**Produces:** bounded completion record, đường đọc sạch cho task UI kế tiếp và exact source references. Không tạo release acceptance.

- [ ] **Step 1 — Fresh-context routing review.** Giao một agent mới prompt thông thường ở handoff §3, không nhắc UIC IDs/file paths/recipe names. Chỉ cho repository instructions bình thường; ghi actual route/queries/files đọc. Agent phải tìm mapping, pinned APIs, đúng production control/Lab và focused tests.
- [ ] **Step 2 — Sửa route khi có gap.** Ưu tiên mapping/entry links. Nếu skill cần bổ sung, chỉ route ngắn tới sources và selected plan, tuân skill editing requirements; không nhét business specification/test matrix vào SKILL hoặc tạo personal skill mới.
- [ ] **Step 3 — Reconcile task evidence.** Đối chiếu từng acceptance assertion và source changes; kiểm recipe/public API/helper distinctions, same candidate/native/Owner decisions, actual discovery/execution counts và boundaries. Đánh giá ảnh hưởng A04 lên evidence A02/A03 nếu có extraction.
- [ ] **Step 4 — Verify docs và final source state.** Kiểm links/anchors, path/symbol/command validity, task owners/status, no historical reactivation, UTF-8/CRLF, git diff và runtime gitlink. Không chạy full suites/SQL cho handoff docs-only.
- [ ] **Step 5 — Commit và đóng bounded scope.** Chỉ COMPLETE khi checklist §7 đạt; nếu native decision còn PENDING, ghi phần đã hoàn thành và giữ scope OPEN tại owner. Refresh entry pages dẫn tới next task/status; không sửa historical approvals.

## 7. Tiêu chí hoàn thành toàn scope

- [ ] UIC-03 và UIC-04 có source/guide/focused tests/native evidence với per-recipe decision tại toolkit owner.
- [ ] A01 reconcile mọi API mà frozen samples thực sự dùng với runtime pin; không reference demo assembly hoặc đổi gitlink vì examples.
- [ ] SAL production pilot đạt acceptance assertions của A02, với gap fixes hẹp hoặc NO-CHANGE có evidence.
- [ ] Users/Roles có mapping/master–detail evidence và giữ navigation/draft contracts.
- [ ] Helper inventory/decision hoàn tất; nếu extraction, cả hai consumers được verify và candidate evidence cập nhật.
- [ ] Clean-context route tìm đúng recipe/pin/Lab/test scope; existing UIC-01/UIC-02 routes vẫn đúng.
- [ ] Revisions, native results, Owner decisions và limitations thống nhất; không còn task bắt buộc hoặc selected candidate decision chưa giải quyết được đánh dấu COMPLETE.

## 8. Thực thi và review cadence

Dùng [Codex handoff](../development/COMPOSITION_EXPANSION_CODEX_HANDOFF.md) để chạy từng task. Commit riêng A01 checkpoint, mỗi gap fix, helper nếu có và final handoff. Toolkit samples/acceptance và product adoption là các PR/commit dễ review riêng; merge source không tự đóng native/Owner tasks.

Có thể chuẩn bị source/docs hai repo song song; mutation vào shared integration files và native GUI chạy tuần tự. Khi gặp API thiếu tại pin, tạo reproduction/decision record với precise gap, tiếp tục phần unaffected; không edit vendor trực tiếp hoặc đẩy commit lên app với pin mới ngoài phạm vi.
