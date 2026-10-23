# UI Next — active roadmap for the SME desktop foundation

**Date:** 2026-10-23
**Status:** SOLE ACTIVE ROADMAP — T00–T05 VERIFIED in bounded task scope; A01–A06 focused evidence VERIFIED at recorded source/native boundaries; T06 VERIFIED in bounded source/native scope; T07–T13 NOT STARTED
**Base:** `v1a-pro-ui-next@7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`
**Design:** [Design and control policy](20261023-design-and-control-policy.md)
**Execution:** [Implementation plan](20261023-implementation-plan.md)
**Evidence:** [Validation and handoff](20261023-validation-and-handoff.md)

## Selection and scheduling boundary

The Owner requested analysis and an active roadmap for the earlier UI proposals, archiving old plans and explicitly excluding their unfinished work. This document makes that selection concrete. The original activation contained documentation only; subsequent T00/T01 implementation is recorded in the evidence linked below.

The selected direction is incremental shared UI infrastructure with thin application adapters. It keeps the existing compact visual composition, SourceGrid document editors, business behavior and active `net48` application. The main SME benefit is faster, predictable keyboard entry with visible, local outcomes; reducing repeated code supports that behavior. A new general-purpose ERP grid or application framework is unnecessary for this scope.

The [archive manifest](../archive/20261023-pre-ui-next/README.md) freezes the former plan set. SAL-T07/T08 incomplete native/attended/Owner acceptance, older deferred defects/visual exceptions, deleted REL/P6/net8 work and production prerequisites are not copied into this queue, renamed as UI Next tasks, or made prerequisites. New tests below cover the UI changes this roadmap makes. Using the same DPI, control or business fixture does not close an archived acceptance row.

Existing implemented invariants remain binding: exact decimal rules; seven Sales columns; independent duplicate-Product rows; manual prices; gift quantity/cost and zero revenue; authorization and session fencing; authoritative uncertain-write recovery; buffer admission before Save/Post; no automatic Post retry; schema10 and the existing receipt/sale/reconciliation protocol. Business contracts remain in [Direct Sales](../DIRECT_SALES.md), [costing](../DIRECT_SALES_COSTING.md) and [query/UI](../DIRECT_SALES_QUERY_AND_UI.md).

## Refreshed source baseline

| Component | Exact inspected revision | Result of refresh |
|---|---|---|
| MyDmsVn | `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` | Branch includes PR #43 and moved assessment; source comparison performed against the assessment's `9d8a199` |
| BootstrapSourceGrid | `2bd7874f7e80b22ef181763749c2a2938ab7f16e` | App gitlink unchanged; editor registry, formatted/lookup adapters and ownership rechecked |
| Bootstrap5WinFormUI | `2be9a4d778fda7016207b75470020be6126f2f3f` | Nested pin unchanged; calendar, formatter, feedback, tab, grid and theme contracts rechecked |
| SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944` | Nested pin unchanged; editing/clipboard ownership retained |

These are consumed pins, not a claim about current upstream HEADs. Before an upstream implementation, compare its current branch and compatibility policy explicitly. UI and grid libraries retain both `net48` and `net8.0-windows`; that does not reactivate the app's parked modern runtime.

| Finding | Current evidence and implication |
|---|---|
| F01/F02 — native dates and calendar capability | [SalesReceiptControl](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs) still uses `BootstrapDatePicker` for SaleDate and native nullable date filters. [CalendarPicker](https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapCalendarPicker.cs) supplies themed date selection, not free-text completion or a time editor. Selection rules and a typed composition are needed. |
| F03/F04 — feedback scope | [DesktopNotifications](../../src/MyDmsVn.Desktop.Application/DesktopNotifications.cs) still carries Kind/Message; [FoundationShellForm](../../src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs) displays it globally. Local labels exist, so the gap is consistent scope/persistence and adoption of existing Toast/Alert. |
| F05 — duplicate-code validation | [ProductFeatures](../../src/MyDmsVn.Server.Application/Catalog/ProductFeatures.cs) and other catalog handlers still omit `Field` metadata on DuplicateCode conflicts. [ApiResponseMapper](../../src/MyDmsVn.Server.Application/ApiResponseMapper.cs) and [DesktopViewModelBase](../../src/MyDmsVn.Desktop.Application/DesktopViewModelBase.cs) need that field identity to target Code. Preserve Conflict status. |
| F06 - exact numeric input at activation | [ReceiptNumericInput](../../src/MyDmsVn.Desktop.WinForms/ReceiptNumericInput.cs) and the former ReceiptNumberEditor retained strict parsing and FormatMode.None at the base revision. [T05 evidence](evidence/20261023-uin-t05-decimal-entry.md) records the latter editor replacement. Existing Numeral/Date formatters sanitized input; enabling them directly would have changed rejection/value semantics. |
| F07/F08 — inline editing | Both [Sales](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs) and [Goods Receipt](../../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs) already implement guarded sessions. DGV already has `BootstrapLookupColumn`; extend typed input parity, not a new lookup column or universal grid. |
| F09 — shared workspace primitives | App-owned [tabs](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactWorkspaceTabs.cs) and [appearance](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs) still carry repeated work. Generic close requests, scoped busy presentation and small semantic surface/text controls remain useful seams. |
| Refresh correction — R4 | `f5039cb` repaired inline-validation/footer ownership. [SalesReceiptControlTests](../../tests/MyDmsVn.Desktop.Tests/SalesReceiptControlTests.cs) and [native input tests](../../tests/MyDmsVn.Desktop.Tests/SalesReceiptLookupNativeInputTests.cs) now preserve clearing corrected numeric errors without erasing operation feedback. Keep these regressions; do not plan the old defect as unfixed. |
| Refresh correction — historical SAL progress | [T08 evidence](../evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb21bae3df4e9995150e42689635b8d307b`, separate GUI test `4697744063eab8484aeb2df2f0a6d23f42e2ca12`, runnable automated PASS and retained original failures. Full native125, attended observations and dated Owner acceptance remain OPEN in the archive. T08 is not NOT STARTED. |

This refresh is source/document analysis, not a new execution of those tests.

## Selected product decisions

These defaults make implementation reviewable. They are planning decisions under the requested scope, not claims that the Owner individually approved every API name or visual duration. A change to a default must be recorded with affected acceptance cases; it need not restart this assessment.

| Decision | Selected behavior |
|---|---|
| Investment | Reuse existing library engines; introduce only reusable control/lifecycle seams proven by the app. Keep SQL/domain business policy in MyDmsVn. |
| First visible slice | Complete duplicate Code and field/cell feedback in Product and Sales; route outcomes to the originating workspace instance. Expand after that pilot passes. |
| Feedback | Success uses a passive tab Toast; actionable errors/recovery use persistent tab Alerts; field/cell errors have persistent targeted presentation. Shell handles global/session state. |
| Numeric | Preserve exact raw candidate and current culture grammar; add generated grouping without dropping input. External grouped paste remains rejected initially. No rounding/clamping/truncation as validation. |
| Date | Date-only typed input with `d`, `d/M`, `d/M/yyyy`; fixed DMY grammar and four-digit year. Capture local reference date at edit start. Compact and two-digit-year forms remain disabled. |
| Date-time exception | Preserve existing UTC date-time/seconds behavior in Stock Card. A general typed date-time/timezone control is not scheduled without a concrete consumer contract. |
| Tab lifecycle | Library reports requests; app decides Save/Discard/Cancel and admission. Busy mutations or unresolved outcomes refuse ordinary close; session loss forces protected cleanup. |
| Theme/busy | Small native-backed semantic Panel/Label; per-content busy host using existing spinner/animation. Retain native layout and caller ownership. |
| Grids | Keep SourceGrid for receipt/sale documents. Add DGV decimal hosting using the same parser scenarios and independent host lifecycle; no silent app grid replacement. Date columns wait for a real editable date-column consumer. |
| Paste | Explicit all-or-nothing TSV paste into existing writable numeric cells, staged and validated before local draft mutation. No implicit Save/Post, product creation, row creation or formula evaluation. |
| Delivery | Small upstream changes, explicit integration pins, focused application adoption, measured behavior. No library-wide rewrite or blanket dependency upgrade. |

## Execution queue

At activation all rows were **NOT STARTED**. [T00 evidence](evidence/20261023-t00-field-errors.md) verifies its bounded application/session contract; [T01 evidence](evidence/20261023-uin-t01-workspace-feedback.md) records workspace feedback; [T02 evidence](evidence/20261023-uin-t02-validation-pilot.md) records Product/Sales presentation. The [current-candidate completion ledger](evidence/20261023-uin-t00-t02-native-completion.md) verifies T00–T02 with Light/Dark native100/native125 and focused Application/Session/GUI tests. [T03 evidence](evidence/20261023-uin-t03-inline-sessions.md) verifies common inline sessions on the current Goods/Sales adapters at native100. [T04 evidence](evidence/20261023-uin-t04-decimal-core.md) verifies pure decimal parsing/projection and application-policy compatibility. Final package/Owner review remains T13, not task-level acceptance here. [T05 evidence](evidence/20261023-uin-t05-decimal-entry.md) verifies the native decimal control, BSG adapter and Sales/Goods receipt pilots on measured Light/Dark native100/native125. [T06 evidence](evidence/20261023-uin-t06-quick-dates.md) verifies the UI control and required Goods/Sales dates with measured Light/Dark native100/native125. UIN-A07 nullable Sales filters remain with T11. T07–T13 remain NOT STARTED. Follow table order by default; independent work may proceed only after its listed dependencies and without conflicting file ownership.

| Task | Priority / outcome | Repository ownership | Dependencies | Completion evidence |
|---|---|---|---|---|
| UIN-T00 | P0 — explicit field-error contract and duplicate-code mapping | App Server/Contracts/Desktop.Application | None | UIN-A01: Conflict retains `code`, denied/general errors remain unbound, stale response rejected |
| UIN-T01 | P0 — workspace-origin feedback scope and tab host | App Desktop.Application/WinForms; reuse existing UI controls | T00 error vocabulary | UIN-A02: correct tab across switch/close/reopen/session change; bounded retention; persistent recovery |
| UIN-T02 | P0 — field/cell invalid presentation and Product/Sales pilot | App binding/adapters, existing ValidationState | T00, T01 | UIN-A01/A03: one error model drives editor/cell/summary; raw input and R4 behavior preserved |
| UIN-T03 | P1 — common inline session mechanisms | App first; narrow BSG seams only if proved necessary | T02 | UIN-A04: identity, commit/cancel, pending lookups and navigation parity in both documents |
| UIN-T04 | P1 — lossless decimal parser/display core | Bootstrap5WinFormUI plus app profile tests | T03 | UIN-A05: lexical preservation, strict culture/precision compatibility, independent expected values |
| UIN-T05 | P1 — decimal control, native entry and BSG receipt pilot | UI → BSG → App | T04 | UIN-A06/A04: per-key caret/undo/paste/IME and Save/Post admission in Sales and Goods Receipt |
| UIN-T06 | P1 — typed quick date input and calendar composition | UI → App | T03; T04 result conventions | UIN-A07: date grammar, popup/segment keys, reference clock and pending-date projection/admission |
| UIN-T07 | P1 — close requests, catalog dirty baseline and authoritative Save-close | UI tabs → App shell/workspaces/VMs | T01, T02, T03 | UIN-A08: all close routes, explicit write outcome/revision, refresh distinction, busy/recovery, forced logout and disposal |
| UIN-T08 | P1 — reusable scoped busy host | UI → App workspace binder | T01, T02 | UIN-A09: overlapping operations, delayed show, cancel and protected command routes |
| UIN-T09 | P1 — semantic surfaces/text and theme ownership | UI → App appearance profile | T02 | UIN-A10: Light/Dark and DPI without losing input/error/focus state or leaking resources |
| UIN-T10 | P2 — DGV decimal column and BSG adapter parity | UI and BSG; app regression consumers | T03, T05 | UIN-A11: host-specific reuse/clone/dirty/keyboard/disposal behavior against shared scenarios |
| UIN-T11 | P1 — finish adoption in existing workspaces | App; consume reviewed pins | T05–T10 | UIN-A12/A07: all four catalogs, Sales/Goods Receipt, inventory feedback and nullable Sales dates |
| UIN-T12 | P2 — staged numeric paste and measured editing performance | App draft coordinator, public grid seams | T03, T05, T11 | UIN-A13: rejected batch writes nothing; accepted batch changes local draft once; measured 1/50/200-line Sales workload |
| UIN-T13 | New UI Next integration and bounded Owner review | App plus exact consumed vendor/test/package revisions | T00–T12 | UIN-A14: affected UI cases reconciled on one candidate, explicit unresolved limits; no old-plan completion |

T07–T09 can progress independently of the numeric/date sequence once their own prerequisites are met. T10 is library parity work with a demo/test consumer; it does not authorize making Catalog results editable or replacing current document grids. T11 closes adoption gaps; it does not repeat evidenced pilot work without a changed integration risk.

## Coverage of the original proposals

| Original candidate | Selected disposition |
|---|---|
| UIN-01 control selection/conventions | Defined in the design now; applied/documented with T00–T11. Proposed controls are not used before their pinned implementation exists. |
| UIN-02 complete validation | T00/T02 and T11, including duplicate Code, stable row/field identity, invalid state and readable errors |
| UIN-03 tab feedback | T01/T02/T11, with persistent recovery and bounded inactive-tab behavior |
| UIN-04 shared edit mechanics | T03, then T05/T06/T10 integrations |
| UIN-05 fast numeric entry | T04/T05, preserving current strict lexical/value semantics |
| UIN-06 quick dates | T06/T11 for date-only fields; full date-time and compact-date profiles explicitly unscheduled |
| UIN-07 tab close/busy | T07 and T08, with separate library presentation and app admission ownership |
| UIN-08 theme primitives | T09 and T11; a small Panel/Label pair, not a wrapper for every native layout control |
| UIN-09 grid cooperation | T10 plus common validation/session contracts. Existing lookup retained; date-column hosting is deferred until a concrete editable date-column consumer exists. |
| UIN-10 paste/entry aids/performance | T12 selects bounded numeric paste and measurement. Barcode workflows, saved column layouts, formula engines, pivoting and speculative virtualization remain unscheduled. |

## Out of scope

No payment/debt/returns, pricing/tax engine, new sales columns, new business modules, schema/valuation/security redesign, modern-runtime reactivation, HTTP host, product localization or production release programme. No automatic recovery of an uncertain write, hidden re-post, full spreadsheet engine, docking suite or global notification centre rebuild.

Unfinished archived plan work is deliberately outside the task list. If later requested, it needs its own scheduling decision referencing its original evidence. It is not an implicit tail of UIN-T13.

## Cross-repository delivery and status updates

Use **UI upstream change → BSG integration/pin when needed → MyDmsVn explicit pin/adoption**. For app-only tasks, no vendor pin change is necessary. Read each repository's own AGENTS/API/compatibility policy before changing it. Preserve SourceGrid's base type, selection/editor ownership and native scrolling; modify SourceGrid itself only for a demonstrated missing public seam, never for application business rules.

Each task may contain several small reviewed commits. Its [implementation section](20261023-implementation-plan.md) defines files and tests. Record a compact evidence entry per completed slice; status moves NOT STARTED → IN PROGRESS → VERIFIED only for actual evidence, with BLOCKED/NOT RUN where necessary. Final bounded Owner review remains explicit. A docs-only PR may be merged to activate this roadmap without claiming any runtime outcome.
