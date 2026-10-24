# Candidate backlog and validation for the SME UI foundation

> Historical assessment — 2026-10-22. Scheduling/proposal status below is preserved for that date. The [2026-10-23 UI-NEXT roadmap](20261023-active-roadmap.md) records the subsequent bounded selection and baseline refresh, which superseded old T08/R4 status summaries. New work follows the [execution index](../plans/README.md). These source findings do not imply new runtime verification.

**Date:** 2026-10-22  
**Status:** PROPOSED CANDIDATES — not scheduled, not active, not implemented by this assessment

Read the [baseline/index](README.md), [source assessment](20261022-ui-foundation-assessment.md), and [input contracts](20261022-input-and-inline-editing-contracts.md). The current [SAL execution index](../archive/20261023-pre-ui-next/plans/README.md) remains authoritative. Priority below expresses relative value once a work package is selected; it does not authorize interrupting the current SAL queue.

## 1. Recommended order

| ID | Priority | Candidate | Primary repository / dependency | Reviewable completion |
|---|---|---|---|---|
| UIN-01 | P0 | Control-selection and interaction conventions | MyDmsVn docs; inspected UI catalog | Explicit date/number/lookup/feedback matrix, native-control exceptions and sample bindings |
| UIN-02 | P0 | Complete field/cell validation path | MyDmsVn; existing ValidationState and API metadata | Duplicate Code and inline quantity errors visibly identify the exact target; errors survive relevant refreshes |
| UIN-03 | P0 | Workspace feedback composition | MyDmsVn; existing Alert/ToastContainer | Success/error/recovery routed to originating tab instance; inactive/closed-tab behavior tested |
| UIN-04 | P1 | Shared edit-session mechanics | MyDmsVn initially, then narrow upstream seams | Receipt and Sales use common proven mechanisms without changes to Save/Post or raw numeric rules |
| UIN-05 | P1 | Safe numeric live-format entry | Bootstrap5WinFormUI; UIN-02/04 contracts | Exact candidate preserved; caret/undo/culture behavior proven; receipt integration retains all precision rules |
| UIN-06 | P1 | Typed quick date entry | Bootstrap5WinFormUI and MyDmsVn | Date-only grammar, nullable values, segments and themed calendar work through the same commit boundary |
| UIN-07 | P1 | Tab close and tab busy primitives | Bootstrap5WinFormUI plus app lifecycle | Close request/admission and per-target busy state preserve dirty/recovery rules and cleanup |
| UIN-08 | P1 | Theme surfaces/text and compact layout primitives | Bootstrap5WinFormUI plus app appearance profile | Less manual palette/font code; Light/Dark and DPI preserve input/validation state |
| UIN-09 | P2 | DGV numeric/date parity and BSG adapter refinements | Both UI libraries; stable typed-input contracts | Same behavioral scenarios pass in both hosts, with their own lifecycle implementations |
| UIN-10 | P2 | Staged paste, additional entry aids and measured performance | App + grid seams; concrete workload | Explicit paste grammar/application policy and benchmark-driven improvements |

UIN-02 and UIN-03 can provide immediate user value using existing controls. Numeric/date work deserves focused design because silent value changes are more serious than inconsistent colors. UIN-07/08 can proceed independently after ownership rules are settled. Do not build every candidate at once.

## 2. Control-selection rules to formalize

These are recommended conventions for future approval, not edits to existing AGENTS:

| Need | Recommended default | Explicit exception |
|---|---|---|
| General text | BootstrapTextBox / established lookup input | Native editor only when a required capability is documented |
| Generic formatted string | BootstrapFormattedTextBox | Do not treat a formatting mode as business validation |
| Receipt quantity/price | Current strict raw-preserving path, then approved preserving typed input | Never enable truncating Numeral to enforce SQL scale |
| Small bounded integer/setting | BootstrapNumericBox when native semantics fit | Set range/step/null requirements explicitly |
| Date-only calendar selection | BootstrapCalendarPicker | If typed input is required, use the approved typed date input or recorded interim native exception |
| Rapid document date entry | Proposed typed date input + custom calendar | No claim that existing CalendarPicker already supports this |
| Date-time / UTC field | Explicit compatible native wrapper or later typed date-time profile | Date-only replacement must not discard time/timezone meaning |
| Tab success outcome | Tab-local ToastContainer | Global service only for an intentionally global notification |
| Actionable error/recovery | Contextual Alert + targeted errors when applicable | Never rely solely on an overwritten Shell status label |
| Input validation | Shared binder + ValidationState + readable accessible message | ErrorProvider is supplementary; logical grid cells need their own mapping |

A lightweight adoption guide should include the intended scope, minimal example, known limitations, relevant tests and approved fallback for each row. Record the upstream commit at which an API becomes available. Source facts in this assessment must not be rewritten as implemented proposed APIs.

## 3. Small work packages and ownership

### A. Feedback and validation pilot

Use Product catalog and Sales as the first pair: Product demonstrates server duplicate-code field mapping; Sales demonstrates inline numeric and document-recovery feedback. Address the complete error chain before broad rollout. Retain Conflict status for duplicate values, use explicit field metadata, and preserve the distinction between field errors and general permission/concurrency failures.

Create one workspace composition and binding pattern, then adopt it in Goods Receipt and the remaining catalog screens. Notifications should originate from a workspace-scoped sink or carry an opaque scope/context. ViewModels must not reference WinForms controls. Retain useful global notification behavior for session/application events.

### B. Shared editor pilot

First factor duplicated mechanics locally between Goods Receipt and Sales. Characterize what truly varies: line identity, allowed fields, numeric profiles, lookup availability, gift behavior and command admission. Extract only stable generic seams upstream after both consumers prove them.

Avoid moving the two large adapters wholesale into a generic "business grid". Prefer independently testable parser, edit-session, error projection and navigation pieces. Preserve the active editor across projection changes, and validate request generation before publishing async lookup data.

### C. Numeric/date controls and grid adapters

Use additive opt-in behavior. Keep legacy Numeral/Date formatting compatibility; their sanitization is documented and tested. Do not change the existing public API or default normalization simply to satisfy receipt entry.

Implement and validate the pure parser/commit policy first, then the native input/caret behavior, then each grid adapter. Keep CalendarPicker selection separate from typed date completion; use composition or a reviewed public seam rather than accessing private controls. Match active `net48` requirements; other supported library targets remain subject to upstream compatibility policy.

### D. Tab/theme/busy primitives

The library renders close affordances and reports close requests. The app owns prompts, commit/dirty checks, async write outcomes, content lifetime and disposal. A closed-and-reopened tab with the same logical module key is a new instance.

The busy primitive targets a Control, owns presentation/lifetime, and reuses existing spinner/animation infrastructure. The app decides which operation blocks which region, whether cancellation is meaningful, and which commands remain available. Do not equate cancellation with rollback. Keep lookup busy feedback lightweight and local to the lookup region.

Add a small set of semantic surface/text roles with explicit ownership. Preserve existing vendor-owned palettes and app-owned typography intentionally. Do not recursively recolor private subcontrols or create a subclass for every WinForms layout container.

## 4. Focused acceptance cases

These are future evidence requirements, not tests executed by this assessment.

| Area | Minimum meaningful scenarios |
|---|---|
| Duplicate Code | Create/update conflict retains attempted text; Code invalid state and readable message appear; other fields are unaffected; changing/revalidating Code updates the error correctly |
| Field mapping | Explicit server field metadata reaches the intended editor; unrecognized/general errors remain visible in the summary rather than disappearing |
| Cell errors | Quantity/price parse errors point to stable line+field; row removal/reorder/duplicate Product and delayed server response cannot move an error to another line |
| Multiple errors | Invalid border, ErrorProvider/row indicator and summary show the same current error data; inactive cells remain inspectable |
| Tab notifications | Concurrent results in A/B remain in their originating tabs; switching tabs during an operation does not redirect completion; close/reopen rejects stale messages |
| Recovery feedback | Unknown Post/Create outcome stays visibly unresolved and mutation-locked until the existing authoritative recovery succeeds |
| Numeric semantics | vi-VN/en-US; boundaries; blank/null; signs; zero padding; excess meaningful digits; wrong separators; overflow; paste; no rounding/clamping before validation |
| Numeric interaction | Native per-key end/middle input, selection replacement, deletion around grouping, first key, paste, undo/redo, caret/range/focus preservation |
| Quick date | Day-only and day/month completion; reference-date capture across midnight; leap dates; invalid dates; optional clear; required blank; explicit grammar ambiguity rejection |
| Date keyboard | Segment keys versus popup keys; Enter handled once; Escape popup then field; Tab validation; no accidental value changes from scrolling |
| Lookup | `g`, `u`, `i` typed separately while a canceled predecessor completes; pending query remains separate from key; no caret reset on unchanged selection |
| Commit/session | Save/Post commits current buffer first; invalid buffer blocks; cancel shows current model; removed row/document/session/disposal rejects callbacks |
| DGV integration | Bound decimal/null, configuration cloning, reused editor reset, DataError handling, new row, hidden/read-only traversal and existing lookup compatibility |
| BSG integration | Conversion failure retains editor; popup/focus order; first character; view refresh; editor registry disposal; no private control access |
| Tab close | Clean/dirty/invalid/busy/recovery cases through mouse, keyboard and accessibility; canceled close preserves text/focus; accepted close cleans subscriptions |
| Busy overlay | Overlapping operations; quick completion; target hidden/disposed; permitted cancel action; focus restoration; commands cannot bypass mutation guards |
| Theme/DPI/accessibility | Light/Dark, required native100/125 contexts, newly supported scale contexts as selected; readable errors, focus indication and no dependence on color alone |
| Paste when selected | Full candidate validated before draft mutation; clear failure positions; limits/readonly/duplicate Product rules preserved; no implicit Save/Post |

For generated grouping, test external paste separately from keyboard-generated separators. For short dates, test with injected reference dates rather than the wall clock. For grids, share scenario data but keep adapter-specific integration assertions.

## 5. Keep verification proportionate

Follow [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and the upstream repositories' policies at the time of implementation.

- Pure parsing, date completion, scope routing, metadata mapping and state-transition logic should be covered by focused deterministic tests.
- Use interactive Windows/STA native per-character tests for caret, focus, popup and keyboard behavior. Whole-text assignment does not prove real typing correctness.
- Run the affected adapter/control tests and representative screen integration. Broaden for a concrete shared lifecycle or compatibility risk, or a required acceptance gate.
- Keep native100/125 and existing signed historical exceptions explicit. Additional DPI contexts are new evidence, not retroactive changes to old acceptance.
- Theme/toast/caret work alone does not justify rerunning every SQL integration project. A change to conflict metadata needs application/contract coverage; changes to actual SQL constraint/concurrency behavior need the appropriate SQL tests.
- Record exact source/vendor/test/package revisions and actual outcomes. A source test's existence is not a passing execution result.
- Document-only changes use diff/link/encoding/status checks and do not trigger GUI or SQL runs.

This approach addresses the Owner's concern about growth without turning each small UI change into another full-system test exercise.

## 6. Decisions to record before implementation

| Decision | Recommendation | What remains selectable |
|---|---|---|
| D01 — investment scope | Incremental shared foundation plus app adapters | Per-screen-only repair, recommended shared scope, or a separately justified broader suite |
| D02 — initial slice | Feedback + complete validation in Product and Sales | Owner may prioritize date/number entry first for operational urgency |
| D03 — numeric behavior | Preserve current strict value rules; add live grouping only with a preserving candidate model | Accepted external grouped paste and display profile; no silent precision reduction |
| D04 — date grammar | Day, day/month and full four-digit-year forms; capture current local date at edit start | Optional `ddMM`/`ddMMyyyy`, year-shortening policy and segment-step edge behavior |
| D05 — tab lifecycle | Close request owned by app, with explicit busy/dirty/recovery handling | Prompt wording and close/save/discard choices per document state |
| D06 — presentation | Tab-local Toast for success; persistent Alert for actionable failures; global status for global state | Placement, duration, unread marker/history scope, compact typography |
| D07 — grid strategy | Keep SourceGrid for current documents; add DGV parity when a real consumer needs it | Exact extraction/upstream order; no automatic replacement |

The Owner has authorized this assessment and its documents. These choices are recorded to support the next discussion; they are not a request to reapprove the already completed analysis. A future explicit implementation request can select a bounded package and its concrete behavior without reopening settled facts.

## 7. Session ledger

| Date | Work | Outcome |
|---|---|---|
| 2026-10-22 | App/pinned-library source assessment; comparison to current UI main; inspection of relevant test source and official WinForms documentation | Findings F01–F09, proposed contracts, candidate order and decision defaults recorded; no product changes or runtime verification claimed |

Next session should add the actual selected scope and updated baseline, rather than replace this record with a generic plan or mark candidates complete from design text alone.
