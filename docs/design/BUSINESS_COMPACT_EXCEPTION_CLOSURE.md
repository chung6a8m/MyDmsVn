# Thiết kế khắc phục ba ngoại lệ Business Compact đã được chọn

**Phạm vi được chọn:** Yêu cầu của Owner ngày 2026-10-28 chọn rõ ba nội dung cần sửa: action disabled trong Dark, khung hộp thoại Security đang dùng native Form và văn bản Security nhiều dòng chỉ đọc đang dùng native TextBox. Các lựa chọn dưới đây là quyết định kỹ thuật cho yêu cầu này; phê duyệt native của candidate vẫn đang chờ.

## Mục tiêu và baseline

Làm cho action disabled dễ đọc và phân biệt được trạng thái; dùng modal của toolkit an toàn khi đổi theme; đưa khả năng hiển thị, chọn và cuộn văn bản nhiều dòng của hai vùng chi tiết Security chỉ đọc về toolkit. Baseline app là `5386a6d8efe23099c436eb7ef62141df8d3896b8`; pin WinFormsKit trước sửa là `c232a4c09dc0386a5bd8bb484a5c991acae827d5`. Upstream main `efe2e7212fd36e32c91b914bdc5e4081947124fc` có cùng tree với commit app dùng trước sửa. Implementation pin ban đầu là `1b9c345aa2afd85e38a72d575485c3abdf559451`; sau sửa fixture Windows và cập nhật evidence, candidate tích hợp cuối pin `e06ba1e31f63e60d4a88fd65122f044e27dc7e56` theo [PR #2](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/pull/2), checkout consumer sạch. Giữ SourceGrid tại `f4e457b43582bf01892f50bdc74aa480531e5944`.

## Các phương án và thiết kế được chọn

Nếu chỉ ghi đè màu hoặc thay native control trong app, khoảng trống capability của toolkit vẫn còn và trách nhiệm triển khai bị lặp lại. Thay framework/control diện rộng sẽ mở rộng phạm vi được chọn. Áp dụng các sửa đổi upstream hẹp, tương thích; pin chính xác trong consumer; app bố trí nội dung qua các vùng public do toolkit cung cấp.

### Hiển thị action disabled

Ở baseline khảo sát, `BootstrapButtonRenderLogic` vẽ chữ disabled bằng `MutedText` trên `Surface`, không áp thêm một lớp giảm opacity. Tỷ lệ tương phản tính từ source của Business Compact Dark xấp xỉ 7.86:1. Số đo này không chứng minh lỗi tương phản số học; các nhận xét thị giác lịch sử vẫn thuộc candidate lịch sử. Action Secondary outline đang enabled dùng cùng màu chữ/nền. Chọn nền trung tính `SurfaceSecondary` và màu chữ bình thường `Text` cho palette disabled, giữ border disabled và hành vi bất hoạt. Lựa chọn này ưu tiên khả năng đọc nhãn; action đang hoạt động tiếp tục giữ mức nhấn theo variant. Kiểm thử tương phản và khả năng phân biệt trạng thái với Light/Dark và palette thực tế của consumer. Ngưỡng hồi quy 4.5:1 là mục tiêu khả năng đọc của sản phẩm, không phải khẳng định rằng control disabled chịu một yêu cầu WCAG tương ứng. Appearance native vẫn phải được review.

### Quyền sở hữu và bố trí modal

Sửa cách tái sử dụng Font tiêu đề do `BootstrapModalHeader` sở hữu theo mẫu typography token/Font đã có: khi family/size/style bằng nhau, giữ Font còn sống, tránh gán một Font tương đương rồi dispose instance mà Label vẫn giữ. Kiểm thử đổi theme nhiều lần với typography không đổi, đổi typography và disposal; không dispose Font do consumer sở hữu.

Dùng `BootstrapModal` cho các hộp thoại ghi dữ liệu, quyết định và review import không dùng SQL hiện có. Chỉ đặt nội dung app trong `BodyPanel` và `FooterPanel`; giữ kích thước riêng và hành vi command/validation/dữ liệu bí mật/recovery/session của app. Dùng backdrop static để click bên ngoài không đóng hộp thoại. Giữ owner của modal, Accept/Cancel và điều kiện chặn đóng khi thao tác đang chờ hoặc đang phục hồi. Bộ duyệt appearance của app chỉ được đi vào các panel nội dung public này, không duyệt header/surface nội bộ của toolkit. Cập nhật công cụ Lab giải phóng mutation đang giữ để tìm action trong footer public.

### Văn bản nhiều dòng

Mở rộng `BootstrapTextBox` theo hướng bổ sung các property `Multiline`, `WordWrap` và `ScrollBars`, chuyển tiếp xuống native editor. Giá trị mặc định giữ hình học và hành vi single-line; chỉ chế độ multiline mới dùng toàn bộ vùng chiều cao editor có sẵn. Giữ cùng native editor khi đổi text/theme/read-only và chuyển accessible name/description đến editor đang focus. Giữ selection, caret, copy, giá trị chỉ đọc và hành vi cuộn native. Consumer migration chỉ gồm `SecurityUserDetailView.Explanation` và `SecurityPermissionCatalogView.Details`, giữ chiều cao, văn bản, accessible name và ngữ nghĩa chỉ đọc hiện có. Các adapter và input định dạng tiếp tục giữ mặc định single-line.

## Ranh giới

Sản phẩm giữ net48; toolkit giữ net48 và net8.0-windows, assembly identities, cách sử dụng qua ProjectReference và pin SourceGrid chính xác. Yêu cầu này không thay MessageBox Close thông thường, authorization, SQL, chính sách session/draft, hành vi nghiệp vụ, Login, package hoặc release. Xác nhận Owner riêng cho sửa focus `3b5f95e2` vẫn đang chờ. Evidence lịch sử và các ngoại lệ đã được chấp nhận giữ nguyên; chỉ evidence của candidate mới mới có thể đóng các ngoại lệ được chọn.

## Nghiệm thu

Các kiểm thử toolkit tập trung bao phủ palette/trạng thái disabled, vòng đời Font/theme của modal, văn bản nhiều dòng/layout/selection/read-only/accessibility và tương thích single-line. Kiểm thử consumer bao phủ owner của hộp thoại Security thực tế, chặn đóng khi thao tác đang chờ, xóa dữ liệu bí mật, truyền theme qua phần body public, hai vùng multiline và presentation của sản phẩm/Lab. Kiểm thử adapter liên quan bảo vệ tương thích TextBox dùng chung. Cross-build chỉ là bằng chứng compiler. EXC-T05 đã ghi discovery Windows không rỗng, execution STA tuần tự và cleanup: toolkit controls 75/75/TFM, adapter 40/40/TFM và consumer 163/163 net48/x86, không fail/skip. Ghi source/test/pin, môi trường, kết quả và giới hạn trong candidate evidence.

Review các vùng đã đổi trong Light/Dark ở physical 100%/125%, với kích thước nội dung thông thường/tối thiểu, gồm đổi theme khi hộp thoại đang mở và chọn/sao chép/cuộn văn bản chỉ đọc. Physical 100%/DeviceDpi 96 và physical 125% với probe per-monitor-aware 120×120 đã PASS; Lab net48 vẫn báo `DeviceDpi: 96` ở 125% và giá trị đó được ghi riêng, không dùng thay physical-scale proof. Lấy quyết định Owner tập trung trước khi đánh dấu bất kỳ ngoại lệ nào đã đóng.
