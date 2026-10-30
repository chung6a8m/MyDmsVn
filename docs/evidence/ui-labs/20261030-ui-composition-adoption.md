# UI Composition Adoption — app evidence

## Baseline và scope

- Ngày 30/10/2026; task CMP-T03, [selected plan](../../plans/20261030-001-business-compact-composition-adoption.md); app inspected source/test `53b951c5974283c47b5f0bc2484d037ab312af47`, branch `codex/cmp-t03`.
- Product/target: Windows, net48, bitness hiện hữu. Task chỉ thay Markdown/skill routing; smallest verification scope là diff, links/anchors, source/API/status và UTF-8 không BOM/CRLF.
- Recipe source `1420f252ca198cd4409e0e945fa6433b30b943b3`; handoff docs `d439cbdb043e1b798d1f991f0266350d9f61441e`; runtime gitlink `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` giữ nguyên. [Mapping](../../design/UI_COMPOSITION_MAPPING.md) chứa exact source/guide/plan/evidence links và product ownership.

## Source/API reconciliation

Đã đọc guide và evidence ở handoff, recipe forms tại source candidate, catalog và identity dialog tại app baseline. `git ls-tree HEAD vendor/MyDmsVn.WinFormsKit` xác nhận pin runtime nêu trên. Đối chiếu `git diff --name-only 433e5b5… 1420f25… -- src` tại repository toolkit: delta thuộc tabs/busy; các file TextBox, ComboBox, Button, DataGridView, CheckBox, Label, validation enum và theme manager dùng bởi recipes không đổi. Các API public trong mapping có tại pin; không đề xuất API production mới hoặc pin upgrade.

UIC-01 map vào hai-pane catalog hiện hữu; giữ module/lifecycle/search, read-only, selection/scroll, details/copy và current-owner state. Demo fixtures, scenario controls và metrics không chuyển sang app. UIC-02 map đúng nhánh user-edit `ShowIdentityDialogAsync(true, false)` làm tham chiếu tương thích; shared builder và các nhánh create-user/create-role/edit-role không đổi.

Toolkit evidence tại handoff ghi focused automation hai TFM, bounded physical100/125, Owner approval candidate `1420f25` và representative attended matrix closure. Đây là kết quả của toolkit owner, **không phải PASS mới của app** và không khẳng định mọi state ở mọi tổ hợp đã được quan sát.

## CMP-T03 — checkpoint

**COMPLETE — docs/skill routing only.** Đã có mapping UIC-01/UIC-02, exact source và handoff links, public API reconciliation với runtime pin, deviations/ownership, Lab/test owners và đường skill → mapping → source. Checkpoint UIC-01 đủ đầu vào tài liệu cho CMP-T04; không chứng minh product pilot đã thực hiện.

**NO product source/runtime change. NOT RUN app GUI/native verification.** Không chạy build, GUI, SQL, package hoặc actual Login cho Markdown-only diff. Không có app candidate visual mới và **không tuyên bố quyết định Owner visual**. Historical evidence giữ đúng candidate/phạm vi; không chuyển approval toolkit thành approval app.

Docs verification: `git diff --check`; kiểm local links/anchors và exact toolkit object paths tại SHA được dẫn; kiểm status CMP-T03 COMPLETE, CMP-T04/CMP-T05 PLANNED và plan acceptance OPEN; kiểm UTF-8 không BOM, CRLF cho các file thay đổi. Skill clean-context behavior review chưa chạy trong task này và thuộc CMP-T05; static routing review không thay kết quả đó.

## Remaining work

- CMP-T04 **PLANNED**: dùng production catalog trong Security Lab, đánh giá evidence còn áp dụng và bổ sung quan sát mapping/geometry cần thiết; chỉ đổi layout nếu có gap cụ thể.
- CMP-T05 **PLANNED**: fresh-context routing review, đối chiếu UIC-02 compatibility/gap và handoff.
- Plan completion/acceptance **OPEN**. Không mở SQL, release, dialog rewrite hoặc archived acceptance.
