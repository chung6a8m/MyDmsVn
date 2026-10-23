# Proposed input and inline-editing contracts

> Historical assessment — 2026-10-22. Scheduling/proposal status below is preserved for that date. The [2026-10-23 active roadmap](20261023-active-roadmap.md) now selects the bounded work, and its refreshed baseline supersedes old T08/R4 status summaries. These source findings do not imply new runtime verification.

**Date:** 2026-10-22  
**Status:** Design recommendations; no API or implementation commitment  
**Context:** [Assessment](20261022-ui-foundation-assessment.md) · [Baseline](README.md)

This document defines observable behavior to discuss before choosing new controls or extracting shared infrastructure. Existing implementations cited below are evidence; names prefixed with "proposed" are illustrative.

## 1. Separate the responsibilities

| Responsibility | Appropriate owner |
|---|---|
| Text editing, selection/caret, formatting, parsing, nullable values, bounds, generic validation state | Bootstrap5WinFormUI input engine/control |
| Cell display, hosted editor lifecycle, native grid key routing and error rendering | BootstrapSourceGrid adapter or DataGridView cell/column implementation |
| Document/line identities, allowed fields, request-to-row mapping, active/history lookup rules | MyDmsVn desktop adapter/ViewModel |
| Save/Post authorization, concurrency, pricing/costing and database truth | Existing application/domain/server layers |

Do not make a generic control depend on Sales DTOs, `SaleLineId`, SQL, an application API client or permission names. The application may supply these as opaque identity/policy values where necessary. A common editing contract does not require a single implementation for two different grid engines.

## 2. Numeric entry: preserve information while improving presentation

### Existing behavior and why it matters

`BootstrapFormattedTextBox` already formats during text changes, maps caret/selection through raw positions, supports separator deletion and undo/redo, and exposes `RawValue`. Its raw value is a string. The built-in Numeral formatter sanitizes characters and truncates integer/fraction lengths according to options; that is a formatting contract, not strict financial validation. [Live formatting][ui-formatted] · [Numeral normalization][ui-numeral]

The application's [BCI specification](../BUSINESS_COMPACT_UI.md#7-numeric-input-and-display), [ReceiptNumberEditor][app-number-editor] and [ReceiptNumericInput][app-number-parser] deliberately use None mode. The parser checks the entire lexical candidate before decimal conversion, accepts insignificant zero padding, rejects excess nonzero precision and preserves invalid text. SAL inherits these rules. Turning on Numeral with scale 4 is not a safe replacement.

`BootstrapNumericBox` already wraps NumericUpDown with typed Value, range, increment and formatting properties. It is suitable for small bounded quantities/settings when its native semantics fit. It has no public preserved-invalid-buffer contract. `NumericUpDown.DecimalPlaces` changes display precision rather than enforcing the precision of Value. [NumericBox source][ui-numeric-box] · [Microsoft DecimalPlaces][ms-decimal-places]

### Proposed value model

Maintain three distinct concepts:

| State | Meaning |
|---|---|
| Pending lexical candidate | What the user is attempting to enter, including incomplete/invalid input and meaningful digits |
| Display text and selection mapping | The visible presentation, including generated group separators, with stable caret/range mapping |
| Committed typed value | The last successfully parsed and validated `decimal` or nullable value |

The committed model changes only after a successful commit boundary. A formatter must not silently turn an invalid candidate into an acceptable business value. Retain enough information to distinguish generated grouping from text entered or pasted by the user. A stock formatter's sanitized `RawValue` is not automatically the original lexical candidate.

For valid candidates, grouping can update as the user types. For invalid/intermediate candidates, preserve the editable representation and report its state; do not coerce it. Formatted display must round-trip to the same value and retain any information still needed to reject the attempted input.

### Proposed configurable policies

- Explicit culture snapshot for an edit session, including decimal/group separators and signs.
- Effective fractional precision and numeric bounds; optional integer-digit limits checked without truncation.
- Required/optional value and blank semantics. Optional blank means null, not zero.
- Negative/zero allowance supplied by the field profile.
- Grouping/display policy independent of accepted paste grammar.
- Overflow and precision behavior: reject for document money/quantity inputs; no silent rounding or clamping.
- Commit/cancel and validation events with pending text separate from committed Value.
- Input alignment, selection/first-character behavior and compact sizing through public configuration rather than private native-control traversal.

Suggested field profiles are application configuration, not hard-coded UI library business rules:

| Field | Existing contract to preserve |
|---|---|
| Receipt/sales quantity | Positive; maximum `99999999999999.9999`; effective scale 4 (`decimal(18,4)`) |
| Purchase unit cost / sale unit price | Nonnegative; maximum `999999999999999.9999`; effective scale 4 (`decimal(19,4)`) |
| Gift sale line | Keep price and full quantity; revenue zero by application rule; no formatter-driven alteration |
| Percent / integer settings | Separate future profiles with explicit limits; not assumed to share price rules |

The sales limits are defined in [SalesContractLimits][app-sales-limits]; generic input defaults must not invent narrower bounds.

### Numeric examples

The following examples use an en-US decimal separator; equivalent vi-VN cases use comma. The existing parser rejects user-supplied grouping. Supporting correctly grouped paste would be a deliberate extension, not an accidental result of enabling formatting.

| Input | Expected document-entry behavior |
|---|---|
| `1234.5` | May display `1,234.5` while editing under an approved live-display policy; commit exactly 1234.5 |
| `1.23000` | Accept as exactly 1.23 under existing effective-precision rules |
| `1.23001` | Preserve candidate and reject commit |
| A far-tail nonzero fractional digit | Reject before decimal conversion can round it away |
| `-`, `.`, empty required field | May exist temporarily; cannot commit as a valid required quantity |
| `-1` in a positive-only field | Reject; do not remove the minus sign and accept 1 |
| `1e3`, currency text or mixed separators | Reject under the current document grammar |
| `1,234` pasted into a vi-VN field | Interpret only under the explicitly selected culture/grammar; never guess by stripping punctuation |
| Value outside the SQL-compatible bound | Reject and retain text; do not clamp to a maximum |

Do not redefine existing Numeral behavior globally: current tests expressly cover its truncating behavior. Add an opt-in preserving policy or a dedicated typed composition, with existing defaults unchanged. Reuse proven caret/undo mechanisms where they fit; do not implement a second untested text engine merely to expose `Value`.

### Input experience to verify

Typing at the end is insufficient evidence. Check insertion in the middle, selection replacement, Backspace/Delete adjacent to generated separators, select-all replacement, paste, undo/redo, decimal keypad entry, focus changes, and live theme/DPI/font changes. Vietnamese IME/text composition needs explicit native evidence for text/lookup controls; numeric-only fields should define which input is accepted without corrupting composition or silently changing values.

## 3. Date and date-time entry

### Existing contracts are narrower than the requested input experience

`BootstrapCalendarPicker` is a sealed, painted, date-only selection control, with nullable `SelectedDate`, bounds, selection events and calendar popup. `DateFormat` formats its summary; it is not an input mask. `BootstrapDatePicker` wraps the native DateTimePicker. `BootstrapFormattedTextBox` Date mode shapes digit blocks and separators; its formatter clamps broad day/month components and does not establish that a calendar date exists. Existing tests retain `31/02/2026` as formatted text. [CalendarPicker][ui-calendar] · [Component contract][ui-components] · [Date formatter][ui-date-formatter] · [Formatter tests][ui-date-tests]

The requested fast input therefore needs a semantic parser and commit boundary. A proposed typed date input should compose the existing text foundation and calendar infrastructure. CalendarPicker is sealed and its hosted calendar/native popup implementation is private. It exposes `ShowDropDown()` and `CloseDropDown()` but no editable-text composition seam. Reuse BootstrapCalendar and the shared dropdown infrastructure inside the UI library, or add a reviewed public seam; avoid reflection or private-child access.

### Proposed date-only grammar

Use explicit day/month ordering for the Vietnamese document-entry profile. Keep display culture and accepted grammar intentional; do not rely on permissive `DateTime.TryParse` to decide an ambiguous input. Parse short forms, fill missing components from an injected reference date, then validate the completed calendar date with an exact parser or checked date construction.

Capture the reference date at the start of the edit session from the user's local current date. Completing a field across midnight must not unexpectedly change its month/year. Allow the host to supply another reference date for an explicitly approved business workflow; the default requested here is the current local date.

Examples below freeze the reference date at **22/10/2026**:

| Candidate | Proposed result at commit |
|---|---|
| `5` | 05/10/2026 |
| `05` | 05/10/2026 |
| `5/11` | 05/11/2026 |
| `05/11/2026` | 05/11/2026 |
| `0511` | Optional fast-entry form `ddMM`: 05/11/2026, if selected for the profile |
| `05112026` | Optional fast-entry form `ddMMyyyy`: 05/11/2026 |
| `31/11` | Reject; keep text and show a readable error |
| `29/2` | Reject for reference year 2026; accept for a leap-year reference when valid |
| Empty optional filter | Commit null; clear its applied criterion when the filter operation is admitted |
| Empty required document date | Keep pending empty text; reject commit/Save |
| Two-digit year or ambiguous short form | Reject until an explicit century/grammar policy is chosen |

Completion occurs on an admitted commit (Enter, Tab, successful focus leave or Save). Do not replace `5` with a full date after the first keystroke: the user may be entering another component. Display a subtle format hint when useful. Never silently roll an invalid date into the next month or clamp it to the last day.

The existing `IInputFormatter` Format/Unformat functions are also called on partial prefixes by caret mapping. Do not place current-date completion in those functions. A separate pure parser/commit step makes date semantics testable without focus/rendering side effects. [Caret mapper][ui-caret-mapper]

### Proposed keyboard behavior

| Context | Keys | Behavior |
|---|---|---|
| Text/segment editing | Left/Right | Move within text or selected day/month/year segments according to the selected input mode |
| Selected complete segment | Up/Down | Increment/decrement that segment using explicit bounds/invalid-date policy |
| Input field | F4 or Alt+Down | Open the calendar |
| Open calendar | Arrows | Preserve calendar navigation semantics, including Up/Down by week |
| Open calendar | Enter | Accept the selected date once; do not also submit the form |
| Open popup | Escape | Cancel popup preview and restore the pre-popup pending text/selection first |
| Field with closed popup | Escape | Cancel the pending field edit and restore the current committed model presentation |
| Field commit | Enter / Tab / Shift+Tab | Validate, commit once and route navigation according to the host |

The pre-popup pending text may differ from the committed model. The new composition must stage preview state if it promises popup cancellation: existing CalendarPicker `SelectionChanged` reports a logical selection, and `CloseDropDown()` alone is not a rollback operation. A second Escape with the popup closed can discard the field edit and reproject the current committed model.

Segment stepping must be distinct from popup calendar arrows and from grid row navigation. Default proposal: reject an increment that would produce an invalid day/month combination and preserve the existing candidate; any alternative clamp/carry policy must be explicitly selected and tested. Do not add mouse-wheel value changes to important date/quantity fields without deciding how accidental scrolling is prevented.

### Date-only is different from an instant

For the active `net48` application, document dates can continue to use `DateTime` at midnight with `Kind=Unspecified`, consistent with the current sales date-only contract. Do not assume availability of modern `DateOnly`, and do not apply UTC conversion to document dates.

Date-time input should reuse the engine but have explicit hour/minute/second segments, time-only completion policy, nullable rules, and timezone interpretation. Stock Card UTC filters must keep their existing semantics unless a separate change is selected. A date-only CalendarPicker is not a drop-in replacement for an instant-valued field. [Sales date contract](../DIRECT_SALES_QUERY_AND_UI.md) · [Stock Card source][app-stock-date]

Initial SME priority: typed date-only input for document dates and date filters. Add time/date-time UI when an actual screen requires it; do not postpone date-only improvements until a general date/time/timezone engine exists.

## 4. A common edit-session contract

### Existing mechanics to preserve

Both receipt adapters already protect edits with stable row identity, document generation, an original/candidate distinction and validation before mutation. Ordinary projection changes preserve the active editor. A removed row, replaced document or disposed session invalidates callbacks. These are demonstrated source mechanisms worth consolidating. [Sales session][app-sales-session] · [Goods session][app-goods-session]

A proposed shared edit-session component should accept:

- Opaque workspace/document identity and generation.
- Stable row key and field key, separate from a visible row/column index.
- Current committed value and pending text/selection state.
- Generic parse/validation policy.
- A caller-supplied editability predicate, rechecked before mutation.
- A validated apply callback and a current-model projection callback.

Possible outcomes are commit, reject-and-keep-editing, or cancel. Names and signatures are not finalized APIs. The app decides whether Save/Post/Back/RemoveLine is permitted; a generic component does not infer those business meanings.

Cancellation discards the pending candidate and reprojects the **current** authoritative model. It must not blindly restore an old captured value over a legitimate model change. Query-only lookup text is distinct from an uncommitted data change. [Existing session regression examples][app-session-tests]

### Host behavior

| Action | Proposed invariant |
|---|---|
| F2/double-click/typing | Begin only when editable; retain the initiating character |
| Valid commit | Parse/validate first, mutate model once, update totals from accepted value |
| Invalid commit | Keep text, caret, focus and the last valid model value |
| Tab/Shift+Tab | Commit, then move to the next editable visible cell; skip calculated/disabled fields |
| Enter | Commit or choose lookup item; never implicitly Post |
| Escape | Popup cancellation before cell cancellation; no accidental document command |
| Save/Post | Resolve pending header/inline edits before inspecting dirty state or sending a command |
| Delete/remove | Resolve target by stable identity; cancel removed-row callbacks |
| Refresh/rebind | Preserve active candidate unless an admitted document boundary replaces it |
| Busy/Posted/denied/unknown outcome | Guard all entry paths, including checkbox, keyboard and accessibility setters |

The seven Sales columns and duplicate Product rows remain unchanged. Use a stable client key for unsaved lines, including multiple lines whose persisted ID is zero. ProductId and visible STT are never line identities.

## 5. Validation in a grid

A proposed error store addresses `(document generation, stable row key, field key)` and distinguishes parse, field/business and document errors. An active reusable editor's invalid border is only one projection of that store. The inactive cell still needs an error indicator, accessible description and readable message.

Server paths such as `lines[index].field` must be mapped through the exact submitted request's ordered line snapshot to stable UI keys. Never apply a delayed response using today's visible row index after reorder/removal/reload. Preserve current contract JSON names rather than inventing new quantity/price field names in the UI adapter.

An error summary can navigate to the cell, ensure it is visible, and begin editing when allowed. Multi-field server errors should remain inspectable without forcing a single shared editor to carry every error. Document-level permission/concurrency/recovery failures belong to the Alert region and are not misleadingly painted onto every cell.

For native DataGridView use its cell/row ErrorText and validation lifecycle with the Bootstrap editor state. For SourceGrid use a stable error source projected through cell views/accessibility and the current editor. The shared contract is behavioral, not a requirement to pretend their internal APIs are identical. [Microsoft cell errors][ms-cell-errors]

## 6. What each grid actually needs

| Area | BootstrapSourceGrid | BootstrapDataGridView |
|---|---|---|
| Existing editors | Text, formatted text, lookup registry and adapters | Native standard columns plus existing `BootstrapLookupColumn` |
| Numeric/date additions | Adapt the proposed typed/preserving input through SourceGrid conversion and lifecycle seams | Add matching typed cell/column/editing-control adapters where needed |
| Lookup | Reuse existing adapter; standardize host query/binding policy | Reuse existing lookup column and editor reuse/key handling |
| Error presentation | Cell/row visual/accessibility projection from stable keys | Native cell/row ErrorText plus Bootstrap input state |
| Commit/cancel/navigation | Generic session helpers with SourceGrid-specific routing | Native DataGridView lifecycle and `IDataGridViewEditingControl`, not a copied SourceGrid controller |
| Shared work | Parser cases, semantic field profiles, identity/error contracts, acceptance scenarios | Same behavioral contracts, engine-specific tests |

The DGV lookup implementation already handles editor reuse, dirty notifications and keyboard ownership. Its event row/column context is not automatically a durable business key after data replacement. Treat improved identity support as a proposed seam, not a reproduced defect. [Lookup column][ui-lookup-column] · [Editing control][ui-lookup-editor]

For new DGV columns, verify bound decimal/null values, `Clone` behavior for configuration, reused editor reset, native validation/DataError behavior, new-row placeholder and read-only/hidden-column traversal. Microsoft describes the column + cell + editing-control structure required for hosting custom controls. [Microsoft custom cells][ms-custom-cells]

The current custom receipt numeric wrapper mainly contributes alignment, sizing, select-all and first-character handling. A narrow public configuration seam could eliminate it without exposing the inner TextBox or relaxing strict validation. [Receipt wrapper][app-number-editor] · [Existing BSG formatted adapter][bsg-formatted]

SourceGrid editor instances remain local to their owning grid, normally shared by column/configuration. Never share an adapter across grid instances. The registry disposes both its editor component and control, including editors never started; application-created custom adapters retain an explicit application disposal path. Sharing session contracts does not imply sharing editor instances. [Registry ownership and disposal][bsg-registry]

## 7. Paste, lookup and performance scope

### Paste

SourceGrid already has clipboard operations, but its native paste writes cells individually through `SetCellValue`, bypassing the normal StartEdit/EndEdit session. This is not a receipt-safe batch edit contract. [Native paste][sg-paste] · [Range application][sg-range-data] · [Editor setter][sg-editor]

Before enabling Excel-style paste, define destination columns, lookup-key resolution, maximum rows, duplicate handling, readonly behavior and failure semantics. Recommended first policy: parse/validate the entire proposed block into a detached candidate, report errors with row/column positions, then apply atomically to the in-memory draft. Persisting still requires explicit Save. Partial apply is a separate product choice, not a default fallback. Undo of a batch can follow once there is a real need and a bounded history design.

### Lookup

Reuse existing pending-text versus committed-key behavior. A generic asynchronous provider seam may standardize loading/no-results/error presentation, debounce/cancellation and latest-generation checks. Keep API calls, authorization, active-only versus historical display rules, and entity-specific matching in the app. Source replacement must not reset raw text/caret for an unchanged selected key.

For efficient SME entry, keyboard selection, code/name search, readable result columns and retain-on-error behavior are higher priority than a broad query language. Barcode submission and create-new-item flows should be explicit workflows if selected later.

### Performance

The current sales contract limits a document to 200 lines. [SalesContractLimits][app-sales-limits] Measure edit latency, repeated full projections and view allocations before introducing virtualization or a new grid base. Prefer incremental updates and retaining the active editor. Large search results already belong to bounded queries/paging, not unrestricted loading into every dropdown. No performance measurements or claims of tested capacity are made in this assessment.

## Source references

[ui-formatted]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapFormattedTextBox.cs#L152-L285
[ui-numeral]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Formatting/BootstrapNumeralInputFormatter.cs#L45-L138
[ui-numeric-box]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapNumericBox.cs#L116-L207
[ui-calendar]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapCalendarPicker.cs#L14-L273
[ui-components]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/docs/COMPONENTS.md#L981-L1054
[ui-date-formatter]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Formatting/BootstrapDateInputFormatter.cs#L29-L66
[ui-date-tests]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/tests/MyDmsVn.Bootstrap5WinFormUI.Tests/Formatting/BootstrapDateInputFormatterTests.cs#L10-L55
[ui-caret-mapper]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Formatting/InputCaretMapper.cs#L7-L28
[app-number-editor]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/ReceiptNumberEditor.cs#L9-L36
[app-number-parser]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/ReceiptNumericInput.cs#L11-L79
[app-sales-session]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs#L306-L441
[app-goods-session]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs#L321-L428
[app-session-tests]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/tests/MyDmsVn.Desktop.Tests/GoodsReceiptInlineEditorTests.Sessions.cs#L181-L285
[app-stock-date]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/StockCardControl.cs#L223-L248
[app-sales-limits]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Contracts/SalesContracts.cs#L7-L14
[bsg-formatted]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/2bd7874f7e80b22ef181763749c2a2938ab7f16e/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapFormattedTextBoxEditor.cs#L38-L93
[bsg-registry]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/2bd7874f7e80b22ef181763749c2a2938ab7f16e/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapSourceGridEditorRegistry.cs#L7-L111
[ui-lookup-column]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapLookupColumn.cs#L9-L82
[ui-lookup-editor]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/Internal/BootstrapLookupEditingControl.cs#L8-L176
[sg-paste]: https://github.com/chung6a8m/sourcegrid/blob/f4e457b43582bf01892f50bdc74aa480531e5944/SourceGrid/SourceGrid/Grids/GridVirtual.cs#L1434-L1454
[sg-range-data]: https://github.com/chung6a8m/sourcegrid/blob/f4e457b43582bf01892f50bdc74aa480531e5944/SourceGrid/SourceGrid/Common/RangeData.cs#L177-L196
[sg-editor]: https://github.com/chung6a8m/sourcegrid/blob/f4e457b43582bf01892f50bdc74aa480531e5944/SourceGrid/SourceGrid/Cells/Editors/EditorBase.cs#L197-L200
[ms-decimal-places]: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.numericupdown.decimalplaces?view=netframework-4.8.1
[ms-cell-errors]: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.datagridviewcell.errortext?view=netframework-4.8.1
[ms-custom-cells]: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-host-controls-in-windows-forms-datagridview-cells
