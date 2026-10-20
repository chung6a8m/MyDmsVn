# Business Compact and inline Goods Receipt implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the Business Compact WinForms shell/receipt layout and true inline product, quantity and unit-cost editing while preserving the accepted receipt, session and recovery behavior.

**Architecture:** Keep the current Local application contracts and pinned BootstrapSourceGrid integration. Add an application-owned editable grid, edit-session adapter and lossless numeric parser; use existing lookup/formatted adapters, incremental projections and explicit command boundaries. Prove native accessibility first, then integrate and style the same interaction path.

**Tech Stack:** Windows Forms, `net48`, shipped x86 Desktop, CommunityToolkit.Mvvm, Bootstrap5WinFormUI, BootstrapSourceGrid, existing SourceGrid editors and existing xUnit/Desktop/Session infrastructure.

**Spec:** [BUSINESS_COMPACT_UI.md](../BUSINESS_COMPACT_UI.md). Read with [BUSINESS_COMPACT_ACCEPTANCE.md](../BUSINESS_COMPACT_ACCEPTANCE.md) and the sole [TEST_POLICY](../TEST_POLICY.md).

**Status:** ACTIVE. BCI-T00 native proof complete in its test-host scope; [observed evidence and decision](../BUSINESS_COMPACT_ACCEPTANCE.md#81-bci-t00-native-editor-proof--2026-10-20). BCI-T01 scalar parser complete; [scalar evidence](../BUSINESS_COMPACT_ACCEPTANCE.md#82-bci-t01-lossless-scalar-conversion--2026-10-20). BCI-T02 projection/session foundation complete; [adapter-host evidence](../BUSINESS_COMPACT_ACCEPTANCE.md#83-bci-t02-stable-projection-and-cell-sessions--2026-10-20). BCI-T03 lookup/command integration complete in its focused control-test scope; [evidence](../BUSINESS_COMPACT_ACCEPTANCE.md#84-bci-t03-lookup-and-command-integration--2026-10-20). BCI-T04 editable native accessibility complete in its focused hosted/package scope; [evidence](../BUSINESS_COMPACT_ACCEPTANCE.md#85-bci-t04-editable-native-accessibility--2026-10-20). BCI-T05–T06 are PLANNED / NOT RUN. Complete frozen Business Compact UI/package acceptance is not implied.

**Planning baseline:** `v1a-pro@f37038b250821a538b9cdc10a6d6d8b4bc1f386f`, assessed 2026-10-20. The owner requested this sole active roadmap and deletion of older unimplemented plans. [Index and retirement record](README.md).

## Global constraints

- Product and all ten active test projects remain `net48`; shipped Desktop/admin packages remain Windows x86. AppCore/net8 stays parked.
- Keep vendor pins initially: BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- Scope is shared shell styling and Goods Receipt presentation/input coordination for existing fields; preserve API/DTO/schema, explicit UoW, authorization, rowversion, ledger/balance and Post semantics.
- SourceGrid is the selected grid. A different grid requires the recorded BCI-T00 decision path and explicit owner selection; no silent fallback or blanket dependency upgrade.
- Preserve the full editability predicate, `RequiresPostOutcomeReload`, active/historical lookup rules, dirty guards, session disposal and stale-result fencing at edit start and commit.
- Keep raw input until commit validation; four effective fractional digits, Quantity greater than zero/max `99999999999999.9999`, UnitCost at least zero/max `999999999999999.9999`; never clamp, truncate or round a meaningful invalid digit into a different valid value.
- Use one shared editor per owning grid/column configuration. No cross-grid editor sharing, private-child reflection, per-cell control population or global static factory replacement.
- Existing English UI copy remains; no wholesale localization or new receipt fields. Dimensions are logical 96-DPI targets subject to measured editor/text bounds.
- New runtime acceptance is interactive Windows/net48/x86 at measured 100%/125%, Light/Dark, with native keys/mouse/UIA and real packaged Login/SQL evidence. Historical counts or exceptions cannot satisfy new obligations.
- Follow direct project tests, bounded GUI-owned processes, serialized STA, existing owned SQL fixtures and positive cleanup. No new runner, approval-file, global lease or automatic prefix cleanup.
- Markdown is UTF-8 without BOM/CRLF in the working tree; C# follows repository BOM/CRLF. Keep evidence sanitized and preserve historical candidate provenance.

## Review focus

Each risk below has a named test in its owning task; it is not left for final manual review alone.

1. A far-tail nonzero digit that decimal parsing rounds away must be rejected before conversion; trailing zeros must retain their exact value (T01).
2. Two new lines both have ID zero, or a reload replaces a persisted line object; a late editor callback must not write another row (T02).
3. Clicking New while a lookup has pending text must not implicitly select the highlighted result through native focus loss; dirty numeric text must not vanish (T03).
4. A Post may have committed before a timeout, and reload may be denied; all input paths must remain locked until an authoritative read resolves it (T03).
5. A cached accessible child or reused native editor must not mutate a removed row, Posted document or next user's workspace (T04).

## Delivery and task order

| Task | Depends on | Deliverable | Acceptance IDs |
|---|---|---|---|
| BCI-T00 | Baseline/source inspection | Native editor/accessibility proof and recorded implementation decision | A01–A03; early feasibility of A25 |
| BCI-T01 | T00 | Lossless culture-aware numeric conversion and scalar tests | A04–A06; numeric prerequisites for A07 |
| BCI-T02 | T01 | Stable grid projection, identity and edit-session adapter | A08–A11 |
| BCI-T03 | T02 | Lookup, command admission and preserved receipt/session/recovery behavior | A07, A12–A23 |
| BCI-T04 | T03 | Editable native accessibility and semantic package-probe interaction | A24–A26 |
| BCI-T05 | T04 | Business Compact shell/receipt layout and visual/focus behavior | A27–A28 |
| BCI-T06 | T00–T05 | New frozen integrated candidate and reconciled acceptance evidence | A29–A30 and all prior obligations |

The A-prefix abbreviates BCI-A in the acceptance matrix. T00 is a proof, not a shortcut to shipping a prototype. Tasks are reviewable commits; no partial task sequence is declared the completed UI. Independent test preparation can overlap only after the consumed interface is fixed; shared production files are edited sequentially.

## Source and file map

Existing production anchors: `src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs`, `FoundationShellForm.cs`, `AccessibleActionGrid.cs`, `AccessibleNumericBox.cs`, `AccessibleLookupBox.cs`; `src/MyDmsVn.Desktop.Application/GoodsReceiptEditorViewModel.cs`, `GoodsReceiptLineViewModel.cs`, `GoodsReceiptProductOptionsViewModel.cs`; `src/MyDmsVn.Contracts/InventoryContracts.cs`.

The plan introduces the following app-owned files; `ReceiptNumericInput.cs` is implemented by T01; `ReceiptEditIntent.cs`, `GoodsReceiptLinesGrid.cs` and `GoodsReceiptLinesGridAdapter.cs` by T02; `GoodsReceiptControl.InlineEditing.cs` by T03. Remaining file names are proposals. None is an existing vendor API:

| New file under `src/MyDmsVn.Desktop.WinForms/` | Responsibility |
|---|---|
| `ReceiptNumericInput.cs` | Pure lexical/decimal validation and `ReceiptNumericField` enum |
| `ReceiptEditIntent.cs` | Command intent enum: Save, Post, SelectReceipt, NewReceipt, Refresh, AddLine, RemoveLine |
| `GoodsReceiptLinesGrid.cs` | Receipt-specific editable grid and accessibility bridge |
| `GoodsReceiptLinesGridAdapter.cs` | Sessions, projection, editor events, identity and command-intent resolution |
| `GoodsReceiptControl.InlineEditing.cs` | Focused partial for wiring the adapter to existing receipt commands/state |
| `BusinessCompactAppearance.cs` | Shared app-owned visual metrics, typography and theme application |
| `BusinessCompactNavigation.cs` | App-owned navigation composition if the stock sidebar cannot express the selected palette |

Proposed test files are `tests/MyDmsVn.Desktop.Tests/ReceiptNumericInputTests.cs`, `GoodsReceiptInlineEditorTests.cs`, `GoodsReceiptInlineAccessibilityTests.cs`, and `BusinessCompactLayoutTests.cs`. Extend the existing GoodsReceipt/Receipt/InventoryAccessibility/Packaged tests for the behaviors they already own. No new test project is required.

## Verification convention for T00–T05

Run the following pattern from the repository root in a provisioned interactive Windows session, substituting the exact class implemented by the task. This example is a command to execute later, not evidence of a run now.

```powershell
$bciProject = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$bciFilter = 'FullyQualifiedName~GoodsReceiptInlineEditorTests'
$bciResults = 'TestResults/bci-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet restore $bciProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $bciProject -c Release -f net48 --no-restore -p:PlatformTarget=x86 `
  --filter $bciFilter --logger 'trx;LogFileName=bci-task.trx' --results-directory $bciResults `
  -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'Task verification failed.' }
```

For changed behavior in T01–T05, first prove the new observable regression fails for the intended missing behavior, then implement and run its named cases plus affected existing regressions. T00 records observed existing capabilities and gaps; an already-working vendor capability does not require a manufactured failing test. Preserve its successful proof and add a failing assertion only for an actual unmet requirement. A zero-match selector, compile failure, skipped mandatory GUI case or hang is not the intended failing assertion or a PASS. Inspect source/discovery and actual TRX. Use `--no-build` only after a successful build of that exact test candidate. Pure parser tests do not require showing a form; test project/runtime prerequisites still apply.

### BCI-T00 — Native editor and accessibility proof

**Files:** Read the source map and pinned vendor editor sources. Create the initial `GoodsReceiptInlineEditorTests.cs` test host using existing `StaTest`/`WinFormsTestGuard`; update the decision/evidence section of `BUSINESS_COMPACT_ACCEPTANCE.md` only from observed results. No shipping app replacement in this proof.

**Interfaces:** Consume `grid.BootstrapEditors.CreateLookupBox(typeof(int))` and `grid.BootstrapEditors.CreateFormattedTextBox(typeof(decimal))` through the existing [public registry property](https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f/src/MyDmsVn.BootstrapSourceGrid/Controls/BootstrapSourceGrid.cs#L69-L73). These are current factory methods; no new factory overload is assumed. Produce a recorded choice of stock adapters plus app accessibility bridge, or a narrowly specified seam with compatibility/ownership requirements. The app class contracts below remain proposed until implemented.

- [x] Inventory the exact checked-out application and nested vendor pins, current GUI test/package helpers and current read-only accessible-cell behavior; record any delta from the planning baseline before adapting the plan.
- [x] Add `Native_cell_entry_uses_registered_bootstrap_editor`, `Logical_values_remain_typed_when_display_changes` and `Shared_editors_dispose_with_their_grid`; assert native focus/input, int/decimal cell values, correct closed-cell labels and disposal without external-editor substitution.
- [x] Run those cases with the verification pattern and a bounded native UIA observation of the real hosted editor. Record keyboard/popup/native accessible-tree behavior on net48/x86; do not infer it from API shape or direct VM assignment.
- [x] Record the smallest supported implementation path. Default to app-owned grid accessibility and current adapters. If a control factory/alignment seam is needed, identify its exact upstream file/API and tests; any vendor change gets a separate traceable reviewed commit/pin. If native UIA cannot meet the contract, present the measured SourceGrid/DGV trade-off for explicit owner selection before a grid switch.
- [x] Commit the retained proof tests and factual decision/evidence. Missing native resources or an unresolved accessibility path leaves T00 incomplete; it does not authorize skipping accessibility or relabeling a mockup as proof.

### BCI-T01 — Lossless numeric conversion

**Files:** Create `ReceiptNumericInput.cs` and `ReceiptNumericInputTests.cs`; inspect existing InventoryContracts and numeric/overflow tests. No contract or server behavior change.

**Interfaces:** Introduce `internal enum ReceiptNumericField { Quantity, UnitCost }` and `internal static bool ReceiptNumericInput.TryParse(string text, ReceiptNumericField field, CultureInfo culture, out decimal value, out string? error)`. Return false with an actionable error for invalid input; caller retains its original text. Use `GoodsReceiptContractLimits` constants, not divergent copies of bounds.

- [x] Write the failing tests `Extra_fractional_zeros_are_exact_but_meaningful_overprecision_is_rejected`, `Culture_and_paste_preserve_the_same_decimal_value`, and `Field_bounds_and_zero_rules_match_receipt_contracts` with the assertions below.

```csharp
// Retained test contract; implemented and verified by BCI-T01.
Assert.True(ReceiptNumericInput.TryParse("1.23000", ReceiptNumericField.Quantity,
    CultureInfo.GetCultureInfo("en-US"), out var value, out _));
Assert.Equal(1.23m, value);
Assert.False(ReceiptNumericInput.TryParse("1.23001", ReceiptNumericField.Quantity,
    CultureInfo.GetCultureInfo("en-US"), out _, out _));
Assert.False(ReceiptNumericInput.TryParse("0.0000000000000000000000000000000000000001",
    ReceiptNumericField.UnitCost, CultureInfo.GetCultureInfo("en-US"), out _, out _));
```

- [x] Run the parser-class selection and confirm the expected missing/incorrect behavior, then implement raw lexical checks before decimal conversion: surrounding whitespace allowed, optional leading culture sign, ungrouped digits/culture decimal separator, no exponent/currency/group separator, effective fractional precision at most four. Zero padding beyond four is allowed; meaningful tails are rejected before the parser can round them away.
- [x] Add explicit vi-VN `1,23000` and en-US `1.23000` equivalence; reject their wrong/group separators. Assert exact maxima, one unit above each maximum, negative/empty/malformed input, quantity zero rejected and cost zero accepted. A wrong input remains available unchanged to the UI.
- [x] Run `FullyQualifiedName~ReceiptNumericInputTests` plus the existing relevant decimal/overflow ViewModel cases, inspecting actual selected names. No GUI/SQL run is required merely for this pure parser unless an actual integration dependency changed.
- [x] Commit the parser and meaningful boundary tests. Record scalar evidence; full inline overflow/command behavior remains owned by T03/A07.

### BCI-T02 — Stable projection and cell sessions

**Files:** Create `ReceiptEditIntent.cs`, `GoodsReceiptLinesGrid.cs` and `GoodsReceiptLinesGridAdapter.cs`; extend `GoodsReceiptInlineEditorTests.cs`. Prepare targeted changes in `GoodsReceiptControl.cs`; T03 owns complete production command wiring.

**Interfaces:** `GoodsReceiptLinesGrid : BootstrapSourceGrid`. `GoodsReceiptLinesGridAdapter(GoodsReceiptLinesGrid grid, GoodsReceiptEditorViewModel editor, Func<bool> canEdit)`. Public-to-the-app methods are `bool TryFinishEdit(ReceiptEditIntent intent)`, `void CancelActiveEdit()`, `void ResetDocument(IReadOnlyList<GoodsReceiptLineViewModel> lines)`, `void RefreshEditability()` and `void Dispose()`. The grid is caller-owned; adapter disposal unsubscribes/invalidate sessions and coordinates editor cleanup with the grid registry owner without double ownership.

- [x] Add failing tests `State_refresh_does_not_replace_the_active_cell`, `Pending_cell_edits_participate_in_dirty_command_guards`, `Unsaved_zero_id_lines_are_distinguished_by_reference` and `Reloaded_line_references_replace_old_edit_targets_safely`. Observe typed VM values, raw input/caret/focus and event count, not only private flags.
- [x] Implement document generation plus line reference/local key plus column identity, original value and raw candidate. Revalidate membership/generation/canEdit before any commit or deferred event. Retain one shared editor per grid/column configuration; store ProductId in the editable product cell and configure its label conversion separately.
- [x] Implement incremental cell/totals/status updates. Never replace/Redim the active cell in ordinary state notifications. A deliberate ResetDocument resolves/cancels the old session, increments generation and replaces mappings; map selection after Save by valid persisted identity with an explicit new-line fallback.
- [x] Exercise cancel without VM mutation, multiple ID-zero rows, remove/reinsert at the same visual index, VM notifications during edit, reload replacing objects, and dispose with an active lookup. Run the inline-class selection plus affected existing control/VM tests.
- [x] Commit the adapter/projection work and record its tests. Editable accessibility is not accepted until T04; a partial adapter is not the completed product feature.

### BCI-T03 — Lookup and command integration

**Files:** Modify `GoodsReceiptControl.cs` and make it partial; create `GoodsReceiptControl.InlineEditing.cs`; extend the adapter. Update `GoodsReceiptControlTests.cs`, `ReceiptAcceptanceTests.cs`, `GoodsReceiptInlineEditorTests.cs` and affected Session product-option tests only where needed. Existing VMs/contracts remain the authority for Save/Post and business validation.

**Interfaces:** Existing command handlers call the same adapter `TryFinishEdit(intent)` and continue only when its intent policy succeeds. `canEdit` reads the current busy/read-only/access-denied/uncertain-Post/session state. Use existing lookup view models and cancellation/query generations; no new remote lookup service or per-cell VM.

- [x] Add failing tests for A07 and A12–A23 using their exact proposed names in the acceptance matrix. Include unresolved lookup at Save, query-only New, changed numeric text before Post, meaningful zero/overflow failures and late query/Save completion after row or session replacement.
- [x] Wire active lookup queries/results to the existing product-options flow. Preserve same-context pending text and selected historical labels; query/highlight does not mutate ProductId. Resolve the exact selection only on a valid commit, with no first-result auto-selection. Remove the old line-grid Enter/Click path that rebuilds the grid and cancels editing as a side effect.
- [x] Route Save through successful cell completion; then use existing Save. Post completes or rejects the candidate and rechecks clean/persisted ID/version before calling the existing Post, with no implicit Save. SelectReceipt/New/Refresh preserve dirty guards and do not lose pending numeric text. An allowed New cancels query-only pending lookup without selecting a highlight; an aborted navigation preserves pending input. Explicit Remove cancels only its target candidate and invalidates that target before removal; explicit Add resolves the current candidate then focuses Product on the new line.
- [x] Remove the external line editor panel and its synchronization/event path after the inline path is wired. Update existing tests to exercise actual cell editors; do not keep invisible Quantity/Product controls as a substitute. Retain header lookup behavior, server field-error mapping by submitted line order, existing notifications and raw input after failed commands.
- [x] Cover full editability at start and commit, read denial clearing sensitive detail, unknown Post committed/not-committed outcomes and denied reload, repeated commands, stale expectedVersion, session disposal and independent total/line overflow. Run focused inline/control/Receipt/VM tests and affected Session lookup tests with existing direct commands; broaden SQL only for actual changed backend behavior or T06.
- [x] Commit integrated editing and preserved failure boundaries, with actual tests/evidence. T03 success does not waive the native UIA and visual work still required.

### BCI-T04 — Editable native accessibility

**Files:** Extend `GoodsReceiptLinesGrid.cs` and adapter; create `GoodsReceiptInlineAccessibilityTests.cs`; update `InventoryAccessibilityAcceptanceTests.cs`, `GuiPackageProbe.cs` and affected `PackagedGuiAcceptanceTests.cs`. Keep `AccessibleActionGrid` read-only for the result-list surfaces that still use it.

**Interfaces:** Override `protected override AccessibleObject CreateAccessibilityInstance()` in the new grid. Accessible actions/values call the same adapter identity/parse/commit pipeline and never write a VM directly. Preserve meaningful grid name `Goods receipt lines`, row/column context and active editor metadata; row identity includes document generation and stable local/persisted mapping, not display ordinal alone.

- [x] Add failing tests `Accessible_line_cells_expose_correct_editability_and_identity`, `Native_uia_reaches_the_active_shared_editor`, and `Accessible_edits_preserve_validation_and_pending_selection_state`. Include cached children after Remove/New/reload and a reused editor after moving from a writable Draft to a Posted/denied context.
- [x] Replace the old line-cell read-only contract deliberately: Product/Quantity/Unit cost are editable only when allowed; row-number/Total cells, receipt result lists and Posted cells remain read-only. Expose value/error/pending-versus-committed information through an app-owned provider and real native editor reachability.
- [x] Implement the smallest T00-proven seam if native editor metadata is insufficient. Stock adapters are sealed and do not instantiate app wrappers; do not claim setting AccessibleName alone recreates typed value validation or dynamic Help. Any custom adapter owns its keyboard/focus/disposal equivalence tests.
- [x] Update the package probe to locate row/cell and actual editor by semantic accessibility, then type/select/commit natively. Preserve ordinary Login and owned bounded process control; do not use hidden controls, VM injection, ordinal-only selectors or arbitrary coordinate clicks to claim UIA success.
- [x] Run focused inline-accessibility and affected inventory-accessibility tests, plus the affected bounded package probe after building a suitable clean candidate. Record native tree/focus evidence and cleanup, then commit. No current-candidate test total is inferred from historical 329-row results.

### BCI-T05 — Business Compact shell and receipt layout

**Files:** Create `BusinessCompactAppearance.cs` and, if needed, `BusinessCompactNavigation.cs`; modify `FoundationShellForm.cs`, `GoodsReceiptControl.cs` and the grid sizing/style code. Create `BusinessCompactLayoutTests.cs`; update existing shell/layout assertions for deliberate changes. Other catalog content is not redesigned.

**Interfaces:** App-owned `BusinessCompactAppearance.Apply(Control workspace)` applies the spec's shared visual values and measured bounds; the shell theme path preserves those values when mode changes. Existing navigation actions/session behavior remain the integration contract. Prefer app-owned navigation composition for an independent navy palette; an additive library appearance seam is allowed only with recorded compatibility benefit and a pinned upstream change.

- [ ] Add focused behavior checks `Compact_shell_retains_visible_fields_and_native_focus_order` and `Cell_editors_and_popups_remain_usable_after_scroll_resize_and_theme_change`. Use logical test viewports 944x600 and 1680x900 at 96 DPI, and record the measured scaled bounds at 100%/125%; do not assume a constructor height proves visual fit.
- [ ] Implement metadata above the dominant line grid, list/detail roles, visible command/status/footer areas, error placement and retained uncertain-Post reload action. Use body Segoe UI 10pt, heading 14pt, secondary 9pt; spacing 8/16, navigation 224 expanded/64 collapsed, row target 36 logical pixels. Row height must still meet actual editor preferred/minimum size and measured text/padding at the current DPI.
- [ ] Apply the selected palette through owned theme/appearance code; retain navigation selection/disabled/keyboard and session actions. Make cell display and active numeric input right-aligned. If stock wrappers cannot express native TextAlign, use the T00-recorded app adapter or narrow additive property rather than patching private child controls.
- [ ] Verify native tab order, no implicit blank trailing row, header/footer reachability, numeric text/caret/error visibility, popup work-area bounds, scroll/resize and Light/Dark transitions. Smaller available space must remain reachable through reflow/scrolling; no clipping behind the footer. Run focused layout/shell/inline interaction tests and collect actual screenshots for affected states.
- [ ] Commit visual work and its evidence. Retain existing theme resource ownership and disposal. Full integrated candidate acceptance remains T06.

### BCI-T06 — Frozen integrated UI acceptance

**Files:** Update `BUSINESS_COMPACT_ACCEPTANCE.md` with actual results/artifact references, this plan's checkboxes, [the execution index](README.md), root `AI_CONTEXT.md` and affected documentation. Test/driver corrections belong to their owners and require rerunning the affected frozen evidence; no invented PASS or new release plan.

**Interfaces:** Consume completed T00–T05 behavior and A01–A30 evidence. Produce an independently reviewable BCI acceptance record with exact product/test/package/vendor provenance, actual test discovery/execution, native observations and resource cleanup.

- [ ] Freeze a clean product revision, test/driver revision and package using the current [packaging runbook](../LOCAL_RELEASE_PACKAGING.md). Record manifest/EXE/test DLL hashes independently; record any intentional product/driver revision difference. Review the complete feature diff and inventory affected existing/new tests before selecting the final gate.
- [ ] On provisioned interactive Windows/net48/x86, run Desktop.Session unfiltered once; run Desktop unfiltered at measured 100% and 125% with Light/Dark cases; run the separate bounded PackagedGuiAcceptanceTests scope at 100%. Use direct projects and existing finite process budgets; verify actual new discovery names/Theory rows and TRX, not an expected old count. Do not overlap GUI/SQL owners.
- [ ] Execute the updated packaged inline flow with real Login and independent read-only SQL oracles: exact key/decimal persistence, Save/reopen, one Post stock effect, stale/denied/uncertain-outcome recovery. Link each applicable result to A29 and the existing regression it preserves. Keep identities, workers and positive cleanup separate and verified.
- [ ] Perform focused attended Business Compact/inline observations at 100% Light/Dark and 125% Light/Dark, including product popup arrows/Enter/Escape/Tab, numeric input, raw error retention, native accessibility, windowed/maximized layout, scrolling and readonly/recovery state. Obtain explicit review of those concrete observations; the historical Warehouse Down exception is not an inline waiver. 150% is optional exploration unless separately added to scope.
- [ ] Reconcile every A01–A30 row with applicable source/test/package/UI evidence; retain failed trials and unresolved exceptions. Missing/skipped/aborted/timed-out mandatory cases or uncertain cleanup leave acceptance incomplete. Broaden backend tests only when the actual feature diff changes their shared behavior; no automatic full ten-project release certification is part of this UI milestone.
- [ ] Commit the factual acceptance/handoff only after the required evidence and review exist. Mark BCI complete in its actual UI scope, retain operational prerequisites as unaccepted/unscheduled, and name the next owner-selected action without reviving deleted plans.

## Documentation PR verification

This plan/specification/roadmap PR requires Markdown diff, local links/anchors, source-reference/API distinctions, task/acceptance coverage, single-roadmap status and encoding checks. No build, SQL or GUI invocation is represented as executed by creating these documents. Production files, tests, schema and vendor pins stay unchanged in this PR.

The older unimplemented REL/P6 plan and modern-runtime procedure are removed; completed plan/evidence records remain. [Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) are factual unaccepted constraints with no active execution checklist. [History](../HISTORY.md#2026-10-20--business-compact-roadmap-reset) records the owner decision and immutable retired sources.
