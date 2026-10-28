# Kế hoạch triển khai khắc phục các ngoại lệ Business Compact

> Hướng dẫn cho agent: triển khai ba sửa đổi toolkit độc lập và review theo phạm vi, sau đó tích hợp và review consumer. Theo dõi riêng triển khai, kiểm chứng và phê duyệt native.

**Mục tiêu:** Khắc phục ba ngoại lệ UI được Owner chọn bằng sửa đổi toolkit hẹp và tích hợp consumer.
**Kiến trúc:** Sửa WinFormsKit tương thích, cập nhật một gitlink chính xác, dùng nội dung modal public và các hợp đồng ứng dụng Security hiện có.
**Công nghệ:** C#/WinForms; toolkit net48 và net8.0-windows; app net48; các helper native NUnit/xUnit hiện có.
**Đặc tả:** [Thiết kế được chọn](../design/BUSINESS_COMPACT_EXCEPTION_CLOSURE.md).
**Ngày chọn:** 2026-10-28. **Trạng thái:** source đã triển khai, đã pin upstream chính xác và cross-build PASS; source và bằng chứng build sẵn sàng review; kiểm chứng Windows/native và phê duyệt Owner NOT RUN / PENDING.

## Ràng buộc chung

Giữ assembly identities, pin SourceGrid, trách nhiệm nghiệp vụ/permission/session/draft, trạng thái BOM hiện có và CRLF của C#, cùng Markdown UTF-8 không BOM/CRLF. Giữ nguyên evidence UIL lịch sử. Xác nhận `3b5f95e2` tiếp tục PENDING riêng. Tuân theo [TEST_POLICY](../TEST_POLICY.md); không chọn chạy lại toàn bộ SQL/release hoặc ma trận lịch sử.

## Trọng tâm review

- Các Font có giá trị bằng nhau không được để native Label giữ một Font đã dispose; khi typography đổi, chỉ giải phóng tài nguyên do toolkit sở hữu.
- Nút X/Escape/backdrop của modal và native Close phải tuân theo điều kiện chặn khi thao tác đang chờ/phục hồi, đồng thời giữ owner và việc xóa dữ liệu bí mật.
- Chế độ multiline không được đổi chiều cao single-line/adapter, selection/caret hoặc mặc định của input định dạng/password.
- Native editor đang focus phải giữ accessible name và khả năng chọn nội dung chỉ đọc sau khi đổi theme.
- Action disabled phải tiếp tục bất hoạt và được accessibility nhận diện là disabled, đồng thời dễ đọc và khác action Secondary outline đang enabled.

## Bảng công việc

| Công việc | Kết quả cần giao | Triển khai | Kiểm chứng / nghiệm thu |
|---|---|---|---|
| EXC-T01 | Palette action disabled của toolkit và kiểm thử hồi quy có ý nghĩa | IMPLEMENTED | Cross-build PASS; native NOT RUN |
| EXC-T02 | Sửa vòng đời Font tiêu đề modal của toolkit và kiểm thử hồi quy | IMPLEMENTED | Cross-build PASS; native NOT RUN |
| EXC-T03 | Hợp đồng public cho văn bản nhiều dòng và kiểm thử hồi quy của toolkit | IMPLEMENTED | Cross-build và compiled API review PASS; native NOT RUN |
| EXC-T04 | Pin upstream chính xác và tích hợp modal/text Security | SOURCE IMPLEMENTED; đã pin `1b9c345aa2afd85e38a72d575485c3abdf559451` | Consumer locked restore/build theo pin cuối PASS; native NOT RUN |
| EXC-T05 | Evidence Windows/native tập trung, quyết định Owner và bàn giao | Source/pin/build sẵn sàng review; native còn chờ | Windows/native NOT RUN; Owner PENDING |

## EXC-T01 — Action disabled

Tệp: upstream `Controls/BootstrapButtonRenderLogic.cs`, các kiểm thử button và component contract liên quan; review số đo palette consumer trong evidence và kiểm thử vẽ bằng palette Business Compact Light/Dark thực tế.

- [x] Thêm kiểm thử hồi quy cho palette/khả năng đọc và trạng thái disabled trước khi đổi production code.
- [x] Dùng secondary surface trung tính và màu chữ bình thường với border disabled hiện có; giữ native disabled input/accessible state.
- [ ] Kiểm chứng mặc định Light/Dark và palette app, sự khác biệt với enabled Secondary outline, và trạng thái disabled không đổi theo hover/pressed.

## EXC-T02 — Vòng đời modal

Tệp: upstream `Controls/BootstrapModalHeader.cs`, các kiểm thử typography/theme của modal theo phạm vi.

- [x] Thêm kiểm thử hồi quy cho trường hợp đổi theme với typography không đổi khiến title giữ Font đã dispose. Tái hiện native/RED-GREEN vẫn NOT RUN trên Linux.
- [x] Tái sử dụng typography do toolkit sở hữu khi không đổi; chỉ thay/dispose Font sở hữu khi giá trị thực sự thay đổi.
- [ ] Kiểm chứng đổi theme lặp, đo/vẽ title, đổi typography và disposal cuối qua các helper modal hiện có.

## EXC-T03 — Văn bản nhiều dòng

Tệp: upstream `Controls/BootstrapTextBox.cs`, các kiểm thử multiline/tương thích theo phạm vi và component contract public.

- [x] Thêm kiểm thử cho layout theo chiều dọc, chế độ wrap/scroll, chọn/sao chép khi chỉ đọc và hình học single-line không đổi.
- [x] Thêm các property chuyển tiếp native có tài liệu, giữ mặc định tương thích và chỉ đổi layout khi multiline.
- [x] Giữ cùng đối tượng native editor; chuyển metadata accessibility public đến EDIT đang focus.
- [ ] Kiểm chứng TextBox, accessory/validation hiện có và các adapter owner bị ảnh hưởng bằng các lựa chọn đã discovery.

## EXC-T04 — Consumer

Tệp: `vendor/MyDmsVn.WinFormsKit`, `BusinessCompactAppearance.cs`, Security editor bindings/các hộp thoại quyết định và import, `SecurityUserDetailView.cs`, `SecurityPermissionCatalogView.cs`, công cụ Lab giải phóng thao tác đang giữ và các kiểm thử UI theo phạm vi.

- [x] Pin commit upstream đã review `1b9c345aa2afd85e38a72d575485c3abdf559451` theo [PR #2](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/pull/2); checkout consumer sạch, giữ nguyên SourceGrid lồng bên trong.
- [x] Chuyển nội dung/action của hộp thoại hiện có vào body/footer public; giữ các điều kiện chặn close/session/recovery, dữ liệu bí mật và công cụ Lab giải phóng thao tác đang giữ.
- [x] Chỉ theme các vùng nội dung public do app sở hữu và chuyển hai vùng văn bản chỉ đọc sang toolkit.
- [x] Thêm kiểm thử hồi quy consumer tập trung trước khi đổi hành vi; giữ các kiểm thử native hiện có và cập nhật các assertion phụ thuộc native chrome được thay.
- [x] Locked restore/build các target toolkit và graph app bị ảnh hưởng; ghi cross-build riêng với native execution.

## EXC-T05 — Nghiệm thu native và bàn giao

- [ ] Discovery và chạy thực tế các lựa chọn toolkit/consumer theo phạm vi trên Windows tương tác, tuần tự, dùng các helper hữu hạn hiện có.
- [ ] Kiểm tra các vùng bị ảnh hưởng trong Light/Dark ở 100%/125%, kích thước thông thường/tối thiểu, đổi theme khi hộp thoại mở, action disabled và multiline copy/scroll.
- [ ] Ghi evidence đúng candidate và quyết định Owner có ngày; giữ các ngoại lệ OPEN cho đến khi evidence hỗ trợ đóng chúng.
- [x] Cập nhật contracts/routing hiện hành và cung cấp chính xác thay đổi upstream/consumer cùng giới hạn còn lại.

## Lệnh kiểm chứng

Dùng tên test class thực tế trong source được từng task bổ sung sau triển khai. Trên Linux, kiểm chứng compiler-only dùng `dotnet restore ... --locked-mode -p:EnableWindowsTargeting=true`, sau đó `dotnet build ... --no-restore -p:EnableWindowsTargeting=true`; các lệnh này không chạy WinForms. Trên Windows, theo upstream `docs/TESTING.md` cho cả hai target và adapter bị ảnh hưởng, cùng [UI Lab runbook](../development/UI_LAB.md) của app, với discovery không rỗng và kết quả TRX thực tế. Không coi trường hợp skip/thiếu runtime là PASS.
