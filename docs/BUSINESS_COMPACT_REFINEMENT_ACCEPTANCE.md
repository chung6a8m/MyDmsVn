# Business Compact lookup and visual refinement — acceptance

**Status:** T00 COMPLETE / RED proof retained; T01 COMPLETE / focused hosted Windows GREEN ([evidence](evidence/bcf/20261021-t01-lookup-binding.md)). T02 COMPLETE / lookup gate PASS on frozen `67fff29` ([native matrix and attended evidence](evidence/bcf/20261021-t02-lookup-gate.md)); A01–A13 PASS within that input scope. Visual work NOT STARTED; full BCF/Owner and release acceptance remain ungranted. The unchanged-product failures remain in [T00 evidence](evidence/bcf/20261021-t00-native-typing.md).
**Prepared:** 2026-10-21. **Branch:** `v1a-pro`. **Planning baseline:** `6040d7bcfca698fc9daba8268bb94e4454e8eacb` (PR #40 merged).

## 1. Scope, authority and ordering

This ledger verifies the [BCF design](BUSINESS_COMPACT_REFINEMENT.md) and [active plan](plans/20261021-001-business-compact-lookup-and-visual-parity.md).
[TEST_POLICY](TEST_POLICY.md) owns execution; [TEST_STRATEGY](TEST_STRATEGY.md) owns coverage and [tests/README](../tests/README.md) owns commands.
BCF first repairs typing in `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`, then implements visual parity with the three Owner-provided mockups.
Inline Product is a regression/reference path; it is not a fourth reported broken control.
Phase 2 includes the Product catalog content, its inspector, the shared shell/theme and the detail-focused Goods Receipt workspace. UI text remains English.

BCF does not expand APIs, schema, permissions, modules, persistence or runtime targets. Product remains WinForms `net48`, shipped x86 Desktop, with existing Local contracts.
The completed [BCI acceptance](BUSINESS_COMPACT_ACCEPTANCE.md) remains historical evidence for its exact revisions and scope; BCF neither rewrites nor retroactively invalidates it.
Its totals and exceptions cannot establish BCF PASS. Operational release prerequisites remain unaccepted/unscheduled.

| Task | Required outcome | Admission to next phase |
|---|---|---|
| BCF-T00 | Deterministic native-key baseline proof; bounded regression failures retained | Tests fail for the intended binding/caret cause, not driver/focus errors |
| BCF-T01 | Minimal app binding fix and focused boundary regressions | New native-key cases and affected existing cases pass |
| BCF-T02 | Focused interactive Windows gate for all three controls | Mandatory lookup rows below PASS before T03 begins |
| BCF-T03 | Shared shell and Light/Graphite theme parity | Focused shell/theme evidence available |
| BCF-T04 | Product catalog content and inspector parity | Catalog comparisons and interactions verified |
| BCF-T05 | Detail-focused receipt, commands and layout parity | Receipt comparisons and affected workflow checks verified |
| BCF-T06 | Frozen candidate, integrated acceptance and Owner review | All obligations reconciled with actual evidence |

## 2. Baseline and provenance

| Item | Planning value | Evidence boundary |
|---|---|---|
| App baseline | `6040d7bcfca698fc9daba8268bb94e4454e8eacb` | Source review only; not a new native run |
| BootstrapSourceGrid | `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f` | Pinned integration remains the default |
| Bootstrap5WinFormUI | `aba102e33c48937fd92468791c287afda0a59e77` | DataSource reconciliation already preserves pending text |
| SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944` | Preserve editor ownership/lifecycle |
| Reported behavior | Filter loses a typed query; header typing can reorder characters and move caret | Owner observation plus source trace; Linux review is not native execution |
| Introduced-by status | Relevant filter/header binding existed before PR #40 | Repair existing defects; do not label them new PR regressions |
| Future native target | Interactive Windows, serialized STA, `net48`/x86, native 100% and 125% | NOT RUN until executed and recorded |

Current source anchors are [GoodsReceiptControl](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs), [AccessibleLookupBox](../src/MyDmsVn.Desktop.WinForms/AccessibleLookupBox.cs), [CatalogLookupViewModel](../src/MyDmsVn.Desktop.Application/CatalogLookupViewModel.cs) and [DebouncedAsyncAction](../src/MyDmsVn.Desktop.Application/DebouncedAsyncAction.cs).
The accessibility subclass only exposes pending/committed Help; it does not implement typing.
The filter reselects its committed value during ApplyState. Header binding reselects the same key, then restores only raw Text, without caret/selection preservation.
A canceled preceding debounce completes normally and its operation continuation can also call ApplyState before the newest response.
The exact observed `igu` order is timing-dependent. Tests assert correct input `gui` and selection state, not a hard-coded broken ordering.

## 3. Native-key proof contract

`GoodsReceiptLookupTypingTests`, `NativeLookupTypingDriver` and `GoodsReceiptLookupFixture` were implemented in the existing Desktop test project at `dbd079825f00fcf4b96294c90918cbaf712f7606`. T01 implements `GoodsReceiptLookupBindingTests` and extends the existing driver/fixture; exact source and output hashes are recorded in its evidence.
Keep stable BCF-A identifiers if implementation changes the test names. Existing `GoodsReceiptControlTests`, `InventoryAccessibilityAcceptanceTests` and relevant Session lookup tests are regression anchors.
Use the existing `StaTest`, `WinFormsTestGuard`, bounded message pumping and disposal conventions. No new general-purpose runner, gate manifest or process framework is required.

The driver must locate the actual native inner EDIT of the intended named control, verify its HWND/process ownership and native focus, and record failed focus preconditions separately from product failures.
Use actual per-character keyboard/`WM_CHAR` input to that focused EDIT, or owned-process `SendInput` with verified foreground ownership. Pump messages with finite deadlines between deliberate steps.
Read native selection with `EM_GETSEL` or an equivalent public native editor observation; assert caret position and selected range as well as Text, committed ID and HasPendingText.
Whole `.Text` assignment, `WM_SETTEXT`, UIA `ValuePattern.SetValue`, direct SelectedValue assignment and VM injection do not establish per-key typing/caret correctness.
They remain useful setup or separate accessibility checks, and must be labeled as such.

Use controllable delay/source completions to exercise both paths:

1. Type `g`, `u` while the first query is pending; cancel that earlier task, pump its completion/ApplyState, then type `i` while the newest query remains pending.
2. Type a character, complete its response and resulting binding, then type the next character; include the final operation-completion ApplyState.
3. Start with a null filter selection, and with existing same-selection Warehouse/Employee headers; repeat an unchanged-data notification while input is pending.
4. Move the caret into the middle and insert; select a range and replace it through real character input. Compare expected Text and native selection after each rebind.

A first isolated character normally reaches binding after the app debounce/response. Earlier canceled work or another state notification can cause an earlier refresh.
Do not infer a synchronous first-key reset from the report or treat a fixed sleep as deterministic coordination.
T00 must retain intended failing baseline evidence before the product fix; T01/T02 record passing evidence on their exact new candidate.
Compile failures, missing desktop, lost focus, failed ownership checks, hangs and skipped cases do not count as the intended failing regression proof.

## 4. Observable lookup obligations

A01–A03 retain T00 **RED** baseline evidence and T01 hosted GREEN. A01–A13 now **PASS the T02 lookup gate** on frozen `67fff29`, with four native contexts and separate attended evidence. Message-input, synthetic-source and final-snapshot limitations remain explicit in the T02 evidence; this is not visual/full BCF acceptance. “All three” means the filter plus Warehouse and Employee headers, with explicit per-control evidence.

| ID | Task | Observable acceptance | Required evidence | Status |
|---|---|---|---|---|
| BCF-A01 | T00–T02 | A single real character remains pending in each control after response binding and operation completion; null filter stays unselected | Native-key test, Text/selection/committed ID before and after binding | T00 RED retained; T02 PASS — 67fff29 |
| BCF-A02 | T00–T02 | Per-key `g`, `u`, `i` produces `gui`, with caret after the last character, when a preceding canceled task completes between keys | Controlled cancellation and bounded pumping; all three controls | T00 RED retained; T02 PASS — 67fff29 |
| BCF-A03 | T00–T02 | Middle insertion and selected-range replacement retain exact text, caret and range across response and unrelated state refresh | Native `EM_GETSEL` assertions, not Text-only checks | T00 RED retained; T02 PASS — 67fff29 |
| BCF-A04 | T01–T02 | Pending query never commits a new key, clears an existing header key, dirties the receipt by selection, or applies an unintended list filter | Control and VM/list-request assertions before explicit commit | T02 PASS — 67fff29 |
| BCF-A05 | T01–T02 | A slower old response cannot overwrite the latest pending query/results; cancellation completion cannot reset input | Source ignores cancellation deliberately; latest generation wins | T02 PASS — 67fff29 |
| BCF-A06 | T01–T02 | F4 and Up/Down navigate the owned popup without committing; Enter or explicit mouse result selection chooses the intended result once and changes the logical key/list filter exactly once | Owned popup/native keys/mouse and selection/command observations | T02 PASS — 67fff29 |
| BCF-A07 | T01–T02 | Escape restores/cancels presentation according to existing committed-selection policy; Tab and Shift+Tab preserve intended focus order and resolve input without accidental selection | Native keys, paste, outside-click/deactivation, focused HWND and resulting committed/pending state | T02 PASS — 67fff29 |
| BCF-A08 | T01–T02 | Optional null/active-only filter and unchanged header selections survive repeated option notifications; historical inactive selected header values remain visible but unavailable as new choices | Binding tests plus native pending-input checks | T02 PASS — 67fff29 |
| BCF-A09 | T01–T02 | New cancels query only after navigation admission; guarded receipt selection/Refresh retain pending state; admitted boundaries invalidate old results | Dirty guard, query generation and control state observations | T02 PASS — 67fff29 |
| BCF-A10 | T01–T02 | Denied/busy/Posted/RequiresPostOutcomeReload states preserve their existing edit/navigation restrictions; typing repair does not enable forbidden commits | Focused state cases; distinguish usable list filter from read-only header | T02 PASS — 67fff29 |
| BCF-A11 | T01–T02 | Disposal, sign-out/session replacement and document changes reject late lookup publications and cause no disposed-control callback or next-user contamination | Existing guard/lifecycle cases plus late source completion | T02 PASS — 67fff29 |
| BCF-A12 | T01–T02 | Pending/committed accessibility Help, historical labels and inline Product behavior remain correct; ordinary state refresh does not disturb inline editor ownership/input | Relevant existing accessibility/Product tests and focused regression selection | T02 PASS — 67fff29 |
| BCF-A13 | T02 | All mandatory native-key/binding rows pass on interactive Windows at native 100% and 125% in Light/Dark; attended per-key smoke covers all three controls; focus/exception/cleanup checks pass | Exact source/test hashes, actual TRX cases, separate attended observations, environment and cleanup | T02 PASS — 67fff29 |

BCF-A13 is a hard phase gate. Do not start T03 visual implementation with missing, failed, skipped, timed-out or unexplained lookup cases.
A Linux build/source review or an existing whole-string UIA test cannot satisfy it. No historical Warehouse Down exception transfers into BCF.
Use the smallest meaningful affected tests during T00/T01, then one focused T02 matrix; do not run the full Desktop project after every binding edit.

## 5. Visual references and observable parity

The Owner-provided references are design targets, not application screenshots or runtime proof:

| Reference | Target surface | Interpretation |
|---|---|---|
| [01-catalog-light.png](design-references/business-compact/01-catalog-light.png) | Light shell, Product catalog and inspector | Match hierarchy, spacing, table density, selection and inspector composition |
| [02-goods-receipt.png](design-references/business-compact/02-goods-receipt.png) | Light detail-focused Goods Receipt | Match receipt prominence, header/line/command composition and reduced list footprint |
| [03-catalog-graphite.png](design-references/business-compact/03-catalog-graphite.png) | Graphite shell, Product catalog and inspector | Match semantic palette and contrast; extend this palette to the receipt layout from 02 |

See the [reference index](design-references/business-compact/README.md) and BCF design for provenance and implementation interpretation.
Use the same synthetic data, selection, scroll position, theme and window size for side-by-side comparisons. Separate mockup-versus-app images from Light-versus-Graphite app comparisons.
Measure layout in logical pixels at 1680×900 and 1200×760; additionally verify 944×600 reachability. Record measured native DPI and physical capture dimensions separately.
Capture both native 100% and 125%; simulated resizing or manual font scaling is not native-DPI evidence.

| ID | Task | Observable acceptance | Required evidence | Status |
|---|---|---|---|---|
| BCF-A14 | T03/T06 | Shared Light shell matches reference 01: navigation, title/breadcrumb, content hierarchy, spacing and restrained chrome | Same-data side-by-side at both main logical sizes; native100/125 | NOT RUN |
| BCF-A15 | T03/T06 | Graphite shell/catalog matches reference 03 with readable text, borders, selection and focus; theme switching retains state | Same-data Graphite comparisons and interactive switch/focus observations | NOT RUN |
| BCF-A16 | T04/T06 | Product catalog content matches references 01/03: toolbar/search/filter, dense readable grid, selected row and inspector hierarchy | Light/Graphite screenshots plus working search/select/inspector commands | NOT RUN |
| BCF-A17 | T05/T06 | Goods Receipt matches reference 02 with detail as primary surface, compact receipt navigation, coherent metadata, inline lines and reachable commands/totals | Same-data Light comparison; Graphite receipt uses 03 palette and 02 layout | NOT RUN |
| BCF-A18 | T03–T06 | Hover/focus/selected/disabled/loading/empty/error states are coherent and distinguishable; native popups and inspectors remain readable | Both themes, keyboard focus, open lookup and representative state captures | NOT RUN |
| BCF-A19 | T05/T06 | Receipt New/selection/Refresh/Save/Post/line commands retain their guards; Back rejects dirty/pending data, busy and recovery states, allows clean Posted navigation and preserves access-denied clearing; admitted query-only Back cancels without committing | Focused workflow/native Back cases including Warehouse/Employee/Product mouse-down and abandoned clicks, invalid numeric data, Posted, access-denied and uncertain-Post observations | NOT RUN |
| BCF-A20 | T03–T06 | Layout works at 1680×900 and 1200×760 logical shell sizes; 944×600 keeps essential fields/commands reachable with intentional scrolling and no overlap | Bounds/reachability checks and captures at native100/125 in both themes | NOT RUN |
| BCF-A21 | T03–T06 | Typography, icons, contrast, numeric alignment and spacing reflect reference hierarchy without clipping labels, errors or native accessory controls | Measured layout plus reviewed screenshots and keyboard/accessibility checks | NOT RUN |
| BCF-A22 | T06 | Frozen build retains lookup gate and affected catalog/receipt behavior in the shipped host; actual Login/package checks are identified separately from hosted tests | Exact package/source/test hashes, scoped package results and cleanup evidence | NOT RUN |
| BCF-A23 | T06 | Every BCF row has reviewable evidence or an explicit Owner disposition; deviations from each mockup are enumerated and accepted | Final comparison index, actual case reconciliation and dated Owner acceptance | NOT RUN |

Static screenshots alone do not establish command, keyboard, focus, popup or validation behavior. Record those interactions separately.
Do not claim pixel identity across native WinForms controls; identify material deviations in hierarchy, geometry, density and palette and obtain an explicit disposition.
Reference fidelity does not authorize removing required fields, misleading disabled states or hiding validation/uncertain-Post information.

## 6. Execution scope and frozen acceptance

T00–T02 require only the new focused Desktop native-key/binding cases and affected existing control/Session cases selected from inspected source.
T03–T05 use focused shell/catalog/receipt layout and behavior checks appropriate to the changed surfaces.
T06 runs the complete Desktop.Session project once and the complete Desktop GUI project once per required native-DPI context, plus the scoped shipped-package/attended checks specified by the plan. Both themes must be exercised by the actual required cases.
Discover and reconcile the actual candidate's Facts/Theory rows; do not copy any historical count as an expected total.
A focused repair rerun after a failed broader run must retain that failure and identify the repaired scope; it does not silently become an unfiltered PASS.

Binding/visual documentation alone requires no SQL suite. Binding and visual implementation do not require the entire SQL suite unless they actually change a SQL-owned contract.
Where a scoped package workflow uses SQL, apply existing designated non-production allocation/ownership/drain rules and record independent assertions for that workflow.
Reuse existing bounded process helpers for package/unsafe probes; native host ownership and deterministic cleanup remain mandatory.
Record exceptions, DataError events, timeouts, process-tree/output drain and resource cleanup; unresolved cleanup blocks further GUI admission.

## 7. Evidence ledger — to be completed by execution

Keep baseline failures and later repairs as separate rows. Each outcome belongs only to its stated candidate and scope; lookup gate PASS does not establish visual/full BCF acceptance.

| Gate | App/test revision and hashes | Environment / actual selected cases | Result / evidence | Status |
|---|---|---|---|---|
| T00 baseline proof | Product/vendor unchanged from PR #40; test `dbd079825f00fcf4b96294c90918cbaf712f7606`; hashes in linked record | Interactive Windows/net48/x86, Light, hosted DPI 96 (OS AppliedDPI 120); 15/15 Theory rows executed | 15 intended product failures, no skip/timeout/abort; 15 clean ownership/STA/guard/drain records; [evidence and failed attempts](evidence/bcf/20261021-t00-native-typing.md) | COMPLETE / RED proof; T02 OPEN at that checkpoint |
| T01 focused repair | Source/test revisions and output hashes in [T01 evidence](evidence/bcf/20261021-t01-lookup-binding.md); vendor pins unchanged | Interactive Windows/net48/x86; hosted DPI 96, Light for new cases; 172 discovered/executed | 172 PASS, zero skips/failures/timeouts; 107 clean lookup guard/drain records; independent review admission race fixed with 3 RED→GREEN rows | COMPLETE / hosted GREEN; T02 OPEN at that checkpoint |
| T02 lookup gate native100 | Test/package source `67fff29`; exact separate binaries/hashes in [T02 evidence](evidence/bcf/20261021-t02-lookup-gate.md) | Windows/net48/x86; monitor96/window96 DPI; Light172 + Dark172; attended all three in both themes | 344 automated PASS, 214 clean measured lookup records; 1/1 real-package session PASS; human reports stable; independent no-inventory checks and owned SQL/process cleanup PASS | COMPLETE / A01–A13 input gate PASS |
| T02 lookup gate native125 | Same frozen test/package source and bytes; runtime/vendor unchanged from T01 | Windows/net48/x86; monitor120/window96 DPI; Light172 + Dark172; attended all three in both themes | 344 automated PASS, 214 clean measured lookup records; 1/1 real-package session PASS; human reports stable; independent no-inventory checks and owned SQL/process cleanup PASS | COMPLETE / A01–A13 input gate PASS |
| T03–T05 visual checks | TBD | Both themes; sizes/DPI/data/states TBD | Reference/app comparison index and deviations TBD | NOT RUN |
| T06 frozen Desktop | TBD | Actual discovered/executed cases and DPI contexts TBD | Broader run TRX/hashes/outcomes/cleanup TBD | NOT RUN |
| T06 package and attended review | TBD | Package hash, Login scope, theme/DPI/workflows TBD | Runtime observations, cleanup and Owner decision TBD | NOT RUN |

Each completed entry must identify app and test revisions independently, vendor pins, build/package hashes when applicable, OS/runtime/DPI/theme, actual executed case counts and outcomes, sanitized evidence and remaining limitations.
Native100/native125, hosted/package and automated/attended results remain distinct. Missing resources, skips and unexecuted cases remain NOT RUN or BLOCKED with a reason.
Final BCF acceptance belongs to the frozen candidate and approved UI scope; it does not establish production release readiness.
