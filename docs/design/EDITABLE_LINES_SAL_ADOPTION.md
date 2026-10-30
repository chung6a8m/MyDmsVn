# Editable Lines Composition and SAL Adoption — Selected Design

**Selected:** 30/10/2026, theo yêu cầu Owner tạo active roadmap và plan chi tiết cho các đề xuất mở rộng composition. **Implementation:** PLANNED; tài liệu này xác định thiết kế, không ghi nhận runtime/native acceptance.

**Execution owner:** [active app plan](../plans/20261030-002-editable-lines-sal-adoption.md). [Toolkit companion](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/main/docs/plans/20261030-002-editable-lines-master-detail-recipes.md) sở hữu các task toolkit; mỗi task có một nơi cập nhật trạng thái. [Business Compact](BUSINESS_COMPACT.md), [independent Labs](INDEPENDENT_UI_LABS.md) và [test policy](../TEST_POLICY.md) tiếp tục là policy hiện hành.

## 1. Mục tiêu và lựa chọn

Ưu tiên một vòng khép kín: xác định nhu cầu từ SAL → recipe nhập nhiều dòng bằng SourceGrid → mapping vào production control → kiểm chứng trong Sales Lab. Sau đó bổ sung recipe master–detail trung tính và mapping Users/Roles của Security. Kiểm kê/tái sử dụng helper là công việc bắt buộc; trích helper mới chỉ thực hiện khi đạt điều kiện ở §7.

| Phương án đã cân nhắc | Đánh đổi | Lựa chọn |
|---|---|---|
| Master–detail trước, rồi mới grid/SAL | Dễ minh họa cấu trúc danh sách–chi tiết; giá trị bổ sung cho những bề mặt Security đã có composition thấp hơn phần phối hợp editor | Là follow-on gọn trong roadmap này |
| Editable Lines + SAL pilot trước | Kiểm chứng lookup/numeric/cell lifecycle bằng nghiệp vụ đã có và hạ tầng test hiện hữu | Luồng chính được chọn |
| Xây helper/framework trước khi pilot | Phải quyết định public contract khi chưa biết phần lặp ổn định, tăng phạm vi migration | Chỉ đánh giá helper hẹp sau pilot; không chọn framework |

Thành công được đánh giá bằng đường áp dụng có thể lặp lại, tính đúng của interaction và một pilot thật. Không đặt mục tiêu giảm số dòng code, số lượng helper hay phần trăm tiết kiệm thời gian khi chưa có dữ liệu. Pilot không tìm thấy gap có thể kết thúc bằng mapping và evidence NO-CHANGE.

## 2. Baseline và ranh giới đã kiểm tra

| Thành phần | Revision tại kích hoạt |
|---|---|
| MyDmsVn main | `014e82d0013b0a16d73da0b35287d79d5848cc10` — sau PR #66 |
| WinFormsKit main | `25b6c77a4d95d0df112aeba389d61a58ad0ee6db` — sau PR #7 |
| Runtime toolkit của app | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` |
| Recipe source UIC-01/UIC-02 sau correction | `bdac29bf4aca20db6618570ecd9a354630bc2e5f`; handoff `078eb348ac40b7a7e762a9b3f8b4da3fb19f314c` |

UIC-01/UIC-02 và CMP-T01–CMP-T05 giữ completed bounded status của plan cũ. Các native approvals, focused confirmation còn PENDING và SAL acceptance lịch sử giữ nguyên phạm vi; chúng không trở thành task hoặc PASS của roadmap mới.

Source hiện có:

- [SalesReceiptControl](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs) tạo production grid/adapter, filters, First/Previous/Next và chuyển List/Detail trên cùng đối tượng.
- [SalesLabForm](../../samples/MyDmsVn.Desktop.UiLab/Sales/SalesLabForm.cs) khởi tạo chính control đó với scenario ports; không cần bản sao UI.
- [SalesReceiptLinesGridAdapter](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs) có inline edit, numeric paste và validation; [InlineEditSession](../../src/MyDmsVn.Desktop.WinForms/InlineEditSession.cs) đã là helper nội bộ dùng chung.
- [SecurityWorkspaceControl](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs) dùng `Master(...)`/`Compose(...)` cho Users và Roles; [SecurityWorkspaceClose](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceClose.cs) giữ navigation admission.

Thực thi phải đọc lại then-current main/gitlinks và phần delta liên quan. Baseline trên là checkpoint phân tích, không phải lệnh checkout đè các thay đổi mới của Owner.

## 3. Ownership và sản phẩm đầu ra

| Nội dung | Owner | Kết quả được chọn |
|---|---|---|
| UIC-03 — Editable Lines | WinFormsKit / GridGallery | Mẫu trung tính dùng public SourceGrid editor registry, states và focused tests |
| UIC-04 — Master–Detail | WinFormsKit / ControlGallery | Danh sách phẳng + detail chỉ đọc, selection/state/resize/theme rõ ràng |
| SAL pilot | MyDmsVn | Mapping UIC-03 vào `SalesReceiptControl`, native evidence và gap fixes hẹp nếu cần |
| Master–detail product reference | MyDmsVn | Mapping Users/Roles; giữ draft/navigation/recovery hiện có |
| Business Compact và helpers | MyDmsVn | Dùng lại presentation primitives; quyết định helper theo §7 |
| Generic renderer/editor defect | Toolkit, nếu được chứng minh | Ghi reproduction và dependency follow-up riêng; không tự nâng app pin |

UIC-03/UIC-04 là ID mới đã dành cho roadmap, chưa là capability/source đã triển khai. Gallery class, fixture model và helper demo không phải public production API. Product không reference ControlGallery/GridGallery hoặc kế thừa DemoFormBase.

## 4. Contract của UIC-03 khi chuyển thể vào SAL

### 4.1. Những lớp dữ liệu cần phân biệt

1. Raw input: text/caret/range/query đang nhập; có thể chưa hợp lệ.
2. Committed cell value: giá trị đã chấp nhận vào đúng dòng của draft.
3. Saved draft: server đã xác nhận lưu đúng snapshot/version.
4. Posted document: trạng thái nghiệp vụ đã được xác nhận theo contract và recovery.

Cell Escape/Cancel, demo Restore snapshot và app dirty-close là ba thao tác có phạm vi khác nhau. Không suy rollback toàn phiếu từ việc engine đã hủy một editor. Không thay app command admission bằng nút Apply/Restore của Gallery.

### 4.2. API và lifecycle

Tại runtime pin, đọc [editor registry](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/433e5b53f979e37cc6b69bad6dfcde0975c7ecaf/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapSourceGridEditorRegistry.cs), [decimal adapter](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/433e5b53f979e37cc6b69bad6dfcde0975c7ecaf/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapDecimalInputEditor.cs) và [lookup adapter](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/433e5b53f979e37cc6b69bad6dfcde0975c7ecaf/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapLookupBoxEditor.cs).

- Tạo editor qua `grid.BootstrapEditors.CreateLookupBox(Type)` / `CreateDecimalInput(Type, DecimalInputProfile)`; constructors của adapters không phải public factory cho consumer.
- Registry sở hữu editor theo grid; tái sử dụng theo column/configuration trong cùng grid, không tạo editor mỗi cell hoặc dùng chung adapter giữa grids.
- `BootstrapControl` là điểm cấu hình control công khai. Không truy cập private children hoặc reflection để ép caret/theme/height.
- Profile numeric khai báo culture/nullability/precision/range theo contract của caller; không đổi profile trong active edit.
- SourceGrid sở hữu BeginEdit/EndEdit/navigation/conversion. Product adapter sở hữu admission, validation mapping, line/document identity và setter nghiệp vụ.
- Theme/resize không tạo lại document/control/adapter. Dispose hoặc chuyển document đã được chấp nhận phải vô hiệu hóa callback trước khi context cũ bị thay thế.

Source API reconciliation không tự chứng minh native tương thích. Evidence phải ghi recipe source SHA, toolkit runtime SHA, app source/test SHA riêng.

### 4.3. Các tình huống bắt buộc của pilot

| Tình huống | Kết quả cần quan sát |
|---|---|
| Gõ lookup từng phím; A/B hoàn tất ngược thứ tự | Raw text/caret/focus đúng; stale response không ghi vào committed identity, unit hoặc dòng khác |
| Numeric invalid rồi Save/Post | Raw input còn nguyên, đúng ô invalid/focus, không dispatch mutation |
| Numeric valid rồi bấm command khi editor còn mở | Chỉ commit một lần; payload chứa giá trị cuối; xét dirty/Post sau commit |
| Hai dòng cùng Product, gồm dòng mới chưa có server ID | Dòng có identity độc lập; thao tác/validation không gắn theo ProductId hoặc số thứ tự |
| Add/Remove/Back trong active edit | Đi qua intent/admission hiện có; từ chối thì giữ input; không làm orphan callbacks |
| Numeric paste hợp lệ/không hợp lệ | Dùng snapshot/planner/patch hiện có; không biến thành vòng gán cell từng phần |
| Save error hoặc Post chưa rõ kết quả | Feedback thuộc đúng workspace; dữ liệu/recovery giữ đúng contract; Post không được replay |
| Theme, viewport nhỏ, dispose | Command/identity/error truy cập được; control identity và input không bị dựng lại; tài nguyên thuộc owner được giải phóng |

Các quy tắc demo như tự điền giá hoặc tự sửa quantity không được chuyển vào SAL. Cột boolean của mẫu chỉ là minh họa; Gift của SAL vẫn do application/domain tính.

## 5. Contract SAL phải giữ

- Giữ bảy cột: STT, Product, Unit, Quantity, UnitPrice, Gift, Amount. UnitPrice nhập thủ công; duplicate Product rows độc lập. Gift giữ giá, doanh thu bằng 0, đầy đủ quantity/cost.
- Moving average ở actual Post; posted valuation bất biến. Không thêm thu tiền, công nợ, trả hàng, pricing/tax workflow.
- Giữ LINQ to DB, Product `Any`, `SaleId DESC`, opaque validated cursors và First/Previous/Next. Không OFFSET, total count bắt buộc hoặc numbered-page jump. Tổng phiếu tính từ toàn bộ dòng của phiếu được chọn, không chỉ dòng khớp Product filter.
- `SalesReceiptListViewModel` giữ Pending/Applied criteria, item/cursor/selection snapshot và cơ chế lỗi/recovery. Presentation không chiếm quyền sở hữu cursor hoặc tự query ngoài ViewModel.
- `SalesReceiptEditorViewModel.CommandAsync` commit pending buffers trước khi xét dirty và snapshot. Draft chưa Save/đang dirty phải Save trước Post; không tự ghép Save+Post.
- Giữ `RequiresCreateOutcomeReview`, `RequiresPostOutcomeReload`, optimistic version và authoritative reload cùng SaleId; busy visual không thay command guards.
- Giữ List ⇄ Detail hiện hữu trên cùng control/editor/adapter. Không ép SAL thành hai pane đồng thời để giống recipe master–detail.
- Dùng `WorkspaceFeedbackHost`, busy binding và inline validation hiện có. Error/recovery cần sự chú ý phải còn truy cập được trong workspace tương ứng.

Nguồn quyết định là [AGENTS](../../AGENTS.md), [list VM](../../src/MyDmsVn.Desktop.Application/SalesReceiptListViewModel.cs), [editor VM](../../src/MyDmsVn.Desktop.Application/SalesReceiptEditorViewModel.cs), [query service](../../src/MyDmsVn.Server.Infrastructure/Sales/SqlSalesReceiptQueryService.cs), và [UI Lab runbook](../development/UI_LAB.md).

## 6. UIC-04 và Security master–detail

UIC-04 minh họa danh sách đối tượng → detail chỉ đọc; nó không chứa document header–lines, permission editor, dirty-save coordinator hoặc async service framework. Dữ liệu/transition giả lập cần xác định được để test.

Mapping Users/Roles phải giải thích selection key, master search/activity, identity header, detail và các state. Layout primitives native hợp lệ; Business Compact sở hữu kích thước/typography sản phẩm. Đọc `Compose(...)` hiện có trước khi cân nhắc sửa hoặc trích helper.

Khi đổi đối tượng, giữ điểm navigation admission, editor identity/revision và recovery guard. Khi filter ẩn selected row, không tự hủy draft hoặc áp chính sách selection demo vào sản phẩm. Khi bị từ chối navigation, giữ đối tượng/draft/focus hiện tại; không đổi selection rồi mới phát hiện mất dữ liệu.

Nguồn/source-only inventory không đủ kết luận defect layout. A03 có thể kết thúc NO-CHANGE; mọi gap sửa trong A03 phải nằm ở composition Users/Roles đã chọn. Mutation dialogs, permission semantics và broad SEC redesign không thuộc scope này.

## 7. Điều kiện helper và phạm vi đã cho phép

### 7.1. Kiểm kê bắt buộc

Ghi purpose/call sites/ownership của `BusinessCompactAppearance`, `WorkspaceFeedbackHost`, `WorkspaceBusyBinder`, `FieldValidationBinder`, `InlineEditSession<TLine>`, `ReceiptNumericPasteTargets`, `ReceiptNumericPastePlanner`, `SecurityHeaderLayout` và `Master/Compose` của Security. Ghi USE AS IS / NOT APPLICABLE / CANDIDATE cùng lý do; không ép grid vào field-validation binder nếu contract khác.

### 7.2. Điều kiện trích helper mới

Chỉ trích khi có ít nhất hai production consumers độc lập cùng cần contract, hoặc một lỗi generic cụ thể đã được tái hiện; có thể viết ngắn inputs/ownership/layout/focus/keyboard/reentrancy/disposal; và test được ở mọi consumer bị ảnh hưởng. Hai lời gọi Users/Roles trong một constructor không tự tạo yêu cầu public toolkit API.

Candidate được phép đánh giá trong roadmap: caption/editor composition của `GoodsReceiptControl.CompactField(string, Control, int)` và `SalesReceiptControl.Field(string, Control)`. Nếu cả hai cần cùng contract sau pilot, được phép trích một helper `internal` nhỏ trong Desktop.WinForms và migrate đúng hai consumer/local factories (gồm các lời gọi field của chúng). Tên/file/API mới phải được ghi vào evidence trước code; giữ control instance, TabIndex, scaling và ownership, không tạo field schema/binding DSL. Nếu khác biệt vẫn có chủ đích, giữ hai local methods và ghi NO EXTRACTION.

Phạm vi điều kiện này cho phép implementer quyết định kỹ thuật từ evidence; không yêu cầu hỏi lại Owner cho từng rename/helper nhỏ. Public toolkit API, generic grid controller, migration diện rộng, namespace change hoặc runtime pin upgrade cần follow-up riêng; tiếp tục phần không bị phụ thuộc khi gặp gap đó.

### 7.3. Các lớp cần giữ tại product

Permission/session, document generations, dirty-close, server validation mapping, numeric domain rules và unknown Post recovery giữ tại app. Palette/font/metrics Business Compact giữ tại product, dùng public theme API. SourceGrid engine và toolkit composite internals không được sửa trong scope app.

## 8. Verification và completion

- Toolkit kiểm recipe theo task owner trên `net48;net8.0-windows`; product giữ `net48`. Dùng test projects/STA helpers hiện hữu.
- Chọn tests theo changed boundary; source-only/docs-only không chạy GUI/SQL. UI pilot dùng native Windows và fake ports, không chứng minh SQL/accounting/real Login/package/release.
- Dùng Light/Dark, physical100/125 và normal/constrained client geometry thực đo. Không lấy outer form làm workspace; không đổi font hoặc resize screenshot thay physical DPI.
- Matrix đại diện: default state tại cả theme/scale/size; các tình huống khó tập trung viewport nhỏ/125 và những key paths bị đổi. Không nhân mọi state với mọi tổ hợp mà không có rủi ro tương ứng.
- Candidate visual mới cần dated Owner decision tại task; no-change mapping dùng đúng evidence còn áp dụng và ghi observations mới, không tạo quyết định visual giả. K03 handoff tách theo recipe để SAL không phải chờ UIC-04.
- Kết thúc bằng clean-context routing review: bắt đầu từ AGENTS/AI_CONTEXT/index, tìm đúng sample, pin, production surface và focused tests mà prompt không nhắc tên recipe.

Không đóng roadmap bằng build/source review đơn thuần. App plan và toolkit plan ghi rõ task chưa chạy, môi trường bị thiếu, native decision pending hoặc dependency gap; evidence cũ không được sửa để biến thành PASS mới.
