# UI Next — design and control policy

**Date:** 2026-10-23
**Status:** Preserved design policy for the [historical UI-NEXT roadmap](20261023-active-roadmap.md). Initial proposal language retains its planning context; later implementation/results belong to the linked candidate evidence. The approved [DateInput Unified Compact design](20261024-dateinput-unified-compact-design.md) has subsequent [candidate evidence](evidence/20261024-dateinput-unified-compact-correction.md), including the original failure and bounded acceptance. New work follows the [execution index](../plans/README.md).
**Baseline:** `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` and its three pinned UI repositories

## 1. Ownership and reuse

| Layer | Owns | Must not own |
|---|---|---|
| Bootstrap5WinFormUI | Native control behavior, visual state, theme/DPI, accessible semantics, typed input primitives, generic close/busy presentation | API calls, permissions, document dirty rules, persistence or business scale limits |
| BootstrapSourceGrid | Thin SourceGrid editor/cell presentation, public lifecycle, per-grid registry ownership | A second theme system, global editor replacement or application Save/Post rules |
| MyDmsVn.Desktop.Application | Workspace/session/operation identity, messages, typed business errors, application admission and authoritative state | Form, Control, TabPage, native handles or vendor rendering dependencies |
| MyDmsVn.Desktop.WinForms | Workspace composition, field/cell binding, native host adapters, command/close intent routing | Duplicated parser/animation engines, hidden data writes or authority based only on Enabled |
| Server Application/Contracts | Explicit transport-neutral error metadata and existing business rules | Guessing an editor from a human message or leaking authorization detail |

Reuse `BootstrapToastContainer`, `BootstrapAlert`, `BootstrapValidationState`, `BootstrapSpinner`, `BootstrapCalendarPicker`, formatted-input history/caret machinery and existing grid lookup adapters. New controls below are small compositions/extensions. Preserve the SourceGrid public model and legacy formatter defaults.

## 2. Control-selection matrix

| Need | Selected control/pattern | Interim/explicit exception |
|---|---|---|
| General text | `BootstrapTextBox` with application error binding | Native input requires a documented capability reason |
| Formatted string | `BootstrapFormattedTextBox` with documented formatter semantics | Formatting is not business validation |
| Receipt quantity/price/cost | Proposed `BootstrapDecimalInput` after T04/T05 | Existing `ReceiptNumberEditor` + `ReceiptNumericInput` remain until compatible behavior passes |
| Small bounded integer setting | `BootstrapNumericBox` with explicit range/step | Display DecimalPlaces is not a precision validator; do not inherit a default range blindly |
| Selection-only date | Existing `BootstrapCalendarPicker` | Calendar selection is date-only, not a text-entry replacement |
| Document date/nullable date filter | Implemented `BootstrapDateInput` with required header / nullable filter profiles | [Unified Compact correction](20261024-dateinput-unified-compact-design.md): one surface, icon-only trigger and logical caret preservation; new acceptance pending |
| UTC date-time/seconds | Existing explicit compatible native input | Stock Card keeps time, UTC and inclusive displayed-second upper-bound semantics |
| Tab success | Local `BootstrapToastContainer` | `BootstrapToastService(relativeTo)` selects a monitor, not a TabPage |
| Actionable failure/recovery | Persistent `BootstrapAlert` in the owning workspace | Shell StatusBar is not the only presentation of an actionable tab error |
| Field/cell error | Shared error projection + `ValidationState` + readable message | `ErrorProvider` supplements persistent/accessibility feedback |
| Surface/text | Proposed `BootstrapPanel` / `BootstrapLabel` with semantic roles | Keep native layout containers where no repeated theme behavior needs a wrapper |
| Blocking local operation | Proposed `BootstrapBusyHost` around a content region | Lookup/query progress stays local where whole-tab blocking is unnecessary |

Each adopted pattern needs a short sample, supported behavior, fallback and first available upstream pin. No proposed API is described as current source. Source evidence is in the [refreshed assessment](20261023-active-roadmap.md#refreshed-source-baseline) and [original control assessment](20261022-ui-foundation-assessment.md).

### T11 adoption examples and retained native controls

The four `CatalogKind` variants (Product, Warehouse, Employee and Customer) share `CatalogControl`: its factory creates one `WorkspaceFeedbackScope` and scoped notification adapter per open control, while `FieldValidationBinder`, `WorkspaceBusyBinder` and the semantic inspector handle local errors, blocking operations and theme roles. Goods Receipt uses the same per-control scope for list, editor and lookup ViewModels. A factory-created Stock Balance/Card control routes query failures to its own scope; the shell hosts that scope with its tab. A hidden tab retains its own error until selected or closed, and disposal invalidates it. These app compositions require no new upstream pin; the first feedback/binder pins are recorded with T01/T02/T08, and semantic controls first appear in UI `15ef4032e9420a04bb4c6279704406c8d7424859` (current UI pin `910c19493b36d1db2fb0b92b7fc436f263ec564d`, BSG pin `a2c2cb749ac5656a56b1cfebfc7718b889d95152`).

Sales From/To use `BootstrapDateInput` with a nullable profile: begin a null edit session, retain raw pending text through unrelated projections, then parse both candidates on Apply. An explicit empty candidate commits null; invalid input or From > To leaves the prior query criteria and raw text intact. The required Sales/Goods document dates retain their T06 behavior. The first date-input UI pin is `78c1673e1125b72ba282b6ab9254bafc642cec80`; the app uses the current pin above. When typed date entry cannot represent the required UTC time and seconds, Stock Card retains its native `DateTimePicker` and existing displayed-second upper-bound conversion.

Native layout panels and grid controls remain where no shared semantic behavior is needed. Stock Balance/Card retain a native `ProgressBar` for query-only progress; it does not authorize commands or block the whole tab. Sales/Goods preserve their established receipt grids and inline adapters. Operation labels use `BootstrapLabel` semantic roles where their text reports state or error; `ErrorProvider` continues to supplement persistent, accessible field feedback.

## 3. Feedback scope and error presentation

Create a new opaque workspace-instance identity for each actual open workspace. A reopened tab with the same module key is a different instance. Capture authenticated-session, document and operation generations when an operation is admitted. Query freshness and write-outcome freshness are different domains: a new list query must not erase a still-relevant completed Save result.

The proposed app scope/sink exposes capture and publish behavior without WinForms references. A host subscribes to that scope, marshals to its UI thread, and rechecks identity after dispatch. It handles messages before handle creation and rejects stale/closed/session-replaced publications. No result is routed using the tab selected at completion.

| Outcome | Presentation and retention |
|---|---|
| Successful user command | Passive local Toast, initially five seconds of visible time; no focus change |
| Inactive-tab success | Retain the latest success with occurrence time; show on return, expire after five minutes; no unbounded hidden queue |
| Active-tab success burst | At most three visible and five pending transient notices per workspace; coalesce same operation, discard expired/oldest successes first |
| Parse/field/cell failure | Persistent target invalid state, readable message and summary; no Toast on each keystroke |
| Permission, concurrency, stock or other actionable command failure | Persistent local Alert with a safe next action; keep unbound errors visible |
| Unknown Create/Post outcome | State-driven persistent Alert and existing mutation lock until authoritative recovery; no auto-retry or dismiss-to-unlock |
| Session/application event | Global Shell presentation and existing security cleanup |

Transient queue limits are app policy; `MaximumVisibleToasts` does not bound the container's full queue. Use a small anchored region, not a full-size Panel intercepting all input. Do not reparent/dispose a Toast after ownership has passed to its container. Preserve hidden-time pause and lifecycle rules.

An inactive workspace retains actionable state and an error/unread marker. Acknowledging a message clears only the attention marker, not unresolved recovery/validation state. Permission loss/logout scrubs protected content and its notices. A generic banner must not expose inaccessible record details.

### Validation identity and clearing

Keep one current error collection and project it into `ValidationState`, readable field help, ErrorProvider, inactive cell/row indicators and an accessible summary. Use `BootstrapValidationState.Invalid`; do not add a parallel `IsInvalid`. Local parsing may clear its parse error but does not prove server uniqueness or stock acceptance; do not set every field green merely because it changed.

Server errors keep their typed code/status. Add `Field = "Code"` metadata to catalog DuplicateCode failures, producing JSON field `code`. Do not attach every Conflict to Code, parse translated messages, or disclose field details for authorization failures. Unknown field paths remain in the summary.

Grid errors use document generation + stable line instance + field. ProductId and row index are not line identity; unsaved duplicate Products remain distinct. Resolve server `lines[i]` paths through the submitted row snapshot, then check current identity/version before projection. A removed row's error is retired or retained as an unbound submission error, never moved to its neighbour.

Clear only errors made obsolete by a relevant edit/revalidation or admitted document boundary. Preserve operation feedback independently and retain the repaired R4 behavior in current Sales tests. Focus the first invalid target only after an explicit user command or summary navigation, not when a delayed error arrives in an inactive tab.

## 4. Inline edit state and host cooperation

Committed model value, pending lexical input and formatted display are separate state. A session captures stable line/field and generation, original edit context and current candidate. Projection or unchanged lookup selection preserves raw text, caret/range and focus. Invalid conversion leaves the editor and candidate available for correction.

| Intent | Required behavior |
|---|---|
| Enter/Tab | Resolve current buffer once; commit only if valid; native host owns traversal after admission |
| Escape with popup | Cancel popup preview and restore pre-popup pending buffer; do not also cancel the field in the same key event |
| Escape without popup | Discard local candidate and project current model; never overwrite a newer model with an old snapshot |
| Save/Post | Resolve active inline, header lookup and typed-date buffers before dirty/state checks and the current command path |
| Back/close/new/select | Run navigation admission before destructive focus-loss validation; rejected navigation preserves exact buffer |
| Document/session replacement or row removal | Invalidate affected sessions/callbacks; dispose owned resources |
| Posted/busy/denied/recovery | Same guards for mouse, keyboard, checkbox, programmatic/accessibility and paste routes |

Extract only mechanisms demonstrated in both receipt adapters. App callbacks decide allowable fields, gift semantics, business parsing profiles and Save/Post admission. Share pure parser/scenario data across DGV and BSG; retain different lifecycle implementations.

SourceGrid owns begin/commit/cancel, placement and final conversion. Share a registry editor within one grid/configuration, never across grids. Registry-created adapters/controls are disposed even if never started; app-created custom adapters remain explicitly app-owned. DGV owns `IDataGridViewEditingControl`, reused-editor reset, dirty notification, clone/configuration propagation, DataError and navigation. Its existing lookup column remains the lookup path.

Parsing a candidate must be side-effect free. `GetEditedValue`/`ParseFormattedValue` must not mark a shared control committed before native validation and the single accepted model application. Revoke prior validation eligibility before each conversion/revalidation attempt. The adapter must verify that the native accepted value equals the stored validated candidate, as well as the current cell/editor/session identity, before applying once. Failed revalidation or a different native value cannot reuse an earlier proof. A failed host commit retains candidate text; cancel reprojects the current model.

## 5. Lossless numeric entry

### Compatibility profile

Use the existing [parser](../../src/MyDmsVn.Desktop.WinForms/ReceiptNumericInput.cs) and [tests](../../tests/MyDmsVn.Desktop.Tests/ReceiptNumericInputTests.cs) as the application contract, including outer trim, leading/trailing decimal forms, all-zero fractional padding and literal culture signs/separators. Quantity stays positive within decimal(18,4); UnitPrice/UnitCost stay nonnegative within decimal(19,4). Keep existing maxima and meaningful-fraction rules in the app profile, not hard-coded library defaults.

Inspect all fractional digits before `decimal.TryParse`: extra zero padding can represent the same allowed value; a nonzero digit beyond scale, including a far tail, rejects. No exponent, currency syntax, alternate decimal separator or external grouped paste is newly accepted. Multi-character culture tokens must not be reduced to single characters. Freeze a cloned culture/profile for the edit session.

### Proposed parser and control boundary

The pure parser returns Empty, Incomplete, Invalid or Valid with an optional exact value and structured failure; it never mutates input. The control retains `PendingText` independently of read-only accepted `Value`. An explicit profile/initialization seam starts or resets an admitted edit session and freezes its immutable profile. Reused grid editors must reset candidate, errors, history and bindings before another cell/profile; configuration changes during a pending edit cannot reinterpret it. Display inserts grouping only when every synthetic separator can map back to the candidate. User-entered separators are not silently stripped as if generated by the control.

If formatting is not reversible, display the candidate verbatim with its error. Intermediate signs, decimal tokens or compositions are not discarded while typing; final validity follows the captured app profile at commit. Successful explicit commit may normalize display without changing value. Rejected commit keeps significant text and the last committed value.

Reuse formatted-input history/caret infrastructure through a narrow reviewed seam. Do not replace the engine or change legacy Numeral/Date defaults. Define selection mapping for insertion/deletion beside grouping, middle replacement, paste, undo/redo and first-character replacement. Do not reformat during IME composition; reconcile afterward. Programmatic binding and generated formatting must not create duplicate undo entries or dirty notifications.

Initial external paste grammar remains strict ungrouped text in the captured culture. Copy/Cut use the mapped selection of the pending lexical candidate, including invalid characters, and remove only control-generated grouping; they never substitute the last accepted Value. Cut then applies a normal candidate edit. Provenance cannot depend only on a changed display: if candidate `1234` displays `1,234`, selecting all and pasting external `1,234` must preserve and reject that new external candidate even though the visible text is identical. A later grouped-import profile needs a separate explicit grammar and is not part of this roadmap.

## 6. Typed date input with a themed calendar

The implemented `BootstrapDateInput` composes native text entry and `BootstrapCalendarPicker`. The Owner-approved [Unified Compact correction](20261024-dateinput-unified-compact-design.md) records its visual and caret behavior; [UIN-D01–D03](20261024-dateinput-unified-compact-correction.md) is the historical implementation record, not an instruction to execute again. CalendarPicker is sealed and has no free-text parser; reuse its engine through reviewed internal/public seams, not application access to private popup controls. Do not put partial-date completion inside `IInputFormatter.Format`, which is invoked on partial prefixes during caret mapping.

The date control follows the same explicit profile/session initialization and read-only accepted Value contract as decimal input. Capture its profile at an admitted session boundary. Capture the injected local reference date when a fresh pending candidate begins, including a successful Up/Down step; retain it while that candidate is pending. Opening the calendar without a pending candidate uses the current date; popup focus movement does not recapture a pending candidate's clock. Resolve missing parts on explicit commit or deliberate segment stepping; stepping normalizes only the pending display and does not accept a new Value. The app uses `DateTime` at midnight with `Kind.Unspecified` for date-only values, without UTC conversion.

| Input at reference date 23/10/2026 | Result at commit |
|---|---|
| `5` | 05/10/2026 |
| `5/11` | 05/11/2026 |
| `5/11/2027` | 05/11/2027 |
| Empty optional filter | null |
| Empty required document date | Invalid; keep text and prior committed value |
| `31/11`, `29/2/2027`, `5/11/27`, compact forms such as `0511` | Invalid; no rollover, clamping, century guess or implicit alternate grammar |

DMY is explicit even under an en-US numeric culture. Slash is the initial accepted separator. Min/max apply equally to typed input, calendar choice and segment steps. Display uses `dd/MM/yyyy`; help announces short forms and required/optional behavior. Compact `ddMM`/`ddMMyyyy` and time input remain future explicit profiles.

Left/Right/Home/End retain native text selection semantics; the day/month/year segment at the caret or selection can use Up/Down. Day stepping deliberately moves one calendar day; month/year stepping preserves the day and refuses an impossible date instead of clamping. The correction preserves logical caret/selection through normalization before host notification, keeps a collapsed caret collapsed, and rejects mutation for readonly/disabled/IME states. Parsable partial DMY may be normalized by a deliberate segment step using the candidate's reference date. Invalid input or a rejected step keeps the raw text and selection. Mouse wheel does not alter a date accidentally.

F4/Alt+Down opens the calendar. Popup arrows navigate the calendar, not text segments. Selection is previewed in the pending buffer and accepted once; Escape restores pre-popup text, and a subsequent Escape can cancel the field. Tab validates before traversal. Popup focus loss must not double-commit or trap focus.

Sales and Goods Receipt have typed date-input adoption and identity-aware projection guards. Preserve them: unrelated notifications/theme refresh cannot overwrite pending date text or caret. Pending date changes participate in dirty/close/navigation admission and Save/Post buffer resolution. Sales From/To retain null semantics, remain independent fields and validate order before an admitted query. DateInput owns one border/focus/validation surface and an embedded calendar icon without a date caption; standalone CalendarPicker summary behavior remains compatible.

Stock Card's UTC date-time filter, displayed seconds and inclusive upper-bound conversion remain unchanged. A typed time/timezone editor is a separate future consumer-driven design, not a hidden dependency of T06.

## 7. Tabs and busy presentation

### Close requests

Extend native `BootstrapTabControl` with per-page close affordance and a request event carrying the actual TabPage/reason. The library never removes/disposes a page automatically. Intercept pointer close intent before focus validation destroys a pending edit. Mouse, Ctrl+W and accessibility use the same admission path; duplicate requests during admission are ignored.

The app checks pending numeric/date/lookup text as well as VM dirty state. Clean workspaces close; dirty/invalid ones use an injectable Save/Discard/Cancel decision. Save invokes existing buffer/command admission and closes only on authoritative success. Rejected Save or Cancel preserves text/focus. Discard abandons only local unsaved changes. Query-only work may cancel/drain after close admission; in-flight mutations and unknown outcomes refuse ordinary close with a persistent reason. Forced sign-out remains non-vetoable security cleanup.

Catalogs need an accepted editor baseline and monotonic edit revision: the base has neither catalog dirty state nor a result-bearing Save contract. T07 adds those bounded desktop contracts and exposes authoritative save outcomes for Catalog and Sales through the existing command cores; Goods Receipt adapts its existing result. Capture workspace/session/editor identity and submitted edit revision before awaiting. Task completion, `!IsBusy` or an empty error collection never proves a write succeeded. The same explicit mutation outcome drives feedback and close admission. A response must not overwrite newer fields or close a newer edit/document/workspace.

Separate write outcome from subsequent list refresh. On confirmed write success followed by refresh failure, retain the saved baseline for the same editor identity, preserve newer edits, and keep this close attempt open with a persistent “saved; refresh failed” warning. A later explicit clean close may proceed; never replay the write to repair a refresh. Rejected/canceled-before-write saves keep the attempted text. Cancellation or an ambiguous result after dispatch is not evidence of failure or success; keep the tab open and preserve existing receipt recovery rules without automatic replay or a new persistence protocol.

Preserve native TabPages, selection, overflow, images, focus cues and keyboard behavior. Remove the app dictionary/page and dispose scope/content once after admission. Reopening the module cannot receive old callbacks. This is closable tabs, not docking or saved window layouts.

### Busy host

The proposed `BootstrapBusyHost` targets one content region and composes existing spinner, semantic text and a theme-colored backdrop. Initial backdrop is an opaque/inset native-safe surface, not a promise of arbitrary translucent sibling compositing. Keep tab headers and permitted cancellation/recovery affordances reachable.

Acquire a disposable presentation token per operation; releasing an older token cannot hide a newer operation. Initially delay display by 150 ms, with no minimum hold after completion. Cancel requests only an app-declared cancellable operation and never promises rollback. Hide/dispose stops scheduling; UI-thread affinity, handle recreation, DPI/layout, hidden tabs and faults are explicit lifecycle cases.

Do not recursively overwrite/restore child Enabled state. Command admission remains authoritative before the delayed overlay appears and through keyboard/UIA. Restore focus only if the original control, workspace and focus context are still current.

## 8. Theme surfaces and text

Add a small native-backed `BootstrapPanel` and `BootstrapLabel` using existing theme manager, semantic colors, typography and DPI helpers. Surface roles are Body/Surface/Alternate; text roles are Body/Muted/Success/Warning/Danger. Preserve Panel layout/scrolling and Label AutoSize, wrapping, mnemonic, accessibility and designer construction.

Define precedence between a semantic role and explicit caller-owned color/font overrides. Do not dispose caller resources. Unsubscribe theme events and release owned fonts/GDI objects deterministically. App density/typography remain in `BusinessCompactAppearance`. Preserve its existing exclusion of vendor-private children, while explicitly treating a semantic Panel’s public content children as application-owned: traverse nested content and adopt dynamically added children. Generic Panel/Label Body/Text assignments must not override semantic roles. Apply app typography only through the documented precedence; retain tab/native-frame workarounds until a proven upstream change replaces their actual behavior.

Theme changes preserve values, pending text/caret, error state, selection, focus and operation locks. Errors remain understandable without color alone.

## 9. Bounded paste and performance

T12 accepts tab/newline-separated values only into a selected rectangle of existing writable numeric cells: Quantity plus UnitPrice in Sales, Quantity plus UnitCost in Goods Receipt. Accept CRLF/LF and one terminal newline, with a 262,144-character cap, one or two columns and no more than the selected existing rows; reject excess input without truncation. Reject headers, Product/Unit/IsGift/read-only targets, formulas, extra columns, unavailable rows and unsupported mixed ranges. Do not create rows or resolve Products from arbitrary text.

Resolve the active editor through normal admission first. The separately admitted active edit precedes the paste transaction; rejection compares against that post-admission snapshot. Snapshot document generation, row keys, field keys and draft revision; parse/validate the full candidate; recheck state/permissions before one in-memory draft application. Any invalid cell or stale/removed row rejects the whole patch with positions/messages and no partial mutation. Accepted paste produces one coherent dirty/totals update and leaves Save/Post explicit. Native SourceGrid per-cell clipboard writes cannot be used as this atomic path.

Validation alone is insufficient: mutable setters and `PropertyChanged` can reenter during application. The app must supply an admission/mutation boundary that prevents observers from seeing a partial batch, publishes notifications only after the coherent patch and preserves the prior draft on rejection. Every relevant ordinary line/header/collection edit must also advance the draft revision under that same gate before its first observable callback; incrementing only during paste or a later Dirty callback is insufficient. Reuse/extend T07’s edit-revision contract where available. A reentrant attempt to apply an old patch from an ordinary edit’s first notification must reject and preserve the newer draft. If that cannot be demonstrated, keep paste disabled; do not ship partial application under this contract.

Measure edit/commit and projection time, allocations and retained controls at 1, 50 and the existing Sales maximum of 200 lines on a named reference machine. Do not invent the same line limit for Goods Receipt. Compare the same candidate/environment and repeated observations; record p50/p95 and event/projection counts. Set a justified regression budget from measurements; no unmeasured throughput/capacity claim. Optimize only a demonstrated hot path. No speculative virtualization, new grid or global benchmark framework.
