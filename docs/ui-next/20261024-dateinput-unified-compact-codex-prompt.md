# Prompt giao Codex — DateInput Unified Compact

Prompt dưới đây giao việc triển khai phạm vi đã được Owner duyệt. Đọc thiết kế và plan trong repo; không dùng ảnh phác họa hoặc kết quả test cũ để suy ra bản sửa đã đạt.

---

Bạn hãy triển khai đợt sửa **DateInput — phương án 01 Unified Compact** trong chuỗi PR đang mở của MyDmsVn.

## Nhánh và tài liệu

- App: `chung6a8m/MyDmsVn`, PR **#45**, nhánh **`codex/uin-t13`**, base `v1a-pro-ui-next`.
- UI: `chung6a8m/MyDmsVn.Bootstrap5WinFormUI`, PR **#73**, nhánh **`codex/uin-t10-dgv`**.
- BSG: `chung6a8m/MyDmsVn.BootstrapSourceGrid`, PR **#18**, nhánh **`codex/uin-t10-parity`**.

Fetch các nhánh, kiểm tra trạng thái worktree và thay đổi mới trước khi sửa; giữ nguyên công việc không thuộc nhiệm vụ. Không reset về SHA cũ. Đọc theo thứ tự:

1. `AGENTS.md`, `AI_CONTEXT.md`.
2. `docs/ui-next/20261023-active-roadmap.md` — mục **Active DateInput correction**.
3. `docs/ui-next/20261024-dateinput-unified-compact-design.md` và ảnh tham chiếu được liên kết trong đó.
4. `docs/ui-next/20261024-dateinput-unified-compact-correction.md`.
5. `docs/ui-next/20261023-validation-and-handoff.md` — mục **DateInput correction acceptance**; `docs/TEST_POLICY.md`, `docs/TEST_STRATEGY.md`, `tests/README.md`.
6. `AGENTS.md` và tài liệu control/test/API liên quan của từng repo upstream trước khi sửa repo đó.

Owner đã chọn và chốt thiết kế 01. Hãy thực hiện **UIN-D01 → UIN-D02 → UIN-D03**, tự quyết các chi tiết nội bộ thông thường trong phạm vi đã chốt; không yêu cầu chọn lại visual. Có thể dùng subagent cho công việc độc lập và review, nhưng tuần tự hóa các lần chạy GUI/SQL theo policy. Không tự merge PR.

## Kết quả cần đạt

**UIN-D01 — caret:** sửa trong `BootstrapDateInput` để Up/Down liên tiếp giữ đúng segment ngày/tháng/năm và caret/selection. Khi chuẩn hóa `5/1` thành `05/02/2026`, ánh xạ theo segment logic; không chỉ phục hồi index tuyệt đối. Giữ caret thu gọn nếu trước đó không có selection. Khôi phục selection trước `PendingTextChanged`. Giữ reference-clock, pending/accepted, IME, giới hạn ngày và các guard ReadOnly/disabled/busy/denied/recovery.

**UIN-D02 — visual 01:** một viền/nền thống nhất, ngày xuất hiện một lần, icon lịch nằm trong ô và không có caption ngày hoặc chevron phụ. Mục tiêu khoảng 32 logical px tại 96 DPI, icon 16 px và vùng bấm 28–32 px; theme/DPI/font thực tế quyết định kích thước để không cắt chữ. Giữ From/To độc lập và native text editing. Tái sử dụng calendar engine và icon/theme infrastructure. `BootstrapCalendarPicker` dùng riêng vẫn hiển thị ngày bình thường; không đổi semantics chung của `BootstrapTextBox.Text`. Ưu tiên seam nội bộ; nếu buộc đổi public API, phải giải thích và kiểm chứng compatibility/API baseline.

**UIN-D03 — tích hợp:** commit/push UI trước, cập nhật/commit/push UI pin trong BSG, sau đó cập nhật BSG pin trong app PR #45. Giữ SourceGrid pin. Thêm native integration tests cho From/To và ngày chứng từ Goods/Sales, kiểm tra state projection và admission. Chỉ sửa host layout/binding khi có lỗi tích hợp cụ thể.

## Kiểm chứng và evidence

- Thêm regression có assertion caret/selection sau từng phím, cả Up/Down lặp trên Month/Year, raw `d/M`, selection qua dấu `/`, bước bị từ chối, ReadOnly và IME. Chứng minh lỗi trước sửa khi có thể; không coi compile/discovery fail là RED.
- Kiểm tra trigger sau mở/chọn/hủy lịch bằng chuột và bàn phím, focus/validation/accessibility/disposal, standalone CalendarPicker và preferred size trước/sau docking.
- Chạy các case UI bị ảnh hưởng trên net48/x86 và net8.0-windows/x64; app trên net48/x86, interactive Windows, serialized STA, exception guard và hang protection hiện có. Dùng selector thực tế trong plan, kiểm tra discovery và TRX; không suy ra fixture từ tên file.
- Kiểm tra Light/Dark, native100/native125 và font lớn; ghi riêng monitor DPI và window DPI. Chạy bounded actual-Login DateInput smoke trên build mới bằng hạ tầng package/process hiện có; hosted tests không thay thế actual Login.
- Nếu cần SQL cho package smoke, dùng đúng test instance phi production đã chỉ định, resource ownership và cleanup hiện có. Không mở lại full SQL/full GUI/Session hoặc các roadmap archive khi không có rủi ro thay đổi tương ứng.
- Ghi evidence mới với ngày chạy thực tế, exact source/test/vendor/package hashes, cases/counts/outcomes, failures/skips/blockers và cleanup. Giữ nguyên evidence lịch sử. Owner đã duyệt thiết kế, còn visual chạy thật cần disposition riêng; nếu chưa có thì để pending.

Chia commit dễ review cho caret, visual, pins/integration và evidence; push vào đúng các nhánh PR đang mở. Khi hoàn tất, báo cáo bằng tiếng Việt: commit từng repo, pin cuối, test đã chạy và môi trường, kết quả/bất kỳ blocker nào, bản build/ảnh thực tế để Owner xem, và trạng thái UIN-D01–D03/UIN-DA01–DA04. Hoàn thành mọi phần có thể thực hiện; phần thiếu môi trường hoặc Owner quan sát phải ghi rõ, không khai PASS.
