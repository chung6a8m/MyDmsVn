# Business Compact lookup and visual parity implementation plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. Track the checkboxes and preserve the two-phase ordering. Creating this roadmap does not execute its implementation tasks.

**Goal:** Repair manual typing in the three Goods Receipt lookups first, then complete the shell, Products catalog and receipt presentation against the three owner-provided designs.

**Architecture:** Keep the existing WinForms/Local application, pinned SourceGrid editors and business contracts. Correct application-owned lookup synchronization without reselecting an unchanged committed identity during pending input. Refine the shared shell and catalog, then present the existing receipt control in guarded List/Detail modes without replacing its document, adapter or session.

**Tech stack:** C#, WinForms, .NET Framework 4.8, Windows x86, Bootstrap5WinFormUI, BootstrapSourceGrid/SourceGrid, CommunityToolkit.Mvvm and the existing xUnit/native GUI test owner.

**Spec:** [BCF design](../BUSINESS_COMPACT_REFINEMENT.md). **Acceptance:** [BCF-A01–A23](../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md). **References:** [three original mockups](../design-references/business-compact/README.md).

**Status:** ACTIVE — 2026-10-21. BCF-T00 COMPLETE as a test-only RED checkpoint ([evidence](../evidence/bcf/20261021-t00-native-typing.md)); T01 COMPLETE with [focused hosted Windows GREEN evidence](../evidence/bcf/20261021-t01-lookup-binding.md). T02 OPEN; T03–T06 NOT STARTED. Full A01–A13 acceptance awaits T02.

**Planning baseline:** `v1a-pro@6040d7bcfca698fc9daba8268bb94e4454e8eacb`, the merge of [PR #40](https://github.com/chung6a8m/MyDmsVn/pull/40). Its product tree matches reviewed head `5ac2bb592aaa947245ea27f9e53b5bc39aaa8ec1`. BCI is completed history; this is the sole active roadmap.

## Global constraints

- Product and all ten active test projects remain `net48`; shipped Desktop/admin packages remain Windows x86. AppCore stays parked.
- Default vendor pins remain BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77` and SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- Repair exactly `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`. Inline Product is a reference and regression target, not a fourth reported broken control.
- Preserve API/DTO/schema, permissions, explicit UoW, rowversion, Draft/Post, ledger/balance, session and SQL-resource ownership. No new receipt fields or business modules.
- Preserve strict numeric parsing, culture, full decimal bounds, pending versus committed values, historical lookup labels, row identity, dirty guards and `RequiresPostOutcomeReload`.
- Phase 2 now includes Products content and detail-focused receipt navigation. This explicitly supersedes the old BCI catalog exclusion and always-visible receipt split only for the new implementation.
- Keep existing English UI copy; reference Vietnamese labels and sample data are visual examples. No wholesale localization or pixel-identical rendering promise.
- Use supported public APIs/application-owned seams. Any proven vendor gap needs a narrow compatible upstream change, its own tests and explicit pin provenance; no blanket upgrade, private-child patch or silent grid replacement.
- GUI work uses interactive Windows, serialized guarded STA, finite waits, the existing bounded GUI-owned processes and positive cleanup. No restored runner/manifest/approval-file framework.
- Markdown is UTF-8 without BOM, CRLF in the working tree and LF in Git blobs. Preserve historical evidence and failures.

## Review focus

1. Completion of a canceled earlier search can run ApplyState between `u` and `i`; T00/T01 must assert the expected prefix and native selection after every step, ending with `gui`, caret 3 and unchanged committed ID.
2. Shared warehouse results can rebind both the list filter and header; T01 must keep each host's pending query and logical role independent, including null filter and inactive historical selection.
3. Back-to-list mouse-down can commit a highlighted lookup before a click guard; T05 must admit navigation before native focus validation and preserve rejected pending numeric input.
4. Catalog layout changes affect a shared control used by four catalog kinds; T04 must preserve every field/action and CatalogChangedMessage-driven lookup reload without adding business operations.
5. Graphite, resize and native 125% can hide focus or clip real editors even when screenshots look acceptable; T03–T06 must measure native bounds and retain raw input/selection through transitions.

## Delivery order and gates

| Task | Depends on | Deliverable | Acceptance |
|---|---|---|---|
| BCF-T00 | Baseline inspection | Reproducible native per-key failing regression proof | A01–A03 |
| BCF-T01 | T00 | Minimal binding repair and boundary regression coverage | A01–A12 |
| BCF-T02 | T01 | Focused Windows gate for all three repaired controls | A01–A13 |
| BCF-T03 | T02 PASS | Shared shell, visual tokens and Light/Graphite chrome | A14–A15, A18, A20–A21 |
| BCF-T04 | T03 | Products toolbar/results/inspector; shared-catalog regressions | A16, A18, A20–A21 |
| BCF-T05 | T04 | Guarded detail-focused receipt and command hierarchy | A17–A21; recheck A01–A12 |
| BCF-T06 | T00–T05 | Frozen integrated candidate, comparisons and owner acceptance | A01–A23 |

The A-prefix abbreviates BCF-A. **T02 must pass before visual implementation starts.** Documentation preparation does not satisfy that gate. A failing/blocked case stays open; do not compensate by redesigning controls first. T00 test-only commits may retain the expected failing proof on the working implementation branch, but no repair milestone is accepted until T01/T02 are green.

## Source and proposed file map

Existing source anchors are `GoodsReceiptControl.cs`, `GoodsReceiptControl.InlineEditing.cs`, `GoodsReceiptLinesGridAdapter.cs`, `ReceiptEditIntent.cs`, `BusinessCompactAppearance.cs`, `BusinessCompactNavigation.cs`, `FoundationShellForm.cs` and `CatalogControl.cs` under `src/MyDmsVn.Desktop.WinForms/`.

Read `CatalogLookupViewModel.cs`, `DebouncedAsyncAction.cs`, `GoodsReceiptEditorViewModel.cs` and `CatalogViewModel.cs` under `src/MyDmsVn.Desktop.Application/` before changing binding or presentation boundaries. The current `CatalogControl` contains its binding adapter and is shared by Products/Warehouses/Employees/Customers; do not assume separate per-kind control classes exist.

| Proposed new file | Responsibility |
|---|---|
| `tests/MyDmsVn.Desktop.Tests/NativeLookupTypingDriver.cs` | Focused per-character native input and caret/range observation, reusing existing test primitives |
| `tests/MyDmsVn.Desktop.Tests/GoodsReceiptLookupFixture.cs` | Hosted receipt with controllable delay/source completion; owns and drains all work |
| `tests/MyDmsVn.Desktop.Tests/GoodsReceiptLookupTypingTests.cs` | Native keyboard reproduction and retention cases |
| `tests/MyDmsVn.Desktop.Tests/GoodsReceiptLookupBindingTests.cs` | Binding identity, history, cancellation and command/lifecycle boundaries |
| `tests/MyDmsVn.Desktop.Tests/BusinessCompactRefinementTests.cs` | Shared shell/catalog/receipt composition and native focus/resize/theme assertions |
| `src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.Presentation.cs` | Proposed partial owning List/Detail presentation and its guarded transitions |
| `src/MyDmsVn.Desktop.WinForms/ReceiptWorkspaceMode.cs` | Proposed `List`/`Detail` presentation enum |

The native typing driver, fixture and typing tests above are implemented at the T00 checkpoint; T01 implements the binding tests and extends the existing driver/fixture. The refinement tests and presentation files/APIs remain proposed. Keep test helpers inside the existing Desktop test project. Extract only the layout responsibilities needed to keep the current receipt control understandable; do not perform an unrelated architecture rewrite.

## Verification convention

Future task commands run from the repo root on a provisioned interactive Windows session. Inspect `global.json`, project references and current discovery first. A selector referring to a proposed class is runnable only after that class has been implemented.

```powershell
$bcfProject = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$bcfFilter = 'FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests'
$bcfResults = 'TestResults/bcf-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
dotnet restore $bcfProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $bcfProject -c Release -f net48 --no-restore -p:PlatformTarget=x86 `
  --filter $bcfFilter --logger 'trx;LogFileName=bcf-task.trx' --results-directory $bcfResults `
  -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'BCF task verification failed; inspect actual TRX.' }
```

For T00, the intended product assertions must fail and be recorded; a driver/focus/compile failure, zero discovery, skip or hang is not a valid RED. For later steps, reconcile actual discovered/executed Facts and Theory rows with no missing mandatory case. Use `--no-build` only for verified identical outputs. No fixed historic case total is a target.

### BCF-T00 — native per-key reproduction before repair

**Files:** create the proposed driver, fixture and typing-test files above; read `GoodsReceiptInlineEditorTests.cs`, `InlineReceiptTestDriver.cs`, `GoodsReceiptControlTests.cs`, `StaTest.cs` and `WinFormsTestGuard.cs` in the existing Desktop test project.

**Interfaces:** proposed internal `NativeLookupTypingDriver(BootstrapLookupBox lookup)` verifies the real focused native EDIT belongs to that control; `void TypeCharacter(char value)`, `void SelectRange(int start, int length)`, `void Key(Keys key)`, and read-only `Text`, `SelectionStart`, `SelectionLength` expose observable input. Reuse existing native key/mouse primitives and bounded STA pumping. The fixture supplies controlled delay/source barriers rather than sleeping or blocking the UI dispatcher.

- [x] Add Theory cases for each of the three controls: `First_character_survives_result_and_operation_completion`, `Cancelled_preceding_search_preserves_per_key_order`, and `Rebinding_preserves_mid_string_caret_and_selected_range`.
- [x] Make the fixture start with null filter and selected Warehouse/Employee; queue `g`, then `u`, complete the canceled earlier operation while the latest search remains pending, then type `i`. Separately complete a response between two keys. Assert after each step; do not assign the whole query through Text/UIA.
- [x] Use exact success assertions such as `Assert.Equal("gui", driver.Text)`, `Assert.Equal(3, driver.SelectionStart)`, `Assert.Equal(0, driver.SelectionLength)`, plus unchanged committed ID, native focus and `HasPendingText`. For range replacement, select `u` in `gui`, type `a`, complete rebind, and assert `gai`, caret 2 and selection length 0.
- [x] Run the typing selector against the unmodified product baseline and retain sanitized expected-failure evidence, including failing assertion and source/test revision. Confirm the filter-clear and header-caret defects independently; the broken string need not always equal `igu`.
- [x] Commit the test/proof work as a reviewable T00 checkpoint; leave production code unchanged until the failure is explained. Gate status remains OPEN until T01/T02 pass.

T00 checkpoint: test commits `845ca0a` and `dbd0798`; final source selects 15 Theory rows (five per target), all fail the intended product assertions with native ownership, STA mutation, guard and drain checks satisfied. [Sanitized evidence and retained invalid attempts](../evidence/bcf/20261021-t00-native-typing.md) distinguish hosted DPI 96 from OS scaling and leave the full T02 matrix uncredited.

### BCF-T01 — minimal binding repair and boundaries

**Files:** modify `src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs`; create the proposed binding-test file; extend typing fixture/cases as needed. Inspect pinned `BootstrapLookupBox.Data.cs` and existing `GoodsReceiptLinesGridAdapter.BindProductOptions()`; no vendor change is expected for this defect.

**Interfaces:** preserve the existing `BindEditorLookup(BootstrapLookupBox lookup, IEnumerable<CatalogLookupOption> options, int? selectedId)` helper contract or a clearly named app-only equivalent used by all three targets. Ordinary result binding synchronizes options and changed committed identity; explicit document transitions separately cancel pending query presentation and invalidate requests.

- [x] Add focused cases `Same_identity_binding_preserves_pending_text_and_selection`, `Shared_warehouse_results_preserve_each_hosts_role`, `Only_explicit_selection_changes_filter_or_header`, `Historical_selection_survives_search`, and `Late_completion_cannot_publish_after_boundary`.
- [x] Remove unconditional reselect-and-restore-text behavior when committed identity is unchanged. Let the pinned DataSource reconciliation retain pending input; use explicit selection only for an actual identity/context transition. Preserve optional active-only filter semantics and historical selected header labels. Do not fix only by moving caret to Text.Length or increasing debounce.
- [x] Exercise a canceled operation's ApplyState and an ignored-cancellation late response. Keep UI updates harmless to newer input; narrow irrelevant rebinding/continuations only if needed and proven. Do not split the shared lookup ViewModel solely to mask the binding fault.
- [x] Add real F4/Up/Down/Enter/Escape/Tab/Shift+Tab cases, paste and popup mouse/outside-click/deactivation cases, non-first result choice, rejected dirty New/select/Refresh, admitted query-only cancellation, Posted/busy/access/recovery locks, disposal/session replacement and inline Product regression cases. Assert model/filter effects as well as rendered input.
- [x] Run the T00/T01 selectors plus affected `GoodsReceiptControlTests`, `GoodsReceiptInlineEditorTests` and `InventoryAccessibilityAcceptanceTests`. Add focused Session lookup cases if their shared logic changes. Inspect TRX, verify no leaks/late callbacks, then commit the repair.

T01 checkpoint: 172 discovered/executed/passed rows, including 92 binding and the unchanged 15 T00 typing rows. [Evidence](../evidence/bcf/20261021-t01-lookup-binding.md) records exact revisions, failed trials, the independent review/admission repair, native owned-message input limits and scoped verification ruling. This hosted result does not satisfy T02's native100/125 Light/Dark, attended or package obligations.

### BCF-T02 — close the lookup gate on Windows

**Files:** update `docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md` with observed lookup evidence; extend existing bounded `GuiPackageProbe.cs`/`PackagedGuiAcceptanceTests.cs` only where the test-host proof cannot establish the native packaged path.

**Interfaces:** consumes T00 native driver/cases and T01 binding semantics; produces candidate-attributed BCF-A01–A13 evidence. It does not create another test runner or claim BCF visual completion.

- [ ] Run the focused native-key/binding matrix at actual 100% and 125%, Light and Dark, one GUI owner at a time. Include the list filter and both headers explicitly; check native focus, caret/range, dropdown, cleanup and committed IDs.
- [ ] Perform an attended per-key smoke for `g`, `u`, `i`, slow typing, mid-string correction and selection in each control on the built candidate. Record observations independently from automation. Use controlled sources for deterministic timing; use the real Local/package path where required by the acceptance boundary.
- [ ] Reconcile actual selectors/discovery/TRX, product/test hashes, Windows/monitor scaling, results and disposal. Preserve failing baseline and earlier failed attempts. No unrelated full SQL suite is required for application binding; package smoke uses the existing owned fixture when it needs SQL.
- [ ] Mark A01–A13 PASS only with their evidence. Missing Windows, mandatory control/theme/scale, exception-free completion or cleanup leaves T02 BLOCKED/OPEN. Commit the ledger; only then admit T03.

### BCF-T03 — shared shell and Light/Graphite visual system

**Files:** modify `BusinessCompactAppearance.cs`, `BusinessCompactNavigation.cs` and `FoundationShellForm.cs`; create `BusinessCompactRefinementTests.cs`; extend `FoundationShellFormTests.cs` and `BusinessCompactLayoutTests.cs`.

**Interfaces:** reuse `BusinessCompactAppearance.Apply(Control)` and `Scale(Control,int)`, existing navigation Items/Icon/selection events and shell workspace/session actions. Shared style ownership must not dispose vendor-owned fonts or editors. Any new appearance helpers remain application-owned and documented in their owning file.

- [ ] Add composition/focus cases `Shell_has_grouped_icon_navigation_and_compact_session_actions` and `Graphite_chrome_and_focus_remain_readable_after_theme_switch`; preserve the existing nested/disabled/collapsed navigation checks.
- [ ] Implement brand area, meaningful module icons, section labels, selected state, user context, compact theme/session controls and integrated tab appearance from references 01/03. Preserve existing module permission and workspace disposal behavior.
- [ ] Apply semantic surface/border/text/selection/focus/error tokens through supported APIs. Resolve bright tab/input/selector islands in Dark; document any unavoidable native chrome precisely instead of silently declaring parity. If a public seam is missing, prove it and scope a compatible traceable upstream change before advancing that pin.
- [ ] Verify 224/64 logical navigation widths, keyboard access, accessible names, focus contrast, resize/theme continuity and native editor bounds. Run the focused shell/refinement/layout tests and lookup regression selector.
- [ ] Capture matched Light/Graphite shell evidence at the specified client sizes with actual DPI metadata. Review composition against references, record remaining native limits and commit.

### BCF-T04 — Products toolbar, results and inspector

**Files:** modify `src/MyDmsVn.Desktop.WinForms/CatalogControl.cs` and shared appearance only as needed; extend `CatalogControlTests.cs` and `BusinessCompactRefinementTests.cs`.

**Interfaces:** reuse the existing catalog binding adapter, selected ID, field definitions and New/Save/Cancel/Activate/Deactivate/search/paging commands. Use actual result count; preserve CatalogChangedMessage notifications and open receipt lookup reload behavior. Other catalog kinds retain their fields and workflow.

- [ ] Add `Product_catalog_composition_matches_reference_in_both_themes`, `Catalog_toolbar_preserves_search_paging_and_editor_commands`, and a Theory covering all four catalog kinds for shared layout/validation/activation behavior.
- [ ] Place Products title/count, search, Refresh and Add product in a coherent toolbar; show a dense grid beside the inspector. Add inspector heading/code, textual status badge, basic-information section, Save/Cancel group and distinct Activate/Deactivate action using existing data.
- [ ] Preserve selection, explicit create/cancel behavior, server validation, busy/error state and query cancellation. Layout/theme changes must never reset an edit. Retain existing selection/new command semantics; do not silently add a new catalog persistence or discard workflow.
- [ ] Verify empty/no-selection/loading/error and active/inactive states with multiple pages and long Unicode names. Run catalog/refinement tests plus focused catalog-change/lookup regressions; ensure changes to shared layout do not remove another catalog's fields/actions.
- [ ] Capture reference 01/03 comparisons with the same synthetic data, selected record, page and viewport. Inspect density, typography, row status/selection and inspector hierarchy; commit the reviewed screen.

### BCF-T05 — guarded detail-focused receipt presentation

**Files:** create `GoodsReceiptControl.Presentation.cs` and `ReceiptWorkspaceMode.cs`; modify `GoodsReceiptControl.cs`, `GoodsReceiptControl.InlineEditing.cs`, `GoodsReceiptLinesGridAdapter.cs`, `ReceiptEditIntent.cs` and only necessary line-grid presentation code. Extend `GoodsReceiptControlTests.Inline.cs`, `BusinessCompactLayoutTests.cs`, `BusinessCompactRefinementTests.cs` and relevant accessibility tests.

**Interfaces:** proposed `internal enum ReceiptWorkspaceMode { List, Detail }`; proposed `GoodsReceiptControl.TryShowReceiptList(): bool`; add `ReceiptEditIntent.ReturnToList`. Add ReturnToList to the navigation branch of `GoodsReceiptLinesGridAdapter.TryFinishEdit` (New/Select/Refresh), avoiding the default committing `EndEdit(false)` path. Route Back through navigation admission/dirty/busy/recovery guards before native focus validation, not `CanEditLines()`. Mode changes reuse the same control/editor/adapter and do not load/dispose/refresh or commit a document merely to change presentation.

- [ ] Add `Back_to_list_rejects_dirty_or_pending_numeric_input`, `Back_mouse_down_does_not_commit_highlighted_lookup`, `Allowed_query_only_back_cancels_requests_without_committing`, `Mode_switch_preserves_list_and_document_identity`, and `Receipt_composition_and_commands_match_reference`. Parameterize Back mouse-down/admission/abandoned-click cases over pending Warehouse, Employee and inline Product; the current focus-retention predicate covers only inline sessions and must be extended. Add clean Posted Back and denied-list clearing cases. Replace the old always-visible 260-pixel split assertion deliberately.
- [ ] Open Goods Receipts in List mode; successful New/open enters Detail with list hidden. Back restores list search/filter/page/selected ID/scroll. Reject dirty/pending-data/busy/uncertain-Post Back with original raw text/caret/focus; admitted clean query-only Back explicitly cancels header/line query presentation and stale requests. Allow clean Posted detail to return to the list; if list access is denied, show its existing cleared denied state and do not restore cached rows. Reopening the retained clean document preserves line selection; actual New/load boundaries remain explicit.
- [ ] Build title + receipt number + textual status/saved-state hierarchy. Keep opaque Version in the model, remove it from the primary visual surface. At sufficient width place Date/Warehouse/Employee in three columns and full-width Note below; reflow without losing focus/input.
- [ ] Put document commands near the header with Post primary and Save secondary. Put line count/Add above the grid and a visible accessible Remove action for the selected row; keep explicit keyboard removal and stable row identity. Keep Total prominent and error/recovery text visible. No implicit Save-before-Post, auto-new row or destructive discard command.
- [ ] Ensure Product/Quantity/Unit cost/Total coexist at the reference wide viewport; use full-width detail to recover space, not reduced numeric safety bounds. Constrained/max-value cases may scroll with recorded reachability. Run affected receipt/inline/accessibility/refinement tests and all three lookup regressions after moving their controls.
- [ ] Capture multi-line Light/Graphite receipts, saved/dirty/invalid/Posted/recovery states and list/detail transitions. Recheck row identity through scroll/resize/theme and shell session teardown. Commit the presentation with focused evidence.

### BCF-T06 — frozen comparison and integrated acceptance

**Files:** update `docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md`; create reviewed captures and an index under `docs/evidence/bcf/`; update current status pointers only after actual acceptance. Existing package/owned-process test infrastructure remains the execution mechanism.

**Interfaces:** consumes completed T00–T05 contracts and BCF-A01–A23; produces exact source/test/package evidence and explicit owner visual sign-off against all three reference files.

- [ ] Freeze product/test/vendor revisions and build the actual Windows x86 package through `deployment/release/Publish-LocalRelease.ps1` using its documented parameters. Inspect current test discovery; do not reuse historic 415/108 counts.
- [ ] Run one reconciled integration round on the frozen candidate: complete Desktop.Session once for its platform-independent scope, complete Desktop GUI at native100 and native125, and the required existing bounded packaged scenarios. Theme-specific obligations must execute in both themes; do not multiply unchanged SQL-owner suites or repeat full runs after every small edit.
- [ ] Exercise real packaged Login, catalog edit/message-to-open-lookup refresh, receipt create/Save/reopen/edit, explicit Post and uncertain-Post reload. Use the existing owned SQL fixture and independent assertions for saved IDs/version and exactly-one posting effects where these workflows require them; retain positive cleanup.
- [ ] Produce matched comparisons for catalog Light/Graphite and receipt Light/Graphite, with multi-page/multi-line fixtures, actual client bounds/DPI, selection and scroll offsets. Verify 1680x900 and 1200x760 logical targets plus 944x600 reachability. If a physical monitor cannot show a target, record the fitted size and leave that full target NOT TESTED until suitable equipment or an explicit bounded owner exception exists.
- [ ] Repeat the native per-key proof after all visual changes. Reconcile every mandatory acceptance row, accessibility/focus/error/readonly states, capture provenance and failed trials. Record native rendering exceptions by control/state and evidence; a generic WinForms limitation cannot close a visual gap.
- [ ] Obtain owner review of the actual comparisons against all three designs, record the accepted candidate and any narrowly scoped exception, then mark BCF complete and update entry docs/index. A documentation commit or previous BCI sign-off cannot supply this acceptance.

## Completion boundary

The planning commit established this active queue, source baseline, reference images and unexecuted obligations only. The subsequent T00 test-only checkpoint records intended native-key RED failures; it does not repair lookup behavior, implement visual refinement or grant acceptance. Next implementation task is T01; use [the acceptance ledger](../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) for actual progress.
