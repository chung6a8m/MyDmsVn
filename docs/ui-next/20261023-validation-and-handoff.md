# UI Next — validation and cross-session handoff

**Date:** 2026-10-23
**Status:** T00 bounded contract and T01 focused hosted evidence recorded; A01/A02 partial, final acceptance open
**Authority:** [TEST_POLICY](../TEST_POLICY.md) remains the sole execution policy. This document selects behavior for the [new active roadmap](20261023-active-roadmap.md), not an additional test framework.

## Evidence boundary

The original documentation activation verified source references, plan/archive status, links, encoding and a docs-only diff. Subsequent [T00](evidence/20261023-t00-field-errors.md) and [T01](evidence/20261023-uin-t01-workspace-feedback.md) implementation evidence records focused product/session/host tests. Source test existence alone is not a PASS.

The old SAL-T07/T08 open native125, attended and Owner obligations remain in the [archive](../archive/20261023-pre-ui-next/README.md). They are neither prerequisites nor acceptance rows here. New UI Next checks use UIN-A identifiers and exact current candidates; they cannot change historical SAL/BCF results. Production prerequisites remain unaccepted and unscheduled.

## Acceptance matrix

All rows began **NOT RUN** at activation. Current states below separate focused evidence from full row closure.

| ID / task owner | Required observable behavior | Minimum meaningful evidence | Current evidence state |
|---|---|---|---|
| UIN-A01 / T00,T02 | Duplicate Code create/update retains attempted text, Conflict and `code` target; correct editor shows invalid/readable state; unrelated fields stay intact; general/authorization errors remain safe and visible | AT mapper/catalog + ST VM + focused Product native UI; delayed old response and relevant-field clearing | VERIFIED in task scope: [current-candidate AT/ST and Light/Dark native100/native125](evidence/20261023-uin-t00-t02-native-completion.md); package/Owner remains T13 |
| UIN-A02 / T01 | Concurrent A/B outcomes remain in originating workspace; switch/close/reopen/document/session changes reject stale results; hidden success retention is bounded; persistent recovery survives status/Toast changes | Deterministic captured-scope tests and real host lifecycle tests including pre-handle publication/UI dispatch | VERIFIED in task scope: [current-candidate ST and Light/Dark native100/native125](evidence/20261023-uin-t00-t02-native-completion.md); package/Owner remains T13 |
| UIN-A03 / T02 | Same current errors drive editor border, readable message, ErrorProvider, inactive cell/row and accessible summary; duplicate/zero-ID/reordered/removed rows cannot steal another line's error | Native Sales pilot + accessible keyboard navigation; submitted-index-to-stable-line mapping; R4 clearing regressions | VERIFIED in task scope: [current-candidate ST and Light/Dark native100/native125](evidence/20261023-uin-t00-t02-native-completion.md); package/Owner remains T13 |
| UIN-A04 / T03,T05 | Commit applies once after host acceptance; stale validation proof or a different native accepted value cannot apply; invalid buffer is retained; cancel projects current model; Save/Post/navigation resolve all pending buffers; locked routes cannot mutate | Both receipt adapter session tests, controlled late callbacks, native Enter/Escape/Tab/Back and existing admission tests | T03 VERIFIED for its recorded adapters: [native100 x86, 117/117 selected](evidence/20261023-uin-t03-inline-sessions.md). T05 new-control integration VERIFIED in its bounded source/native scope: [UI/BSG and Sales/Goods Light/Dark native100/native125](evidence/20261023-uin-t05-decimal-entry.md); T13 package/Owner review remains open |
| UIN-A05 / T04 | Decimal candidate is lossless; exact maxima/scale/sign/culture/null grammar match current app policy; meaningful far-tail digits reject before decimal rounding; generated grouping is distinguishable from pasted input | Pure parser/display vectors with fixed expected values, app-profile differential characterization and legacy formatter compatibility | VERIFIED in pure T04 scope: [two-target parser/projection, legacy formatter and app oracle evidence](evidence/20261023-uin-t04-decimal-core.md); native T05 verified separately under [UIN-A06](evidence/20261023-uin-t05-decimal-entry.md) |
| UIN-A06 / T05 | Numeric format-as-you-type retains caret/range, first key, middle edits, group-boundary delete, paste, undo/redo, IME and model commit semantics | Real native per-character control + BSG + Sales/Goods tests; Copy/Cut retain invalid pending selection; identical-display external grouped paste rejects; reuse across frozen profiles/disposal and no duplicate dirty notifications | VERIFIED in bounded T05 scope: [two-target UI/BSG, app net48 x86, measured Light/Dark native100/native125](evidence/20261023-uin-t05-decimal-entry.md); physical IME-language typing remains unobserved |
| UIN-A07 / T06,T11 | Day/day-month/full DMY completion uses captured local reference; valid leap/min/max/null handling; invalid dates/short years/compact forms do not normalize silently; popup/segment keys commit once; pending date survives projection | Pure clock-injected parser, native key/calendar tests, required headers and nullable Sales filter admission; Stock Card UTC regression | T06 required-header contribution VERIFIED in bounded source/native scope: [UI/BSG, Goods/Sales Light/Dark native100/native125 and Stock Card regression PASS](evidence/20261023-uin-t06-quick-dates.md). Nullable Sales filters remain T11 |
| UIN-A08 / T07 | All close routes use one admission; four catalogs track accepted baseline/dirty revision; explicit write outcome governs Save-close, never Task/IsBusy/errors; rejection retains text, refresh failure stays distinguishable, stale prompt/result cannot close newer edits; busy/recovery refuses and logout scrubs | Pointer-before-focus, Ctrl+W/accessibility, dirty create/update and unchanged close, duplicate/canceled/failed Save, saved-plus-refresh-failure, newer revision/document/session/reopened instance during async admission; disposal once | NOT RUN |
| UIN-A09 / T08 | Busy presentation is local, delayed and token-owned; old completion cannot hide a new operation; Cancel means request only; keyboard/UIA guards remain; no stale focus/Enabled restoration | Overlap/out-of-order/fast/faulted/hidden/disposed scenarios, native focus and existing admission regressions | NOT RUN |
| UIN-A10 / T09 | Semantic Panel/Label preserve layout/accessibility and override ownership; public nested/dynamic children receive app appearance, private children remain excluded, roles survive generic appearance refresh; theme/DPI preserve input/error/focus and release resources | Both themes/native100/125 on affected composition, long text/mnemonic/readability, repeated open/close/theme lifetime tests | NOT RUN |
| UIN-A11 / T10 | DGV decimal and BSG numeric hosts share value/error behavior while honoring independent lifecycle; existing lookup keeps keyboard, dirty and async semantics | DGV clone/recycled-editor/reset/new-row/null/readonly/DataError tests; BSG conversion rejection/registry disposal; common scenario vectors | NOT RUN |
| UIN-A12 / T11 | Four catalogs and existing Sales/Goods/stock workspaces use shared feedback/theme/close/busy rules; native exceptions are explicit; nullable filter binding retains pending text | Focused affected screen tests + multi-tab/session scenarios + bounded host smoke; no business/pagination/column changes | NOT RUN |
| UIN-A13 / T12 | Full TSV plan validates before mutation; reject invalid/stale/readonly/unsupported targets with no observable partial patch; accepted patch affects local draft once; measurements use real workload | Pure planning + ordinary-edit revision-before-first-notification and reentrant-observer/identity/guard tests + native paste; 1/50/200-line Sales observations with named environment, distributions and resource counts | NOT RUN |
| UIN-A14 / T13 | One exact candidate reconciles affected UI behavior, public APIs/pins, bounded actual-Login smoke and separate Owner observations; remaining limits are explicit | Selected final UI Next cases, exact artifact hashes, cleanup and dated bounded review; no imported old-plan completion | NOT RUN |

## Source-verified regression anchors

These files exist at the recorded base. Extend them where they cover the changed behavior; do not create redundant tests just to match a planned filename. NEW files in the implementation plan remain proposals until added/discovered.

| Area | Existing app paths relative to repository root |
|---|---|
| Error metadata | `tests/MyDmsVn.Server.Application.Tests/CatalogApplicationTests.cs`, `ApiResponseMapperTests.cs` in the same project |
| VM/session errors | `tests/MyDmsVn.Desktop.Session.Tests/DesktopViewModelBaseTests.cs`, `CatalogViewModelTests.cs`, `FoundationViewModelTests.cs` in that project |
| Catalog/shell | `tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs`, `FoundationShellFormTests.cs` |
| Strict numeric grammar | `tests/MyDmsVn.Desktop.Tests/ReceiptNumericInputTests.cs` |
| Both inline sessions | `tests/MyDmsVn.Desktop.Tests/GoodsReceiptInlineEditorTests.Sessions.cs`, `SalesReceiptGridTests.cs` |
| Current R4 and admission | `tests/MyDmsVn.Desktop.Tests/SalesReceiptControlTests.cs`, `SalesReceiptLookupNativeInputTests.cs`, `SalesReceiptControlAdmissionTests.cs` |
| Accessibility/layout | `tests/MyDmsVn.Desktop.Tests/GoodsReceiptInlineAccessibilityTests.cs`, `SalesReceiptAccessibilityTests.cs`, `BusinessCompactLayoutTests.cs`, `BusinessCompactRefinementTests.Catalog.cs`, `BusinessCompactRefinementTests.Receipt.cs` |
| Bounded packaged host | `tests/MyDmsVn.Desktop.Tests/PackagedGuiAcceptanceTests.cs`, `PackagedSalesAcceptanceTests.cs`, `GuiOwnedProcessTests.cs` |

Preserve current Sales cases `Resolved_numeric_validation_clears_footer_and_provider_without_clearing_operation_feedback`, `Save_commits_real_numeric_buffer_before_snapshot`, `Post_commits_raw_cell_then_rejects_new_dirty_state_without_post` and `Back_rejects_invalid_raw_edit_without_native_focus_validation_or_loss`. They establish useful regression intent; their existence alone does not establish a run on a future UI Next candidate.

For upstream control/editor work, use the exact test projects and existing anchors named by each task. UI and BSG retain both supported TFMs. Read their current test policies, API baseline and public seam docs; do not copy app-only test commands into an upstream repository or silently drop its compatibility obligations.

## Verification scope by slice

| Change | Select first | Broaden only for |
|---|---|---|
| Field metadata/pure scope logic | Focused Application/Session tests and affected build | A changed shared error/session contract affecting other callers |
| Native binder/host/tab/busy/theme | Focused guarded STA tests and both themes; native DPI cases for changed geometry/input | Shared shell lifecycle or actual package composition affected |
| New numeric/date parser | Deterministic pure grammar/value/display vectors and legacy compatibility | Changed shared formatter/history seams |
| Native input and grid adapter | Real per-key native controls, host lifecycle and affected receipt regression | Shared keyboard/popup/clone/editor ownership changes |
| Staged paste | Pure planner, guarded atomic application and native entry | A demonstrated shared draft-notification/totals risk |
| Documentation activation | Diff, source/path/link/status/encoding and archive preservation | Executable source/configuration also changes |

A field metadata or theme edit does not select the old SAL six-SQL-owner fresh/reuse programme. No database schema/valuation change is planned. If implementation discovers that it needs such a change, stop that expansion and record a separate scope decision; do not hide it inside a UI task.

### Native interaction and accessibility

Use interactive Windows with serialized guarded STA, finite waits and existing exception/DataError capture. For changed numeric/date/lookup interaction, type actual characters and observe native edit focus, raw candidate, caret/range and committed effects; `.Text = ...`, `WM_SETTEXT` or UIA SetValue alone is not typing proof. Include pending IME composition where the control participates in text entry.

New controls and their affected app composition require Light/Dark at measured native100 and native125. Record actual monitor DPI, client geometry and test kind. Use supported compact content sizes sufficient to expose clipping/focus/errors; do not import the archived wide125 1680-logical visual-reference gate. A missing native context is BLOCKED/NOT RUN for the **new affected cases**, not an excuse to label them passed or to resume the old SAL task.

Keep hosted control tests, actual native-scale observations, actual-Login packaged automation and human observations distinct. Errors need readable text, accessible names/descriptions and keyboard summary navigation; color/ErrorProvider alone is insufficient. Observe focus order, Enter/Escape/Tab once-only behavior and cancel without data loss.

### Commands and discovery

Use source-verified existing projects and actual implemented test names. Restore locked dependencies before `--no-restore`, and rebuild the selected current source before `--no-build`. Examples below are templates, not claimed executions or permanent acceptance counts:

```powershell
$project = 'tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~DesktopViewModelBaseTests|FullyQualifiedName~CatalogViewModelTests' `
  --logger 'trx;LogFileName=ui-next-session.trx' `
  --results-directory ('TestResults/ui-next-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Focused session tests failed.' }
```

Select newly implemented scope/parser cases only after source inspection and nonempty discovery. Follow [tests/README](../../tests/README.md) for actual app x86/native execution parameters and resource prerequisites; follow upstream policies for hang protection and dual-target verification. A successful empty filter, skipped mandatory case or compile failure is not PASS or an intended RED.

### Final UIN-T13 scope

Freeze app/vendor/test/package identity. Reconcile A01–A13, then run affected App/Session cases and the changed native UI scenario set on that candidate. Use one complete Desktop.Session run if shared scope/admission changed broadly; select the affected Desktop GUI cases and explicitly justify additional breadth. This does not require an automatic all-ten-project, all-SQL or full historical GUI matrix.

Use bounded actual-Login package smoke for changed composition: Product duplicate Code, Sales/Goods numeric/date admission, tab routing, busy/close and unresolved-outcome presentation. Existing business fixtures may be reused as regressions. SQL-backed smoke keeps the designated non-production connection, positive allocation/token ownership, sequential owners and verified worker/process drain; never substitute production data or automatic prefix cleanup.

Record short attended observations for the new input/feedback/close experience separately from automation and obtain dated bounded Owner disposition. Old SAL attended/native125/frozen acceptance remains open independently. UI Next verification is not release acceptance.

## Evidence record and task-state protocol

Create sanitized records under `docs/ui-next/evidence/YYYYMMDD-uin-tNN-description.md` as work executes. No placeholder file implies a test was run. Each record must contain:

1. Task/acceptance IDs, changed behavior and exact app/UI/BSG/SourceGrid/test/package revisions or hashes.
2. Environment, target, native DPI/theme/client bounds where relevant, actual commands and selected/discovered/executed cases.
3. Expected versus observed behavior, PASS/FAIL/BLOCKED/NOT RUN, skips/omissions, original failures and scope of repair runs.
4. Owned resource/process/SQL cleanup, unresolved resources and sanitized artifacts; no secrets/raw proprietary identifiers.
5. Compatibility/API/pin changes, remaining limits, review disposition and the next ready task.

Keep implementation state (NOT STARTED / IN PROGRESS / VERIFIED), evidence state and Owner acceptance separate. A partial native100 pass does not close a row with required native125 behavior. A later relevant change invalidates only affected evidence, not every historical run. For pure refactoring, existing characterization can pass before extraction; RED is required for new failure behavior where meaningful, not manufactured by weakening a test.

## Activation handoff

At the original planning boundary all T00–T13 implementation states were NOT STARTED and all A01–A14 runtime states were NOT RUN. T00–T02 and A01–A03 are VERIFIED in their bounded task-owned scope, with [current-candidate evidence](evidence/20261023-uin-t00-t02-native-completion.md). T03/A04 are [VERIFIED for current Goods/Sales adapters](evidence/20261023-uin-t03-inline-sessions.md) at native100. T04/A05 are [VERIFIED for pure parser/projection and application policy](evidence/20261023-uin-t04-decimal-core.md); T05 native control/adapter and receipt pilots are [VERIFIED in their bounded source/native scope](evidence/20261023-uin-t05-decimal-entry.md). T13 still owns final package/Owner review and A14 remains NOT RUN. T06 is [VERIFIED for its bounded source/native scope](evidence/20261023-uin-t06-quick-dates.md); T11 still owns nullable Sales filters, and T07 is the next task in table order. The original activation PR contained the source refresh, selected contracts, bounded implementation plan, archive and authority/link updates only.

Before publishing the docs PR, verify exact base/target branch, documentation-only changed paths, six archived plans plus frozen old index, preservation of historical bodies/results except archive notices/link relocation, local links/anchors, proposed API labels, UTF-8 without BOM/CRLF working tree and normalized LF Git blobs. Record actual publication/verification outcomes in the PR; never claim a push or PR before its remote result is confirmed.
