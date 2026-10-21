# Business Compact refinement: lookup correctness and visual parity

**Initiative:** BCF — sole active roadmap selected on 2026-10-21.
**Status:** T00 test-only RED checkpoint COMPLETE; [15 native-key product failures retained](evidence/bcf/20261021-t00-native-typing.md). T01 binding repair COMPLETE with [focused hosted Windows evidence](evidence/bcf/20261021-t01-lookup-binding.md); T02 OPEN. Visual work remains NOT STARTED.
**Baseline:** merged PR #40, `v1a-pro` at `6040d7bcfca698fc9daba8268bb94e4454e8eacb`.
**Plan:** [BCF execution plan](plans/20261021-001-business-compact-lookup-and-visual-parity.md).
**Acceptance:** [BCF acceptance ledger](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md).

## 1. Purpose and authority

First correct three receipt lookups and prove their native keyboard behavior.
Only after that gate, complete the shared shell, Products catalog and receipt composition
against the three retained owner references. These are sequential gates in one roadmap.
This document authorizes no runtime PASS and does not describe proposed behavior as shipped.

BCI-T00–T06 remain COMPLETE / ACCEPTED in their recorded historical scope.
Their accepted decision to exclude catalog redesign and retain a receipt split list is historical.
BCF deliberately expands that scope; it does not reopen or relabel BCI evidence.
The [BCI design](BUSINESS_COMPACT_UI.md), [acceptance](BUSINESS_COMPACT_ACCEPTANCE.md)
and failed/accepted trial provenance remain intact. Operational release acceptance remains separate.

## 2. References and scope

The [reference register](design-references/business-compact/README.md) retains unchanged originals.

| Reference | Intended composition |
|---|---|
| [01 — catalog Light](design-references/business-compact/01-catalog-light.png) | Shared shell, Products toolbar, dense results and right inspector |
| [02 — Goods Receipt](design-references/business-compact/02-goods-receipt.png) | Detail-focused document, metadata, dominant line grid and command hierarchy |
| [03 — catalog Graphite](design-references/business-compact/03-catalog-graphite.png) | Consistent Dark surfaces, chrome, selection, focus and inspector |

The images define visual structure and hierarchy, not new business data or localization.
Keep established English UI copy; existing localized inventory labels need no unrelated rewrite.
Native geometry need not be pixel-identical to the raster references. No similarity percentage
or uncalibrated screenshot score can establish acceptance.

In scope: the three named lookup repair targets, their binding/lifecycle tests, shared shell,
Products content, receipt list/detail presentation, theme consistency and native accessibility.
Other catalogs inherit shell/shared changes while retaining their fields, commands and semantics.
No unrelated module redesign, supplier/tax/discount/unit feature or new document workflow.

## 3. Fixed architecture and behavior boundaries

- Keep product `net48`, Windows x86 packages and existing DPI awareness.
- Keep BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`.
- Keep Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`.
- Keep SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- Preserve current application/server interfaces, DTOs, schema, permissions and SQL ownership.
- Preserve typed decimals, strict raw-input validation, historical selections and accessibility.
- Preserve optimistic rowversion, explicit Save/Post, uncertain-Post recovery and one-effect posting.
- Preserve session expiry, access-denied clearing, disposal and stale async-result rejection.

Use supported vendor public APIs and narrow application-owned integration seams first.
Do not recursively style private vendor children, use reflection to patch internals, upgrade the
whole vendor graph or silently replace the grid. A proven missing seam requires a separately
recorded narrow decision, compatible upstream change and evidence before changing these pins.
No vendor change is included in this planning commit.
Proposed helper/interface names in implementation notes must be labeled proposed until committed.

## 4. Phase 1 — repair the three lookup targets (BCF-T00–T02)

The repair targets are exactly:

| Host | Existing control | Committed meaning |
|---|---|---|
| Receipt list | `WarehouseFilterLookup` | Optional warehouse filter; never receipt warehouse assignment |
| Receipt header | `WarehouseLookup` | Warehouse ID on the current editable receipt |
| Receipt header | `EmployeeLookup` | Employee ID on the current editable receipt |

Inline Product is an existing reference pattern and a mandatory regression target;
it is not one of the three repair targets. Inspect its working binding/lifecycle before repair.
At the baseline, header/filter controls use existing lookup view models and bind through
`GoodsReceiptControl`; do not assume a vendor bug or split view models without evidence.
Keep the shared warehouse lookup architecture unless focused tests prove isolation is necessary.

### Binding and lifecycle contract

Distinguish pending query, highlighted result, committed selection, and application ID.
Typing and arrow navigation must not choose a warehouse/employee or apply a list filter.
Bind async results without resetting raw query, caret or selection when committed identity is unchanged.
Do not call a selection-resetting binding operation on ordinary query-result notifications.
An explicit commit alone changes the intended filter/header value through its existing workflow.
Empty, unmatched and ambiguous candidates must follow existing validation/cancel rules;
never silently commit the first or merely highlighted result.

Filter typing must not assign the receipt warehouse; header typing must not apply the filter.
Shared result notifications may not erase another host's pending query or committed historical value.
Retain inactive/historical selected values in the Warehouse/Employee headers while disallowing
them as new choices. The optional list filter keeps its existing active-only options.
A late result after query replacement, document change, mode change, access clearing or disposal
must not mutate another host, document or session. Keep ownership and cancellation explicit.

Re-evaluate busy, Posted/read-only, access, authenticated session and uncertain-Post locks
at edit start, commit and completion. A disabled or stale accessibility setter cannot mutate state.
Preserve the intended difference between permitted list inspection and receipt mutation;
`RequiresPostOutcomeReload` keeps its existing authoritative navigation/mutation lock.

### Native per-key proof

Use real hosted/native editors and owned popups, not direct view-model calls as key evidence.
For each target, record the initial committed value, raw text, highlighted option, popup state,
focused HWND/control identity and resulting filter/header value before and after every step.
Execute each key independently with finite waits; a long happy-path script cannot replace the matrix.

| Input or boundary | Required observable result |
|---|---|
| Focus, type first character, continue typing and paste | Correct host retains complete raw query/caret; no unintended commit |
| F4 and supported popup gesture | Popup opens for the focused host within monitor work area |
| Down / Up | Highlight moves in the intended popup; query and committed ID remain distinct |
| Enter | Explicitly resolves the intended valid choice; no Save/Post or unrelated-host assignment |
| Escape with popup open | Cancels/closes pending presentation and restores committed selection |
| Escape with popup closed | Follows the host's cancel behavior without an unrelated mutation |
| Tab / Shift+Tab | Existing valid commit/cancel policy resolves predictably, then native focus traverses correctly |
| Mouse result / outside click / deactivation | Defined selection or cancellation; no implicit highlighted-first fallback |
| Result refresh / resize / theme switch | Query, caret, committed identity and validation survive supported relayout |
| New / receipt selection / refresh / disposal | Existing guards apply; stale callbacks cannot revive old state |

Use non-first matching options and both successful/no-match queries to detect accidental fallback.
Test host switching while a query is pending, shared warehouse result rebinding, historical values,
Posted, busy, denied access, session expiry and uncertain-Post recovery locks.
Keep inline Product F2/type/F4/arrows/Enter/Escape/Tab/Shift+Tab and numeric raw-input regressions.

The T02 input gate closes only after focused RED/GREEN binding evidence, native per-key tests and attended
observations pass in Light/Dark at actual 100% and 125%. No result is credited by this document.
A native failure blocks visual implementation; diagnose its actual binding/input boundary first.

## 5. Shared shell (BCF-T03)

Compose a recognizable brand area with app name and icon, grouped navigation with meaningful
icons and section labels, visible selected state, and a user/session context area.
Use the existing navigation actions and module permissions; do not add business destinations.
Keep keyboard focus, disabled ancestors, accessible names and selected workspace identity intact.

Retain explicit expand/collapse behavior, initially 224 logical units expanded and 64 collapsed.
Collapsed items need meaningful icons, full accessible names and discoverable tooltips;
initial-letter substitutes are insufficient for the intended finished composition.
Keep theme/session actions compact and reachable, with workspace tabs visually integrated.
Avoid the baseline's large standalone theme button competing with document commands.
Do not duplicate user/session controls merely to imitate decorative content in a reference.

## 6. Products catalog (BCF-T04)

Use one toolbar containing Products title, result count, Search, Refresh and Add product.
Keep the title/count as one hierarchy and place actions on the same row when space permits.
The result count comes from actual query/paging state, not a hard-coded mockup number.
Search, debounce/cancellation, paging, row identity and activation semantics remain unchanged.

Use a dense results grid beside a right inspector. Preserve product code/name/unit/active fields
and existing validation; do not invent data to fill the reference. The inspector shows a clear
heading, selected code, textual active/inactive badge and basic-information group.
Give Save/Cancel a coherent command group and Deactivate a distinct, accessible placement.
Retain Activate for inactive products and existing New/Save/Cancel/Deactivate guards.
Layout/theme changes must not reset an edit. Preserve existing selection/create/cancel
semantics; a new catalog discard-confirmation workflow is not part of this refinement.

Retain a visible paging/count footer and clear selected row; errors and inactive status must
have text/shape cues as well as color. A blank/no-selection inspector has a deliberate empty state.
At constrained width, inspector/results remain reachable through an explicit layout choice;
do not squeeze measured fields or silently remove editing actions.

## 7. Detail-focused Goods Receipt (BCF-T05)

Keep the existing single Goods Receipts workspace tab/control and application workflow.
Opening/creating a receipt presents detail-focused mode with the list hidden by default.
Provide an explicit Back to receipts action that reveals the list without opening another document tab.
Retain list query, warehouse/status filters, page, selection and scroll identity across mode switches.
Selecting a receipt or New enters detail after the existing guard succeeds.

Apply the existing navigation admission rules in both directions, including dirty/pending data,
busy, access-clearing and uncertain-Post boundaries. Back is not an edit operation: a clean Posted
detail may return to the list. Do not gate it with `CanEditLines()`. If list access is denied,
return only to the existing cleared access-denied presentation; never restore cached protected rows.
Back cannot bypass a rejected pending edit or `RequiresPostOutcomeReload`.
A rejected dirty/numeric/recovery Back preserves raw text, caret, focus and the current mode.
An allowed clean query-only Back explicitly cancels pending header/line lookup queries and
invalidates their async requests; it never commits a highlighted result. Keep the same control,
editor and adapter; presentation changes alone do not reload, dispose or implicitly commit data.
Retain deliberate document boundaries and stale-result fencing for actual New/load operations.

Proposed app-owned contract: `ReceiptWorkspaceMode { List, Detail }`,
`GoodsReceiptControl.TryShowReceiptList(): bool` and `ReceiptEditIntent.ReturnToList`.
These names are not baseline APIs. Add ReturnToList to the navigation branch of
`GoodsReceiptLinesGridAdapter.TryFinishEdit`, alongside New/Select/Refresh, so it cannot fall
through to committing `EndEdit(false)`. Route Back through existing navigation guards before
native focus validation; return false without changing presentation when rejected.
The mouse-down focus-retention predicate must cover pending Warehouse, Employee and inline
Product hosts, not only the current inline-session predicate. An abandoned click preserves
query/caret/focus; an admitted click cancels query-only state without committing a highlighted row.

Show business title, receipt number, textual status badge and accurate saved/unsaved/busy state.
Hide opaque version text from the main surface while retaining the exact concurrency token.
Use three metadata columns for date/warehouse/employee when measured widths allow it;
Note spans the full available row. Reflow to two/one columns before clipping fields.

Make the line grid the dominant editing surface, with a labeled header, line count and Add line.
Expose Remove for the selected/targeted row through a visible accessible row or context action;
retain a keyboard-discoverable command and identify the line by stable reference/key.
Preserve the five existing columns, native inline editors, right-aligned numeric values,
read-only row number/Total and explicit no-auto-blank-row policy.

Give Post primary emphasis, Save secondary emphasis and New/Refresh clear supporting roles.
A dirty receipt still requires explicit Save before Post; visual prominence cannot bypass guards.
Keep document commands reachable near the header and a prominent, accurately formatted total
near the grid/footer. Errors, recovery instructions and Posted read-only feedback stay visible.

## 8. Graphite and shared visual system (BCF-T03–T05)

Own shared typography, spacing, surfaces, borders, selection, focus, validation and disabled tokens.
Start from Segoe UI 10pt body, 14pt headings, 9pt secondary; retain measured editor preferred sizes.
Use 16-unit outer insets, 8-unit related-field gaps and compact row targets measured against native inputs.
Do not reduce editor heights merely to match raster density or rescale device preferred sizes twice.

Light and Graphite must cover shell/tab chrome, toolbar, grid/header, inspector, fields, popup,
scrolling surfaces and command states coherently. Resolve the baseline's bright islands in Dark
through supported APIs or app-owned controls; record unavoidable native rendering limits explicitly.
No silent exemption for an unthemed control and no claim of full Graphite parity while gaps remain.
Focus, selection, active editing, error and read-only states must remain distinguishable without color alone.
Preserve focus contrast, text/caret and pending values during theme/DPI/resize transitions.

## 9. Measurable composition and native evidence

Use the acceptance ledger as the authoritative checklist, with source/test/package provenance.
For each screen/theme/scale, record bounds for shell, toolbar, grid, inspector or metadata,
commands, total and error/recovery region; capture the actual state and any horizontal scroll offset.
At wide viewports, all intended major regions must coexist without overlap or clipped commands.
At constrained viewports, record explicit reflow/scroll behavior and prove every action reachable.

Required shell logical viewport targets are 1680x900 and 1200x760, plus 944x600 reachability.
Measure actual client/work-area geometry separately from outer-window dimensions and OS scaling.
At actual 125%, a 1680x900 logical client may exceed the available monitor; record the fitted size
and mark the full target NOT TESTED unless actually displayed on a suitable monitor.
Use actual Windows 100%/125%, both themes, serialized STA and bounded owned native/UIA processes.
Do not simulate DPI by an environment variable or count viewport arithmetic as native evidence.

Use deterministic synthetic fixtures with multiple catalog pages, active/inactive products,
selected and empty inspectors, and a multi-line receipt extending beyond visible grid rows.
Include long names, non-first selections, fractional/max-width numeric values, temporary rows,
validation errors, Draft dirty/saved, Posted, busy and uncertain-Post states.
Single-row screenshots alone cannot prove density, scroll identity or row-action behavior.

Native evidence must reconcile keyboard/mouse/UIA observations, editor bounds, popup work area,
scroll/resize/maximize/restore, theme transitions, command locks and focused error recovery.
Capture with explicit tool limits: PrintWindow black padding or pre-composition pixels do not
establish physical display scaling or foreground focus. Retain actual monitor/window measurements
and owner observations separately; never attribute capture padding to on-screen app layout.

## 10. Completion and handoff

Follow [TEST_POLICY](TEST_POLICY.md), [TEST_STRATEGY](TEST_STRATEGY.md) and [tests/README](../tests/README.md).
Use focused affected tests during implementation; complete the new frozen integrated UI/package
and attended obligations before BCF acceptance. Historical BCI PASS does not satisfy new behavior.
Keep actual discovery/TRX counts, exact revisions/hashes, failed trials and positive resource cleanup.

Complete T02 before visual work; then T03 shell/theme, T04 Products, T05 receipt and T06 integrated acceptance.
A remaining mandatory native or composition finding keeps its obligation open.
Owner review compares all three references against named actual screens and documented limits.
BCF completion certifies only its recorded UI scope; release signing, install/upgrade/restore
and other operational prerequisites remain unaccepted/unscheduled unless separately authorized.
