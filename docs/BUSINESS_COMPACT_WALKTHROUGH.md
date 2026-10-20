# BCI-T06 — Quan sát Business Compact trên frozen candidate

**Trạng thái: IN PROGRESS / CHƯA NGHIỆM THU.** Đối chiếu [frozen evidence](BUSINESS_COMPACT_ACCEPTANCE.md#87-bci-t06-frozen-integrated-ui-acceptance--2026-10-20). Phiên100 đầu dùng package `0.1.0-bci-t06`, product `09e92d3e7d3d59bd515457de4af034509b348d31`; test/driver `f858ec9024c7f3a507da9b4fb52cd34e79d9ac91`. Candidate2 cho lượt xác nhận tiếp theo là `0.1.0-bci-t06.2`, product/test `e20b8c7f43dc73a9613d7f04d0d52158809663cf`, có bản sửa dấu focus; chưa có kết quả attended candidate2. Mỗi phiên dùng database thử riêng và giới hạn 30 phút. Chỉ ghi nhận cleanup sau khi app đóng bình thường, process/SQL drain và fixture xác minh ownership rồi dọn thành công. Automation không thay thế review trực tiếp. Không có nghiệm thu production từ phiếu này.

1. Trong hộp `BCI-T06 temporary test Login`, bấm **Copy password**, rồi **Continue to Login**. Đăng nhập app thật bằng `gui.p5.owner` và dán mật khẩu. Không gửi mật khẩu vào chat. Các danh mục thử GUI-PROD, GUI-WH, GUI-EMP đã được seed qua fixture; bước này không chứng minh tạo danh mục qua UI.
2. Mở Goods Receipts, New, chọn Warehouse/Employee tương ứng. Add line, Product GUI-PROD, Quantity 2.5, Unit cost 4, Note `GUI draft`. Save, New, rồi mở lại phiếu đã lưu; kiểm tra dòng/metadata. Sửa Quantity 3.125, Note `GUI edited`, Save.
3. Ở Light rồi Dark: vào ô Product bằng F2; F4 mở popup; gõ query `UIA Product` để thấy ba lựa chọn, thử Down và Up qua các dòng khác nhau, Enter chọn; mở lại, Escape đóng popup rồi Escape hủy ô. Thử Tab/Shift+Tab và xác nhận không vô tình đổi product. Khôi phục đúng GUI-PROD (UIA Product Updated) và Save sau phép thử. Bấm nút `Light / Dark` trên thanh đầu app để đổi theme. Không bỏ qua Down ở 125% Dark.
4. Ở mỗi theme: sửa Quantity thành `8.00001`, bấm Save. Xác nhận raw text và lỗi còn, không bị làm tròn hay mất caret. Báo trạng thái để Codex chụp cửa sổ đang mở. Escape hủy hoặc sửa lại 3.125 rồi Save. Quantity/Unit cost căn phải, Amount readonly; thử F2 và điều hướng bằng phím, xác nhận tên/control readonly trong quan sát accessibility được đối chiếu với UIA riêng.
5. Thử cửa sổ thường, maximize, thu hẹp và scroll ngang/dọc. Thêm các dòng tạm đến khi có scroll dọc (thường khoảng 12 dòng), sửa Quantity ở dòng cuối để kiểm tra đúng identity sau scroll, sau đó xóa đúng các dòng tạm, chỉ giữ dòng gốc 3.125 @ 4 trước Save. Khi có dữ liệu chưa lưu, Refresh/New phải giữ dữ liệu và yêu cầu Save; không có bước Discard. Báo khi đã trở về đúng một dòng `GUI edited` để Codex đối chiếu SQL chỉ đọc.
6. Chờ Codex báo đã cài fault thử. Bấm Post đúng một lần, chờ khoảng 30 giây. Kiểm tra trạng thái chưa biết kết quả: editor/Save/Post bị khóa, Refresh vẫn đến được bằng phím; quan sát cả Light và Dark. Báo khi thấy để chụp, đối chiếu Draft/no stock, gỡ đúng fault thử.
7. Khi Codex báo fault đã gỡ: Refresh tường minh, xác nhận Draft và Post bật lại, rồi Post đúng một lần. Quan sát Posted readonly cả Light/Dark, báo để chụp và đối chiếu SQL một ledger movement và balance đúng 3.125.
8. Đóng app bình thường. Codex xác minh drain/cleanup rồi mới đổi Scale hoặc chạy scope khác. Xác nhận các quan sát thực tế, ghi rõ lỗi hoặc mục chưa thử; không xác nhận theo checklist nếu chưa nhìn thấy.

Ở 125% dùng một phiên/database mới, cùng trình tự; 1920×1080 không chứa được client 1680×900 logical, sẽ ghi kích thước fit/maximized thực tế.
## Kết quả thực tế

Phiên attended100 bắt đầu ngày2026-10-21, Owner đã xác nhận Login thật, Save/New/mở lại giữ2.5@4 rồi sửa/Save3.125@4 NoteGUIedited. Oracle SQL chỉ đọc xác nhận Draft đã lưu, no ledger/balance. [Ảnh đã review](evidence/bci-t06/README.md) ghi nhận Light Draft và raw lỗi8.00001; chưa có sign-off tổng thể. Phiên đầu trước đó chỉ bàn giao mật khẩu rồi đóng/cleanup đầy đủ, không được tính kết quả quan sát. Các dòng dưới là nghĩa vụ còn thiếu, không phải kết quả tự động suy ra từ T05/GUI-P5. Không chuyển ngoại lệ Warehouse125Dark Down của GUI-P5 sang Product inline.

| Nội dung | 100% Light | 100% Dark | 125% Light | 125% Dark |
|---|---|---|---|---|
| Product F4/query, Down/Up/Enter/Escape/Tab/Shift+Tab | OBSERVED — Owner confirmed; final review pending | OBSERVED — Owner confirmed; final review pending | NOT RUN | NOT RUN |
| Numeric/căn phải, raw lỗi/caret, Save bị chặn | OBSERVED — Owner confirmed raw/error retained and caret keys; final review pending | OBSERVED — Owner confirmed raw/error retained and caret keys; final review pending | NOT RUN | NOT RUN |
| Accessibility native, input editable/computed readonly | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| Window/maximize/scroll/theme, đúng dòng sau scroll | OBSERVED — Owner confirmed; final review pending | OBSERVED — Owner confirmed; final review pending | NOT RUN | NOT RUN |
| Post uncertain: khóa mutation, Refresh và retry tường minh | NOT RUN | OPEN — locks observed; Owner reports Tab skips Refresh; retry not run | NOT RUN | NOT RUN |
| Posted readonly | NOT RUN | NOT RUN | NOT RUN | NOT RUN |

Login và oracle SQL dùng chung trong từng phiên Scale; không suy ra rằng Login được lặp lại riêng ở mỗi theme.

| Phiên Scale | Login thật, tạo/Save/New/mở lại/sửa | SQL Draft trước Post; uncertain no stock; một movement/balance sau Post | Exit/drain/positive cleanup |
|---|---|---|---|
| 100% | OBSERVED — Owner confirmed actual Login and saved/reopened/edited data | Draft3.125@4/no stock verified; timeout WAITFOR observed; unchanged Draft/no stock verified before fault removal; retry/Posted NOT RUN | PASS — normal close, process/SQL drain and owned cleanup |
| 125% | NOT RUN | NOT RUN | NOT RUN |

Ảnh cửa sổ, số đo monitor/physical HWND/client, UIA và oracle SQL chỉ được thêm sau khi quan sát. Ảnh PrintWindow là client capture của đúng HWND, không phải ảnh toàn desktop vật lý; phải ghi ranh giới này khi bổ sung evidence. Không chụp Login hay hộp mật khẩu. Chỉ công bố ảnh đã review, không chứa bí mật hoặc SQL identity.

**Owner review:** PENDING. Owner reported that the focus border appeared to skip enabled Dark100 Refresh; Enter had not been tried. Native package Tab/Enter subsequently passed2/2 with independent SQL recovery/retry checks. Reviewed screenshots and pinned source identify a visual defect: Dark focus color equals the button fill, while grid selection borders remain visible. An app-owned contrasting focus cue passed the corrected hosted render regression and26/26 affected layout/editor cases; candidate2 is frozen and attended confirmation remains pending. The prior attended fault was removed after readonly SQL verified unchanged Draft/no stock; mouse Refresh resolved Draft, app closed normally and positive cleanup succeeded. Posted/retry and125% remain NOT RUN for attended review. Không tự ký thay Owner.
