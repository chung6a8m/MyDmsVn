# Business Compact inline Goods Receipt — acceptance specification

**Status:** BCI-T00 native proof complete in its recorded host scope; BCI-T01–T06 remain PLANNED / NOT RUN. No integrated Business Compact product behavior is accepted.
**Prepared:** 2026-10-20. **Planning baseline:** `v1a-pro` at `f37038b250821a538b9cdc10a6d6d8b4bc1f386f`.

## 1. Scope and authority

This specification verifies the [Business Compact design](BUSINESS_COMPACT_UI.md) and [BCI implementation plan](plans/20261020-001-business-compact-inline-goods-receipt.md). It is an acceptance ledger, not an executable test manifest or a replacement for [TEST_POLICY](TEST_POLICY.md).
The product remains Windows Forms `net48`, with the shipped x86 Desktop and current Local application contracts. SourceGrid owns edit lifecycle; application code owns receipt state, validation presentation and command admission.
All BCI-A identifiers below are stable obligations. A01–A03 now have T00 proof-host evidence below; other outcomes remain NOT RUN, including rows that cite an existing regression. Existing means source already exists; it does not mean the new inline implementation has passed it.
Names outside the retained T00 proof identify proposed work in existing test projects; those proposed APIs/tests are not already implemented. Retain the obligation ID if a reviewed implementation changes a proposed test name.
The planned application seams are `ReceiptNumericInput.TryParse`, `GoodsReceiptLinesGrid`, `GoodsReceiptLinesGridAdapter` and `TryFinishEdit(ReceiptEditIntent intent)`; their exact contracts are owned by the design and plan.

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
| E-G | [GoodsReceiptControl](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs), [line VM](../src/MyDmsVn.Desktop.Application/GoodsReceiptLineViewModel.cs), [editor VM](../src/MyDmsVn.Desktop.Application/GoodsReceiptEditorViewModel.cs) | Current full cell rebuild and external-editor synchronization must become safe for an active grid edit. |

E-R's `Rowversion_conflict_is_inline_and_keeps_dirty_header_line_and_opaque_version` refers to an inline error message, not editing inside a grid cell.
Keep read-only semantics for result lists and computed cells. E-A's existing line-cell read-only assertion must be deliberately revised for editable Draft fields; deleting it without replacement loses an accepted accessibility boundary.
Registry-created vendor editors do not automatically instantiate E-U's application subclasses. Prove the actual hosted editor's native accessible tree and value/selection behavior before declaring compatibility.

## 4. Acceptance matrix

`New:` names below are proposed methods except the retained T00 proof in `GoodsReceiptInlineEditorTests` within the existing Desktop test project; numeric pure cases use the proposed `ReceiptNumericInputTests`; accessibility cases use `GoodsReceiptInlineAccessibilityTests`, and layout cases use `BusinessCompactLayoutTests` in that same project. E-* references are existing anchors, not new-result credit.
Each row belongs to its named plan task. BCI-T06 reconciles every row and the affected existing regressions; it cannot waive an earlier failure by rerunning a narrower successful selection.

### 4.1. Editor proof and strict numeric input

| ID | Owner | Test anchors | Required observable result | Outcome |
|---|---|---|---|---|
| BCI-A01 | BCI-T00 | Existing: E-G/E-U; New: `Native_cell_entry_uses_registered_bootstrap_editor` | Actual mouse/F2/type entry focuses an editor hosted in the selected line cell; Quantity/Unit cost/Product can be changed without using external line editors. | PASS — T00 host only (§8.1) |
| BCI-A02 | BCI-T00 | Existing: vendor adapter sources; New: `Logical_values_remain_typed_when_display_changes` | Lookup commits the logical key and formatted input commits typed decimal; display labels/grouping never become DTO values. Cell display remains correct after edit closes. | PASS — T00 host only (§8.1) |
| BCI-A03 | BCI-T00 | Existing: vendor registry ownership tests; New: `Shared_editors_dispose_with_their_grid` | One editor per grid/configuration is reused sequentially; no per-cell control allocation or cross-grid sharing; never-started and active-popup controls dispose with the workspace. | PASS — T00 host only (§8.1) |
| BCI-A04 | BCI-T01 | Existing: E-C decimal boundary test; New: `Extra_fractional_zeros_are_exact_but_meaningful_overprecision_is_rejected` | Preserve raw text until validation: `1.23000` is exactly 1.23; `1.23001` and a far-tail nonzero beyond four places fail before decimal parsing can round them away. No meaningful digit is silently truncated. | NOT RUN |
| BCI-A05 | BCI-T01 | Existing: E-A numeric validation; New: `Culture_and_paste_preserve_the_same_decimal_value` | Exercise vi-VN/en-US decimal separators, typing and paste with the captured culture; accept ungrouped input and reject grouping, wrong/mixed separators, exponent/currency syntax and ambiguity with an actionable error while retaining raw text. Display formatting cannot turn invalid pasted input into a valid different value. | NOT RUN |
| BCI-A06 | BCI-T01 | Existing: E-C/E-A; New: `Field_bounds_and_zero_rules_match_receipt_contracts` | Quantity must be greater than zero and within its contract maximum; Unit cost may be zero and must not exceed its maximum. Negative, empty, malformed and over-range input is rejected without clamping or VM mutation. | NOT RUN |
| BCI-A07 | BCI-T03 | Existing: E-V overflow methods; New: `Overflow_preserves_input_and_blocks_commands` | Both line multiplication and aggregate overflow remain safe; error state retains entered values, does not display a fabricated total and dispatches no Save/Post. | NOT RUN |

### 4.2. Stable grid state, lookup and command boundaries

| ID | Owner | Test anchors | Required observable result | Outcome |
|---|---|---|---|---|
| BCI-A08 | BCI-T02 | Existing: E-G/E-C; New: `State_refresh_does_not_replace_the_active_cell` | Unrelated VM/status/total notifications preserve active cell/editor, raw text, selection and caret; targeted updates do not rebuild the editing row or duplicate a commit. | NOT RUN |
| BCI-A09 | BCI-T02 | Existing: E-C dirty guards; New: `Pending_cell_edits_participate_in_dirty_command_guards` | Pending text cannot be ignored by selection/New/Refresh/Post guards. Cancel restores the original typed value; successful commit marks the correct receipt dirty once. | NOT RUN |
| BCI-A10 | BCI-T02 | Existing: E-C saved-line reselection; New: `Unsaved_zero_id_lines_are_distinguished_by_reference` | Multiple unsaved lines with `Id == 0` remain distinct across selection, add/remove and recalculation; late events cannot update another row with the same zero ID. | NOT RUN |
| BCI-A11 | BCI-T02 | Existing: E-C/E-G; New: `Reloaded_line_references_replace_old_edit_targets_safely` | Save/reload replacing VM objects preserves intended selection by valid persisted identity; stale references are invalidated, and subsequent editing updates the current line exactly once. | NOT RUN |
| BCI-A12 | BCI-T03 | Existing: E-L/E-C; New: `Current_query_survives_same_context_refresh` | Current pending query is retained during same-context option/state refresh; committed key and pending text remain distinct, with no silent selection or stale query overwrite. | NOT RUN |
| BCI-A13 | BCI-T03 | Existing: E-C explicit New test; New: `Explicit_new_cancels_pending_lookup_after_navigation_guard` | After existing dirty/discard policy admits New, pending text/selection intent and outstanding query are canceled; a late result cannot populate or select a product in the new receipt. | NOT RUN |
| BCI-A14 | BCI-T03 | Existing: E-L/E-R session fencing; New: `Late_lookup_results_cannot_cross_row_receipt_or_session` | Rapid queries, row switches, removal, receipt change and sign-out invalidate obsolete results, including canceled operations whose provider completes anyway. Current input and next-user state remain intact. | NOT RUN |
| BCI-A15 | BCI-T03 | Existing: E-C historical references/E-R reopen; New: `Historical_product_display_does_not_enable_inactive_new_selection` | Reopened historical selection stays readable; active-only new choices and rename/deactivate/reactivate refresh retain existing business rules without losing the selected line. | NOT RUN |
| BCI-A16 | BCI-T03 | Existing: E-A F4 test; New: `Lookup_popup_keys_mouse_and_focus_commit_once` | Native F4/open, filtering, arrows/Page keys, Enter, mouse choice, Escape, Tab/Shift+Tab and outside focus behave as designed; popup Escape differs from cell cancel, and one selection produces one logical commit. | NOT RUN |
| BCI-A17 | BCI-T03 | Existing: E-C/E-R; New: `Save_flushes_active_edit_or_dispatches_nothing` | `TryFinishEdit(Save)` returns false for invalid raw/lookup input and keeps its error/focus; success uses the newly committed typed values. Save never serializes a pre-edit value accidentally. | NOT RUN |
| BCI-A18 | BCI-T03 | Existing: E-R Post/dirty cases; New: `Post_never_auto_saves_or_uses_pre_edit_values` | Post finishes or rejects the active edit, then respects dirty/persisted identity/opaque version rules. Changed input needs explicit Save; repeated clicks cannot issue duplicate commands. | NOT RUN |
| BCI-A19 | BCI-T03 | Existing: E-C navigation guards; New: `All_edit_intents_observe_their_commit_or_cancel_policy` | Exercise SelectReceipt/NewReceipt/Refresh/AddLine/RemoveLine intents as well as Save/Post; failed completion leaves row and input intact, and explicit cancellation never writes pending values. | NOT RUN |
| BCI-A20 | BCI-T03 | Existing: E-R conflict test/E-P stale clients; New: `Conflict_retains_inline_input_and_original_expected_version` | A stale Save/Post does not silently reload, retry or overwrite the winner; inline input, dirty state and the appropriate opaque version/error remain observable. | NOT RUN |
| BCI-A21 | BCI-T03 | Existing: E-R authorization cases; New: `Denied_or_readonly_state_blocks_active_and_future_edits` | Posted, busy, access-denied and read-revoked states prevent mutation through keys, mouse, active control, command and accessibility; denied read clears sensitive stale detail. | NOT RUN |
| BCI-A22 | BCI-T03 | Existing: E-R uncertain Post matrix/E-P timeout; New: `Uncertain_post_retains_lock_until_authoritative_read` | Cover server-committed and not-committed outcomes, exception/API failure, denied reload and recovery. No edit/Save/Post resumes until a successful read resolves the state; only a justified retry may post once. | NOT RUN |
| BCI-A23 | BCI-T03 | Existing: E-R disposal/session switch; New: `Disposed_or_old_session_completions_cannot_overwrite_current_input` | Pending Save/Post/lookup cancellation and grid disposal prevent late UI values, notifications, queries or selection from contaminating a replacement workspace/session. | NOT RUN |

### 4.3. Accessibility, visual shell and integrated acceptance

| ID | Owner | Test anchors | Required observable result | Outcome |
|---|---|---|---|---|
| BCI-A24 | BCI-T04 | Existing: E-A/E-U; New: `Accessible_line_cells_expose_correct_editability_and_identity` | Draft input cells expose meaningful row/column/name/value and editability; computed/status/list/Posted cells remain read-only. Cached actions re-resolve valid identity and reject removed or replaced targets. | NOT RUN |
| BCI-A25 | BCI-T04 | Existing: E-A/E-U/E-P; New: `Native_uia_reaches_the_active_shared_editor` | Cross-process native UIA can find, focus, edit and commit the actual hosted control by semantic identity; reuse across rows updates accessible state. No coordinates, ordinal-only selectors or direct VM write substitutes. | NOT RUN |
| BCI-A26 | BCI-T04 | Existing: E-A numeric/lookup guards; New: `Accessible_edits_preserve_validation_and_pending_selection_state` | Native Value/selection actions use the same strict numeric and lookup pipeline as keys/mouse; disabled/stale setters cannot mutate, and pending text is distinguishable from a committed key. | NOT RUN |
| BCI-A27 | BCI-T05 | Existing: E-R layout/E-A tab tests; New: `Compact_shell_retains_visible_fields_and_native_focus_order` | At supported workspace widths, windowed/maximized and Light/Dark, controls/header/footer/errors remain visible; native Tab/Shift+Tab follows the design and read-only computed cells do not trap entry flow. | NOT RUN |
| BCI-A28 | BCI-T05 | Existing: E-A render/vendor sizing tests; New: `Cell_editors_and_popups_remain_usable_after_scroll_resize_and_theme_change` | Actual 100%/125% Windows scaling preserves editor text/caret/error visibility, row alignment and popup work-area bounds while scrolling/resizing/theme switching; no focus loss silently commits an unintended value. | NOT RUN |
| BCI-A29 | BCI-T06 | Existing: E-P receipt/invalid/lookup/stale/timeout cases; New: `Packaged_inline_receipt_flow_reconciles_with_readonly_sql` | Actual Login and native inline create/edit/reload/Save/Post flow persists exact decimals and keys; independent SQL proves one movement per line, balance reconciliation and no denied/failed mutation. | NOT RUN |
| BCI-A30 | BCI-T06 | Existing: E-P/GUI-P5 cleanup evidence; New: candidate reconciliation record | New frozen source/test/package, all required discovery names/Theory rows, attended observations, exception capture and owned resource cleanup reconcile. No missing/skipped/aborted/timed-out mandatory case is credited as PASS. | NOT RUN |

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

## 9. Deferred production constraints

Business Compact acceptance is not production certification. Removing the unimplemented REL plan from the active queue does not satisfy clean-install, backup/restore/recovery, supported OS/SQL, signing, legacy-data or operational sign-off prerequisites.
Those prerequisites remain documented deferred constraints in the current roadmap and [deferred work](DEFERRED_WORK.md); this acceptance spec schedules no replacement release plan or G1–G5 work.
HTTP/P7, parked AppCore/net8, new inventory features, migration-policy changes and deployment tooling remain outside BCI scope unless separately authorized.
