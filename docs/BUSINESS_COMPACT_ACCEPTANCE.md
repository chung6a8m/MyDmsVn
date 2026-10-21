# Business Compact inline Goods Receipt — acceptance specification

> **Completed historical baseline — 2026-10-21:** BCI was accepted in its recorded UI scope and merged through PR #40 (`6040d7b`). Current work is [BCF — lookup correctness and visual parity](plans/20261021-001-business-compact-lookup-and-visual-parity.md). Original planning/source references, outcomes and exceptions below remain historical; they do not establish a new BCF PASS.

**Status:** BCI-T00 proof, T01 scalar parser, T02 projection/session foundation, T03 lookup/command integration and T04 editable native accessibility and T05 compact layout complete in their recorded scopes; BCI-T06 is COMPLETE / ACCEPTED in the frozen Business Compact UI scope on 2026-10-21; see §8.7 and its final Owner sign-off. Production release prerequisites remain UNACCEPTED / UNSCHEDULED.
**Prepared:** 2026-10-20. **Planning baseline:** `v1a-pro` at `f37038b250821a538b9cdc10a6d6d8b4bc1f386f`.

## 1. Scope and authority

This specification verifies the [Business Compact design](BUSINESS_COMPACT_UI.md) and [BCI implementation plan](plans/20261020-001-business-compact-inline-goods-receipt.md). It is an acceptance ledger, not an executable test manifest or a replacement for [TEST_POLICY](TEST_POLICY.md).
The product remains Windows Forms `net48`, with the shipped x86 Desktop and current Local application contracts. SourceGrid owns edit lifecycle; application code owns receipt state, validation presentation and command admission.
All BCI-A identifiers below are stable obligations. A01–A03 have T00 proof-host evidence, A04–A06 have T01 scalar evidence and A08–A11 have T02 adapter-host evidence below; A07/A12–A23 have the focused T03 control evidence in §8.4; A24–A26 have T04 hosted/native and focused package evidence in §8.5; A27–A28 have T05 hosted native 100%/125% layout evidence in §8.6; A29 has candidate1 product09e92d3/testf858ec9 Desktop100/package checkpoint evidence and candidate2 sourcee20b8c7 affected28/28 plus attended follow-up in §8.7; candidate2 Desktop100/125 and separate package100 passed; attended125 replayA+B2 completed normally; final Owner review and A30 sign-off were explicitly accepted on 2026-10-21. Historical results do not satisfy the new candidate. Existing means source already exists; it does not mean the new inline implementation has passed it.
T00–T05 source/test names and T06 `Packaged_inline_receipt_flow_reconciles_with_readonly_sql` are implemented in existing projects. Remaining candidate reconciliation and attended outcomes are evidence obligations, not implemented behavior or inferred PASS. Retain the obligation ID if a reviewed implementation changes a proposed test name.
T01 implements internal `ReceiptNumericInput.TryParse` and `ReceiptNumericField`; T03 connects them to the receipt controls. T02 implements `GoodsReceiptLinesGrid`, `GoodsReceiptLinesGridAdapter` and `TryFinishEdit(ReceiptEditIntent intent)` as an internal foundation tested against a hosted grid and real VM; T03 now supplies shipping-control command wiring and T04 supplies writable accessibility and active real editor reachability; their exact contracts are owned by the design and plan.

## 2. Accepted historical baseline — external line editors

The [final GUI-P5 record](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) accepts the previous line-selection plus external Product/Quantity/Unit-cost editor workflow. It does not establish inline editing, new accessibility patterns or a new package result.

| Evidence | Recorded accepted value | Boundary |
|---|---|---|
| Product/package source | `d5207b2ce1eae09e9317625b3d7d486c994f09fe`; `1.0.0-guip5-t04-c2` | Preserve exact source/package provenance; do not relabel as HEAD. |
| Desktop test source | `12e6a20a7af8e5c0f6bf820f172580f01e44181b` | Final test DLL hash and package hashes are in the verification record. |
| Session test build | `cb86fbc` metadata | Source was recorded unchanged; no new build is inferred. |
| Recorded gates | Session 108/108; Desktop 329/329 at 100%, 329/329 at 125%; separate packaged 18/18 | 784 passing invocations / 437 distinct obligations, not future BCI counts. |
| Attended Inventory | [T05](GUI_P5_T05_WALKTHROUGH.md): 44 PASS / 1 PARTIAL | Warehouse 125% Dark Down remains NOT TESTED with an explicit Owner-accepted exception. |
| Vendor integration | `MyDmsVn.BootstrapSourceGrid@09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f` | Existing shared editor capability does not prove receipt integration. |
| Approved visual baseline | Actual 100%/125%, Light/Dark, interactive Windows/net48/x86 | 150% was outside that matrix; optional exploration only unless scope expansion is approved. |

The old Down exception does not transfer to future inline lookup acceptance. BCI must execute its required keyboard paths or record an explicit new disposition; another popup's PASS is not equivalent evidence.
The earlier 139/282/312/324/326/329 counts describe their own frozen revisions. Discover and reconcile the actual new candidate rather than using any historical count as a target.

## 3. Existing source and regression anchors

| Anchor | Existing source / tests | Contract to preserve or deliberately replace |
|---|---|---|
| E-C | [GoodsReceiptControlTests](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs) | Decimal precision/range, dirty navigation guards, saved-line reselection, explicit New cancellation and historical product references. |
| E-R | [ReceiptAcceptanceTests](../tests/MyDmsVn.Desktop.Tests/ReceiptAcceptanceTests.cs) | Conflict input retention, duplicate suppression, uncertain Post, denied reads/writes, session disposal, header/filter layout. |
| E-A | [InventoryAccessibilityAcceptanceTests](../tests/MyDmsVn.Desktop.Tests/InventoryAccessibilityAcceptanceTests.cs) | Native focus, names, external numeric Value access, pending/committed lookup Help, Posted state and stale row actions. |
| E-V | [GoodsReceiptEditorViewModelTests](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs) | `Boundary_line_amount_overflow_is_safe_and_blocks_save`, `Aggregate_overflow_across_safe_lines_is_safe_and_blocks_save`, persisted identity/version. |
| E-L | [Product option tests](../tests/MyDmsVn.Desktop.Session.Tests/GoodsReceiptProductOptionsViewModelTests.cs), [list tests](../tests/MyDmsVn.Desktop.Session.Tests/GoodsReceiptListViewModelTests.cs) | Query intent, cancellation, catalog refresh and late-response fencing. |
| E-P | [PackagedGuiAcceptanceTests](../tests/MyDmsVn.Desktop.Tests/PackagedGuiAcceptanceTests.cs), [GuiPackageProbe](../tests/MyDmsVn.Desktop.Tests/GuiPackageProbe.cs) | Actual Login, receipt/stock reconciliation, stale clients, denial, timeout/reload/retry and owned process cleanup. |
| E-U | [AccessibleActionGrid](../src/MyDmsVn.Desktop.WinForms/AccessibleActionGrid.cs), [AccessibleNumericBox](../src/MyDmsVn.Desktop.WinForms/AccessibleNumericBox.cs), [AccessibleLookupBox](../src/MyDmsVn.Desktop.WinForms/AccessibleLookupBox.cs) | Existing result cells are deliberately read-only; virtual Open selects external editors. Numeric/lookup accessibility is application-specific. |
| E-G | [GoodsReceiptControl](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs), [line VM](../src/MyDmsVn.Desktop.Application/GoodsReceiptLineViewModel.cs), [editor VM](../src/MyDmsVn.Desktop.Application/GoodsReceiptEditorViewModel.cs) | Current inline adapter must preserve active edits during ordinary state updates and reset only at deliberate document boundaries; T03 replaced the old full rebuild/external-editor path. |

E-R's `Rowversion_conflict_is_inline_and_keeps_dirty_header_line_and_opaque_version` refers to an inline error message, not editing inside a grid cell.
Keep read-only semantics for result lists and computed cells. T04 deliberately revised E-A's line-cell read-only assertion for editable Draft fields and retained read-only computed/Posted semantics; preserve those replacement assertions.
Registry-created vendor editors do not automatically instantiate E-U's application subclasses. Prove the actual hosted editor's native accessible tree and value/selection behavior before declaring compatibility.

## 4. Acceptance matrix

`New:` retains the planning labels for now-implemented A01–A29 method names in the existing Desktop project. Numeric cases use `ReceiptNumericInputTests`, accessibility uses `GoodsReceiptInlineAccessibilityTests`, and layout uses `BusinessCompactLayoutTests`. A30 is a candidate reconciliation/review obligation. E-* references are regression anchors, not new-result credit; §8.1–§8.7 distinguish source and actual scoped execution.
Each row belongs to its named plan task. BCI-T06 reconciles every row and the affected existing regressions; it cannot waive an earlier failure by rerunning a narrower successful selection.

### 4.1. Editor proof and strict numeric input

| ID | Owner | Test anchors | Required observable result | Outcome |
|---|---|---|---|---|
| BCI-A01 | BCI-T00 | Existing: E-G/E-U; New: `Native_cell_entry_uses_registered_bootstrap_editor` | Actual mouse/F2/type entry focuses an editor hosted in the selected line cell; Quantity/Unit cost/Product can be changed without using external line editors. | PASS — T00 host only (§8.1) |
| BCI-A02 | BCI-T00 | Existing: vendor adapter sources; New: `Logical_values_remain_typed_when_display_changes` | Lookup commits the logical key and formatted input commits typed decimal; display labels/grouping never become DTO values. Cell display remains correct after edit closes. | PASS — T00 host only (§8.1) |
| BCI-A03 | BCI-T00 | Existing: vendor registry ownership tests; New: `Shared_editors_dispose_with_their_grid` | One editor per grid/configuration is reused sequentially; no per-cell control allocation or cross-grid sharing; never-started and active-popup controls dispose with the workspace. | PASS — T00 host only (§8.1) |
| BCI-A04 | BCI-T01 | Existing: E-C decimal boundary test; New: `Extra_fractional_zeros_are_exact_but_meaningful_overprecision_is_rejected` | Preserve raw text until validation: `1.23000` is exactly 1.23; `1.23001` and a far-tail nonzero beyond four places fail before decimal parsing can round them away. No meaningful digit is silently truncated. | PASS — T01 scalar only (§8.2) |
| BCI-A05 | BCI-T01 | Existing: E-A numeric validation; New: `Culture_and_paste_preserve_the_same_decimal_value` | Exercise vi-VN/en-US decimal separators, typing and paste with the captured culture; accept ungrouped input and reject grouping, wrong/mixed separators, exponent/currency syntax and ambiguity with an actionable error while retaining raw text. Display formatting cannot turn invalid pasted input into a valid different value. | PASS — T01 scalar only (§8.2) |
| BCI-A06 | BCI-T01 | Existing: E-C/E-A; New: `Field_bounds_and_zero_rules_match_receipt_contracts` | Quantity must be greater than zero and within its contract maximum; Unit cost may be zero and must not exceed its maximum. Negative, empty, malformed and over-range input is rejected without clamping or VM mutation. | PASS — T01 scalar only (§8.2) |
| BCI-A07 | BCI-T03 | Existing: E-V overflow methods; New: `Overflow_preserves_input_and_blocks_commands` | Both line multiplication and aggregate overflow remain safe; error state retains entered values, does not display a fabricated total and dispatches no Save/Post. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |

### 4.2. Stable grid state, lookup and command boundaries

| ID | Owner | Test anchors | Required observable result | Outcome |
|---|---|---|---|---|
| BCI-A08 | BCI-T02 | Existing: E-G/E-C; New: `State_refresh_does_not_replace_the_active_cell` | Unrelated VM/status/total notifications preserve active cell/editor, raw text, selection and caret; targeted updates do not rebuild the editing row or duplicate a commit. | PASS — T02 adapter host only (§8.3) |
| BCI-A09 | BCI-T02 | Existing: E-C dirty guards; New: `Pending_cell_edits_participate_in_dirty_command_guards` | Pending text cannot be ignored by selection/New/Refresh/Post guards. Cancel restores the original typed value; successful commit marks the correct receipt dirty once. | PASS — T02 adapter host only (§8.3) |
| BCI-A10 | BCI-T02 | Existing: E-C saved-line reselection; New: `Unsaved_zero_id_lines_are_distinguished_by_reference` | Multiple unsaved lines with `Id == 0` remain distinct across selection, add/remove and recalculation; late events cannot update another row with the same zero ID. | PASS — T02 adapter host only (§8.3) |
| BCI-A11 | BCI-T02 | Existing: E-C/E-G; New: `Reloaded_line_references_replace_old_edit_targets_safely` | Save/reload replacing VM objects preserves intended selection by valid persisted identity; stale references are invalidated, and subsequent editing updates the current line exactly once. | PASS — T02 adapter host only (§8.3) |
| BCI-A12 | BCI-T03 | Existing: E-L/E-C; New: `Current_query_survives_same_context_refresh` | Current pending query is retained during same-context option/state refresh; committed key and pending text remain distinct, with no silent selection or stale query overwrite. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A13 | BCI-T03 | Existing: E-C explicit New test; New: `Explicit_new_cancels_pending_lookup_after_navigation_guard` | After existing dirty/discard policy admits New, pending text/selection intent and outstanding query are canceled; a late result cannot populate or select a product in the new receipt. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A14 | BCI-T03 | Existing: E-L/E-R session fencing; New: `Late_lookup_results_cannot_cross_row_receipt_or_session` | Rapid queries, row switches, removal, receipt change and sign-out invalidate obsolete results, including canceled operations whose provider completes anyway. Current input and next-user state remain intact. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A15 | BCI-T03 | Existing: E-C historical references/E-R reopen; New: `Historical_product_display_does_not_enable_inactive_new_selection` | Reopened historical selection stays readable; active-only new choices and rename/deactivate/reactivate refresh retain existing business rules without losing the selected line. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A16 | BCI-T03 | Existing: E-A F4 test; New: `Lookup_popup_keys_mouse_and_focus_commit_once` | Native F4/open, filtering, arrows/Page keys, Enter, mouse choice, Escape, Tab/Shift+Tab and outside focus behave as designed; popup Escape differs from cell cancel, and one selection produces one logical commit. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A17 | BCI-T03 | Existing: E-C/E-R; New: `Save_flushes_active_edit_or_dispatches_nothing` | `TryFinishEdit(Save)` returns false for invalid raw/lookup input and keeps its error/focus; success uses the newly committed typed values. Save never serializes a pre-edit value accidentally. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A18 | BCI-T03 | Existing: E-R Post/dirty cases; New: `Post_never_auto_saves_or_uses_pre_edit_values` | Post finishes or rejects the active edit, then respects dirty/persisted identity/opaque version rules. Changed input needs explicit Save; repeated clicks cannot issue duplicate commands. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A19 | BCI-T03 | Existing: E-C navigation guards; New: `All_edit_intents_observe_their_commit_or_cancel_policy` | Exercise SelectReceipt/NewReceipt/Refresh/AddLine/RemoveLine intents as well as Save/Post; failed completion leaves row and input intact, and explicit cancellation never writes pending values. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A20 | BCI-T03 | Existing: E-R conflict test/E-P stale clients; New: `Conflict_retains_inline_input_and_original_expected_version` | A stale Save/Post does not silently reload, retry or overwrite the winner; inline input, dirty state and the appropriate opaque version/error remain observable. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A21 | BCI-T03 | Existing: E-R authorization cases; New: `Denied_or_readonly_state_blocks_active_and_future_edits` | Posted, busy, access-denied and read-revoked states prevent mutation through keys, mouse, active control, command and accessibility; denied read clears sensitive stale detail. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A22 | BCI-T03 | Existing: E-R uncertain Post matrix/E-P timeout; New: `Uncertain_post_retains_lock_until_authoritative_read` | Cover server-committed and not-committed outcomes, exception/API failure, denied reload and recovery. No edit/Save/Post resumes until a successful read resolves the state; only a justified retry may post once. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |
| BCI-A23 | BCI-T03 | Existing: E-R disposal/session switch; New: `Disposed_or_old_session_completions_cannot_overwrite_current_input` | Pending Save/Post/lookup cancellation and grid disposal prevent late UI values, notifications, queries or selection from contaminating a replacement workspace/session. | PASS — T03 focused control scope (§8.4); writable UIA remains T04 |

### 4.3. Accessibility, visual shell and integrated acceptance

| ID | Owner | Test anchors | Required observable result | Outcome |
|---|---|---|---|---|
| BCI-A24 | BCI-T04 | Existing: E-A/E-U; New: `Accessible_line_cells_expose_correct_editability_and_identity` | Draft input cells expose meaningful row/column/name/value and editability; computed/status/list/Posted cells remain read-only. Cached actions re-resolve valid identity and reject removed or replaced targets. | PASS — T04 scoped native/provider/package evidence (§8.5) |
| BCI-A25 | BCI-T04 | Existing: E-A/E-U/E-P; New: `Native_uia_reaches_the_active_shared_editor` | Cross-process native UIA can find, focus, edit and commit the actual hosted control by semantic identity; reuse across rows updates accessible state. No coordinates, ordinal-only selectors or direct VM write substitutes. | PASS — T04 scoped native/provider/package evidence (§8.5) |
| BCI-A26 | BCI-T04 | Existing: E-A numeric/lookup guards; New: `Accessible_edits_preserve_validation_and_pending_selection_state` | Native Value/selection actions use the same strict numeric and lookup pipeline as keys/mouse; disabled/stale setters cannot mutate, and pending text is distinguishable from a committed key. | PASS — T04 scoped native/provider/package evidence (§8.5) |
| BCI-A27 | BCI-T05 | Existing: E-R layout/E-A tab tests; New: `Compact_shell_retains_visible_fields_and_native_focus_order` | At supported workspace widths, windowed/maximized and Light/Dark, controls/header/footer/errors remain visible; native Tab/Shift+Tab follows the design and read-only computed cells do not trap entry flow. | PASS — T05 scoped hosted native 100%/125%; wide 125% work-area fit recorded (§8.6); full frozen acceptance remains T06 |
| BCI-A28 | BCI-T05 | Existing: E-A render/vendor sizing tests; New: `Cell_editors_and_popups_remain_usable_after_scroll_resize_and_theme_change` | Actual 100%/125% Windows scaling preserves editor text/caret/error visibility, row alignment and popup work-area bounds while scrolling/resizing/theme switching; no focus loss silently commits an unintended value. | PASS — T05 scoped hosted native 100%/125%; wide 125% work-area fit recorded (§8.6); full frozen acceptance remains T06 |
| BCI-A29 | BCI-T06 | Existing: E-P receipt/invalid/lookup/stale/timeout cases; New: `Packaged_inline_receipt_flow_reconciles_with_readonly_sql` | Actual Login and native inline create/edit/reload/Save/Post flow persists exact decimals and keys; independent SQL proves one movement per line, balance reconciliation and no denied/failed mutation. | PASS — candidate2 automated Desktop100/125 and separate package100 reconcile14 A29 rows per Desktop context; attended100 follow-up PASS; attended125 replayA+B2 completed; final Owner review accepted; A30 reconciled (§8.7) |
| BCI-A30 | BCI-T06 | Existing: E-P/GUI-P5 cleanup evidence; New: candidate reconciliation record | New frozen source/test/package, all required discovery names/Theory rows, attended observations, exception capture and owned resource cleanup reconcile. No missing/skipped/aborted/timed-out mandatory case is credited as PASS. | PASS — frozen runtime/artifact reconciliation and explicit final Owner review accepted 2026-10-21 (§8.7) |

## 5. Cross-cutting acceptance rules

1. SourceGrid cell commit and receipt Save/Post are separate boundaries. Cell commit updates the intended VM; only the existing application commands persist. Preserve rollback/cancel and avoid reentrant ValueChanged-to-full-grid-rebuild loops.
2. Read-only result accessibility remains intact outside the editable line surface. A mouse demonstration cannot substitute for the required native keyboard or cross-process UIA paths.
3. Preserve raw numeric input until strict validation completes. Extra fractional zeros may be exactly reduced; any meaningful fractional digit after the fourth must be rejected before a parser/formatter rounds or truncates it.
4. Row/session/query identity must be captured and revalidated at asynchronous completion. Row index or `Id == 0` alone is insufficient; reload replaces object references even for the same persisted line.
5. Apply full mutation eligibility to edit start and any already active editor. Current grid Enabled state alone was sufficient only because the previous line cells were read-only.
6. Tests must observe input, focus, typed VM values, dispatched requests and error/result state. Do not inspect a private flag alone when an observable failure path can be exercised.
7. Retain meaningful existing regression assertions. When the old external-editor mechanism disappears, replace its interaction path and keep the behavior/error boundary it protected; explain deliberate assertion changes.
8. Accepted historical sign-off is preserved with its source/scope. A changed interaction requires new evidence; no inherited exception or old test total silently satisfies a BCI obligation.

## 6. Execution scope and commands

### Documentation-only PR

Check Markdown diff/encoding, relative links, source/test references, task ownership and consistency among design/plan/roadmap. Confirm only authorized documentation changes and no new runtime acceptance claim.
No GUI, SQL, build, package or historical suite rerun is required for this documentation-only change. Record the static checks actually performed separately from the NOT RUN runtime matrix.

### Implementation tasks BCI-T00–T05

Use existing direct test projects with focused, source-verified cases. Add observable regressions for the actual failure before fixing it, where practical; do not create another project, runner, wrapper, invocation manifest or test framework.
Restore/build the current revision before `--no-restore`/`--no-build`; inspect discovery and actual TRX names so a zero-match or partly matching selector cannot look complete. Example of existing regression selection, to run only during implementation on provisioned Windows:

```powershell
$project = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$bciResults = 'TestResults/bci-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore -p:PlatformTarget=x86 `
  --filter 'FullyQualifiedName~ReceiptAcceptanceTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests' `
  --logger 'trx;LogFileName=bci-focused.trx' --results-directory $bciResults `
  -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'Focused GUI verification failed.' }
```

That example does not include proposed new cases until they exist. Select their verified names explicitly, and include affected GoodsReceipt control/ViewModel, Session lookup and command cases according to task risk.
Keep existing serialized STA/WinFormsTestGuard, finite waits and disposal; noncooperative/fatal/real-package probes use existing GUI-owned bounded subprocesses with verified drain. An STA timeout alone cannot kill a hung UI thread.
Run one GUI/SQL command at a time on admitted resources. Reuse the existing owned SQL fixtures/oracles; SQL requirements are governed by TEST_POLICY, not a new BCI harness. Broaden backend coverage only for an actual shared contract/persistence/security change or named milestone need.

### Integration acceptance BCI-T06

Freeze product source, test source/build, dependencies and package; record actual manifest/EXE/test DLL hashes independently. A package source may differ from driver source only when explicitly recorded and justified.
Use actual interactive Windows/net48/x86 with fresh 100% and 125% measurements, Light/Dark, and an exclusive foreground session for popup-sensitive automation. Record Windows/runtime/SQL versions and display/window measurements instead of assuming supported environments.
Run the required unfiltered Desktop and Desktop.Session scopes plus the bounded real-package acceptance scope identified by the plan. Reconcile new discovery, actual Facts/Theory rows, pass/fail/skip/abort/timeout and any changed selectors; do not require exactly the old 329 rows.
Repeat the affected attended compact-layout/inline workflow at the approved visual matrix. Required arrows/Enter/Escape/Tab and popup observations need actual evidence; the old Warehouse Down exception supplies no new waiver.
Keep ordinary Login, runtime identity, migration and read-only SQL oracle separated. Capture before/after receipt/line/ledger/balance outcomes and positive database/principal/process cleanup; uncertain ownership must fail closed rather than be deleted by prefix.
Missing Windows, SQL, GUI or required artifacts means BLOCKED / NOT RUN, never simulated PASS. 150% remains outside the mandatory scope unless an explicit decision expands it and records the added obligations.

## 7. Minimum evidence for final BCI acceptance

- Exact clean product/test revision(s), vendor pins, package manifest and EXE/DLL hashes; record any working-tree patch and invalidate affected earlier results.
- Actual environment, scale/theme/configuration, selected/discovered/executed test names and Theory rows, commands/TRX hashes, counts and sanitized failure/retry provenance.
- Native keyboard/mouse/UIA observations proving in-cell editing, plus commit/cancel/command/error outcomes linked to stable BCI-A IDs.
- Independent persisted-state checks for the packaged receipt flow, stale/denied/uncertain outcomes and stock reconciliation; mocks do not prove SQL atomicity or privileges.
- Safe screenshots/observations for compact layout, focus, popup bounds and validation at the approved matrix; tester/date and explicit attended acceptance.
- Ownership/worker/GUI drain and positively verified cleanup; all unresolved resources, failures, missing cases and approved exceptions remain visible.
- Every mandatory BCI-A row has its own applicable evidence and reviewer disposition. A full-suite green summary does not replace a missing obligation or attended observation.

## 8. Result template — populate only from actual execution

| Field | Recorded value |
|---|---|
| BCI task / acceptance IDs / date / tester | NOT RUN |
| Product SHA / test SHA / driver metadata / vendor pins | NOT RUN |
| Package location / manifest SHA256 / EXE and test DLL SHA256 | NOT RUN |
| Windows / runtime / process bitness / SQL / scale / theme | NOT RUN |
| Project / command / filter / discovered and executed names | NOT RUN |
| PASS / FAIL / SKIP / BLOCKED / NOT RUN / timeout / abort | NOT RUN — no runtime counts assigned |
| Observable UI / VM / request / independent SQL result | NOT RUN |
| Sanitized artifacts / TRX hashes / failed trials / review | NOT RUN |
| Worker and process drain / owned SQL and principal cleanup | NOT RUN |
| Exceptions / remaining risk / explicit decision | NOT RUN |

A failed trial stays linked to its actual revision; a rerun records a new result. Documentation reviews and package rehashes are not GUI execution. Do not fill PASS by inference from source, another row or historical acceptance.

## 8.1. BCI-T00 native editor proof — 2026-10-20

**Boundary:** T00 proof host only; no shipping `src` change, replacement receipt UI, package, SQL or BCI-T01–T06 acceptance. A01–A03 below refer to the retained native test host, not an integrated Goods Receipt feature. A24–A26 remain NOT RUN.

### Baseline and execution

- Isolated managed worktree `bci-t00-native-proof/CleanArchitecture`, branch `codex/bci-t00-native-proof`; parent source `ebedeea42f31fe0c6ef6fc72237cd54eecbc3efe`. Compared with planning `f37038b`, product and vendor source are unchanged; the only `tests` delta before T00 is `tests/README.md`. The intervening roadmap documentation is not a product revision.
- Pins were initialized recursively and inspected: BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No pin or dependency lock change.
- Windows 11 Home Single Language `10.0.26200`, interactive desktop, .NET Framework release key `533509`, SDK `8.0.425`, Release/net48/x86 testhost and UIA child. Proof measured 96 DPI/100%, 1920x1080, Light. Other DPI/theme configurations are NOT RUN for this proof; T05/T06 own their matrix.
- Existing `StaTest`, `WinFormsTestGuard`, assembly serialization and `GuiOwnedProcess` remain the execution/cleanup owners. The existing temporary probe compiler now accepts the embedded [UIA observation client](../tests/MyDmsVn.Desktop.Tests/GuiInlineEditorProbe.cs); no new test project or runner. Compiler budget 30 seconds; each UIA child 15 seconds with job/tree/pipe drain verified; five observations in one finite STA scope. No SQL identity, database or package is allocated.
- Locked restore succeeded. Baseline `InventoryAccessibilityAcceptanceTests`: 14 discovered/executed PASS, 0 FAIL/SKIP, local `TestResults/bci-t00-baseline/baseline.trx`. Existing read-only line-cell/Open-row assertions are retained unchanged.

Commands from repository root (fresh build is included in each test command):

```powershell
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests' --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/bci-t00-final5 -- RunConfiguration.TargetPlatform=x86
```

Test source is retained at `8338ce26f0877d5c74024ee742c6386cd779222c`; the final5 build was made immediately before that commit from the same test/source contents (parent `ebedeea` plus that test patch). This is not a clean packaged candidate or a product/test revision equivalence claim. Desktop test DLL SHA256: `7A73182E147A3517BD2B7B9CB2D4D60342D30D6A09B73A80C5B7E52FE80289EE`.

| Local TRX | Actual results | SHA256 |
|---|---|---|
| `bci-t00-baseline/baseline.trx` | 14/14 PASS | `1C36C128B3CB5D9EBEFBA5F5EC90F045912F0851CB57ADA5456157E2441ADB60` |
| `bci-t00-final/focused.trx` | 38 PASS / 1 FAIL / 0 SKIP | `39524B2E80B2E0E7CC7197D6CA88055897DE2AB5965FF77920C59AB02DA27FCB` |
| `bci-t00-final2/focused.trx` | 37 PASS / 2 FAIL / 0 SKIP | `B078E0D099E527A9954239C4ACF244F6CFAB1C25AD4BBBE3169460C5D72035E3` |
| `bci-t00-final3/focused.trx` | 37 PASS / 2 FAIL / 0 SKIP | `742FF37DB22E15242CF43BBA008AD7C77FF3E845BD739215B07107193CEA6AEB` |
| `bci-t00-final4/focused.trx` | 38 PASS / 1 FAIL / 0 SKIP | `6E3E7AAD82B62155AC31DE22C1E9AA4356D53EAE048CC76D8806E3D2342238EC` |
| `bci-t00-final5/focused.trx` | **18/18 PASS**, 0 FAIL/SKIP/abort/timeout | `F53D1ED52EBB91A550F548B4A274F91FCBCCF0243A5FAE6B9D9A508ECF4EF579` |

Paths are relative to local `TestResults/`. Final5 reconciles exactly four new Facts (the three plan names plus `Native_uia_observes_the_stock_hosted_editor`) and 14 existing Inventory accessibility invocations. Final4 additionally selected/executed all 21 GuiOwnedProcess invocations, all PASS; its failed row was only the evolving mouse proof. The helper/fixture source is unchanged between final4 and final5; final5 changes only the new proof's hit-test wait. These are separate candidate/run results, **not** one 39/39 run. The final5 18 names were checked in actual TRX against the source inventory. The 21 helper invocations include Theory rows; no historical count was used as new acceptance credit. Baseline/final commands restore/build/test the direct Desktop project; the final/final2/final3/final4 command also includes `|FullyQualifiedName~GuiOwnedProcessTests` in the filter and its own results directory. Raw TRX stays local and is not committed.

### Observations and decision

1. `Native_cell_entry_uses_registered_bootstrap_editor`: F2 enters the registered Quantity/lookup controls inside the active cell with actual native focus; KeyDown/Char starts Unit cost with the first character retained. Numeric Enter commits, Escape retains the previous decimal. The final host uses actual native mouse button input for Unit cost, observes one `MouseDoubleClick` and commits `9.75m`. It first acquires and verifies its own foreground window using a temporary input-thread attachment (always detached), verifies the grid at the measured point, and restores the pointer. Synthetic window-message mouse trials were unreliable and are not credited as physical-input evidence. Lookup typing `P8`, F4/Down/Enter commits key 8; popup Escape then cell Escape cancels separately, and closed lookup Tab advances to Quantity. Shift+Tab/Page/outside-focus and the complete command matrix remain T03 obligations.
2. `Logical_values_remain_typed_when_display_changes`: pending query leaves key 7 committed; lookup selection produces an `int` and a closed-cell product label. A formatted `1,234.5` input commits `1234.5m`; closed cell displays `1,234.5000`. Display conversion is configured independently. Numeral mode here proves presentation/logical separation only; T01/T03 must use lossless None mode plus strict parser, not vendor Numeral truncation as validation.
3. `Shared_editors_dispose_with_their_grid`: sequential rows reuse the same numeric/lookup native handles and editor instance; another grid owns another editor. Grid disposal releases active lookup/popup results grid, used controls and never-started formatted control, with exactly one Disposed event per owned wrapper. The host owns the grid, which owns registry editors; no external editor substitute or per-cell controls.
4. `Native_uia_observes_the_stock_hosted_editor`: a separate x86 UIA process finds Quantity on rows 1/2, Unit cost, and Product on rows 1/2 through current semantic wrapper names. It focuses the inner native EDIT and uses ValuePattern to enter `6.25`; native Enter commits exact decimal cells, while lookup text stays pending with key 7 until cancellation. No coordinates or VM assignments are used by this UIA client. Every child reports verified cleanup; each host/registry is deterministically disposed, and no owned probe/testhost process remained at final inspection.
5. The measured stock numeric wrapper is UIA `ControlType.Edit`; lookup wrapper is `ControlType.ComboBox`. The actual input is a descendant `WindowsForms10.EDIT...`, whose Name initially reflects input text and Help is empty. Numeric wrapper Help is generic `Bootstrap-inspired text input.`. Selecting the wrapper itself as Edit exposed a ValuePattern that did not update native raw input; drivers must descend to the actual Edit. Updating wrapper AccessibleName proves reachability across reuse, **not** typed accessible validation or dynamic pending/error Help.

**Recorded implementation choice:** retain SourceGrid and its pinned stock `CreateLookupBox(typeof(int))` / `CreateFormattedTextBox(typeof(decimal))` adapters for the first integration, with an app-owned editable grid/session accessibility bridge. Native editor reachability and typed commit are feasible; no DGV switch is warranted by these measurements. T02/T03 must preserve lifecycle/ownership and apply full editability and pending-value policies; T04 must implement cell providers that revalidate identity and route Value/actions through that same pipeline. The current `AccessibleActionGrid` stays read-only for result surfaces; it cannot certify editable cells.

**Specified control seam:** stock adapters and their internal controls are sealed. Exact active-control accessibility overrides and right-aligned native numeric input cannot be obtained just by configuring names or cell alignment. The app-only option is an `EditorControlBase` adapter owning a `BootstrapFormattedTextBox`/`BootstrapLookupBox` subclass (`protected Editor` gives native TextAlign); it must reproduce the proven first-character, Enter/Escape/Tab, popup-focus, conversion/cancel and registry-disposal behavior. This remains proposed, not implemented. If a vendor seam is preferred, the exact candidate is additive control-factory overloads in `Editors/BootstrapSourceGridEditorRegistry.cs` plus adapter construction/CreateControl in `Editors/BootstrapLookupBoxEditor.cs` and `Editors/BootstrapFormattedTextBoxEditor.cs`, and public native TextAlign in Bootstrap5WinFormUI `Controls/BootstrapTextBox.cs`. Preserve existing factory overloads/default internal controls and grid-owned one-time disposal; reject null/cross-owner controls; add upstream factory/ownership/keyboard/UIA/align tests and a separately reviewed commit/pin before use. No vendor change is included here. T04/T05 must prove whichever control path they actually implement; the T00 stock proof does not waive their validation/accessibility/alignment requirements.

### Failed trials and limits

Local trial1 failed compilation: the proof initially assumed an absent `Format` property; it now uses the existing `ConvertingValueToDisplayString` boundary. Trial2 had 2 PASS/2 FAIL: wrong UIA wrapper selection and a Char sent without the preceding KeyDown. Trial3 had 3 PASS/1 FAIL; trial4/5 selected native entry only and failed. Source inspection located SourceGrid's `m_keybordActivePosition` capture on KeyDown. Trial6/7 had 3 PASS/1 FAIL because the lookup was still filtered to the committed product; native query selection corrected the driver. Trial8/9 had 4 PASS. Trial10/11 each had 3 PASS/1 FAIL on mouse dispatch; trial12 diagnostic run selected native entry and passed; the first broader `bci-t00-final` run had 38 PASS/1 FAIL on that mouse path. Trial13 had 3 PASS/1 FAIL; retaining the actual mouse-event observer and bounded wait produced the trial14 native-entry PASS. The broader `bci-t00-final2` run had 37 PASS/2 FAIL: mouse dispatch plus existing `Packaged_lookup_keyboard_recovers_its_owned_window_from_a_background_editor` failed its owned foreground precondition (DescendantsOutlivedParent, cleanup verified=True). Trial15 had 3 PASS/1 FAIL on checking SetForegroundWindow return rather than actual foreground state; trial16 native entry passed, while broader final3 again observed an unavailable foreground for mouse input and the same existing foreground helper failure. Final4 passed all 21 helper cases but rejected a transient non-grid mouse hit target; trial17 native entry passed. Final5 adds a bounded hit-target wait before actual mouse input and passes all 18 selected cases. The final driver verifies actual HWND ownership before issuing mouse input. Earlier trials are not erased by later results. Compile errors and driver failures are not intended TDD RED results or product regressions.

Independent skill-mandated review identified an Important competing wait/cleanup deadline in the UIA test. The host now pumps until the bounded GuiOwnedProcess task returns, including cancellation, before disposal. A posted-Enter scheduling concern was treated as a reliability issue and fixed with a finite condition wait for numeric edit completion. No process helper execution/cleanup policy was relaxed.

No app command, strict invalid-input setter, Posted/denied/stale accessible target, native popup UIA selection, ordinary Login, SQL persistence, package or attended Business Compact acceptance is established here. These belong to T01–T06. This is a capability/decision checkpoint, with concrete follow-up seams, not production readiness.

## 8.2. BCI-T01 lossless scalar conversion — 2026-10-20

**Boundary:** internal Desktop.WinForms parser and scalar/error-boundary tests only. A04–A06 are PASS in this scope; native typing/paste/caret retention, strict accessible setters, edit-session culture snapshots and Save/Post/grid integration remain T02–T04 obligations. A07 is still NOT RUN for inline command behavior; the existing ViewModel overflow regressions below do not establish that future interaction.

### Source, environment and implementation

- Base `059bb76754d89562607e44d8d039cf703fc2bd34`, same managed worktree/branch as T00. Parser/test commit **`84066f848a0950f213ea8cb935509fff1157f185`** is the source used for the final build/run. The documentation handoff follows that code commit. All three vendor pins retain the exact T00 values above; no dependency, project graph, lock file, public contract, server or schema change.
- Added [ReceiptNumericInput](../src/MyDmsVn.Desktop.WinForms/ReceiptNumericInput.cs), its internal `ReceiptNumericField` enum and [ReceiptNumericInputTests](../tests/MyDmsVn.Desktop.Tests/ReceiptNumericInputTests.cs). A scoped [friend assembly attribute](../src/MyDmsVn.Desktop.WinForms/Properties/AssemblyInfo.cs) permits direct Desktop tests of the internal API, following the existing repository AssemblyInfo pattern. The parser is not yet called by the shipping receipt control.
- Windows `10.0.26200`, SDK `8.0.425`, .NET Framework/net48, Release/x86. No form/popup, UIA probe, SQL allocation, package, process launcher or display/theme matrix was exercised for T01. The existing VM class includes a serialized STA synchronization-context regression; this is not native editor acceptance.
- `TryParse` inspects the complete raw lexical candidate before decimal conversion. It accepts outer whitespace, a culture-leading sign, ASCII digits and the explicit culture decimal separator; it rejects grouping, currency, exponents, mixed separators, embedded whitespace/NUL and malformed/non-ASCII digits. It requires at least one digit, so `.5`/`1.` (or culture equivalents) are valid, but a sign/separator alone is not.
- Every fractional digit beyond `GoodsReceiptContractLimits.DecimalPlaces` must be zero. Only that proven zero padding is removed from the local parsing candidate. The caller's original string remains available unchanged. Conversion uses explicit culture/styles; success returns the exact decimal and null error, failure returns zero plus a field-specific actionable error. Quantity > 0, UnitCost ≥ 0 and both maxima come directly from `GoodsReceiptContractLimits`. Zero padding is exact even at the contract maxima; no clamp or rounding acceptance is used. Numerical negative zero is zero, so it is allowed only for UnitCost.
- The future session owner must pass its captured culture; parser tests prove the supplied culture overrides thread culture. Literal custom signs/multicharacter separators have a narrow test; arbitrary custom NumberFormatInfo tokens that conflict with digits/each other are not a supported new product contract. Defensive null-culture/unknown-field checks do not introduce another parsing fallback.

### Executed verification and provenance

```powershell
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~ReceiptNumericInputTests|FullyQualifiedName~GoodsReceiptEditorViewModelTests' --logger 'trx;LogFileName=final.trx' --results-directory TestResults/bci-t01-final -- RunConfiguration.TargetPlatform=x86
```

Locked restore succeeded. Baseline used only `FullyQualifiedName~GoodsReceiptEditorViewModelTests`; the RED run selected only `FullyQualifiedName~ReceiptNumericInputTests`. The initial GREEN used the final filter with its own `green.trx`/directory; the final run rebuilt the committed, BOM/CRLF-normalized source. All runs used the direct existing project, net48/Release/x86 flags above; no `--no-build`, zero-match success or skipped mandatory case was credited.

| Local TRX under `TestResults/` | Observed outcomes | SHA256 |
|---|---|---|
| `bci-t01-baseline/baseline.trx` | Existing VM 17/17 PASS | `DDA7807B951E284BE8E5244B6F2076995A8F53EBD32BBA9638393781F78963D9` |
| `bci-t01-red/red.trx` | Parser 12 FAIL / 24 PASS / 0 SKIP | `2073C64410FDF4E16288780FAE0B2A6E6817C6D3C2769DF96C8F7654F46E15FC` |
| `bci-t01-green/green.trx` | Parser 36 + VM 17 = 53/53 PASS | `C20827E5A8E5900B7C708FC369A500201D87E0EE45270AB9E2B66D87EA7AB3E7` |
| `bci-t01-final/final.trx` | **53/53 PASS**, 0 FAIL/SKIP/abort/timeout | `BB0BB1A4C38D377BF7292282242948D546FEE2B750284AC699143BAB4EFAB60B` |

Tests were written before behavior. A compile-only scaffold with the planned signature returned `false`, `value = 0` and `Quantity / Unit cost parser is not implemented.`; this made all 12 valid-value test invocations fail their actual acceptance assertions. The 24 malformed rows passed that scaffold only because every candidate was rejected; they earn coverage from GREEN/final, not from the scaffold. No compile error was counted as intended RED. The completed implementation then passed both parser and existing VM scopes.

Final TRX was reconciled against source: the three named plan methods have 4 precision rows, 4 culture rows and 2 field-bound rows; malformed-input Theory has 24 rows; explicit-thread-culture and custom-sign/separator tests are 2 Facts. This is **36 parser invocations across 6 methods**, plus **17 existing VM Facts**. Exact maxima, smallest representable contract step above maxima, one whole unit above maxima, negative/empty/zero, long padding, tiny far-tail nonzero, culture separator equivalence, grouping/pasted syntax and overflow are exercised. Scalar strings model pasted candidates; native clipboard/keyboard actions were not run. The existing VM scope explicitly includes `Boundary_line_amount_overflow_is_safe_and_blocks_save` and `Aggregate_overflow_across_safe_lines_is_safe_and_blocks_save`; their original assertions are unchanged.

Final built DLL SHA256 values (independent of TRX hashes):

- Desktop.WinForms: `19453277908EC22C129C3D1E12DE4D307948B40822EC256C06A171B205F4D1C2`.
- Desktop.Tests: `45A6886C0B829BAFE4BCB3B4AEA81F48207C51F60D62DCB31D3A423AF18DCF98`.

Raw TRX stays local. No package/EXE hash or SQL/GUI resource cleanup is claimed: none was created by T01. The pure tests completed normally, with thread culture restored in `finally` where changed. An independent read-only code review found no actionable Critical/Important/Minor issue in the three code/test files; it did not execute tests or independently certify TRX. Review limits are the same scalar boundaries above. Static handoff checks cover diff, local links/anchors, planned-versus-implemented names, roadmap and encoding. Next is BCI-T02 stable projection and cell sessions; T00 historical results remain unchanged and no BCI integrated/product release acceptance follows from T01.

## 8.3. BCI-T02 stable projection and cell sessions — 2026-10-20

**Boundary:** A08–A11 are PASS against the new app-owned adapter hosted with a real `GoodsReceiptEditorViewModel`. This is the T02 foundation, not an integrated Goods Receipt feature. `GoodsReceiptControl` still uses the external line editor and its existing projection. T03 owns the complete atomic switch, asynchronous lookup/command routing and panel removal; T04 owns writable accessibility. No A07/A12–A30 product acceptance is inferred from these adapter tests.

### Candidate, implementation and handoff

- Base `bfb35f3485219bd4c186b7aa2807a26b234f9f35`, same managed `bci-t00-native-proof/CleanArchitecture` worktree and `codex/bci-t00-native-proof` branch. Foundation commit `4b4987fc21e07cf1535c3dc75fb787276f57d57b`; current-option admission follow-up and final product/test source **`0cf2bca5d60322f9e1160d0ae4ca037190f779cc`**. Final verified run rebuilt that commit. Vendor pins remain the three exact T00 pins; no project graph/lock, API/DTO, server, schema or permission change.
- Added [GoodsReceiptLinesGrid](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGrid.cs), [adapter](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs), [ReceiptEditIntent](../src/MyDmsVn.Desktop.WinForms/ReceiptEditIntent.cs) and [native session tests](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptInlineEditorTests.Sessions.cs). The existing inline class is partial to retain the original proofs alongside the new tests. No invisible/external inputs substitute for the adapter-host editors.
- One registered stock lookup, Quantity and Unit-cost editor per grid/configuration. Product cells store `int` keys; labels use a separate display conversion. Numeric None-mode input passes through the T01 parser with a read-only culture snapshot. A session captures document generation, line reference, column/cell identity, original value/raw text and current raw candidate. Successful conversion supplies a single-use token before VM mutation; ordinary cell value notifications do not authorize writes.
- Ordinary editor/line/status notifications update non-active cell values and repaint in place. They never replace/Redim the active cell, resize its row or rewrite its control text/caret. Structural changes that leave the active target at the same ordinal defer projection until the edit boundary; removal/movement of that target cancels and invalidates before rebuilding. Native edit-end callbacks queue structural work outside SourceGrid's controller enumeration, with generation/disposal fencing. Commands can flush it after `EndEdit` returns.
- `ResetDocument` is a deliberate cancellation/generation/rebinding boundary. Positive persisted IDs map reload selection; new ID-zero rows use reference identity followed by an explicit nearest-row fallback. The pending mapping survives intermediate empty/partially loaded collections, including a new line gaining its saved ID. The final explicit reset settles an unavailable target onto the fallback. Captured old cells/controllers cannot update a replacement row.
- Full editability is checked at edit start, conversion and VM write: grid availability, VM Posted/read-only, busy, `RequiresPostOutcomeReload` and the supplied `canEdit` predicate. The owner must supply current access/workspace/session eligibility and call `RefreshEditability` on its changes. Numeric dirty text blocks navigation without mutating the VM; Save/Post/Add completion commits only valid input; Remove cancels the active candidate; query-only permitted navigation cancels without selecting a highlight. These are adapter policies, not yet production command wiring.
- Active lookup presentation is retained across option notifications. Admission uses **current VM options**, not the old hosted snapshot, while an unchanged historical selection remains valid. Complete async search generation/result binding and focus-loss intent coordination remain T03 work.
- Adapter disposal invalidates/cancels sessions, unsubscribes VM/options/line/grid events, detaches cell controllers and disposes shared controls through the idempotent owning registry. It leaves the caller-owned grid undisposed. Active popup/results controls and never-started/shared editor ownership remain covered by the native proofs. No consumer font is disposed.
- T03 preparation deliberately leaves `GoodsReceiptControl` untouched: replace its LineGrid construction/Click/Enter path and `SetLineRows` only together with command routing; ordinary `ApplyState` should refresh the new adapter/editability, while deliberate Load/Save/New boundaries call `ResetDocument`. Removing the external panel before that atomic wiring would create an incomplete shipping workflow. The full predicate must include its existing refresh/list busy, access-denied and uncertain-Post state. T02 supplies those consumed interfaces rather than enabling partial editing in production.

### Execution and retained failed trials

Windows `10.0.26200`, SDK `8.0.425`, net48/Release/x86, interactive serialized STA with `WinFormsTestGuard`, finite waits and deterministic disposal. The retained T00 proof reports actual 96 DPI/100%, 1920x1080, Light and runs its bounded owned x86 UIA client. This is not a new 125%/Dark/layout/package matrix. No SQL allocation, runtime identity, package or ordinary Login was used.

```powershell
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~GoodsReceiptEditorViewModelTests|FullyQualifiedName~ReceiptNumericInputTests' --logger 'trx;LogFileName=verified.trx' --results-directory TestResults/bci-t02-verified -- RunConfiguration.TargetPlatform=x86
```

Locked restore succeeded. Baseline selected existing control/VM classes only. Initial RED selected the four exact plan methods plus `Editability_is_checked_at_start_and_commit_and_disposal_releases_sessions`; the compile-only scaffold created plain cells without editors, so all five failed the real native F2 edit assertion. `green`/`green2`/`green3` used that same five-case selection. The boundary selection contained the invalid-candidate, append and query-only methods. Conversion/review/option RED runs each selected their single named regression. The sessions run selected the inline class excluding the four original T00 methods; all broader runs used the full filter above and their own logger/result paths. All commands were direct project tests with the target/build flags above, no `--no-build` or missing-case credit.

| Local TRX under `TestResults/` | Actual result | SHA256 |
|---|---|---|
| `bci-t02-baseline/baseline.trx` | 38 PASS | `45836560689073E19CD446616C21E60D647E73A3BF90BB99F8689083A808E17E` |
| `bci-t02-red/red.trx` | 0 PASS / 5 FAIL | `D3316EB40CFE4EA209FB3E2801103C7AFCD553079D3EF70631EBE326F8519A93` |
| `bci-t02-green/green.trx` | 4 PASS / 1 FAIL | `994918CD4C49F171450D9B61E9D90CEAA91646C5AC6932F3E47FCC5701B8F5E1` |
| `bci-t02-green2/green.trx` | 4 PASS / 1 FAIL | `A661E82FB80FC6CDE87ABF2242DA0EDEE29564DFB38C8EAEC110D3A57817FDAE` |
| `bci-t02-green3/green.trx` | 5 PASS | `A61E678A6B7ED39455263D0E9C113EA60EA564C5FBC71C451542EE87E1157EA5` |
| `bci-t02-boundary-red/boundary.trx` | 1 PASS / 2 FAIL | `E2784744A5DC0A61CA0AB1F1B0DCDA80CA96A3412822A1844CCB59421EBA3126` |
| `bci-t02-conversion-red/conversion-red.trx` | 0 PASS / 1 FAIL | `1D32C4781CE41FC8F926CCCA92F98E2C1944676338A996AB21C374584F195A63` |
| `bci-t02-sessions/sessions.trx` | 9 PASS | `687B9760072700B11D66FB4BD2EA5CED0BD9F85AFF062B9D70F3C73E0883D832` |
| `bci-t02-focused/focused.trx` | 86 PASS / 1 FAIL | `92FF8734D9E43FC24A0214DE1361B75B8EA40BBB688D55CB5F7FF0EE4C283682` |
| `bci-t02-review-red/review-red.trx` | 0 PASS / 1 FAIL | `A28506333342B1949C3183C4E060BAD37A923D792834180E6E904E316F14DEBA` |
| `bci-t02-reviewed/focused.trx` | 88 PASS | `EC8F74A839D89C21060BC6E168862D910A01152F2A23C14C3F390E38C4EE5173` |
| `bci-t02-final/final.trx` | 88 PASS | `AB16495A9B32BA0CDF8EADB44213ECD8202167BEE2B5CFF3F95724DCC2560858` |
| `bci-t02-option-red/option-red.trx` | 0 PASS / 1 FAIL | `DA5A0BE4AF6BCD9F1CBBDEB0F0F9195D4230EEF6D8481B2D76AC71D8BF06C341` |
| `bci-t02-options/options.trx` | 89 PASS | `0F4B30AD96B7C37A952F1DB924BB26555079DAB4D5A4A648F233DC4FB58C1EC1` |
| `bci-t02-verified/verified.trx` | **89 PASS**, 0 FAIL/SKIP/abort/timeout | `3B5D3333F8CB810029FE5AE2065D8EB6FA02C771871C0A8CC93DE2244CC91E63` |

The first two implementation trials failed persisted selection during incremental reload. The boundary trial found a real controller-enumeration/rebuild defect; its product-choice failure was a test fixture incorrectly expecting a historical-only option to be active, corrected by activating the real ProductOptions VM with the deterministic catalog source. The conversion RED exposed a real unsolicited-cell-notification VM write. The broader focused failure was the existing T00 mouse proof's exact hit-window precondition, not a session assertion; the test now temporarily holds **only its owned host** TopMost during mouse input and restores it in `finally`, retaining foreground, exact hit-window, event-count, typed-value and pointer-restoration checks. No assertion was removed or skip added.

Independent read-only code review found one Important new-line selection defect: an ID-zero row retained the previous persisted selection ID. `A_saved_new_line_uses_its_own_selection_fallback_instead_of_a_previous_persisted_id` reproduced row 3 incorrectly selecting row 1, then passed after fixing identity clearing/fallback retention. A final current-option regression independently reproduced an old lookup presentation snapshot authorizing an inactive new choice; admission now rechecks current VM options, and it passes. All failed trials remain recorded. The reviewer did not run tests or certify TRX; no second review is inferred from the corrective runs.

Final TRX names/rows reconcile to **15 inline Facts** (4 retained proofs + 11 new session Facts), **21 control Facts**, **17 ViewModel Facts** and **36 parser invocations** = **89**. Assertions observe actual focused editors, raw text/caret, typed VM/cell values, stable cell references, totals, event counts, cancellation, same-ordinal replacement, stale notification rejection, new-line saved selection and active-popup disposal. Existing control/VM/parser assertions are retained. No old historical project total was used as a discovery target.

Final built Desktop.WinForms DLL SHA256: `95F61D87DC2071161872668E6285DFCFF3780E2F477815B7A8ADB444193CED96`; Desktop.Tests DLL SHA256: `E4090BA01C6788B1B850411F102F47E617086E485B74E60E04C69438EDACF9B7`. Raw TRX is local only. All hosted controls were deterministically disposed and owned UIA clients reported process/tree/pipe drain; final process inspection found no probe/testhost left. No package/EXE or SQL cleanup is claimed because none was created. T03 still must prove the real command/session/lookup path, and T04 must add writable providers/native metadata; T02 does not certify their future behavior or production readiness.

## 8.4. BCI-T03 lookup and command integration — 2026-10-20

**Boundary:** T03 is complete in the focused actual-control/VM scope below. Product, Quantity and Unit cost now edit in the shipping GoodsReceiptControl path; the external line editor panel and its event/synchronization path are removed. A07/A12–A23 outcomes refer to this hosted-control evidence, not frozen package, writable native UIA, native 125%/Dark or production acceptance. Those remaining obligations belong to T04–T06.

### Source and implementation

Base `b003099bd387955755c4c177ea95242aa45ba558`; product/test source `436cd7204d85780e348e909df5802e890c45fc3d`. The same managed worktree and branch from §8.3 are retained. All three vendor pins are unchanged. No application/transport API, DTO, schema, permission, persistence, runtime target, project/lock or module change. The app-owned control helper properties now expose the actual shared lookup/formatted controls, not hidden numeric/lookup substitutes.

- GoodsReceiptControl is partial; [InlineEditing](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.InlineEditing.cs) owns command admission, first-line-error focus and the two receipt-specific input classes. The [adapter](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs) preserves active cells on notifications, consumes T01 parsing, and admits writes using current options and full live editability.
- Product text drives the existing ProductOptions VM. DataSource refresh uses the pinned lookup's committed-selection reconciliation without redundantly selecting its value, preserving raw text and caret. Ending/canceling the product session changes the VM query generation; obsolete providers that ignore cancellation cannot bind into the next row/document/session. Historical labels remain displayable, while historical-only IDs are rejected as new choices.
- Every command applies its existing intent policy before the VM action. Save completes valid input; unresolved product/invalid numeric input retains raw text and dispatches nothing. Post completes input and then requires a clean saved ID/version, with an explicit Save message for changed input and no implicit Save/Post retry. Dirty navigation preserves pending input. Query-only allowed navigation cancels it. Add completes the candidate and focuses the new Product cell; Remove cancels its selected target.
- ReceiptCommandButton retains cell focus during native mouse capture and admits the actual release/click through the same handlers. A canceled drag never resolves the pending query. ReceiptResultsGrid gates pointer navigation before native focus; its inherited AccessibleActionGrid remains the read-only receipt-results bridge. Unsealing that app class changes no other caller's behavior. Writable line accessibility remains T04.
- Full busy/read-only/access-denied/uncertain-Post and disposal guards apply at edit start, conversion, VM write and command admission. Header workflows and opaque expectedVersion remain. First server line errors map through the current submitted line order to the actual editable cell; all indexed error messages remain available on the grid. Native Enter/Tab conversion failures now publish inline status, preserve raw/focus, and clear on valid completion/cancel.
- [GoodsReceiptEditorViewModel](../src/MyDmsVn.Desktop.Application/GoodsReceiptEditorViewModel.cs) now fences async Save/Post/Refresh UI callbacks by document generation, checked inside the dispatcher as well as before follow-up work. An old provider completion cannot overwrite a replacement document or publish its old error/success. Existing lifetime/session cancellation, uncertain-Post read recovery and backend authority remain unchanged.
- Native Tab/Shift+Tab skip row number/Total using SourceGrid's public Unselectable controller and form traversal. No trailing row is created. Line and aggregate overflow calculations/validation remain independently covered by the existing VM cases selected below.

### Executed scope

Windows 10.0.26200, SDK 8.0.425, net48/Release/x86, interactive serialized guarded STA. The retained native proof in this run reports 96 DPI/100%, 1920x1080, Light. The affected existing theme tests ran in their named modes; that is not a native 125%/Dark or frozen layout matrix. Each hosted test uses actual controls, deterministic client doubles, finite waits and disposal; no live SQL, ordinary Login, package probe or frozen package was used.

```powershell
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~ReceiptAcceptanceTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~GoodsReceiptEditorViewModelTests|FullyQualifiedName~ReceiptNumericInputTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests' --logger 'trx;LogFileName=verified.trx' --results-directory TestResults/bci-t03-verified -- RunConfiguration.TargetPlatform=x86
dotnet restore tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~GoodsReceiptProductOptionsViewModelTests|FullyQualifiedName~CatalogLookupViewModelTests|FullyQualifiedName~GoodsReceiptListViewModelTests|FullyQualifiedName~DesktopSessionExpiryTests' --logger 'trx;LogFileName=verified-session.trx' --results-directory TestResults/bci-t03-verified-session -- RunConfiguration.TargetPlatform=x86
dotnet restore src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj --locked-mode
dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release --no-restore -p:PlatformTarget=x86
```

Final committed-source verification: **146/146 Desktop + 28/28 Session PASS**, zero failed/skipped/not-executed/aborted/timed-out. Locked restores succeeded; Desktop.App build succeeded with zero warnings/errors. Reconciled actual TRX invocations:

| Class | Executed PASS |
|---|---:|
| GoodsReceiptControlTests | 35 (21 retained + 14 new integration Facts) |
| ReceiptAcceptanceTests | 29 (including 2 new Facts and retained Theory rows) |
| GoodsReceiptInlineEditorTests | 15 |
| GoodsReceiptEditorViewModelTests | 17 |
| ReceiptNumericInputTests | 36 |
| InventoryAccessibilityAcceptanceTests | 14 |
| Session: GoodsReceiptProductOptionsViewModelTests | 6 |
| Session: CatalogLookupViewModelTests | 12 |
| Session: GoodsReceiptListViewModelTests | 6 |
| Session: DesktopSessionExpiryTests | 4 |

All proposed A07/A12–A23 method names now exist and ran. Three retained acceptance methods were deliberately renamed to the A20/A22/A23 names: conflict, four uncertain-Post combinations and four session-switch/late-success/failure combinations. Their behavior assertions remain; the session-switch scenario now starts with a real inline numeric candidate and checks its submitted value. Existing line multiplication and aggregate overflow cases both ran. The new native input cases include F2, typed query, Enter, Escape, Page keys, Tab/Shift+Tab, physical popup mouse selection, ordinary outside focus, button-window-message down/up with a message loop between them, drag-abort and master-list pointer admission.

### Review, failures and explicit decisions

Initial RED was 0/5 because the original control had no registered cell editors. The first integration trial retained six failures from external-control assumptions and pre-handle line selection; these were corrected against the actual grid/VM. The boundary trial found an old Save overwriting a replacement document (3/4 PASS); the new generation fence made its regression and all 17 VM cases green. A two-case caret trial failed because redundant SelectValue reset pending presentation; using the pinned DataSource reconciliation made both green. Dirty-draft query cancellation, indexed server-error focus and computed-cell Tab traversal each had an observed failing regression before their fixes.

One independent read-only review found two Important issues: unconditional wrapper Focus stole native input focus after failure, and native-key conversion errors were invisible. Regressions reproduced them; focus now changes only when the editor does not ContainsFocus, and inline error events update/clear status for native as well as command commits. The stale-error Minor was fixed by the same tested error lifecycle. No second review or reviewer certification of TRX is claimed; the reviewer did not run GUI/tests. The supplemental review-green trial still failed stale error clearing before native Escape cleanup was corrected.

Explicit decisions/limits:
1. The former external Open-line/read-only provider assertion no longer describes the product. Its replacement retains receipt Open action and typed inline rows; the old AccessibleNumericBox typed/disabled guard is retained as a standalone wrapper test. Writable line provider semantics are **NOT RUN**, owned by T04, not inferred from names or wrapper success.
2. Supplemental physical New-button hit-window preconditions failed. Final New command proof uses native button window messages, with an actual loop between down/up and a drag-abort; physical popup selection remains a separate successful case with exact hit/foreground guards. Do not infer physical New-button/package/UIA success from that distinction. The remaining native package/accessibility milestone must certify the complete user path.
3. App-owned receipt command/results subclasses coordinate before native focus, rather than patching vendor internals or disabling keyboard selection. Their native input/drag/keyboard tests pass; writable UIA and final layout remain downstream costs.
4. The first Session run used an AnyCPU host against the prior x86 output and failed all 28 cases before their bodies with BadImageFormatException. Re-running the same selectors with explicit x86 build/host flags passed 28/28; the failed run remains below. A later outside-focus assertion initially expected wrapper Focus() to return true even though focus was forwarded to its native child; it now verifies ContainsFocus and the committed typed VM value instead.
5. All work is retained on the existing branch/worktree. No merge, push, package publication or release sign-off is implied.

Every TRX below is retained locally; the table contains reviewed counts/hashes only. Intermediate compile failures (incorrect test enum member and a void-lambda return during the generation-fence edit) were repaired before execution and confer no test credit. Per-case RED/diagnostic trials used their named exact-method selectors; broad focused/final/verified runs used the complete Desktop filter above; Session runs used the four-class filter above (the failed initial one omitted x86 flags).

| Local TRX under TestResults/ | Actual outcome | SHA256 |
|---|---|---|
| `bci-t03-boundary/boundary.trx` | 3 PASS / 1 FAIL / 0 not executed | `A484C08A3F045B681D41315340830578B830D7F3BCB5C81C9560F3B2300B0486` |
| `bci-t03-caret-green/caret.trx` | 2 PASS / 0 FAIL / 0 not executed | `958F64EB086A76914B498FD48AB9CEB98D2918FA797228BDBE95934E457A0E62` |
| `bci-t03-caret-red/caret.trx` | 0 PASS / 2 FAIL / 0 not executed | `AB9B9FBD2C3B78BB2F0155140C063421914B1A0AF3EF8745A8784E81AB59D0E6` |
| `bci-t03-diagnostic/diagnostic.trx` | 0 PASS / 1 FAIL / 0 not executed | `AA67C77D38E7D1B651824076316BEA0086BBFB95CD3D87C77394D60607D04076` |
| `bci-t03-errors-red/errors.trx` | 0 PASS / 1 FAIL / 0 not executed | `D0606AC99944C7BDA85D33EE59E30F6CCF4A7546D7EADB42646A87C3B94CC0C1` |
| `bci-t03-final/final.trx` | 146 PASS / 0 FAIL / 0 not executed | `C1354EAC87B00D1660BDEC51951130710BD5E9A691982616710AA0220F7C8FB9` |
| `bci-t03-final2/final.trx` | 145 PASS / 1 FAIL / 0 not executed | `BDB666314379551886DAED4387BC170A83C59620FF1940E0E194DB8EEFBDF0B0` |
| `bci-t03-final3/final.trx` | 146 PASS / 0 FAIL / 0 not executed | `DD9648F33550984F52FBD32384C2B8AAD7AD8F40792101D0BC7069A040B9362D` |
| `bci-t03-focus-diagnostic/focus.trx` | 0 PASS / 2 FAIL / 0 not executed | `060352578AA8095822A0AA0309CE182FC5A2BB70A8E2FF1F459E7D45D25F18F2` |
| `bci-t03-focus2/focus.trx` | 0 PASS / 2 FAIL / 0 not executed | `9D93AD3E07935068F6008993C49CDAE3A2A5AA14EAFE6B48F8333D857CF5B032` |
| `bci-t03-focused/focused.trx` | 136 PASS / 5 FAIL / 0 not executed | `2C9FBC638B167A9FE850471D69BC9600268607B6879E1348DF0118A0ED3F1447` |
| `bci-t03-focused2/focused.trx` | 140 PASS / 2 FAIL / 0 not executed | `92512FAC09F66473EAAF196DDE4DEFC22E8902932654D5B3F913A90D354AC029` |
| `bci-t03-focused3/focused.trx` | 143 PASS / 0 FAIL / 0 not executed | `DB345BC47817D6648DFAF06BC2C274C8165D6D6E180DF098BA589BC5BDF0C53A` |
| `bci-t03-green/green.trx` | 20 PASS / 6 FAIL / 0 not executed | `0FE02630534A6ACB1EA34AB4DE860FF634AD90A07E128B206D99EE3D700B36CC` |
| `bci-t03-guard-red/guard.trx` | 0 PASS / 1 FAIL / 0 not executed | `3538D02BF049E49BEC7A5461807B72E2656A5D9C1FD1577C8F39AE68104FB31B` |
| `bci-t03-integrated/integrated.trx` | 68 PASS / 0 FAIL / 0 not executed | `4C890AB7569F17C24B791E072189980C41F53EDF594B63C6C5DC7865259E5939` |
| `bci-t03-intent/intent.trx` | 0 PASS / 3 FAIL / 0 not executed | `03F19D056FEAFE9F3117291E357EC01398B58457266DF3E3820CD8F09118479D` |
| `bci-t03-intent2/intent.trx` | 0 PASS / 3 FAIL / 0 not executed | `EC4DD9D94ADBD13F0B90DF519369974FC7070B4F2AC36E9CB5C15D1E46FCD753` |
| `bci-t03-intent3/intent.trx` | 0 PASS / 3 FAIL / 0 not executed | `5051094BCBD182E3BD0F164790710B07301716610AF16EA6219079CBBCDA49C2` |
| `bci-t03-intent4/intent.trx` | 0 PASS / 3 FAIL / 0 not executed | `ADDD0162AB6C6C90D95128482A5B379FCE76FF05747292D08F1C6A2304DA6A4F` |
| `bci-t03-lookup/lookup.trx` | 2 PASS / 1 FAIL / 0 not executed | `71637F9741220FDDEFBFD2B39C72EAB84A3C0B8AC5630E5331696473D0524793` |
| `bci-t03-lookup2/lookup.trx` | 9 PASS / 1 FAIL / 0 not executed | `75F2F31CC16B4C11995F057A0BC4518811F8172A1D7A32714E8CBC4BDBF6B71D` |
| `bci-t03-mouse/mouse.trx` | 2 PASS / 0 FAIL / 0 not executed | `960EDC0B62A8D761537B3C9682CEA01D0C8FC241C66BDB6AEE06FFBE576706DE` |
| `bci-t03-pointer-red/pointer.trx` | 0 PASS / 1 FAIL / 0 not executed | `5410FD85053BAAAAC2F00A4775D97D05D608371BE8BA79B43C1ADD85BD6A5BEE` |
| `bci-t03-red/red.trx` | 0 PASS / 5 FAIL / 0 not executed | `E08E79AF9DCDAC0FAC565DE727073AC4BDD1CDE0DD22142BC88A07AE307DBA62` |
| `bci-t03-review-green/review.trx` | 3 PASS / 1 FAIL / 0 not executed | `F0FB6D18BAA6964FDA56F383B139FDC18E43608E918F11B5A29F8FE2C4904F92` |
| `bci-t03-review-red/review.trx` | 1 PASS / 2 FAIL / 0 not executed | `45DA77966401F9D1C513B988BC6A425AEC32C34C9072AB18D036EA4D91B03F8C` |
| `bci-t03-reviewed/focused.trx` | 145 PASS / 0 FAIL / 0 not executed | `5099A77487CB71EDB60BDA5B84A5778DCC2C4B1800B6F6A81141A08404A0DC5F` |
| `bci-t03-session/session.trx` | 0 PASS / 28 FAIL / 0 not executed | `8C50A8A2B324ADA17860CDACAAF151705B07A073959D1C44D384EC92BE4D3B4D` |
| `bci-t03-session-x86/session.trx` | 28 PASS / 0 FAIL / 0 not executed | `C77B2F1113C3849002782DBE3B664FAB25A2567FDE513A5DBF4B293905122DC6` |
| `bci-t03-tab-red/tab.trx` | 0 PASS / 1 FAIL / 0 not executed | `4C83BB0EB2D6BCEF6B86DC17FF8F803B2B8F37DFD72DADA68F0615B479DDDD36` |
| `bci-t03-verified/verified.trx` | 146 PASS / 0 FAIL / 0 not executed | `595A4428BAAA22016E84092A8B546D0FCC4E42EE1AEE075F3ACCAB30FD5D1D0D` |
| `bci-t03-verified-session/verified-session.trx` | 28 PASS / 0 FAIL / 0 not executed | `BDF2466329484430B7B55B82D86E95B7C58D520F10A9CC6C785C34F8C759F1AC` |
| `bci-t03-vm-green/vm.trx` | 18 PASS / 0 FAIL / 0 not executed | `7B89C0B56D2C19E075638D95AB1DF09E0C3BD2564798DBCDBE826C0F482BE91A` |

Final build artifact hashes (EXE is a build output, **not a packaged/accepted candidate**):

| Local artifact | SHA256 |
|---|---|
| `src/MyDmsVn.Desktop.Application/bin/Release/net48/MyDmsVn.Desktop.Application.dll` | `0AF2B73A0F013FE865BE5274F006819CF2C76F7C104BE3BACF3B8974515DD300` |
| `src/MyDmsVn.Desktop.WinForms/bin/Release/net48/MyDmsVn.Desktop.WinForms.dll` | `FB3BE0C204BD663DB09BE0A1398E87E2F48B980A9117743BC11C8582B3931E6A` |
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.Tests.dll` | `027E76188BBAF90212CE1C4EC390DD5A56171992705FA749BCC3E883C5F09622` |
| `tests/MyDmsVn.Desktop.Session.Tests/bin/Release/net48/MyDmsVn.Desktop.Session.Tests.dll` | `B246D7701E223B66C14DE1610659A566A5CEAF26F7478097916208775958EA12` |
| `src/MyDmsVn.Desktop.App/bin/Release/net48/MyDmsVn.Desktop.App.exe` | `FDFA05A8C5E3EA90D0966746E7FFEB0F607875F4096599685E671E18B06EB87D` |

Raw TRX/logs remain local. Hosted controls, shared editors and owned UIA clients completed/disposed; final process inspection found no owned probe or testhost remaining. No SQL allocation/cleanup or ordinary Login is claimed. T04 must implement the writable provider and refresh the bounded semantic package probe; T05/T06 must supply the compact/frozen UI and package evidence. T03 is not production certification.

## 9. Deferred production constraints

Business Compact acceptance is not production certification. Removing the unimplemented REL plan from the active queue does not satisfy clean-install, backup/restore/recovery, supported OS/SQL, signing, legacy-data or operational sign-off prerequisites.
Those prerequisites remain documented deferred constraints in the current roadmap and [deferred work](DEFERRED_WORK.md); this acceptance spec schedules no replacement release plan or G1–G5 work.
HTTP/P7, parked AppCore/net8, new inventory features, migration-policy changes and deployment tooling remain outside BCI scope unless separately authorized.

## 8.5. BCI-T04 editable native accessibility — 2026-10-20

**Boundary:** A24–A26 pass in the focused provider/native-editor and clean-package scope below. This is editable accessibility for the existing receipt layout. T05 compact layout and T06 complete frozen UI/package acceptance remain NOT RUN; no native 125% or production acceptance is implied.

### Source and implementation

Base `8d7f949ce54aed7f5d5d330437d5c3db058337ba`; product and test source `04590b963d6a19d31ec10583f4bed9f68c7f5faf`. Work remains on `codex/bci-t00-native-proof` in the retained managed worktree. Vendor pins are unchanged: BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No API/DTO/schema/permission/runtime/dependency/module change.

- [GoodsReceiptLinesGrid](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGrid.cs) owns a Table/Row/Cell provider. Product, Quantity and Unit cost expose writable Value/actions only under full live editability; row number, Total and Posted cells remain read-only. Existing result-list AccessibleActionGrid stays read-only.
- Cached cells retain document generation plus the actual line reference. Every value/action re-resolves current membership; Remove, New and reload reject obsolete targets, including reloads with the same persisted IDs. Writes marshal to the UI thread, then recheck identity and editability.
- [Adapter](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs) actions enter the same stock editor and T01 raw conversion/commit pipeline as keyboard input. A virtual Value write commits only the cell, never invokes the Save API. Invalid raw precision remains visible, typed VM values stay unchanged and virtual cell Help reports the error. Product Value denotes its committed key; pending native query text is separately identified in Help and cannot select a key by itself. Fresh option captions take precedence while historical captions remain available.
- The grid exposes the active **actual shared stock editor** as a child and updates its contextual name at Begin (`Quantity row 1 editor`, etc.). Cross-process UIA finds the virtual cell semantically, invokes it, finds its native EDIT descendant, focuses it and changes/commits through ValuePattern/native keys. ValuePattern and InvokePattern were observed on the cells. Reuse across Quantity rows 1/2, Unit cost and Product is covered.
- Dynamic Help belongs to virtual cells. The native wrapper description is a snapshot at Begin; no dynamically updated Help on the native inner EDIT is claimed. This app-owned cell bridge plus active editor reachability proved sufficient; no custom editor adapter or vendor seam was needed.
- [Package probe](../tests/MyDmsVn.Desktop.Tests/GuiPackageProbe.cs) now locates the receipt Table, named field cell and real active native editor. Ambiguous line fields fail instead of selecting an ordinal. Lookup selection still uses the owned popup/native keyboard and committed-key help. No coordinate click, hidden editor or VM injection establishes the new UIA evidence. Package receipt assertions require `INLINE_RECEIPT_NATIVE_EDIT=PASS` and retain ordinary Login, independent SQL and process cleanup.

### Executed scope and provenance

Windows 10.0.26200, SDK 8.0.425, net48 Release/x86; interactive serialized guarded STA, en-US. Native hosted proof measured 96 DPI/100%, 1920x1080 Light. Packaged Light and Dark runs independently reported monitor scale 100 and window/monitor DPI 96. These are not native 125% or attended layout acceptance.

Focused Desktop selection: `GoodsReceiptControlTests|ReceiptAcceptanceTests|GoodsReceiptInlineEditorTests|GoodsReceiptEditorViewModelTests|ReceiptNumericInputTests|InventoryAccessibilityAcceptanceTests|GoodsReceiptInlineAccessibilityTests`. **152/152 PASS**, zero failed/skipped/not-executed: respectively 35, 29, 15, 17, 36, 14 and 6 invocations. The six new Facts include the three exact A24–A26 anchors, UI-thread writes, native invalid/raw/read-only actions and refreshed caption versus pending query. No unchanged Session/SQL owner/full solution suite was rerun for this provider-only change.

Commands use the existing Desktop owner directly:

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~ReceiptAcceptanceTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~GoodsReceiptEditorViewModelTests|FullyQualifiedName~ReceiptNumericInputTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~GoodsReceiptInlineAccessibilityTests' --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/bci-t04-final-focused -- RunConfiguration.TargetPlatform=x86

./deployment/release/Publish-LocalRelease.ps1 -Version 0.1.0-bci-t04 -OutputRoot artifacts/release/bci-t04-candidate1
```

Clean candidate `0.1.0-bci-t04`, manifest `sourceTreeDirty=false`, source `04590b9`; separate Desktop and admin migrator net48/x86 packages. No publication or operational-release acceptance occurred. SHA256:

| Artifact | SHA256 |
|---|---|
| release-manifest.json | `E978CCCF2DF72517F9F1929C5A92C53C0521615D97B8ADFC9AEC12DE84FA07FD` |
| packaged Desktop.App.exe | `258A6232BF0A938AFB83B206E9F6EC607EB60739A46DA531A503A4B20E76F568` |
| packaged Desktop.WinForms.dll | `4D78E2FECA8FC3B1353AAABDEEF4C67A277CE0353AB4634E723B53D132F9FB51` |
| packaged admin DbMigrator.exe | `204466DFA9E84BD97CDE60ECE8B758A78701E01AF021B944C4B77BC1C0DBAFD5` |
| test-build Desktop.WinForms.dll | `6D670BB209CCC1E063D182E7366BA62B6D48A149611B86E74EB853CF59A6C0F4` |
| package-test build Desktop.Tests.dll | `B4018C8156561E81665F3799F0675A3B19BEF349ACB15196E9DCC68BDFC3FE7E` |

The package probe test build reports informational version `1.0.0+04590b963d6a19d31ec10583f4bed9f68c7f5faf`. Product bytes stay frozen in the manifest while test assembly provenance is recorded separately.

With `MYDMSVN_GUI_P5_PACKAGE` pointing to that local candidate and `MYDMSVN_GUI_P5_TEST_REVISION=04590b963d6a19d31ec10583f4bed9f68c7f5faf`, the same net48/x86 direct command selected:

| Filter | Actual rows and outcome | Local TRX |
|---|---|---|
| `FullyQualifiedName~Packaged_receipt_draft_reload_edit_post_and_stock_reconcile_with_readonly_sql\|FullyQualifiedName~Invalid_receipt_fields_preserve_input_and_create_no_inventory` | 4/4 PASS: both methods in Light and Dark; 3m40s total | `TestResults/bci-t04-package1/package.trx` |
| `FullyQualifiedName~Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh` | 2/2 PASS: Light and Dark; 3m53s total | `TestResults/bci-t04-package-lookup/lookup-package.trx` |

**Package total: 6/6 PASS**, zero failed/skipped/aborted/timed-out. Lookup probes additionally confirmed offscreen same-process rename delivery, historical inactive labels, rejection of inactive new choices, actual application restart/reopen and readonly SQL reconciliation. All six resource records ended `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`; no uncertain allocation remained.

Receipt probes exercised ordinary Login, native inline lookup/numeric edit, draft Save/reload/edit, second-client duplicate Post rejection, stock balance/card and readonly SQL reconciliation. Invalid input probes preserved user input and independently proved no receipt/line/ledger/balance writes. Each SQL run used the designated non-production master connection only in the owning fixture; runtime, migrator and readonly oracle remained separate.

The final focused replay used the same already-built source `04590b9` and the selector above with `--no-build --no-restore`, `--logger 'trx;LogFileName=verified.trx' --results-directory TestResults/bci-t04-verified`; **152/152 PASS** again. This pins the hosted result to the recorded test-build bytes, which have separate version metadata from the published package. No later product/test edits were made.

### Failure record, review and limits

Actual local TRX trials (PASS/total, all selected rows executed, zero skipped): `red/red.trx` 0/3; `green/green.trx` 2/3; `green2/green.trx` 3/3; `thread-red/thread.trx` 0/1; `focused/focused.trx` 31/33; `focused2/focused.trx` 97/97; `value/value.trx` 18/19; `value2/value.trx` 5/5; `caption-red/caption.trx` 0/1; `final-focused/focused.trx` 152/152; `verified/verified.trx` 152/152. Every directory in this list is prefixed `TestResults/bci-t04-`; package invocations are recorded separately above. These are incremental trials, not an unfiltered Desktop run.

Initial missing-provider RED was 0/3. First native trial passed the two in-process cases but failed compilation of the old managed LegacyIAccessiblePattern assumption; the actual net48 UIA client instead uses the supported Value/Invoke patterns. The UI-thread setter regression failed before marshalling; the caption-refresh regression failed before fresh options were preferred. An intermediate 31/33 trial exposed two stale inventory assertions expecting basic editor names; they now verify contextual names and native F2 entry. The native padding check initially compared decimal display scale as text; it now compares typed decimal equality, with meaningful excess precision still independently rejected. Those failed trials remain local and are not relabeled PASS.

One fresh independent source review found a test-driver loop-capture variable outside scope; it was fixed before the clean candidate commit. No mandatory findings remain. The reviewer did not execute GUI/SQL/tests and did not certify runtime evidence. Its metadata observation is reflected in the virtual Help/native description distinction above.

Raw TRX, SQL allocation/principal identifiers and probe diagnostics stay local. Hosted forms/editors and bounded UIA clients are disposed and drained. The package owner verifies each allocation token and principal identity, drains owned SQL sessions, then records database/principal deletion; uncertainty quarantines and blocks further GUI admission. This focused milestone does not replace T06 full Desktop/Session/package, native 100%/125% Light/Dark or attended visual/keyboard observations. Operational release prerequisites remain unaccepted and unscheduled.

## 8.6. BCI-T05 Business Compact layout — 2026-10-20

**Boundary:** A27–A28 PASS in the hosted control/native 100%/125% scope below. T00–T05 implementation tasks are complete in their recorded scopes. A29–A30 and T06 complete frozen UI/package acceptance remain NOT RUN; operational release prerequisites remain unaccepted and unscheduled.

### Source, ownership and decisions

Base `61c844983382fb29503675f1ff12955c41a96411`; final product/test source `83e5df2028bfbabdba29d79192bcc3c838993dfb`, branch `codex/bci-t00-native-proof`, retained isolated worktree. Source checkpoints are `4dd4bb5` (layout/appearance/navigation/numeric adapter), `c422603` (native monitor observer), `9bebb37` (physical accessible cell/row bounds and expanded interaction checks), and `83e5df2` (obsolete detail scroll extent repair). Documentation/evidence is committed separately after these frozen product/test bytes.

Vendor pins remain BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77` and SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No vendor edit, dependency upgrade, API/DTO/schema/permission/module/runtime or DPI-awareness change.

- [BusinessCompactAppearance](../src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs) owns Segoe UI 10/14/9pt resources per root and releases them with theme subscription on disposal. It applies the selected workspace/nav palette using public APIs and stops recursion into vendor composite internals. Result-list cell fonts/36-unit row heights are restored after AutoSizeCells.
- [BusinessCompactNavigation](../src/MyDmsVn.Desktop.WinForms/BusinessCompactNavigation.cs) composes native buttons for independent navy styling, 224/64 widths, full collapsed accessible names, nested navigation, disabled ancestors, native arrow/Home/End input and selection identity removal. FoundationShellForm retains existing catalog/inventory/session actions. No catalog-content redesign.
- [GoodsReceiptControl](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs) puts receipt identity and four metadata fields above the dominant grid, with Save/Post/Add/Remove, total, busy/error/status and uncertain-Post Refresh reachable below/beside it. Spacing is 8/16; metadata reflows when constrained. Initial master width is 260, minimum 240; minimum detail width 400. Below available bounds, explicit scrolling keeps controls reachable. Docked detail minimum size determines the scroll extent instead of stale prior viewport bounds.
- The pinned sealed numeric adapter has no native TextAlign API. [ReceiptNumberEditor](../src/MyDmsVn.Desktop.WinForms/ReceiptNumberEditor.cs) is an app-owned None-mode equivalent using the formatted input's protected Editor seam, native right alignment and measured text/preferred-height fit. It preserves raw conversion, first-character replacement, selection, rejection and cancellation; adapter owns/disposes the two numeric editors. Product continues through the pinned lookup adapter. No private-child reflection/traversal or vendor patch.
- [Grid adapter](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs) measures numeric widths and row height against text, editor preferred/minimum size and padding, refreshes existing views/editor bounds on font/size/theme changes, and preserves projection/session identity. It permits horizontal grid scrolling at narrow widths. T03 editability/command/uncertain-Post semantics are retained.
- Native 125% measurement exposed incorrect accessible bounds: 212x36 unscaled versus a physical 265x45 Quantity cell. [GoodsReceiptLinesGrid](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGrid.cs) now converts both logical-screen corners to physical screen points, uses that space for active-editor hit testing and unions all visible cells for Row.Bounds. MSAA [accLocation requires physical screen coordinates](https://learn.microsoft.com/en-us/windows/win32/api/oleacc/nf-oleacc-iaccessible-acclocation); the [per-monitor conversion API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-logicaltophysicalpointforpermonitordpi) supplies that conversion independently of caller awareness. The older API fallback is source compatibility only, NOT runtime-verified legacy-OS support.

The approved design, plan and explicit task request authorize these app-owned seams. The decision is to preserve vendor pins and existing product DPI awareness, then measure actual OS scaling independently; no alternative grid or vendor upgrade was selected.

### Executed tests and native measurements

Windows 10.0.26200, .NET SDK 8.0.425; net48 Release/x86, interactive serialized guarded STA, en-US, physical primary screen 1920x1080. The owner changed Windows Scale to 125%, then restored 100% after the run drained. Expected scale is asserted by the bounded owned external observer; setting an environment variable does not simulate DPI. The observer is PerMonitorV2, verifies window PID ownership and measures monitor DPI, physical window/client and semantic Quantity bounds. Product/test windows remain DPI-unaware at GetDpiForWindow=96, with OS virtualization at 125%; no product per-monitor-awareness claim. See Microsoft's [DPI virtualization guidance](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows).

Both final runs select the same **179 invocations**: ReceiptNumericInputTests 36; GoodsReceiptControlTests 35; ReceiptAcceptanceTests 29; GoodsReceiptEditorViewModelTests 17; FoundationShellFormTests 16; GoodsReceiptInlineEditorTests 15; InventoryAccessibilityAcceptanceTests 14; BusinessCompactLayoutTests 11; GoodsReceiptInlineAccessibilityTests 6. Both discovered/selected/executed all 179, **179 PASS / 0 FAIL / 0 skipped/not-executed**, no aborted/timed-out run. The 100% replay uses --no-build and the same already-built DLL bytes as final125. No unfiltered/full-solution or unchanged Session/SQL owner total is inferred.

The 11 layout invocations comprise four exact Compact_shell_retains_visible_fields_and_native_focus_order rows (two requested viewports x Light/Dark), two exact Cell_editors_and_popups_remain_usable_after_scroll_resize_and_theme_change rows, two error/uncertain-Post/Posted rows, nested keyboard/removal navigation, minimum splitter reachability, and a pure logical-to-physical viewport math Fact. The math Fact alone is not native DPI evidence.

Checks include native Warehouse → Employee → Note → grid tab flow, existing reverse-tab/inline navigation, measured editor text height, right alignment, raw 8.00001 and caret preservation through resize/maximize/restore/theme switch, rejected Enter and Escape, row-14 vertical scrolling/isolated 7.25 commit, no blank trailing row, pending Product query/actual owned popup, popup work-area bounds, physical cell hit test and row containment, error footer and uncertain-Post authoritative reload. Reduced 620x440 reachability is tested by scrolling; grid horizontal scrolling is intentional.

Measured bounds (same in Light/Dark; window units are the 96-DPI product coordinate space):

| Native scale / monitor DPI | Requested logical viewport | Actual viewport units | Physical client pixels | Quantity physical cell | Maximized viewport units |
|---|---|---|---|---|---|
| 100% / 96 | 1680x900 | 1680x900 | 1680x900 | 212x36 | 1920x1009 |
| 100% / 96 | 944x600 | 944x600 | 944x600 | 212x36 | — |
| 125% / 120 | 1680x900 | 1504x752 | 1880x940 | 265x45 | 1536x793 |
| 125% / 120 | 944x600 | 944x600 | 1180x750 | 265x45 | — |

The 125% screen cannot hold a full 1680x900 logical client (2100x1125 physical). The fixture fits that requested case to actual work area before asserting visible bounds: 1504x752 units / 1880x940 physical. **Full 1680x900 logical at 125% is NOT TESTED**; the 100% full viewport and both scales' measured fitted/maximized cases are distinct evidence. This is the plan's measured available-space scope, not a complete T06 display matrix or attended sign-off.

Actual commands (run 125 first, then 100 after confirmed setting change/drain):

```powershell
$filter = 'FullyQualifiedName~BusinessCompactLayoutTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~ReceiptAcceptanceTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~GoodsReceiptInlineAccessibilityTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~GoodsReceiptEditorViewModelTests|FullyQualifiedName~ReceiptNumericInputTests'
$env:MYDMSVN_BCI_EXPECTED_SCALE='125'
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $filter --logger 'trx;LogFileName=final125.trx' --results-directory TestResults/bci-t05-final125 -- RunConfiguration.TargetPlatform=x86
dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release --no-restore -p:PlatformTarget=x86
$env:MYDMSVN_BCI_EXPECTED_SCALE='100'
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --filter $filter --logger 'trx;LogFileName=final100.trx' --results-directory TestResults/bci-t05-verified100 -- RunConfiguration.TargetPlatform=x86
```

App build: 0 warnings / 0 errors. Build output is not a packaged/accepted candidate. The App build ran after final125 drained and before the no-build 100% replay; loaded test DLL hashes below are unchanged.

| Local artifact | SHA256 |
|---|---|
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.Tests.dll` | `D91A822BEBD00BA82ABF159ED7E2F2B13850D84F022D878E91DFC101C0F6468B` |
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.WinForms.dll` | `A5BA67A387CC1631B9C02AFB5727B2EE6FC97C1BA560669BD65E11D941E81BEB` |
| `src/MyDmsVn.Desktop.WinForms/bin/Release/net48/MyDmsVn.Desktop.WinForms.dll` | `A5BA67A387CC1631B9C02AFB5727B2EE6FC97C1BA560669BD65E11D941E81BEB` |
| `src/MyDmsVn.Desktop.App/bin/Release/net48/MyDmsVn.Desktop.App.exe` | `EFF86312A749A0D8D3336DC0253C107930ACDB76E64104A3948D9508EDE7D1A9` |
| `TestResults/bci-t05-final125/final125.trx` | `214C2F1137E6772933ECC558A8E4247D05BAD1F23B317AF276356309F2CB786E` |
| `TestResults/bci-t05-verified100/final100.trx` | `98238F6EB6E3CA857B9FCA33A935E37FA970E69E2FF3DA8D36C3915404531036` |

### Images, failures, review and remaining boundary

[Reviewed capture index and per-file hashes](evidence/bci-t05/README.md) retains actual Light/Dark hosted shell/layout, native popup, raw precision/error, uncertain-Post, Posted and maximized states at each native scale. Data is synthetic; the uncertain-Post support correlation is generated test data. These are owned HWND PrintWindow snapshots in the product's 96-DPI coordinate space, not physical desktop rasters or attended package screenshots. Pair their scale labels with the independent physical measurements above. Earlier occluded desktop captures were rejected and stay local. Retained native date/status/lookup accessory colors are visible in the captures; no full glyph/contrast or attended theme certification is inferred.

One fresh independent source review identified splitter-minimum reachability, native button arrow delivery and a logical/physical fixture assumption. Each received a failing regression before repair. Later native 125% observations exposed physical cell/row bounds and redundant detail scrollbars; regression failures preceded the final fixes. No mandatory review finding remains. The reviewer did not run GUI/SQL/tests or certify the runtime result; no second review was spawned.

Incremental TRX trials follow (PASS/total, zero skipped; all selected rows executed). These are development observations, not repeated final acceptance, and earlier source passes do not substitute for final83e5df2. Every path is under local TestResults/; raw TRX/logs and transient process identifiers are not committed.

| Local TRX suffix | PASS/total | SHA256 |
|---|---|---|
| `bci-t05-bounds-green/bounds-green.trx` | 11/11 | `BDCC2047BEE17FA94CA0B915760609FD02F46C7CF6C85F08CAC0AB0CDD11C66A` |
| `bci-t05-bounds-red/bounds.trx` | 0/4 | `C50ACD95E8406AED19B3DE6C776E36E9A738D3F58B7992AC02605D5E01E2C793` |
| `bci-t05-broad/focused.trx` | 173/175 | `E4B9795E360166ED7F6A4C33DA6C3C423C774CA5FAEAAD5539B81FE0C74207C4` |
| `bci-t05-contracts-red/contracts.trx` | 2/7 | `4413B3670A7D2C1E38E83ED08556CEBF158CD24B7F574B4EB2F1FF185541672B` |
| `bci-t05-final100/final100.trx` | 179/179 | `FBB8C6743AF03E3B279942EBF0847A32D938C6A04221755976D842624C9037BE` |
| `bci-t05-final125/final125.trx` | 179/179 | `214C2F1137E6772933ECC558A8E4247D05BAD1F23B317AF276356309F2CB786E` |
| `bci-t05-focused/focused.trx` | 47/47 | `3E8B6EB2D8439E70263581D135397625BD959350A997F06641934F388BFCF26F` |
| `bci-t05-frozen125/frozen125.trx` | 179/179 | `C7DCBC88E5ED27C77BA2DBE5D5559DA20863A3A8252F8C1EC7839E4C4797CC0B` |
| `bci-t05-geometry125/geometry.trx` | 9/11 | `3AA9F355B33E5049A23D604D66AA9D6B4E1C8B3D883D778FDC9D1A4DFBDE964D` |
| `bci-t05-green/green.trx` | 4/6 | `DD77CAAF8E40DE0DBF788EE7C811C188CE1C5F7035AA2CF9E5CCEF011AE183FF` |
| `bci-t05-initial-red/initial.trx` | 0/4 | `4879DAAF35B2FE9DC68220D26EE2D1D7735AD4875F00E1F4291730DF47253B17` |
| `bci-t05-layout125/layout125.trx` | 11/11 | `09DA27A6F1D132A00E21C106674231B2107CA819CA05C964E311B5C9D6B60D85` |
| `bci-t05-layout2/layout.trx` | 2/6 | `4067D59E158C1B4E5363AEF58057B3F5A49B72B3B8BBEFD9C0D45295DC21631A` |
| `bci-t05-measure/measure.trx` | 0/2 | `6DEF8205C86C9FDB3F0FD003682751EA235E52E66BDB7446DCF3D53E6BA0C3F3` |
| `bci-t05-measured125/measured125.trx` | 11/11 | `40935B5D154488EE5EE2A70918A312F68C818E3F3FC4B516CD25167BF6C18FC4` |
| `bci-t05-native-popup/popup.trx` | 2/2 | `DB9DDC0586D44544419C11C185FE8E827C823C14F69C83F70EF36A3547908978` |
| `bci-t05-red/red.trx` | 0/6 | `230D5FF40253C1238A684D77C6900C33A7A3B875CBBADCC5746DD84DB89BDFD1` |
| `bci-t05-review-green/review-green.trx` | 179/179 | `A427653252FD449FFB1E7218DA4737A3C9A00DD5B7AEEB75BB808A019416314A` |
| `bci-t05-review-red/review.trx` | 0/3 | `34F0C51EEEA8527FB5239EC2E5C63AF0B6696211A7E4AFECFCF264F856945E60` |
| `bci-t05-row-red/row.trx` | 5/11 | `2BC20083515A81EF451B95B61EBDC6415A88C869CF16A12F361F08D7C43A0DC9` |
| `bci-t05-scroll-details/scroll-details.trx` | 2/4 | `630B536CD227FB9783CBB54397C25F308846E1903A5FD037C3DA03D702B1339F` |
| `bci-t05-scroll-dock/scroll-dock.trx` | 11/11 | `E12A7B94F503AA4FF88BA499242F47FA067D57429F6641F65197C61C4D2D5F6E` |
| `bci-t05-scroll-green/scroll-green.trx` | 9/11 | `996EC1439A506B572F7FECE9B84C11C726A71158FDE17828681A2FD5FC3431D1` |
| `bci-t05-scroll-layout/scroll-layout.trx` | 11/11 | `689FC60A4E15A9B3AD02F84EED4DDFECC832EC7440F8EA3FAB7C8CA53485ADC5` |
| `bci-t05-scroll-red/scroll.trx` | 2/4 | `AE471695D392E4CBE4ADEEEA1385772CB3BCA4064B383A838BCC932135F79C40` |
| `bci-t05-spacing-red/spacing.trx` | 0/4 | `5B2F27C6CFACBE6F617EE88174FE12F7F57991EA04C91417A6A4AC8BEC997BD1` |
| `bci-t05-states/states.trx` | 19/23 | `1874E2875B5EA1836E607F878CFBD7CB4B5FBA9C44587CB86516165D30A2D45D` |
| `bci-t05-verified100/final100.trx` | 179/179 | `98238F6EB6E3CA857B9FCA33A935E37FA970E69E2FF3DA8D36C3915404531036` |
| `bci-t05-verified125/verified125.trx` | 175/179 | `70419F2B18E2A42F629CC8F7E3E657682B9DF8498CF04D1F3F4A6A7AC4D67C91` |
| `bci-t05-visual/visual.trx` | 6/6 | `0F404CB959E3D2C34AD36045923DC983C17DBBF2EEDEE0738877966F33D3C0D9` |

Initial REDs establish missing typography/native alignment, list-font/selection behavior, metadata spacing, initial split width and navigation/DPI contracts. An intermediate 173/175 run failed two old same-parent Tab assumptions after deliberate metadata regrouping; they now use native traversal. The 19/23 state run failed four desktop-capture occlusion guards, prompting owned PrintWindow capture. Initial native125 trials without the external observer do not establish monitor scale. The 175/179 and bounds-red trials exposed unscaled physical cell geometry; row-red additionally exposed Row.Bounds containing only Product and a wrong test assumption that a new line starts at zero (actual default is one). Scroll trials retained stale old extents until minimum-size/dock layout was corrected. Failed trials are not relabeled PASS.

Hosted controls, shared editors, bounded observer/UIA clients and testhost are disposed/drained; final owned-process inspection found no probe/testhost/application left. No T05 SQL allocation or package was produced, no ordinary Login executed. T04's clean package is historical source04590b9 and cannot certify T05's changed numeric editor/layout. T06 must freeze the new candidate, rerun its affected integrated Desktop/Session/package/SQL/attended matrix and record current product/test provenance. Production prerequisites and GUI-P5's historical Warehouse125Dark Down NOT TESTED exception remain unchanged.

## 8.7. BCI-T06 frozen integrated UI acceptance — 2026-10-20

**COMPLETE / ACCEPTED — 2026-10-21.** Required frozen runtime gates, attended observations and explicit overall Owner review are complete; the final signed scope below controls acceptance. This record concerns Business Compact and inline Goods Receipt on net48/x86; operational release prerequisites remain UNACCEPTED / UNSCHEDULED.

### Candidate1 checkpoint provenance and review

- Same isolated managed worktree/branch as T00–T05. Clean product/package source `09e92d3e7d3d59bd515457de4af034509b348d31`; implementation is unchanged from `83e5df2028bfbabdba29d79192bcc3c838993dfb`. Candidate `0.1.0-bci-t06`, local `artifacts/release/bci-t06-candidate1`, manifest dirty=false. Separate desktop/admin outputs, 205 independently rehashed files (133 desktop +72 admin), schema scripts001–009. This unsigned build candidate is not a production release.
- Frozen checkpoint Desktop test/driver source `f858ec9024c7f3a507da9b4fb52cd34e79d9ac91`. The intentional difference consists only of semantic input selection/diagnostics and new receipt denial/regrant coverage. Session source/build09e92d3 remains byte-identical; 108 unfiltered cases were executed once. At this checkpoint, product/server/vendor/API/schema/permission definitions and project/lock graph were unchanged by T06. The later app-owned focus-cue correction below requires a new product/package freeze.
- Vendor pins: BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`; Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`; SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- One independent source reviewer covered the complete BCI implementation and narrow T06 addenda; no actionable finding remained. An initial suspected ErrorProvider issue was withdrawn after confirming the existing lines error clears before aggregation. Source review does not certify native focus/DPI/SQL outcomes or attended acceptance. No deferred minor finding remains.
- Windows11 `10.0.26200`, SDK8.0.425, interactive en-US, Release/net48/x86, physical1920×1080. Actual native monitor scales must be measured at100%/125%; product/test HWNDs remain DPI-unaware96, with OS scaling. No simulated sizing substitutes for native scale.

| Artifact | SHA256 |
|---|---|
| Candidate manifest | `A76EBFC45E7C317BF08671B183E73C47C30427EE018F9D122134BCCEE1563221` |
| Packaged Desktop.App.exe | `2D2EC9058737D668CA70FCF8CF8EA452098CB191F4203A63AB5D8657F511924E` |
| Packaged Desktop.WinForms.dll | `6F075AB0F621E3ABACFF03576A52889E56ECA6EF11E68AF4F5C0493E25136A8B` |
| Packaged admin DbMigrator.exe | `304C0070BA5C8609496C812E1F08A7194F0E7A3F69DAD0B233851A2F87186D50` |
| Frozen checkpoint Desktop.Tests.dll | `0AB1B9AE19ED98DC430D60791F22E714BA3684281A01ED8A707D4FD7E218F54D` |
| Frozen checkpoint testbin Desktop.WinForms.dll | `4B20A4CB678E419E725830BF394F52A0096610E5B2BBF96C220B179560415C58` |
| Session.Tests.dll | `5EF7E65A1AF02DFAC32EDE81F6A34639A0B4C7B5549A246AEE337FD07F3F5CF2` |

Package and testbin WinForms are not byte-identical: build/source metadata differ. The shipped candidate is independently frozen and native package probes run that exact directory. Legacy variable `MYDMSVN_GUI_P5_TEST_REVISION` admits product/package09e92d3; it does not rename the actual f858ec9 test build.

### Driver corrections and A29

Initial full100 at09e92d3 executed413 rows:404PASS/9FAIL. Eight package rows encountered ambiguous Warehouse input because the compact labeled group exposed a same-named semantic Pane containing an unrelated EDIT. Driver selection now restricts input candidates to Edit/ComboBox/Spinner, retaining rejection of distinct real inputs. The same-name Pane regression was observed RED before the correction. No ordinal fallback or focus-guard relaxation was added.

The other failed row was the existing owned-window background-focus audit. An independent-distraction experiment did not fix it and was reverted; activation diagnostics add metadata only and preserve one SetForegroundWindow plus actual foreground/editor-focus checks. Its original cause remains unassigned. Subsequent focused1/1, process/receipt23/23 and full413/413 passed; these observations do not establish a root cause. Failed trials remain recorded below.

A29 required receipt-specific denial; the old Catalog-only denial did not satisfy it. New `Packaged_inline_receipt_flow_reconciles_with_readonly_sql` executes Light/Dark using actual Login by an ordinary active writer with six explicit grants. Direct Deny is applied after receipt reopen/edit; the native Save fails while preserving input and dirty Post lock. A separate readonly SQL identity verifies original Draft2.5@4, no ledger/balance, and unchanged opaque RowVersion before the owned Deny is removed. Only explicit Save then Post succeeds; exact generated Product/Warehouse/Employee keys are deliberately pairwise distinct and checked independently, with Quantity3.125, cost4, one ledger movement/balance and the ordinary posted actor. The setup identity seeds reference fixtures and grants; this is not runtime bootstrap or Catalog UI creation evidence.

The new native mode first failed0/2 as an expected RED, then passed2/2. Shared original receipt and Catalog denial cases passed4/4. This change required a new415-row test freeze; old413/18 checkpoint passes do not substitute for its gates.

### Candidate1 checkpoint gates, native observations and cleanup

Final admitted discovery at `f858ec9`: **415 distinct rows / 285 methods**, including **20 package rows**. Raw TRX/Theory values and SQL identities remain local. Unadmitted discovery without the explicit package environment yielded deferred Theory names (406); it was not a test execution and was replaced by admitted discovery before the gate.

| Required gate | Actual result | Local TRX SHA256 |
|---|---|---|
| Session unfiltered, source09e92d3 | 108/108 PASS, no FAIL/SKIP | `FE258FF526C9C10232DCCEAA72EDD47AD4C0671ACFC9C2626061509B5F522FDF` |
| Desktop unfiltered100, testf858ec9 | **415/415 PASS**, no FAIL/SKIP/abort/timeout; 19m58; exact names match discovery | `9D6663108D6AE3612D2614431BD969600858DAC745BCF35930A1F0427D55D46B` |
| Separate package100, same frozen test bytes | **20/20 PASS**, no FAIL/SKIP/abort/timeout; 18m12; exact package subset matches discovery | `73176556D9387B70A9BFB291543241A9A1F5935DF813BB6084589E89E3A25C2D` |
| Desktop unfiltered125, same frozen test bytes | NOT RUN | Pending |
| Attended100 Light/Dark +125 Light/Dark and explicit review | Candidate1 checkpoint: partial100; recovery keyboard review OPEN, retry/Posted and125 NOT RUN. Candidate2 follow-up is recorded separately below in the [worksheet](BUSINESS_COMPACT_WALKTHROUGH.md) | Pending |

Native100 was independently observed at monitor96DPI/100%, HWND96, clients944×600 and1680×900 with physical windows960×639 and1696×939; both themes and maximized1920×1009 window units were measured. These are hosted-layout measurements plus packaged E2E, not attended sign-off. The 125% work-area boundary must be measured in the new run.

All19 allocations from the final full100 gate and19 from the separate package100 gate were positively dropped after process/SQL drain. At that checkpoint, all107 local ledgers were in the verified Dropped state (including6 historicalT04, hence101 T06 allocations/trials); no quarantine or owned app/probe/testhost remains. The new attended-support preflight validated native100, all205 package hashes, bounded compiler30s and readonly observation child15s without SQL allocation or app launch. No attended observation is inferred from that preflight. A30 and the BCI milestone remain incomplete; do not turn this progress checkpoint into acceptance.
### Attended100 follow-up and Refresh investigation — 2026-10-21

Two owned attended sessions closed normally and positively cleaned up. The first was restarted after credential handoff and earns no Login/flow credit. In the second, the owner confirmed actual Login, Save/New/reopen preserving2.5@4, edit/Save3.125@4 NoteGUIedited, Product keyboard/popup cases, raw precision error retention/caret navigation and layout/scroll/row identity in Light/Dark. Readonly SQL verified the saved Draft and no stock. A controlled Post WAITFOR was observed; mutation/editor locks were confirmed. Reviewed client captures and their geometry/hashes are linked from the [worksheet](BUSINESS_COMPACT_WALKTHROUGH.md).

The owner reported that Tab appeared to skip enabled Refresh in Dark100, and then clarified that the observation was based on the visible focus border; Enter had not been tried. Source investigation found existing focus painting in the pinned BootstrapButton. An in-process status→Tab assertion passed2/2. An owned SendInput Tab audit against the frozen actual package also passed2/2: UIA focused element was Refresh/Button in both themes. These observations do not dismiss the attended report or establish a visual root cause. Native Enter plus focused/unfocused shell captures passed2/2 on candidate1 (`focus-enter2.trx`, SHA256 `ED79291AADA6B29825CDF91366FDC321A59440A2DD4F3E00589848AE0A8FEAC3`): Tab reached Refresh, Enter reloaded Draft, explicit retry produced exactly one SQL effect, app drained. The first capture experiment failed0/2 after Tab reached Refresh because it required a separate button HWND; Enter was not reached. Its owned supervisor verified process cleanup and both SQL allocations were positively dropped. That diagnostic-tool failure is retained and is not a product regression result. The f858ec9 test DLL is archived locally with the recorded hash; its broad gates are a checkpoint, not certification of this new driver delta.

For attended cleanup only, readonly SQL verified unchanged Draft/no stock, the exactly owned fault was removed, and the owner used mouse Refresh to restore Draft before closing normally. Explicit retry/Posted was not attended. After both sessions and the first two native Tab diagnostics,111 local ledgers were positively Dropped (6 historicalT04 +105 T06); no quarantine. The subsequent shell-capture/Enter diagnostic also positively dropped both allocations:115 local ledgers Dropped in total (6 historicalT04 +109 T06), with no owned app/probe remaining. At that checkpoint A30 and final acceptance remained pending, including native125; the later completed replays and final signed scope below supersede that open checkpoint.

### App-owned keyboard focus correction — candidate2 frozen, gates pending

The [reviewed diagnostic images](evidence/bci-t06/README.md#automated-refresh-focus-diagnostic--native100-2026-10-21) and pinned source identify a visual cause: Dark Primary and Focus tokens both equal `#6EA8FE`; the vendor paints its focus cue inside that fill. Grid selection borders persist after keyboard focus leaves. Keyboard traversal and Enter were functional; this is a focus visibility correction.

`ReceiptCommandButton.OnPaint` now adds a DPI-scaled dotted rectangle in a contrasting Light/Dark token only while Focused, ShowFocusCues and Enabled. It covers existing primary receipt commands without changing command admission, mouse handling, edit buffers, recovery or vendor pins. The packaged timeout driver retains guarded native Tab/Enter and parent assertions; temporary capture code is excluded from the retained driver. The existing hosted uncertain-Post case now compares actual focused/unfocused render pixels with a contrast threshold of3 in Light/Dark.

The initial render-fixture trials had keyboard cues hidden and both failed; they are not valid product RED/GREEN evidence. After explicitly enabling and asserting keyboard cues, the corrected fixture passed2/2 with the fix, then failed2/2 against the old product (zero contrasting pixels). A source restore preserved an older timestamp, so the first broader run used the old incremental-build DLL and failed2/26; the restored source was made newer and rebuilt. The corrected product then passed **26/26** BusinessCompactLayoutTests + GoodsReceiptInlineEditorTests, no skip. These fixture/build mistakes remain retained locally. Independent source review found no actionable issue after restoring the declared C# BOM/CRLF.

Candidate1 and f858ec9 broad gates remain immutable checkpoints. New source/test/package source is `e20b8c7f43dc73a9613d7f04d0d52158809663cf`, candidate `0.1.0-bci-t06.2` (`artifacts/release/bci-t06-candidate2`), manifest dirty=false. All205 files were independently rehashed and admitted discovery is415 distinct rows /285 methods including20 package rows. Its full100 and full125 gates each passed415/415 and separate package100 passed20/20; attended125 replayA+B2 completed normally; explicit final Owner review was **accepted on 2026-10-21**; no old gate is relabeled as evidence of this correction. Candidate2 attended100 follow-up and native125 replayA+B2 are recorded below; overall Owner review was accepted in the final signed scope below. Session108 is unaffected by this rendering-only product change.


Candidate2 affected clean-byte gate: **28/28 PASS**, no FAIL/SKIP,4m10, comprising26 hosted layout/editor rows and2 packaged real-SQL uncertain-Post Light/Dark rows. Each package row verifies actual native Tab→Refresh focus, native Enter, readonly Draft after explicit reload, explicit Post retry and exactly one final SQL effect, with normal app drain. TRX SHA256 `2347232DBD506193C2021A4F9F4C5BC725CD1C99293B5B781F70C738B1E6B85C`. All117 local ledgers are positively Dropped at this checkpoint (6 historicalT04 +111 T06); no owned app/probe remains. Attended candidate2 preflight independently admitted native100 and all205 package hashes, without SQL/app allocation. At that checkpoint full required gates and attended review remained pending; their completed outcomes and final sign-off are recorded below.

### Candidate2 required gates — complete and Owner accepted

| Required gate | Actual result | Local TRX SHA256 |
|---|---|---|
| Session unfiltered, unaffected source09e92d3 | 108/108 PASS retained; unchanged DLL hash verified | `FE258FF526C9C10232DCCEAA72EDD47AD4C0671ACFC9C2626061509B5F522FDF` |
| Desktop unfiltered100, source/teste20b8c7 | **415/415 PASS**, no FAIL/SKIP/abort/timeout;19m50;415 distinct names exactly match current discovery | `715F5DED3A38657F55A64560D4F541F8087D53F82BA362D075F87A416ED69EA3` |
| Separate package100, same frozen bytes | **20/20 PASS**, no FAIL/SKIP/abort/timeout;18m17; exact package subset matches current discovery | `27EE07FBD190779174DDD3A33218EDCFFC193148D15B56C9473E3C784E157147` |
| Desktop unfiltered125, same frozen bytes | **415/415 PASS**, no FAIL/SKIP/abort/timeout;19m56;415 distinct names exactly match current discovery | `E495F222F146D42965529DE297642938D4BBF8C33FE3D897A874A56EFD8F2725` |
| Attended100 +125, explicit final review | Candidate1 unchanged editor/layout plus candidate2 focus/recovery/Posted observed100; candidate2 replayA+B2 observed125 with normal exit/cleanup; explicit final review accepted 2026-10-21 | See worksheet/captures |

Candidate2 full100 and full125 actual TRX counters, each: total/executed/passed415; all failed/error/timeout/aborted/inconclusive/passedButRunAborted/notRunnable/notExecuted/disconnected/warning/inProgress/pending zero. A01–A29 anchors all resolve to passed actual rows in each context; A29 has14 rows per context across receipt flow, direct deny, invalid input, stale clients, lookup and controlled timeout. No skipped or missing mandatory row is accepted. Current native100 observations are monitor96DPI/100%, HWND96, clients944×600 and1680×900, physical windows960×639 and1696×939, maximized1920×1009 window units, both themes. The100% measurements do not certify125%.

All19 allocations from candidate2 full100 were positively dropped, taking the checkpoint from118 to137 Dropped ledgers, with no quarantine/app/probe remaining before starting the separate package100 owner. The separate run used exactly the frozen test/package hashes below, with no rebuild:20/20 passed and all19 allocations were positively dropped. Before requesting native125, all156 resource ledgers were Dropped (6 historicalT04 +150 T06), no quarantine, app or probe remained. Both100 owners had exited0. Native125 is not inferred from these gates.

Candidate2 frozen bytes (the earlier candidate1 hash table remains a separate checkpoint):

| Artifact | SHA256 |
|---|---|
| `artifacts/release/bci-t06-candidate2/release-manifest.json` | `D2F6284D64B62AC6370A2A5680BA5B0A8F334D2022B4CD3508C8C9DAB65243BC` |
| `artifacts/release/bci-t06-candidate2/desktop-net48-win-x86/MyDmsVn.Desktop.App.exe` | `B766385F52A44BA29090EE108794C43381542FD10A7FCD5B74FBD5D2C07D4BF2` |
| `artifacts/release/bci-t06-candidate2/desktop-net48-win-x86/MyDmsVn.Desktop.WinForms.dll` | `A621B888BF77072ED55659FF5DBF3980DD500AC99B5678B439381DC7C46B3D92` |
| `artifacts/release/bci-t06-candidate2/admin-migrator-net48-win-x86/MyDmsVn.Server.DbMigrator.exe` | `51A89F1C8B782B2C05C94063D99D9693B4866D6DF9779C424548DDC11DEB44AA` |
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.Tests.dll` | `F8DFC46D56FB9C7BE043C10B415DB00D31C854B2DD34DBC50766C6358CAD4EF2` |
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.WinForms.dll` | `0E9BE06280DD937581DE2F71BFEADCFD693328900846EE44854BD51AFB5B006B` |

### Candidate2 native125 gate and cleanup — 2026-10-21

Owner switched Windows Scale to125 after both100 owners drained. The bounded observer admitted actual125 and all205 unchanged package hashes without provisioning SQL or launching the app. The unfiltered direct Desktop command then executed415/415 passed on the same frozen bytes; all nonpass counters were zero. The collector reconciled all three required completed gates, exact discovery/subset names and all frozen hashes. No new Session build/run was needed because its source and recorded108-case DLL are unchanged.

Both themes measured monitor120DPI/125% and target HWND96. At requested944×600 logical, viewport944×600, native client1180×750 physical and window1200×799. Requested1680×900 cannot fit this1920×1080 work area at125%: actual fitted viewport1504×752, native client1880×940 physical, window1900×989; maximized1536×793 window units. These are actual fitted dimensions, not a claim that1680×900 logical was displayed in full. Frozen hosted editor/popup/focus/readonly/error tests passed in this context; attended125 remains separate.

All19 full125 allocations were positively dropped after process/SQL drain. At the boundary before attended125, all175 resource ledgers were Dropped (6 historicalT04 +169 T06), no quarantine/app/probe remained. At that checkpoint the automated milestone gates were complete and attended125/final review were pending. The later replayA+B2 below complete attended125; the final Owner review was later accepted in the signed scope below.

### Candidate2 attended100 recovery / Posted follow-up — 2026-10-21

Owner used actual Login, created/saved2.5@4, New/reopened the saved receipt, edited/saved3.125@4 NoteGUIedited. Readonly SQL confirmed Draft/no stock. The controlled runtime WAITFOR was independently observed. Light recovery locks/Tab instruction was completed; Owner explicitly confirmed Dark Status→Tab showed a visible Refresh focus border while Editor/Save/Post stayed locked. After independently verifying unchanged Draft/no stock and removing the owned fault, Owner explicitly confirmed Enter ran Refresh, cleared the unknown message and restored Draft/Post. A second readonly SQL check confirmed saved Draft/no stock before one explicit Post retry. Final SQL confirmed exact keys/values and one movement/balance3.125 with the expected test actor. Owner explicitly confirmed Posted, F2 Quantity readonly and disabled Save/Post/Add/Remove in **both** themes.

Four [reviewed captures](evidence/bci-t06/README.md#candidate2-attended-recovery-and-posted--native100-2026-10-21) record the Recovery/Posted states with measured native100, HWND96, client1184×721 and physical window1200×760. These background client captures do not prove active focus; the visible Dark cue is the Owner's foreground observation plus the separate rendered/native-keyboard regressions. The previous candidate1 editor/layout observations remain attributed to that candidate. This is focused attended follow-up, not a new full100/125 milestone sign-off.

Owner closed by X. The owned attended helper exited0 with `ATTENDED_APP_NORMAL_EXIT_AND_DRAIN=PASS` and `ATTENDED_SQL_POSITIVE_CLEANUP=PASS`. At this boundary all118 local resource ledgers were Dropped (6 historicalT04 +112 T06), no quarantine or owned app/probe remained. The subsequent full100 run on unchanged frozen candidate2 bytes passed415/415. At that checkpoint attended125 and final review remained outstanding; later replayA+B2 below complete attended125, with final Owner review later accepted in the signed scope below.

### Retained development/checkpoint trials

These are separate runs at their recorded revisions, not one aggregate acceptance run. Paths are relative to local `TestResults/`.

| TRX | PASS / total; FAIL | SHA256 |
|---|---|---|
| `bci-t06-activation-diagnostic/activation.trx` | 1/1; 0 FAIL | `86FAC73DC9E8CD1A88165AC0FAA0BF1D75FD790E0C975B910990AD8F9171CA3D` |
| `bci-t06-caption-red/caption-red.trx` | 0/1; 1 FAIL | `D2B33B3B9955BB57DA04BFC77A5140706D90F447FC82EE87318D05F219E2370B` |
| `bci-t06-desktop100/desktop100.trx` | 404/413; 9 FAIL | `D56756246AF4EFE4FD825FEA7CAD5730CF9A2D910A66AC37D85AA1B0663137A1` |
| `bci-t06-driver-green/driver-green.trx` | 1/2; 1 FAIL | `F6FF193347C8A1D75AADBCD63FF9B3DB712AE20CC49BA5F2D01072447CCF8874` |
| `bci-t06-driver-scope/driver-scope.trx` | 23/23; 0 FAIL | `7AE24EDC6CD8A87F0380BACF5740AF4ADF3EDC99AA4847766C95CA79B2C612E6` |
| `bci-t06-frozen-desktop100/desktop100.trx` | 413/413; 0 FAIL | `F25A3CF9E5717FCE6F542797F971E7B258EF266CB8119B7E247C40343ACB5E0E` |
| `bci-t06-frozen-package100/package100.trx` | 18/18; 0 FAIL | `E5570C9EFAD076CE2476A69DB74B0586CF441E4A8F0262ED430001B1F5118C0E` |
| `bci-t06-receipt-deny-green/receipt-deny-green.trx` | 2/2; 0 FAIL | `C43F043C0E047CB09AC1BFA0B173F021E08219FC622CB49DF9E3BD2ADC708736` |
| `bci-t06-receipt-deny-red/receipt-deny-red.trx` | 0/2; 2 FAIL | `A112B571E1E89E1BC904FF5295BD338F483495C24F68EC17FFEEFA49305467B3` |
| `bci-t06-receipt-shared/receipt-shared.trx` | 4/4; 0 FAIL | `519DE587B6789E81BB83FADC86302DDC32A50CCD97F786F7F7D21E5153CC61DE` |
| `bci-t06-selector-diagnostic/selector.trx` | 0/2; 2 FAIL | `BC67795137E6DF3BB0A821D396CE8C5213252A0040F5FB7C5988CA234F4C77B4` |
| `bci-t06-session/session.trx` | 108/108; 0 FAIL | `FE258FF526C9C10232DCCEAA72EDD47AD4C0671ACFC9C2626061509B5F522FDF` |

The413/18 checkpoint is source `b663e94`; the new receipt-denial RED/GREEN and shared scope preceded `f858ec9`. Session is09e92d3. Initial full/selector diagnostic are09e92d3 plus diagnostic metadata, before the correction. No failed trial is relabeled PASS.

### Retained Refresh diagnostic trials

These uncommitted-development runs precede the new freeze. The hidden-cue and stale-build limitations described above remain part of their interpretation.

| Local TRX | PASS / total; FAIL | SHA256 |
|---|---|---|
| `bci-t06-focus-enter/focus-enter.trx` | 0/2; 2 FAIL | `997685CFA38B1E03A5102DC2FCAA8DA220823B3B3AE9346B5C38383B697C53B7` |
| `bci-t06-focus-enter2/focus-enter2.trx` | 2/2; 0 FAIL | `ED79291AADA6B29825CDF91366FDC321A59440A2DD4F3E00589848AE0A8FEAC3` |
| `bci-t06-focus-contrast-red/focus-contrast-red.trx` | 0/2; 2 FAIL | `3B6440A26E603ADFA7528DD55F389515A0CF631D4792BC1D59DFC096C187D691` |
| `bci-t06-focus-contrast-green/focus-contrast-green.trx` | 0/2; 2 FAIL | `137827466D4940083D94B8C52E8154CC928E660A71DABECFA3830736E0ABD635` |
| `bci-t06-focus-contrast-cues/focus-contrast-cues.trx` | 2/2; 0 FAIL | `104E97F0F2F509DC4FDA6684D820D3ED7ED57E256E7A80C53D31A310D8A23A23` |
| `bci-t06-focus-contrast-red2/focus-contrast-red2.trx` | 0/2; 2 FAIL | `3701E5FE9573774E7E1FF85ABCDA990115162A1FBA9B9A86F0DE654900614EDF` |
| `bci-t06-focus-scope/focus-scope.trx` | 24/26; 2 FAIL | `C9E74A5A1A0D02EF8C32773595312C4CBE58590A5DF2A3E324339B86BC55CDC1` |
| `bci-t06-focus-scope2/focus-scope2.trx` | 26/26; 0 FAIL | `58C2BC67EA4D8BEE96CD8575422388DA70BC10B2A1BAA0A70C78B1410CC37A4B` |

### Attended125 attempt1 timeout and mandatory replay — 2026-10-21

The first candidate2 attended125 session used the same frozen bytes and actual native125. Owner completed Login/create/Save/reopen/edit, Product keys including explicit Dark Down, numeric8.00001/raw error/caret retention across themes, layout/scroll/temp-row identity, controlled Post timeout/locks/Refresh and final Posted readonly observations. Readonly SQL verified saved Draft/no stock, unchanged Draft/no stock around explicit reload, and one final movement/balance3.125 before the deadline. These are completed **OBSERVED** facts, not a passed full attended session.

The owned30-minute budget expired before normal exit was recorded: `GuiOwnedProcess` returned **TimedOut**, exit-1, cleanup verified=True. The owner later reported closing by X, but the normal-exit marker is absent and is not inferred. The finally block positively dropped the owned database/principals; all176 resource ledgers were Dropped and no app/probe remained. The failed trial remains retained locally, including its reviewed original captures and capture boundary. This timing failure is not credited as PASS.

A proposed45-minute support retry was rejected by the unchanged harness's hard30-minute attended maximum (`ArgumentOutOfRangeException`, timeout). No app launched and no actual Login is credited; the temporary handoff was closed and SQL cleanup passed, taking the boundary to177 Dropped ledgers. The unsupported support configuration is retained as a failed development trial, excluded from acceptance; no harness maximum or product/test/package byte was changed.

Review against TEST_POLICY's frozen-candidate/full-run rule requires rerunning the mandatory attended125 matrix, not replacing the failed full attempt with a short lifecycle-only pass. The replay is explicitly split **before execution** into two owned finite30-minute groups on unchanged candidate2: **A** Login/Save/reopen/edit plus both-theme Product/numeric/caret/layout/scroll/native UIA and Draft/no-stock SQL, then normal close/drain; **B** new Login/Draft setup plus both-theme uncertain-Post locks/focus/Enter/retry/Posted readonly, independent SQL one-effect check, then normal close/drain. Both groups and final Owner review must complete before A30/T06 can pass. Automated gates remain valid and are not repeated for an unchanged source or support-only split.

Local failed trial `bci-t06-attended125-candidate2.log` SHA256: `471079BC56D10DDB6E5707A2E176EFBAC7B39D25C26D96D5EAE27343A5CC58EA`.

Local failed trial `bci-t06-attended125-close-retry.log` SHA256: `226095113B20A111D0860243292588FA3E4E74BEC145BC73D27B2A6F5DD1DE8D`.

### Attended125 replay A — editor/layout complete, 2026-10-21

Fresh original30-minute helper, same frozene20b8c7 product/test/package and measured native125. Owner performed actual Login, GUI Save2.5@4/New/reopen/editSave3.125@4. In Light and Dark, the requested Product F2/F4/query/Down/Up/Enter/Escape-popup/Escape-cell/Tab/ShiftTab checks were completed, plus maximize/restore/narrow/HV scroll, temporary-row last-line identity and removal. Owner completed numeric right alignment, F2 Total readonly, invalid8.00001 Save blocking/raw/error/caret and Light→Dark retention. Both reviewed Invalid captures independently expose native Quantity Edit writable and computed Total readonly; original125 capture padding remains documented.

After restoring oneGUI-PROD3.125@4 NoteGUIedited and Save, readonly SQL verified exact saved Draft/no stock. Owner closed by X without an unsaved warning. The owned helper exited0 with normal-app-exit/drain and SQL-positive-cleanup markers. All178 ledgers were Dropped/no app or probe remained before replayB started. Local log SHA256 `235B86FD8B5FBC1DC7167DF6FD3F1AC98A214233D0FC4EA21106FB1C60105959`. At that checkpoint only the **editor/layout group** was complete; recovery/Posted and final review were still pending. B2 and the final signed scope below record their subsequent completion. No old timed-out trial is relabeled PASS.
### Attended125 replay B attempt1 — observer failure, 2026-10-21

Same frozen candidate2 bytes and native125. Actual Login/Save/reopen/edit and saved Draft/no-stock SQL passed. Owner explicitly confirmed uncertain-Post mutation locks and visible Status→Tab Refresh focus in Light and Dark; both Recovery captures were reviewed as their actual themes. After the owned fault was removed, explicit Enter reload restored Draft/Post and readonly SQL again verified Draft/no stock. One explicit retry reached Posted: Owner confirmed Dark Posted/F2 Quantity readonly/four mutation buttons locked, and independent SQL verified exactly one movement/balance3.125.

The following Dark Posted capture failed with `System.InvalidOperationException` before a new PNG was written. The support parent cancelled its owned app on the observer exception; normal app exit was not recorded. Positive owned SQL cleanup passed, all179 resource ledgers were Dropped and no owned app remained. This group is **FAILED / cleanup verified**, not PASS; no old Posted capture is credited as this replay. Light Posted and a completed normal lifecycle remain outstanding. Owner explicitly reported keeping the app open on Goods Receipts and only returning to chat. The previous type-only diagnostic cannot assign the root cause; stage-only diagnostics have been added to the ignored observer without changing product/test/package bytes, ownership checks or time budgets.

Local failed log `bci-t06-attended125-B-recovery-posted.log` SHA256: `44A833160EF7A41B37AE3FC4FE1B588CE29081994A4B120B57E2C7570B76728E`. Replay A remains complete within its separate editor/layout scope; at that checkpoint B and final Owner review remained pending. B2 and the final signed scope below record their subsequent completion.
### Attended125 observer diagnostic — completed, root cause unassigned

A separate short owned session on unchanged candidate2/native125 completed actual Login, saved oneGUI-PROD3.125@4 NoteGUIedited and posted it once. Readonly SQL verified saved Draft/no stock and then exactly one movement/balance3.125. Dark Draft and Dark Posted captures succeeded; after Owner explicitly confirmed F2 Quantity readonly and four mutation buttons locked, a second Dark Posted capture also succeeded. All three images were reviewed locally. This did not reproduce the previous observer exception and does not establish its cause or replace mandatory replayB. Stage-only observer diagnostics preserve the same admission/ownership checks and budgets.

Owner closed normally by X; helper exit0, normal app exit/drain and positive SQL cleanup passed. All180 actual resource ledgers were Dropped; no owned app remained. Diagnostic log SHA256: `968CEBE0C7CC3316D10711C27CF621BFB5C61B8D1CFF58CBDE5B26E4643B647D`.

Support helper v2 isolates only known read-only observer failure stages after child exit1 and verified drain; the failed capture remains explicitly PENDING. Identity/visibility/DPI admission failures, timeout, uncertain cleanup, unexpected exceptions, SQL failures and app failures remain fatal. Each capture attempt uses its own directory and publishes its successful PNG/JSON pair only after all checks. The30-minute app and15-second observer limits remain unchanged. Twelve synthetic classification cases passed (success, four permitted read stages, wrong DPI, exit2, wrong owner, minimized window, timeout, undrained child, unexpected exception), with no SQL allocation/app launch; these are support checks, not additional product Facts or acceptance evidence. Frozen product/test/package bytes are unchanged.
### Attended125 replay B2 and final reconciliation — 2026-10-21

Fresh helper v2 session began06:46:22+07 and exited0 before06:57:09+07, within the unchanged30-minute limit. Actual Login/Save/New/reopen/edit produced oneGUI-PROD3.125@4 NoteGUIedited. Owner explicitly confirmed uncertain-Post locks and visible Status→Tab Refresh focus in Light and Dark; actual runtime WAITFOR and both reviewed Recovery captures agree with the controlled failure. Readonly SQL verified unchanged Draft/no stock before owned-fault removal and after explicit Status→Tab→Enter reload. Owner confirmed the error cleared/Draft/Post restored, then performed one explicit retry. Independent SQL verified exactly one ledger movement/balance3.125 with expected actor/keys. Owner confirmed Dark Posted/F2 readonly/four mutation locks and completed the same requested Light checks. Both Posted captures expose Product/Quantity/Unit cost/Total ValueReadonlyTrue.

All four [B2 captures](evidence/bci-t06/README.md#candidate2-attended125-replay-b2--completed-recoveryposted-scope) were model-reviewed with their actual themes and distinct successful attempt paths; no stale artifact or failed attempt is used. Native monitor120/125%, HWND96; reported canvas1480×901 and physical window1500×950 remain separate measurements under the documented PrintWindow boundary. B2 has no observer/capture failure marker. Owner closed by X; normal app exit/drain and positive SQL cleanup passed. At the final boundary all181 actual resource ledgers were Dropped (6 historicalT04 +175 T06); no owned app/probe or quarantine remained.

B2 log SHA256: `9F9C22982371C07B4FAD0DC0982C5300D4B4014A9E6C47282A9EE99C847CEF25`. Support-only v2 script SHA256: `4DE4FAE0C30106C213BD598F62E9D39521DBD30691FB4A804D493432029F533A`; stage-diagnostic observer source SHA256: `8B5A2482A4C14BB359286F93C05C4798C858888FD9FF488C855B3B3850D41D18`; actual B2 observer EXE SHA256: `D2005D7ACE97DAEC74972F1F714F52E1670D4A5D878916001375448EB9223D53`;12-case support-check log SHA256: `C7108399D314DC59E9AD7B354EE3867969EA8C5CB72BA3D9EF2C719BD8FC8790`. These local ignored drivers are intentionally distinct from retained product/test source; they only observe the owned app/coordinate existing fixtures. Frozen binaries and mandatory gate results are unchanged.

Final revalidation read actual TRX, exact discovered names and frozen binary hashes; no new product test run is claimed for documentation/support-only work. Session108/108 retains its verified unchanged DLL/TRX; Desktop415/415 at100 and125 plus separate package20/20 remain complete. Each stable A01–A29 anchor below matched these actual passed rows in both contexts. The table is an anchor count, not an additional test total; complete415-row gates include the affected regressions outside these minimal anchors. A30 is documentary reconciliation and direct Owner review, not a zero-row test PASS.

| Acceptance ID | Passed mapped rows, Desktop100 | Passed mapped rows, Desktop125 |
|---|---|---|
| BCI-A01 | 1 | 1 |
| BCI-A02 | 1 | 1 |
| BCI-A03 | 1 | 1 |
| BCI-A04 | 4 | 4 |
| BCI-A05 | 4 | 4 |
| BCI-A06 | 2 | 2 |
| BCI-A07 | 1 | 1 |
| BCI-A08 | 1 | 1 |
| BCI-A09 | 1 | 1 |
| BCI-A10 | 1 | 1 |
| BCI-A11 | 1 | 1 |
| BCI-A12 | 1 | 1 |
| BCI-A13 | 1 | 1 |
| BCI-A14 | 1 | 1 |
| BCI-A15 | 1 | 1 |
| BCI-A16 | 1 | 1 |
| BCI-A17 | 1 | 1 |
| BCI-A18 | 1 | 1 |
| BCI-A19 | 1 | 1 |
| BCI-A20 | 1 | 1 |
| BCI-A21 | 1 | 1 |
| BCI-A22 | 4 | 4 |
| BCI-A23 | 4 | 4 |
| BCI-A24 | 1 | 1 |
| BCI-A25 | 1 | 1 |
| BCI-A26 | 1 | 1 |
| BCI-A27 | 4 | 4 |
| BCI-A28 | 2 | 2 |
| BCI-A29 | 14 | 14 |
| BCI-A30 | Runtime/artifacts reconciled | PASS — explicit overall Owner review accepted 2026-10-21 |

Earlier timed-out/failed trials remain failed with verified cleanup; the diagnostic did not identify the old observer exception's cause. Separate complete replayA+B2 now satisfy the mandatory125 observations without waiving those trials. No unresolved product finding is known from source review or the completed gates. Final BCI UI sign-off was explicitly accepted by the Owner on 2026-10-21; production release prerequisites remain UNACCEPTED / UNSCHEDULED.

### Final T06 acceptance and handoff — 2026-10-21

The Owner explicitly confirmed overall acceptance in this chat after the completed worksheet and frozen evidence were presented, and requested closing A30 and committing the handoff. **BCI-A01–A30 and BCI-T00–T06 are COMPLETE / ACCEPTED in the recorded Business Compact UI scope.** This final decision supersedes earlier pending-review checkpoint statements; it does not relabel failed development/attended attempts as passed.

Accepted candidate: `0.1.0-bci-t06.2`, product/test source `e20b8c7f43dc73a9613d7f04d0d52158809663cf`, unchanged pinned vendors and all205 packaged hashes. Session108/108, Desktop415/415 at each native100/125 and separate package10020/20 are reconciled with actual discovery/TRX. The attended100 observations preserve their documented candidate1 unchanged-editor/layout versus candidate2 focus/recovery/Posted provenance; fresh candidate2 replayA+B2 completes native125 Light/Dark. Source review, strict numeric/lookup boundaries, native accessibility, layout/scroll identity, explicit recovery/retry, exactly one Post effect and positive cleanup are accepted within that evidence. The observed125 work-area fit and PrintWindow padding limits remain explicit.

All181 actual owned resource ledgers were positively dropped after process/SQL drain. The earlier observer exception remains an unassigned diagnostic failure; it was not reproduced in the short diagnostic and no failure occurred in the complete B2 replay. No mandatory test/attended obligation is missing or waived. Release signing, clean install/upgrade/restore and other operational release prerequisites remain **UNACCEPTED / UNSCHEDULED**; this is not production release acceptance.

Implementation/evidence are handed off through [PR #40](https://github.com/chung6a8m/MyDmsVn/pull/40) targeting `v1a-pro`. Keep the isolated branch/worktree for PR review. The next action is review/integration of that PR; no new roadmap or deferred release work is activated by this handoff.
