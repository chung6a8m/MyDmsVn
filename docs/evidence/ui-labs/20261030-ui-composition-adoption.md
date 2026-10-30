# UI Composition Adoption — app evidence

## Baseline và scope

- Ngày 30/10/2026; task CMP-T03, [selected plan](../../plans/20261030-001-business-compact-composition-adoption.md); app inspected source/test `53b951c5974283c47b5f0bc2484d037ab312af47`, branch `codex/cmp-t03`.
- Product/target: Windows, net48, bitness hiện hữu. Task chỉ thay Markdown/skill routing; smallest verification scope là diff, links/anchors, source/API/status và UTF-8 không BOM/CRLF.
- Đây là refs của checkpoint CMP-T03 ban đầu: recipe source `1420f252ca198cd4409e0e945fa6433b30b943b3`; handoff docs `d439cbdb043e1b798d1f991f0266350d9f61441e`; runtime gitlink `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` giữ nguyên. Post-handoff correction source/docs nằm ở record cuối, không thay decision visual lịch sử. [Mapping](../../design/UI_COMPOSITION_MAPPING.md) chứa exact source/guide/plan/evidence links và product ownership.

## Source/API reconciliation

Đã đọc guide và evidence ở handoff, recipe forms tại source candidate, catalog và identity dialog tại app baseline. `git ls-tree HEAD vendor/MyDmsVn.WinFormsKit` xác nhận pin runtime nêu trên. Đối chiếu `git diff --name-only 433e5b5… 1420f25… -- src` tại repository toolkit: delta thuộc tabs/busy; các file TextBox, ComboBox, Button, DataGridView, CheckBox, Label, validation enum và theme manager dùng bởi recipes không đổi. Các API public trong mapping có tại pin; không đề xuất API production mới hoặc pin upgrade.

UIC-01 map vào hai-pane catalog hiện hữu; giữ module/lifecycle/search, read-only, selection/scroll, details/copy và current-owner state. Demo fixtures, scenario controls và metrics không chuyển sang app. UIC-02 map đúng nhánh user-edit `ShowIdentityDialogAsync(true, false)` làm tham chiếu tương thích; shared builder và các nhánh create-user/create-role/edit-role không đổi.

Toolkit evidence tại handoff ghi focused automation hai TFM, bounded physical100/125, Owner approval candidate `1420f25` và representative attended matrix closure. Đây là kết quả của toolkit owner, **không phải PASS mới của app** và không khẳng định mọi state ở mọi tổ hợp đã được quan sát.

## CMP-T03 — checkpoint

**COMPLETE — docs/skill routing only.** Đã có mapping UIC-01/UIC-02, exact source và handoff links, public API reconciliation với runtime pin, deviations/ownership, Lab/test owners và đường skill → mapping → source. Checkpoint UIC-01 đủ đầu vào tài liệu cho CMP-T04; không chứng minh product pilot đã thực hiện.

**NO product source/runtime change. NOT RUN app GUI/native verification.** Không chạy build, GUI, SQL, package hoặc actual Login cho Markdown-only diff. Không có app candidate visual mới và **không tuyên bố quyết định Owner visual**. Historical evidence giữ đúng candidate/phạm vi; không chuyển approval toolkit thành approval app.

Docs verification: `git diff --check`; kiểm local links/anchors và exact toolkit object paths tại SHA được dẫn; kiểm status CMP-T03 COMPLETE, CMP-T04/CMP-T05 PLANNED và plan acceptance OPEN; kiểm UTF-8 không BOM, CRLF cho các file thay đổi. Skill clean-context behavior review chưa chạy trong task này và thuộc CMP-T05; static routing review không thay kết quả đó.

## CMP-T04 — product pilot

**COMPLETE — no source/runtime change, 30/10/2026.** Đã đi theo skill → mapping → exact UIC-01 source/guide → production `SecurityPermissionCatalogView` trong Security Lab. Bố cục hiện tại phù hợp với recipe đã chuyển thể; kiểm chứng dưới đây không chỉ ra gap cụ thể cần sửa layout. Không sửa production, samples hoặc tests, không thêm CRUD/assignment, không reference ControlGallery và không nâng runtime pin. TDD RED/fix không áp dụng vì không có behavior change; không tạo test trùng lặp để có diff.

### Candidate, evidence reuse và môi trường

| Item | Candidate / observation |
|---|---|
| App source/test tại lượt chạy | `cc498975f49c0b42f87b6e4c52c10a3c5c635cf9`, worktree `cmp-t04-security-catalog`, detached HEAD |
| Production `src` tree | `a5083a939408501cdf86e40fc6548e2b4323e5a4` |
| `samples` tree | `53f7fe35de808c5c6d732791a3da753e31ae2bea` |
| `tests` tree | `900e01fa440760c563edad23820ad88763562735` |
| Runtime toolkit / SourceGrid | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` / `f4e457b43582bf01892f50bdc74aa480531e5944`, unchanged |
| Recipe / handoff | `1420f252ca198cd4409e0e945fa6433b30b943b3` / `d439cbdb043e1b798d1f991f0266350d9f61441e`, source references only |
| Platform | Interactive Windows NT `10.0.26300.0`, SDK `8.0.427`, net48, Release, `PlatformTarget=x86`, test host `TargetPlatform=x86` |
| Display diagnostics | DeviceDpi `96`; Lab work area `1920×1032`; physical scale **unverified in this supplemental pass** |

`git diff 95916e33650648ef9753223605b5e679d6944d2a HEAD -- src samples tests` rỗng; cả ba tree hashes và runtime pin trùng [consumer candidate 29/10](20261029-input-popup-consumer-acceptance.md). Do đó giữ evidence physical100/125, Light/Dark và Owner approval cũ đúng candidate/phạm vi đó. Không biến chúng thành kết quả mới của task này. Evidence CMP-T03 ở trên vẫn là checkpoint docs-only tại thời điểm của nó.

### Focused discovery và execution

Restore locked trực tiếp UI test project và Security launcher; build hai projects Release/net48/x86 đều exit 0, **0 warning / 0 error**. Chọn 11 methods hiện hữu cho các boundary cần xác nhận; không chạy toàn lớp/solution, SQL hoặc archived matrix. Nhóm filter geometry thay cho `Normal_and_minimum_hosts_keep_catalog_actions_reachable` vì kiểm chính xác filter HWND/height/wrap bằng Business Compact; product geometry và actions được bổ sung qua parity test và quan sát Lab. Hai rows native typing bảo đảm per-character/caret/Enter, không dùng whole-string assignment làm bằng chứng per-key.

| Method-qualified selector (`FullyQualifiedName~`) | Discovered / executed / passed |
|---|---:|
| `SecurityWorkspaceControlTests.Role_catalog_render_preserves_selected_definition_native_row_and_filter_identity` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Role_catalog_toolkit_surfaces_keep_read_only_theme_focus_and_copy` | 2 / 2 / 2 |
| `SecurityWorkspaceControlTests.Role_catalog_same_owner_render_keeps_native_member_and_catalog_rows_scroll_and_identity` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Filters_preserve_selected_owner_and_show_catalog_lifecycle` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Catalog_has_no_assignment_actions` | 1 / 1 / 1 |
| `SecurityWorkspaceControlTests.Catalog_search_matches_the_displayed_friendly_name` | 1 / 1 / 1 |
| `SecurityPaneStateTests.Revoked_or_failed_catalog_clears_old_keys_without_clearing_accounts` | 2 / 2 / 2 |
| `SecurityPaneStateTests.Held_catalog_read_exposes_loading_then_successful_empty_independently` | 1 / 1 / 1 |
| `SecurityProductHostParityTests.Sidebar_security_matches_direct_lab_content_and_retains_editor_on_theme_toggle` | 4 / 4 / 4 |
| `SecurityNativeAndResetTests.Role_catalog_native_filter_retains_caret_range_key_and_enter_never_writes` | 2 / 2 / 2 |
| `InputPopupConsumerAcceptanceTests.Catalog_filter_keeps_equal_native_input_heights_without_clipping_when_refresh_wraps` | 4 / 4 / 4 |
| **Total** | **20 / 20 / 20; fail 0, skip 0** |

Exact method names above are joined by `|` into `$filter`, each prefixed `FullyQualifiedName~`. Commands executed from the isolated worktree:

```powershell
git submodule update --init --recursive
dotnet --version
dotnet restore tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj --locked-mode
dotnet restore samples/MyDmsVn.Desktop.UiLab.Security/MyDmsVn.Desktop.UiLab.Security.csproj --locked-mode
dotnet build tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 -v minimal
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --list-tests --filter $filter -- RunConfiguration.TargetPlatform=x86
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --filter $filter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=cmp-t04-catalog.trx' --results-directory TestResults/cmp-t04-catalog -- RunConfiguration.TargetPlatform=x86
dotnet build samples/MyDmsVn.Desktop.UiLab.Security/MyDmsVn.Desktop.UiLab.Security.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 -v minimal
```

Discovery exit 0, 20 actual Fact/Theory rows. Run exit 0, duration `52 s`; TRX counters total/executed/passed `20`, failed/error/timeout/aborted/notExecuted `0`. Raw TRX remains ignored/local at `TestResults/cmp-t04-catalog/cmp-t04-catalog.trx`; no raw result or screenshots committed. Assembly disables test parallelization; native rows use existing STA/pump/guard/finite waits. GUI work was serialized, with computer-use Lab actions only after test process completion; no remaining Lab/testhost process after close.

SHA-256 of actual outputs: UI test DLL `7487594810a85be5e4582d0c653f2b2b9919fdad6383f49bbf1f77e4f72ad599`; Security launcher `a483fbc1b56b6e9829d2d4e0f3bd0a97643b8b65fcaf3bf878eecc58bca97c7d`; production WinForms DLL `16b240f2aed38d05830249110bbb80f3c784535bc77370a057459be4338313ea`. These are development build outputs, not a release package.

### BC-04/05/09/11 và native observations

| Policy | Evidence mới và disposition |
|---|---|
| BC-04 | Product-host TRX: normal outer `1200×760`, shell client `1184×721`, Security content `952×657`; minimum `900×600`, `884×561`, `652×497`; cả hai themes PASS, DeviceDpi=96. Lab launcher outer `1250×850`, client `1234×811`; fixed content lần lượt `952×657` / `652×497`. Diagnostics ban đầu báo normal viewport clipped khi tools/journal hiện; F11 giữ content `952×657` và báo visible, không tăng target viewport. Minimum visible với tools. |
| BC-05 | Filter test mới đo Search/Lifecycle/Refresh cao `32` tại DeviceDpi=96 ở Light/Dark. Host `1200`: filter width `884`, Refresh `(365,3,110,32)`; host `550`: width `384`, Refresh `(3,41,110,32)`, mọi filter trong client bounds. Source row metric vẫn `max(Scale(30), Font.Height+8)`, copy height `34`, details height `116`, padding `8`; không đổi metrics. Tại fixed minimum, Refresh wrap và Copy/Details/count còn hiện, grid cuộn cục bộ. Long friendly name/canonical key ellipsis trong grid nhưng full accessible cell/details vẫn có; chọn group `UiLab.ReviewOnly` cho `1 / 8` và details wrap key dài. Không thấy gap cần ép chiều cao/font hoặc horizontal workspace scroll. |
| BC-09 | Hai lệnh catalog là `Làm mới` và `Sao chép mã quyền`, secondary outline; không có Save/Create/Edit/Delete hoặc assignment. Native per-character `Sales.` giữ raw text, caret/range, focus và selected `Sales.Receipts.Post`; Enter không tạo `Security.Mutate`. Copy test đọc clipboard đúng selected key rồi restore clipboard. |
| BC-11 | Focused tests xác nhận read-only grid/details, selected key/native row/scroll identity, friendly-name/module/lifecycle/no-match filtering, loading→empty và failed/forbidden scrubbing mà không xóa accounts. Source giữ count “chưa khả dụng” khi dataset không current; empty dataset khác no-match. Test assertions ghi nhận read-only flows không có `Security.Mutate`; Lab journal ban đầu chỉ Users/Roles/Catalog và các read results. Light→Dark tại minimum giữ `Sales.Receipts.Read`, selected row/details; group long-key trả đúng selected definition. |

Launched exact `samples/MyDmsVn.Desktop.UiLab.Security/bin/Release/net48/MyDmsVn.Desktop.UiLab.Security.exe`, fixture SEC_L01, tab **Danh mục quyền**, sản phẩm dùng `BusinessCompactAppearance` qua bootstrap hiện hữu. Quan sát bằng computer-use, không bản sao/demo. Native OS ComboBox/scrollbar chrome còn theo hợp đồng pin; không thêm native/custom exception. Khi index tab từ UI snapshot bị stale, input bị từ chối; reobserve rồi dùng điểm trên ảnh mới thành công. Đây là tool targeting retry, không phải test failure hoặc lỗi catalog. Các captures chỉ xem cục bộ trong lượt kiểm tra, không tạo board/approval mới.

**Giới hạn:** physical100/125 không chạy lại trong CMP-T04; lượt bổ sung không xác minh độc lập scale và không phải attended Owner review. Không có candidate visual mới, không ghi approval mới. Không chạy SQL, actual Login, package/release, Security full matrix, dialog rewrite hoặc CMP-T05 fresh-context review. No-gap disposition chỉ áp dụng UIC-01 catalog trong scope đã chọn; không đóng source-focus confirmation `3b5f95e2` hay acceptance lịch sử.

## CMP-T05 — routing review and handoff

**COMPLETE — docs-only, 30/10/2026.** Task dùng báo cáo read-only độc lập trong context sạch do controller cung cấp, sau đó đối chiếu lại source/git objects trước handoff. Không sửa skill vì review không phát hiện routing defect; không sửa production, samples, tests, project graph hoặc runtime pin. Smallest verification scope là diff/link/object/status/encoding; **NOT RUN** build, test, GUI, SQL, actual Login hoặc package/release trong CMP-T05. Kết quả 20/20 và các quan sát ở CMP-T04 giữ nguyên tác giả, candidate và giới hạn của lượt đó.

### Clean-context routing review

Lượt độc lập được giao đánh giá bố cục Danh mục quyền hiện có theo Business Compact và đề xuất phạm vi thay đổi; prompt không cung cấp trực tiếp tên/path recipe. Reviewer tự tìm AGENTS → repository UI skill → execution index/plan → mapping → exact recipe source/handoff; tìm đúng `SecurityPermissionCatalogView`, product factory và Security Lab dùng production controls/fake ports. Kết luận **NO-CHANGE** cho catalog trong scope hiện có, không tạo diff chỉ để thể hiện adoption.

Reviewer phân biệt recipe source với runtime gitlink, sample/helper với public UI-core API; nhận diện hai-pane catalog, filter/state/count semantics, read-only/details/copy, canonical identity và app session/lifetime ownership. Reviewer chọn đúng test project `MyDmsVn.Desktop.Ui.Tests`, method-qualified selectors theo changed boundary tại plan §5.2 và owners native typing/filter geometry. Với review docs-only, reviewer không chạy lại GUI/SQL hoặc full classes/solution. Báo cáo nguồn local `cmp-t05-clean-context-report.md` thuộc workspace SDD của plan; kết quả bền vững được ghi tại mục này, không phụ thuộc file scratch để đọc handoff. Lượt routing không phải native evidence hoặc Owner visual decision.

### Exact refs và candidate continuity at original CMP-T05 handoff

| Ref | Đối chiếu tại CMP-T05 |
|---|---|
| App inspected HEAD trước handoff | `4c05c7598d86b45dbd1bb90c2098733394c063e8`; tracked working tree sạch lúc bắt đầu |
| Recipe source đã được Owner duyệt | Toolkit `1420f252ca198cd4409e0e945fa6433b30b943b3`, cả hai forms có tại object này |
| Toolkit source/docs/status sau merge PR #6 | `d439cbdb043e1b798d1f991f0266350d9f61441e`; recipe source là ancestor; delta source → handoff chỉ là bốn file Markdown, không đổi samples/tests/runtime |
| Toolkit task owner | [Companion ledger tại handoff](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/d439cbdb043e1b798d1f991f0266350d9f61441e/docs/plans/20261030-001-ui-composition-recipes.md): CMP-T01/T02 IMPLEMENTED; focused automation hai TFM PASS; bounded representative attended matrix PASS; Owner APPROVED `1420f25` ngày 30/10/2026. Frozen handoff còn header ACTIVE và một số step checkboxes native/review chưa đánh dấu; task ledger cùng dated attended closure ghi rõ kết quả ở trên. App giữ nguyên metadata của owner toolkit, không tự ghi toolkit plan COMPLETE hoặc dùng checkbox cũ để xóa kết quả đã có evidence. |
| Recipe docs/evidence | [Guide](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/d439cbdb043e1b798d1f991f0266350d9f61441e/docs/guides/UI_COMPOSITION_RECIPES.md), [verification/attended closure](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/d439cbdb043e1b798d1f991f0266350d9f61441e/docs/verification/20261030-ui-composition-recipes.md); fixed source links cho UIC-01/UIC-02 nằm trong [mapping](../../design/UI_COMPOSITION_MAPPING.md) |
| Runtime toolkit / SourceGrid | `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` / `f4e457b43582bf01892f50bdc74aa480531e5944`; giữ nguyên. Không reference Gallery/demo assembly. |
| Production/sample/test continuity | `src` `a5083a939408501cdf86e40fc6548e2b4323e5a4`; `samples` `53f7fe35de808c5c6d732791a3da753e31ae2bea`; `tests` `900e01fa440760c563edad23820ad88763562735`, đều trùng CMP-T04 và consumer candidate `95916e33650648ef9753223605b5e679d6944d2a` |

`git ls-tree HEAD vendor/MyDmsVn.WinFormsKit`, recursive toolkit tree, `git rev-parse HEAD:src HEAD:samples HEAD:tests`, `git diff 95916e33650648ef9753223605b5e679d6944d2a HEAD -- src samples tests`, toolkit `git merge-base --is-ancestor` và `git diff --stat` xác nhận các facts trên. Đọc object bằng `git show`, không checkout/nâng dependency hoặc lấy trạng thái latest upstream. Runtime → recipe `src` delta thuộc tabs/busy; public controls dùng bởi mapping vẫn có tại pin. Product, Security launcher và UI test project vẫn target net48 qua graph hiện hữu.

### UIC-02 compatibility disposition

Đã đọc [shared identity builder tại app inspected HEAD](https://github.com/chung6a8m/MyDmsVn/blob/4c05c7598d86b45dbd1bb90c2098733394c063e8/src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceEditorBindings.cs), guide UIC-02 và [modal exception evidence](20261028-business-compact-exceptions.md). “Sửa thông tin…” dùng `IdentityDialogAsync(true, false)` → `ShowIdentityDialogAsync(true, false)`; toolkit labels/inputs, validation state và first-invalid focus, Save Primary/Cancel Secondary, `MutationDialog : BootstrapModal` cùng body/footer public phù hợp cho tham chiếu recipe. App giữ authoritative mutation/server feedback, busy/close admission, recovery và originating owner/session guards.

**NO-CHANGE; chưa có gap cụ thể để chọn follow-up.** Validation label `MaximumSize=400` khác recipe tính theo content, nhưng source-only khác biệt chưa chứng minh clipping. Cancel của app đóng theo admission, còn demo phục hồi snapshot cục bộ: đây là khác biệt contract hợp lệ. Long-error wrapping tại mọi viewport/DPI chưa có bằng chứng mới; không tuyên bố dialog đạt recipe acceptance. Không sửa `ShowIdentityDialogAsync`, shared builder, related dialogs hoặc secret clearing; mọi follow-up về sau cần một gap có thể tái hiện và selection riêng, với affected create-user/create-role/edit-role branches được xét đúng phạm vi.

### Đối chiếu §7 và completion boundary

| Deliverable §7 | Evidence và quyết định |
|---|---|
| Hai mẫu chạy được + tests | Source `1420f25`, toolkit ledger/evidence tại `d439cbd`; đạt đúng bounded scope của toolkit owner |
| Recipe purpose/source/API/states/layout/ownership/evidence | Guide tại handoff và hai exact source links; đạt |
| Skill → mapping/source, demo/API distinction | CMP-T03 mapping và clean-context discovery CMP-T05; đạt, không cần sửa routing |
| Pilot production controls/Lab/BC/runtime pin | CMP-T04 production SEC_L01, normal/minimum và public APIs tại runtime pin; đạt, no source change |
| Catalog read-only/selection/details/copy/state, không Security.Mutate | CMP-T04 focused 20/20 và source/state ownership; đạt trong scope catalog |
| Native candidate evidence và limits giữ đúng | Toolkit bounded attended/approval riêng; app prior physical100/125 đúng candidate cũ + CMP-T04 supplementary DeviceDpi=96, physical scale unverified; đạt nghĩa vụ ghi evidence, không có approval app mới |
| Clean-context routing | Lượt độc lập tìm đúng đường mà không được chỉ recipe; đạt |
| Hai repo refs/status thống nhất, pin giữ nguyên | Exact merged toolkit refs, task ledger/dates và app §0/CMP-T03–T05 đối chiếu đúng deliverables; đạt. Frozen toolkit header/step metadata được giữ rõ ở trên, không tuyên bố toolkit plan đã đổi COMPLETE; runtime pin giữ nguyên |

Vì toàn bộ deliverables §7 đã đạt và không có gap layout/tương tác còn mở trong scope được chọn, [app plan](../../plans/20261030-001-business-compact-composition-adoption.md) ghi **COMPLETE — bounded composition adoption**. UIC-02 chỉ cần compatibility disposition; không phải một pilot dialog mới. Không có candidate app visual mới nên không thêm Owner gate mới. Completion không có nghĩa mọi state ở mọi tổ hợp đã được attended; không chuyển approval toolkit thành approval app, không đóng confirmation `3b5f95e2` hoặc historical SEC/SAL/release limits.

Không có nhu cầu recipe/helper/runtime upgrade hoặc dialog follow-up được chứng minh trong task này. Điều kiện mở rộng master-detail, SourceGrid editing, public helper và SAL ở plan §8 vẫn là tham khảo chưa được chọn; không thêm roadmap task. Thời gian tìm mẫu và số vòng sửa layout của lần áp dụng thực tế tiếp theo chưa đo, không đưa ra phần trăm tiết kiệm.

Docs verification: `git diff --check`; local links/anchors và exact GitHub source/blob/commit paths qua local git objects; sole status owner, §7 checkboxes và planned-vs-source distinction; UTF-8 strict/no BOM, CRLF working tree/LF staged blobs. Không chạy GUI/SQL/build/test cho Markdown.

## Remaining limits

Scope composition adoption đã hoàn tất có giới hạn; không còn task CMP-T03–T05 mở. Không có app Owner visual decision mới; CMP-T04 supplementary observation không là physical100/125 repeat. UIC-02 chưa có phép đo native mới. Confirmation `3b5f95e2`, historical acceptance và release prerequisites giữ nguyên trạng thái; không mở SQL, release, dialog rewrite hoặc archived acceptance.

## Post-handoff accessibility correction

**DOCS-ONLY app handoff, 30/10/2026.** Correction source/test toolkit `bdac29bf4aca20db6618570ecd9a354630bc2e5f` bỏ `AccessibleName` tĩnh của năm label thông báo động trong hai sample ControlGallery, để `AccessibilityObject.Name` phản chiếu `Text` hiện tại. Toolkit guide/plan/evidence/status tương ứng là `078eb348ac40b7a7e762a9b3f8b4da3fb19f314c`; [mapping](../../design/UI_COMPOSITION_MAPPING.md) dẫn tới source và documents cố định. Visual candidate Owner-approved `1420f252ca198cd4409e0e945fa6433b30b943b3` giữ nguyên là quyết định lịch sử, không được áp lại cho correction accessibility hay AT.

App chỉ cập nhật mapping, entry pages và record handoff. `git ls-tree HEAD vendor/MyDmsVn.WinFormsKit` vẫn là `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`; không đổi app production source, sample, test, project/package graph, SQL, Lab launcher hoặc SourceGrid. Runtime pin đã có public Label/Text và controls mà mapping dùng; không checkout/nâng pin hoặc reference ControlGallery/demo assembly.

Toolkit evidence ghi RED 3/3 đúng boundary accessibility trên mỗi TFM, sau correction discovery 15 cases và execution tuần tự Release net48/net8.0-windows đều 15 PASS / 0 FAIL / 0 SKIP. Đây là evidence của toolkit sample/API và không thay cho app GUI/runtime test. App không chạy build, test, GUI, SQL, actual Login, package/release, Gallery physical100/125, Narrator hoặc NVDA cho docs-only handoff này. Layout/visual không được re-accepted; không thêm Owner gate chỉ do metadata accessibility. Confirmation `3b5f95e2` và mọi giới hạn SEC/SAL/release giữ nguyên.

Docs verification cho record này: diff/link/status/encoding và local git-object review của cả source `bdac29b`, handoff `078eb34`, historical visual candidate `1420f25` và runtime gitlink. Không có claim product or screen-reader acceptance mới.
