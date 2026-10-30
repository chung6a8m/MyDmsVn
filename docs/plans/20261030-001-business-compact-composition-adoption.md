# Business Compact Composition Adoption — Completed Bounded Roadmap

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking. Follow repository instructions and the Owner-selected scope.

**Goal:** Tạo hai mẫu bố cục WinForms chạy được, đưa chúng vào đường đọc của skill mydmsvn-winforms-ui, và chứng minh việc áp dụng trên một bề mặt sản phẩm có thật.

**Architecture:** WinFormsKit sở hữu các ví dụ trung tính trong ControlGallery và hợp đồng sử dụng mẫu. MyDmsVn sở hữu Business Compact, mapping mẫu vào màn hình sản phẩm, skill và bằng chứng UI Lab. Các mẫu chỉ dùng public APIs của UI core; sản phẩm tiếp tục dùng production controls cùng toolkit được pin và không reference assembly demo.

**Tech Stack:** C#, WinForms; WinFormsKit net48 và net8.0-windows; MyDmsVn net48; NUnit của toolkit và xUnit của UI Lab hiện hữu.

**Spec:** Thiết kế đã được Owner chọn ở mục 2 của tài liệu này, cùng [Business Compact policy](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/docs/design/BUSINESS_COMPACT.md) và [Independent UI Labs](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/docs/design/INDEPENDENT_UI_LABS.md).

**Status:** COMPLETE — bounded composition adoption, **30/10/2026**. CMP-T03–CMP-T05 đạt deliverables tại §7; [handoff evidence](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t05--routing-review-and-handoff) ghi clean-context routing, UIC-02 no-change và đối chiếu từng tiêu chí. Toolkit bàn giao correction accessibility recipes tại source/test `bdac29bf4aca20db6618570ecd9a354630bc2e5f`, docs/status/evidence `078eb348ac40b7a7e762a9b3f8b4da3fb19f314c`; visual candidate `1420f25` và approval của nó giữ nguyên như lịch sử riêng. CMP-T01/T02 có implementation, focused verification và bounded representative attended PASS tại owner toolkit. Product pilot giữ source/runtime; không tạo candidate visual hay quyết định Owner visual mới cho app. Completion này không cấp release hoặc acceptance lịch sử còn mở.

## 0. Roadmap hiện hành và quyền sở hữu trạng thái

[Execution index](README.md) chọn plan này cho scope composition adoption. [Plan toolkit tại handoff correction](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/078eb348ac40b7a7e762a9b3f8b4da3fb19f314c/docs/plans/20261030-001-ui-composition-recipes.md) là owner duy nhất của CMP-T01/CMP-T02; plan này sở hữu CMP-T03/CMP-T04/CMP-T05 và điều phối điều kiện hoàn thành toàn scope. Các entry pages chỉ dẫn đến owner, không duy trì bảng tiến độ khác.

| Task | Owner trạng thái | Trạng thái tại owner này | Điều kiện bắt đầu hoặc hoàn tất |
|---|---|---|---|
| CMP-T01 — UIC-01 Filter Results | WinFormsKit companion plan | Xem owner toolkit | Bước triển khai đầu tiên; tạo sample, recipe và focused evidence |
| CMP-T02 — UIC-02 Compact Edit Form | WinFormsKit companion plan | Xem owner toolkit | Dùng contract recipe của CMP-T01; độc lập với nghiệp vụ app |
| CMP-T03 — Skill và mapping | MyDmsVn, plan này | COMPLETE — docs/skill routing | Mapping UIC-01/UIC-02 và runtime API reconciliation hoàn tất; checkpoint UIC-01 mở đầu vào CMP-T04, không cấp app acceptance |
| CMP-T04 — Catalog product pilot | MyDmsVn, plan này | COMPLETE — no source/runtime change | Mapping xác nhận qua production Lab, focused GUI 20/20 và evidence còn áp dụng; không phát hiện gap cần sửa layout |
| CMP-T05 — Routing review và handoff | MyDmsVn, plan này | COMPLETE — docs-only handoff | Clean-context tìm đúng route; UIC-02 không có gap được chứng minh; refs/pin/status và §7 đã đối chiếu |

Thứ tự áp dụng đã chọn: CMP-T01 → checkpoint UIC-01 của CMP-T03 → CMP-T04. CMP-T03 hiện đã hoàn tất cả mapping/link UIC-02; CMP-T04 đã xác nhận catalog hiện hữu phù hợp. CMP-T05 đã hoàn tất routing review và handoff; scope được chọn không còn task triển khai mở. Native GUI trên cùng desktop luôn tuần tự.

**Design approval:** phạm vi và thiết kế được Owner chọn ngày 30/10/2026. **App runtime/visual boundary:** CMP-T04 không thay production/runtime hoặc tạo candidate visual mới; evidence còn áp dụng và quan sát bổ sung xác nhận mapping được giữ đúng phạm vi. Không có Owner visual approval app mới, không đóng các quyết định lịch sử. Candidate visual mới về sau cần scope và quyết định Owner cho đúng candidate.

**Phạm vi đã chọn:** hai Gallery recipes, đường skill/mapping và một pilot catalog chỉ đọc. Sửa dialog user-edit, master-detail, SourceGrid inline-edit, public layout framework, nâng runtime toolkit và triển khai SQL/release không nằm trong queue này.

## Global Constraints

- Product và UI Lab giữ net48; toolkit giữ net48;net8.0-windows và các assembly/namespace hiện hữu.
- SQL-free: dùng dữ liệu giả của Gallery và fake ports của UI Lab; giữ DTO, authorization, session, draft, Save/Post và recovery trong MyDmsVn.
- Dùng host, theme manager, native test helpers và test projects hiện hữu; không tạo framework layout, form schema, renderer, validator hoặc harness tổng quát.
- Native TableLayoutPanel, FlowLayoutPanel và Panel là layout primitives hợp lệ. Không truy cập private children của toolkit composite để ép màu, font hoặc chiều cao.
- Business Compact của sản phẩm vẫn do app cấu hình. Các profile Default/Base14/Base16 của Gallery tiếp tục là profile demo.
- Chỉ các API đã có trong runtime pin của app mới được dùng trong product pilot. Recipe source SHA và runtime toolkit SHA là hai trường độc lập.
- Không thay runtime gitlink chỉ để lấy ví dụ source. Nếu phát hiện API/fix thật sự còn thiếu, ghi một dependency follow-up riêng và giữ các phần không bị chặn tiếp tục được.
- UI Lab Sales/Security giữ độc lập về khởi chạy, graph và trạng thái. Sử dụng control thật trong Lab; không dựng bản sao của màn hình sản phẩm.
- Giữ cấu trúc và hành vi đã được duyệt của Security. Chỉ thay production layout cho một chênh lệch cụ thể đã được ghi nhận.
- Markdown giữ tiếng Việt có dấu, UTF-8 không BOM; khi đưa vào repo tuân thủ .editorconfig/.gitattributes. Skill sửa trong repo phải được commit cùng PR của repo đó.

## Review Focus

Năm điều kiện sau phải có bước kiểm chứng ở task sở hữu:

1. Vùng nội dung hẹp và nhãn tiếng Việt dài: lệnh chính, thông báo lỗi và nội dung nhận diện vẫn truy cập được — CMP-T01/T02/T04.
2. Gõ từng phím, Enter và đổi bộ lọc: giữ raw text/caret/focus, không dispatch thao tác ghi ngoài ý định — CMP-T01/T04.
3. Empty, filtered-empty, loading và unavailable: thể hiện khác nhau; dữ liệu chưa biết không trở thành số 0 — CMP-T01/T04.
4. Đổi theme khi có nội dung đang nhập: giữ draft, control identity, selected row; đóng Gallery page phải giải phóng subscriptions do mẫu sở hữu — CMP-T01/T02/T04.
5. Codex đọc một yêu cầu UI thông thường: tìm đúng mẫu qua skill, phân biệt source tham khảo/API thực, và chọn đúng phạm vi kiểm chứng — CMP-T03/T05.

---

## 1. Baseline và lý do chọn phạm vi

| Nguồn | Baseline đã đọc ngày 30/10/2026 |
|---|---|
| MyDmsVn main | 16c24738a4758ccdceb1f3509f4e37adb0d03464 |
| Runtime gitlink vendor/MyDmsVn.WinFormsKit trong MyDmsVn | 433e5b53f979e37cc6b69bad6dfcde0975c7ecaf |
| WinFormsKit main | 2521dfe99461e8f6d1954da96ffaed0688027455 |

Khi bắt đầu triển khai, đọc lại HEAD, instructions và gitlinks. Nếu baseline thay đổi, chỉ rà soát phần delta liên quan; cập nhật evidence thay vì giả định các kết luận về candidate cũ còn nguyên.

Gallery đã có toolbar, card, input groups và DataGrid/pagination composition. MainForm đăng ký Form factories và dispose trang cũ; DemoPageHostForm đã ghép nhiều Form thành các tab. Test project toolkit đã reference Gallery. Vì vậy, phần mới chủ yếu nằm ở samples, tests và tài liệu.

MyDmsVn đã có helper master/detail và màn hình Security được review. Việc tạo lại các lớp này hoặc mở một đợt redesign rộng sẽ làm tăng chi phí trước khi chứng minh được lợi ích của mẫu.

## 2. Thiết kế đã chọn và các lựa chọn đã cân nhắc

### 2.1. Phương án đã được Owner chọn

Hai mẫu nền tảng, một đường tra cứu ngắn trong skill và một pilot sản phẩm:

- UIC-01 — Filter Results: danh sách chỉ đọc có bộ lọc và trạng thái.
- UIC-02 — Compact Edit Form: form nhập liệu có validation, Save/Cancel và phản hồi.
- Pilot bắt buộc: SecurityPermissionCatalogView trong UI Lab Security hiện hữu.
- Tham chiếu cho UIC-02: dialog “Sửa thông tin…” của người dùng, userTarget=true/create=false. Đợt này lập mapping và đánh giá mức phù hợp; thay đổi dialog là follow-up chỉ khi có vấn đề cụ thể.

UIC-01/UIC-02 chưa tồn tại tại activation baseline; source thực sau correction accessibility có tại `bdac29bf4aca20db6618570ecd9a354630bc2e5f`, còn visual candidate `1420f25` là historical approval riêng. [Mapping](../design/UI_COMPOSITION_MAPPING.md) dẫn tới từng form và handoff docs; các app pilot changes vẫn phụ thuộc gap cụ thể.

| Cách làm | Đánh đổi | Quyết định đã chọn |
|---|---|---|
| Chỉ bổ sung mô tả vào skill/policy | Ít code nhưng thiếu ví dụ chạy được để giải quyết các quyết định layout | Không đủ cho mục tiêu lần này |
| Hai mẫu + skill + pilot thật | Đủ khép vòng tham khảo, áp dụng và kiểm chứng; tận dụng hạ tầng hiện hữu | Chọn |
| Framework layout/form builder dùng chung | Thêm public contracts, lifecycle và chi phí migration trước khi có nhu cầu ổn định | Để sau khi có nhiều consumer thực tế |

### 2.2. Quy tắc sở hữu

Toolkit recipe mô tả cấu trúc vùng, public controls, layout, tương tác mẫu và ownership. Product mapping mô tả cách áp Business Compact, control pin, khác biệt nghiệp vụ và điểm kiểm tra. Skill chỉ dẫn tới hai nguồn này.

Mẫu có thể được đọc và chuyển thể; product không kế thừa DemoFormBase và không ProjectReference đến ControlGallery. Không sao chép palette Business Compact vào toolkit hoặc sao chép một theme walker qua private children.

Bảng catalog sản phẩm tiếp tục chỉ đọc. Không thêm Create/Edit/Delete, pagination mới hoặc permission workflow vào pilot. Sales giữ keyset pagination nếu một mẫu danh sách được dùng cho Sales trong tương lai.

### 2.3. Thứ tự tạo giá trị

Ưu tiên UIC-01 và mapping/pilot của nó trước trong quá trình làm việc. UIC-02 dùng cùng định dạng recipe sau khi UIC-01 đã đủ rõ để người khác áp dụng. Có thể phát triển file của UIC-02 song song sau khi chốt contract, nhưng nghiệm thu đợt đầu luôn bao gồm product pilot.

Master-detail, SourceGrid inline editing, public layout helper và áp dụng toàn bộ SAL/SEC thuộc bước mở rộng sau, dựa trên nhu cầu dùng lại thật.

## 3. File map

### 3.1. WinFormsKit

| File | Hành động và trách nhiệm |
|---|---|
| samples/ControlGallery/MainForm.cs | Thêm mục “Compositions” bằng DemoPageHostForm/DemoPageSection hiện hữu |
| samples/ControlGallery/CompositionListDemoForm.cs | Mới: UIC-01, dữ liệu giả và trạng thái cục bộ |
| samples/ControlGallery/CompositionEditDemoForm.cs | Mới: UIC-02, nhập liệu/validation/lưu-hủy cục bộ |
| tests/MyDmsVn.Bootstrap5WinFormUI.Tests/Demo/CompositionDemoFormTests.cs | Mới: hành vi của hai mẫu; STA, tuần tự và khôi phục theme |
| docs/guides/UI_COMPOSITION_RECIPES.md | Mới: mục lục và hợp đồng của hai mẫu |
| docs/verification/20261030-ui-composition-recipes.md | Mới: candidate, các kiểm chứng thực tế và giới hạn |
| docs/README.md | Link tài liệu recipe |
| docs/plans/20261030-001-ui-composition-recipes.md | Companion plan ACTIVE trong toolkit; owner trạng thái CMP-T01/CMP-T02 |
| docs/ROADMAP.md | Entry selector toolkit dẫn đến companion plan; không sao chép status ledger |

Mỗi form mới có constructor không tham số, theo convention demo hiện hữu, và có thể được tạo bằng Func<Form>. Có thể khai báo public class trong assembly demo như các form hiện hữu; đây không phải API của assembly UI core.

Không thêm project, package hay reference để phục vụ hai mẫu. File dữ liệu chung chỉ được tách khi có nội dung thực sự dùng chung.

### 3.2. MyDmsVn

| File | Hành động và trách nhiệm |
|---|---|
| .agents/skills/mydmsvn-winforms-ui/SKILL.md | Thêm bước tra cứu recipe và báo cáo cách áp dụng |
| docs/design/UI_COMPOSITION_MAPPING.md | Mới: mapping mẫu → Business Compact → màn hình sản phẩm |
| docs/design/BUSINESS_COMPACT.md | Thêm liên kết tới mapping, giữ nguyên nơi sở hữu quy tắc |
| docs/development/UI_LAB.md | Thêm đường kiểm tra pilot và cách phân biệt recipe source/runtime pin |
| docs/evidence/ui-labs/20261030-ui-composition-adoption.md | Mới: baseline, mapping, gap, candidate và quyết định pilot |
| src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityPermissionCatalogView.cs | Chỉ sửa layout khi pilot ghi nhận gap cụ thể |
| tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs | Tái sử dụng hoặc bổ sung assertion đúng hành vi catalog bị ảnh hưởng |
| tests/MyDmsVn.Desktop.Ui.Tests/SecurityPaneStateTests.cs | Owner hiện hữu cho loading/empty/unavailable |
| tests/MyDmsVn.Desktop.Ui.Tests/SecurityProductHostParityTests.cs | Owner hiện hữu cho product/Lab geometry, theme và identity |
| docs/plans/20261030-001-business-compact-composition-adoption.md | Plan này; owner trạng thái CMP-T03–CMP-T05 và phối hợp scope |
| docs/plans/README.md | Execution index; trạng thái app chỉ có một owner |
| AGENTS.md và AI_CONTEXT.md | Chỉ cập nhật đường đọc nếu cần khi kích hoạt/handoff |

Không thay các file source/test chỉ để có diff. Evidence có thể ghi “existing composition mapped; no production layout change needed” nếu kết quả đo không có gap.

## 4. Các task triển khai

### CMP-T01/CMP-T02 — Toolkit recipes do companion plan sở hữu

Đọc [plan toolkit](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/main/docs/plans/20261030-001-ui-composition-recipes.md) để triển khai từng bước và cập nhật trạng thái CMP-T01/CMP-T02. Không cập nhật tiến độ các task toolkit trong file này.

- CMP-T01 cung cấp CompositionListDemoForm(), UIC-01, source reference cụ thể và evidence của filter/results/state/layout/lifetime.
- CMP-T02 cung cấp CompositionEditDemoForm(), UIC-02, source reference cụ thể và evidence của validation/save/cancel/theme/layout.
- Handoff phải tách recipe source SHA khỏi runtime pin của MyDmsVn. App pilot chỉ dùng public API đã có tại pin hoặc ghi dependency follow-up rõ ràng.
- Tests, source APIs và evidence phải đọc tại candidate bàn giao; [mapping](../design/UI_COMPOSITION_MAPPING.md) cố định source và handoff SHA, không suy implementation chỉ từ trạng thái ACTIVE.

### CMP-T03 — Nối recipe vào skill và Business Compact

**Owner:** MyDmsVn. **Depends on:** recipe UIC-01 có source revision cụ thể; UIC-02 hoàn thiện link trước handoff.

**Status:** COMPLETE — docs/skill routing, 30/10/2026; [evidence/checkpoint](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t03--checkpoint). Không đổi product source/runtime; app GUI/native NOT RUN. Clean-context routing review thuộc CMP-T05.

**Files:** SKILL.md, UI_COMPOSITION_MAPPING.md, BUSINESS_COMPACT.md, UI_LAB.md, app plan/evidence.

**Interfaces:**
- Consumes: recipe IDs/source SHA, runtime pin và Business Compact hiện hữu.
- Produces: một mapping record cho UIC-01/UIC-02 và chỉ dẫn trước-code trong skill.

Mỗi mapping record gồm:
- Recipe ID và source URL tại commit.
- Runtime toolkit pin của app.
- Controls/public capabilities đã đối chiếu tại pin.
- Product surface và các BC rules liên quan.
- Các phần áp dụng nguyên cấu trúc, các điều chỉnh bắt buộc và lý do.
- UI Lab/scenario/test owner liên quan.
- Evidence hiện có hoặc cần bổ sung; không suy luận PASS từ link source.

**Steps:**
- [x] Thêm UI_COMPOSITION_MAPPING.md với UIC-01 → SecurityPermissionCatalogView: ghi source revision cụ thể, runtime pin và đối chiếu public APIs tại pin.
- [x] Bổ sung đoạn ngắn vào skill: tìm recipe liên quan, đối chiếu APIs/pin, áp BC, ghi deviations, kiểm chứng trên đúng Lab.
- [x] Thêm rule không tìm được recipe: tiếp tục theo policy; chỉ đề xuất recipe mới khi có nhu cầu lặp lại hay lỗi phối hợp đáng kể.
- [x] Thêm liên kết từ BC policy và UI Lab runbook; không sao chép toàn bộ metrics hoặc recipe vào skill. Kiểm tra đường đọc và mapping UIC-01 để mở checkpoint cho CMP-T04.
- [x] Khi CMP-T02 bàn giao, hoàn tất mapping/link UIC-02 → dialog user “Sửa thông tin…” như tham chiếu tương thích; không sửa dialog trong task này.
- [x] Kiểm tra diff, links, source/API distinctions, status và encoding; docs-only không chạy SQL/GUI.
- [x] Commit skill cùng các references trong repository.

**Checkpoint UIC-01 mở pilot — đạt:** có source link tại commit, đối chiếu runtime APIs/pin, catalog mapping và đường skill → mapping → source đã kiểm tra. Checkpoint riêng không đủ đóng CMP-T03; mapping/source link UIC-02 cũng đã hoàn tất trong cùng scope docs này. Checkpoint này mở đầu vào CMP-T04 tại thời điểm CMP-T03; kết quả pilot hoàn tất được ghi ở mục CMP-T04 bên dưới.

**Done:** Codex bắt đầu từ AGENTS/skill có thể tìm hai recipe và hiểu rõ phần nào là demo, phần nào là API, phần nào do product quyết định.

### CMP-T04 — Product pilot với Danh mục quyền

**Status:** COMPLETE — no source/runtime change, 30/10/2026; [pilot evidence](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t04--product-pilot). App source/test `cc498975f49c0b42f87b6e4c52c10a3c5c635cf9`; runtime pin `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`. Production/sample/test trees trùng consumer candidate `95916e3`; 20/20 focused rows PASS và Lab normal/minimum bổ sung không chỉ ra gap cần sửa. Không tạo candidate visual mới hoặc cấp approval mới; physical100/125 mới không chạy.

**Owner:** MyDmsVn. **Depends on:** CMP-T01 và checkpoint UIC-01 của CMP-T03. Không yêu cầu toàn bộ CMP-T03 DONE; có thể làm trước khi UIC-02 và mapping tương ứng hoàn tất.

**Files:** SecurityPermissionCatalogView.cs khi có gap; app evidence; các test owners hiện hữu trong file map khi bị ảnh hưởng.

**Interfaces:**
- Consumes: UIC-01 và production SecurityWorkspaceControl qua UI Lab Security.
- Produces: mapping được kiểm chứng, gap dispositions, candidate evidence; thay đổi source giới hạn nếu thực sự cần.

**Baseline behavior phải giữ:**
- Nhóm bên trái và vùng kết quả/chi tiết bên phải; grid chỉ đọc.
- Search, module group và lifecycle lọc đúng cùng một dataset.
- Duy trì selected key và scroll anchor khi còn hợp lệ.
- Copy/Details chỉ tới đúng key đang chọn.
- Loading/unavailable, empty dataset và no matches có thông báo khác nhau.
- Không có Security.Mutate trong journal khi thực hiện luồng đọc của pilot.
- Theme dùng BusinessCompactAppearance và bootstrap hiện hữu, không demo profile.
- Product outer size không bị nhầm với workspace client size.

**Steps:**
- [x] Mở production catalog trong Security Lab SEC_L01; ghi app/pin/theme, outer/client/content và DeviceDpi trong evidence.
- [x] Đọc lại consumer evidence còn áp dụng, xác nhận tree identity và BC-04/05/09/11; không có gap cụ thể, giữ source.
- [x] Disposition tái hiện/TDD: không áp dụng vì không có gap hoặc behavior change; không tạo test RED giả.
- [x] Disposition layout fix: không cần; giữ catalog, filters/data/state, shared appearance và Lab host.
- [x] Chọn methods từ ba test owners yêu cầu; bổ sung hai methods hiện hữu vào selection cho native typing và narrow filter geometry, discovery/execution 20/20, không thêm assertion trùng lặp.
- [x] Quan sát Lab tại 952×657 và 652×497; focused SQL-free product-host parity hiện hữu PASS ở normal/minimum Light/Dark. Không mở rộng physical matrix vì source/runtime không đổi.
- [x] Disposition Owner gate: không có candidate visual mới; giữ approval cũ đúng candidate/phạm vi, không tuyên bố approval mới.
- [x] Commit evidence/status; không thay source/test/runtime pin.

**Done:** Pilot đã chứng minh recipe và skill dùng được trong sản phẩm, cùng bằng chứng về hành vi và geometry tương ứng. Một kết quả “view hiện tại phù hợp; không cần sửa layout” là hợp lệ, nhưng phải có mapping và bằng chứng áp dụng rõ.

### CMP-T05 — Kiểm tra đường áp dụng và handoff

**Status:** COMPLETE — docs-only, 30/10/2026; [routing/handoff evidence](../evidence/ui-labs/20261030-ui-composition-adoption.md#cmp-t05--routing-review-and-handoff). Không có routing defect, dependency gap hoặc UIC-02 dialog gap được chứng minh; giữ skill và production source.

**Owner:** MyDmsVn, có đối chiếu toolkit. **Depends on:** CMP-T01–T04.

**Files:** hai recipe docs/evidence, UI_COMPOSITION_MAPPING.md, skill, hai execution indexes tương ứng.

**Steps:**
- [x] Giao một lượt read-only cho Codex trong context sạch: đánh giá bố cục catalog hiện có theo Business Compact và đề xuất phạm vi thay đổi cần thiết. Không nhắc trực tiếp tên/path recipe trong prompt.
- [x] Kiểm tra Codex tìm đúng skill → mapping → source, phân biệt runtime pin, chọn đúng production view và tests. Không dùng kết quả lượt này làm bằng chứng native.
- [x] Disposition routing: Codex tìm đúng đường, không cần sửa skill; không chép source vào skill.
- [x] Đối chiếu UIC-02 với user-edit dialog hiện hữu. Chỉ ghi follow-up nếu có gap; không sửa ShowIdentityDialogAsync chung trong scope này.
- [x] Chốt source refs của recipe sau merge, runtime pin thực, app candidate và evidence links.
- [x] Đánh dấu từng task đúng trạng thái; chỉ close toàn plan khi các deliverables đã đạt tiêu chí ở mục 7.
- [x] Ghi các nhu cầu mở rộng có bằng chứng, không tự thêm vào active roadmap; commit handoff.

**Done:** Có hai mẫu, đường sử dụng rõ, một product pilot và hướng dẫn đủ để người khác tiếp tục mà không cần chỉ dẫn miệng.

## 5. Kiểm chứng tập trung

### 5.1. Toolkit verification thuộc companion owner

[Plan toolkit](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/main/docs/plans/20261030-001-ui-composition-recipes.md) sở hữu commands, source-verified discovery và native verification của hai Gallery samples trên net48/net8.0-windows. Consumer dùng kết quả tại đúng source revision và không ghi lại kết quả toolkit thành PASS mới ở đây. Product verification vẫn chạy với runtime pin của app.

### 5.2. Product pilot

Dùng net48 và bitness hiện hữu của product. Khởi đầu bằng các methods liên quan đã xác minh trong source, sau đó chọn union nhỏ nhất theo diff:

- SecurityWorkspaceControlTests.Role_catalog_render_preserves_selected_definition_native_row_and_filter_identity
- SecurityWorkspaceControlTests.Role_catalog_toolkit_surfaces_keep_read_only_theme_focus_and_copy
- SecurityWorkspaceControlTests.Role_catalog_same_owner_render_keeps_native_member_and_catalog_rows_scroll_and_identity
- SecurityWorkspaceControlTests.Catalog_has_no_assignment_actions
- SecurityWorkspaceControlTests.Filters_preserve_selected_owner_and_show_catalog_lifecycle
- SecurityWorkspaceControlTests.Catalog_search_matches_the_displayed_friendly_name
- SecurityWorkspaceControlTests.Normal_and_minimum_hosts_keep_catalog_actions_reachable
- SecurityPaneStateTests.Revoked_or_failed_catalog_clears_old_keys_without_clearing_accounts
- SecurityPaneStateTests.Held_catalog_read_exposes_loading_then_successful_empty_independently
- SecurityProductHostParityTests.Sidebar_security_matches_direct_lab_content_and_retains_editor_on_theme_toggle

Các methods trên là nguồn lựa chọn, không phải union bắt buộc trong mọi lần sửa. Kiểm tra lại chúng sau khi baseline thay đổi. Dùng method-qualified filter; không chạy cả lớp lớn hoặc toàn solution chỉ để tiện.

Nếu không thay production/runtime, dùng lại evidence còn áp dụng và chỉ bổ sung quan sát còn thiếu cho mapping. Nếu thay source hoặc shared presentation, chọn lại scope theo diff. Không thêm SQL suite vào một thay đổi bố cục.

### 5.3. Native visual scope

| Bề mặt | Cấu hình cần đánh giá |
|---|---|
| Hai Gallery compositions mới | Light/Dark; Windows physical 100% và 125%; content thực ở kích thước bình thường và nhỏ của Gallery |
| Product catalog có source change | Light/Dark; physical 100% và 125%; normal/minimum workspace trong Lab và product host, theo phạm vi affected parity |
| Product catalog không đổi source/runtime | Evidence còn áp dụng + đo/quan sát bổ sung cần thiết để xác nhận mapping |
| UIC-02 product reference | Đối chiếu source/evidence của user-edit dialog; native thay đổi production thuộc follow-up nếu gap được chọn |

Không nhân mọi trạng thái với toàn bộ ma trận. Dùng trạng thái mặc định để kiểm geometry, chọn một trường hợp khó tại viewport nhỏ/125% cho long text và error wrapping; mở rộng chỉ khi kết quả chỉ ra rủi ro.

Product parity tests hiện chứa cặp outer/content 1200×760 → 952×657 và 900×600 → 652×497. Đây là baseline để đo lại, không là kích thước device pixels áp cứng cho mọi DPI.

## 6. Tổ chức PR triển khai sau commit kích hoạt

### PR A — WinFormsKit

Title đề xuất: Add reusable list and compact edit composition examples.

- CMP-T01/CMP-T02, recipe docs và evidence.
- Giữ source production toolkit ngoài diff nếu không có lỗi được chứng minh.
- Chia commit theo mẫu để review và khôi phục riêng.
- Form implementation có thể chia cho hai worker sau khi contract rõ; MainForm/docs/tests chung do một người tích hợp.

### PR B — MyDmsVn

Title đề xuất: Route Business Compact UI work through composition recipes.

- CMP-T03/CMP-T04/CMP-T05, mapping, skill, pilot và evidence.
- Có thể chuẩn bị draft/docs song song khi recipe format đã rõ.
- Chốt links tại source commit thật trước khi hoàn tất; không link HEAD động làm baseline duy nhất.
- Giữ runtime gitlink 433e5b53… nếu pilot dùng đủ APIs tại pin này.

Khuyến nghị thực thi từng task có review độc lập, với một người điều phối hai repo. Native GUI trên cùng Windows desktop vẫn chạy tuần tự. Mọi sửa skill phải được git commit; không phân phối một bản skill rời ngoài repository.

Owner đã chọn kế hoạch ngày 30/10/2026; phần việc mỗi repo được điều hướng qua execution index hiện hữu. Toolkit PR #6 là handoff lịch sử `d439cbdb043e1b798d1f991f0266350d9f61441e`; correction handoff `078eb348ac40b7a7e762a9b3f8b4da3fb19f314c` ghi source/test `bdac29b`. App đã hoàn tất source-linked mapping, pilot và handoff trong scope này. Các mục PR ở trên mô tả cách tổ chức scope, không chọn thêm task merge/publish hoặc follow-up. Giữ quyết định trong repository sở hữu và giữ nguyên trạng thái/evidence lịch sử.

## 7. Tiêu chí hoàn thành và giá trị đo được

Plan chỉ hoàn thành khi:

- [x] UIC-01/UIC-02 chạy được trong Gallery hiện hữu và có tests hành vi phù hợp.
- [x] Recipe docs có purpose, source/API, states, layout behavior, ownership và evidence.
- [x] Skill dẫn tới mapping/source; mẫu và public API được phân biệt rõ.
- [x] Product pilot dùng production controls trong Lab thật, đúng Business Compact và runtime pin.
- [x] Catalog giữ read-only, selection/details/copy và state semantics; flow đọc không có Security.Mutate.
- [x] Native evidence cần thiết được ghi nhận đúng candidate; hạn chế hoặc quyết định còn chờ được giữ nguyên.
- [x] Một lượt Codex context sạch tìm và áp dụng đúng đường đọc.
- [x] Hai repo có source links và status thống nhất; runtime pin không bị thay đổi vô tình.

Chỉ số đánh giá thực dụng:

1. Người triển khai tìm đúng mẫu mà không cần được chỉ đường trực tiếp.
2. PR UI nêu được mẫu tham khảo, phần chuyển thể và lý do.
3. Không có native/custom interactive exception mới thiếu capability reason.
4. Không có lỗi bố cục hoặc tương tác còn mở trong phạm vi đã chọn.
5. Ghi thời gian tìm mẫu và số vòng sửa layout ở lần áp dụng thực tế tiếp theo; không đặt phần trăm tiết kiệm trước khi có số liệu.

## 8. Mở rộng sau khi có bằng chứng

**Disposition CMP-T05:** chưa có nhu cầu mở rộng được chứng minh hoặc gap UIC-02 cần chọn follow-up. Các điều kiện dưới đây chỉ là điều kiện tham khảo cho lần chọn scope sau; không tạo task mới. Long-error wrapping của user-edit chưa được xác minh ở mọi viewport/DPI, nhưng thiếu phép đo không tự chứng minh lỗi.

- Master-detail recipe khi ít nhất hai bề mặt thực tế cần cùng một cấu trúc; đánh giá selection/draft navigation trước khi tách abstraction.
- Grid editing recipe khi có nhu cầu phối hợp lookup/numeric rõ; SourceGrid đặt tại GridGallery, giữ lifecycle thuộc engine/adapter/app đúng nơi.
- Public layout helper chỉ khi nhiều consumer có cùng contract ổn định, không phải vì số dòng Controls.Add lặp lại.
- Chỉnh dialog user-edit nếu phát hiện gap cụ thể. ShowIdentityDialogAsync dùng chung user create/edit và role create/edit; nếu sửa builder chung phải kiểm chứng các nhánh liên quan, bao gồm secret clearing ở create-user.
- Áp dụng SAL khi có task SAL cụ thể; giữ keyset pagination, inline-edit và Save/Post hiện hữu.

## 9. Nguồn đã đối chiếu

- [MyDmsVn source baseline](https://github.com/chung6a8m/MyDmsVn/commit/16c24738a4758ccdceb1f3509f4e37adb0d03464)
- [WinFormsKit source baseline](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/commit/2521dfe99461e8f6d1954da96ffaed0688027455)
- [Repository UI skill](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/.agents/skills/mydmsvn-winforms-ui/SKILL.md)
- [Gallery MainForm](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/2521dfe99461e8f6d1954da96ffaed0688027455/samples/ControlGallery/MainForm.cs)
- [DemoPageHostForm](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/2521dfe99461e8f6d1954da96ffaed0688027455/samples/ControlGallery/DemoPageHostForm.cs)
- [Toolkit test project](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/2521dfe99461e8f6d1954da96ffaed0688027455/tests/MyDmsVn.Bootstrap5WinFormUI.Tests/MyDmsVn.Bootstrap5WinFormUI.Tests.csproj)
- [Toolkit instructions](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/2521dfe99461e8f6d1954da96ffaed0688027455/AGENTS.md)
- [Permission Catalog view](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityPermissionCatalogView.cs)
- [Identity dialog composition](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceEditorBindings.cs)
- [Product host parity tests](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/tests/MyDmsVn.Desktop.Ui.Tests/SecurityProductHostParityTests.cs)
- [Existing workspace tests](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs)
- [Existing pane state tests](https://github.com/chung6a8m/MyDmsVn/blob/16c24738a4758ccdceb1f3509f4e37adb0d03464/tests/MyDmsVn.Desktop.Ui.Tests/SecurityPaneStateTests.cs)
