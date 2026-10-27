> **Archived on 2026-10-25.** Snapshot from `6ee6951c58f6e4ad92f991521ff5c553d2d22526`; original statuses, commands and candidate evidence below describe their recorded dates. Read the [archive boundary](../README.md) for later merge facts and retained limitations. This document does not schedule work; use the [current execution index](../../../plans/README.md). Local links were rebased for this location.

# DateInput Unified Compact — Correction Implementation Plan

> **Historical correction record.** The original implementation instructions below have been executed within the bounded scope recorded in [candidate evidence](../../../ui-next/evidence/20261024-dateinput-unified-compact-correction.md). Use the [current execution index](../plans/README.md) for new work; do not resume former PR branches or replay these tasks. Original inspection anchors, checklists, failed candidates and acceptance limits remain provenance.

**Goal:** Deliver the approved one-surface DateInput and preserve native caret/selection through segment stepping in the existing PR chain.

**Architecture:** Keep native text entry and the current calendar engine inside BootstrapDateInput. Repair logical caret mapping and internal chrome/trigger composition upstream, then update UI → BootstrapSourceGrid → MyDmsVn pins and verify the existing application hosts.

**Tech Stack:** App/test `net48`, Windows x86; UI/BSG `net48;net8.0-windows`; existing WinForms, theme/DPI/icon infrastructure and SourceGrid dependency.

**Spec:** [Owner-approved design](20261024-dateinput-unified-compact-design.md). **Historical task record:** [UI-NEXT roadmap](20261023-active-roadmap.md#active-dateinput-correction). **Acceptance:** [UIN-DA01–DA04](20261023-validation-and-handoff.md#dateinput-correction-acceptance).

## Global constraints

- Original delivery chain: app PR #45 on `codex/uin-t13`, UI PR #73 on `codex/uin-t10-dgv`, and BSG PR #18 on `codex/uin-t10-parity`. These are historical branches; inspected SHAs remain in the design.
- Option 01 is selected. From/To remain independent; preserve ordinary collapsed caret/selection instead of adding option 02's automatic segment selection.
- App/test `net48`, Windows x86; libraries retain `net48;net8.0-windows`. Native GUI runs require interactive Windows and existing serialized STA/exception/hang protection.
- Preserve raw date grammar, profile/bounds, reference-clock rules, pending/accepted separation, commit/cancel and all application editability/recovery guards.
- Use theme metrics, native text measurements and DPI scaling for the approximately 32-logical-pixel design. No fixed physical-height workaround or hard-coded palette.
- Preserve standalone CalendarPicker summary/popup behavior and generic TextBox assignment semantics. New private/internal helpers are implementation details, not presumed existing APIs.
- At the original documentation-only activation UIN-D01–D03 were NOT STARTED and runtime evidence was NOT RUN. The task rows and linked candidate evidence record later results; design approval alone never certified the running build.
- Preserve historical evidence and archived boundaries. No full SQL/full GUI/Session repetition without a concrete changed risk; follow [TEST_POLICY](../../../TEST_POLICY.md).

## Review focus

| Input or condition | Expected behavior and owning task |
|---|---|
| Short raw date and selection across separators | Normalization maps logical endpoints correctly, including collapsed and full-segment selections — D01 |
| Repeated Month/Year arrows, rollover and rejected steps | Stay in the same segment; retain frozen reference, invalid raw input and prior accepted value — D01 |
| Read-only/disabled input and IME composition | Programmatic arrow/trigger routes do not bypass native or app admission — D01/D02 |
| Child trigger open/cancel, focus, nullable empty and standalone picker | No caption leakage or hidden commit; normal standalone summary and popup behavior retained — D02 |
| Theme/font/DPI/host projection or disposal during pending input | Single readable surface and valid lifecycle without losing text/selection or opening a query — D02/D03 |

## File and responsibility map

| Repository | Existing paths to inspect/modify |
|---|---|
| UI | `src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapDateInput.cs`; `BootstrapCalendarPicker.cs` in the same folder only for a necessary internal embedding seam; `BootstrapTextBox.cs` only for a proven narrow chrome seam, not a generic Text setter workaround |
| UI tests | `tests/MyDmsVn.Bootstrap5WinFormUI.Tests/Controls/BootstrapDateInputInteractionTests.cs`, `BootstrapDateInputTests.cs`, `BootstrapCalendarPickerTests.cs`, `BootstrapCalendarPickerFocusTests.cs`; existing formatting tests remain regression anchors |
| UI documentation | `docs/COMPONENTS.md`, relevant compatibility/API documentation and baseline test if the public surface changes |
| BSG | `vendor/Bootstrap5WinFormUI` gitlink and its existing upstream/pin documentation; inspect its AGENTS and actual projects before choosing affected builds |
| App | `vendor/MyDmsVn.BootstrapSourceGrid` gitlink; `tests/MyDmsVn.Desktop.Tests/DesktopDateInputIntegrationTests.cs`, `SalesReceiptControlTests.cs`; existing bounded package tests for DateInput smoke |
| App hosts | `src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs`, `GoodsReceiptControl.cs` and its presentation partials only if integration needs a demonstrated sizing/binding repair |
| Planning/status | This plan, active roadmap, validation addendum and the new sanitized evidence record after execution |

Do not edit vendored source without committing the corresponding owning repository and updating its explicit pins. Keep SourceGrid at the inspected revision. Resolve current branches/worktrees before changing a detached submodule; preserve unrelated local changes and use isolated worktrees where needed.

## UIN-D01 — Preserve caret and selection through segment steps

**State:** VERIFIED in bounded [candidate evidence](../../../ui-next/evidence/20261024-dateinput-unified-compact-correction.md). **Depends on:** approved design. **Acceptance:** UIN-DA01.

**Interfaces:** Consume existing `OnEntryKeyDown(object? sender, KeyEventArgs e)`, `CurrentSegment()`, `SetCandidate(string candidate, bool pending)`, native `SelectionStart`/`SelectionLength` and `QuickDateParser.Step`. Produce the same public DateInput API with logical selection restoration before `PendingTextChanged`; do not invent a new application binding contract.

- [x] **Add failing native regressions** in `BootstrapDateInputInteractionTests`. Use the existing STA host and key-message helper. New method names below are proposed until implemented/discovered:
  - `DateInputCorrection_Repeated_segment_arrows_preserve_caret_and_selection`: Day/Month/Year × Up/Down, check date and selection after every key, native focus retained, accepted Value unchanged before commit.
  - `DateInputCorrection_Short_date_normalization_preserves_logical_segment`: raw `5/1`, Month caret, injected 23/10/2026 reference; Up gives `05/02/2026`, then `05/03/2026` with Month caret. Cover a selected whole segment and a range crossing a separator.
  - `DateInputCorrection_Rejected_step_preserves_candidate_and_selection`: invalid raw input, impossible Month/Year and min/max. These may already pass and remain characterization, not fabricated RED.
  - `DateInputCorrection_ReadOnly_and_IME_reject_segment_mutation`: assert raw text, accepted Value and selection unchanged on non-admitted arrow input. A native TextBox's ReadOnly setting alone does not protect a programmatic full-text replacement.
- [x] **Run the focused cases on the old implementation.** Record meaningful behavioral failures; missing prerequisites, compile errors and zero discovered tests are not expected RED. Preserve existing reference-clock rollover tests.
- [x] **Implement logical endpoint mapping.** Snapshot the active segment and selection endpoints before stepping; update text, then restore mapped native selection before notifying the host. Keep a collapsed caret collapsed. Retain existing date-step, reference-clock and commit rules; apply narrow guards where the new tests demonstrate a bypass.
- [x] **Run the focused DateInput selection on both UI targets** using the command guidance below. Check actual TRX invocations, exceptions and selected caret assertions; fix root causes without loosening assertions.
- [x] **Review and commit the upstream caret slice** with a message such as `Fix DateInput caret preservation during segment stepping`. Record exact source/test revisions and outcomes for the later evidence record.

## UIN-D02 — Implement the Unified Compact visual

**State:** VERIFIED in bounded scope after Owner keyboard-highlight finding on first candidate, rendered/package native100/native125 repair checks and dated Owner acceptance. **Depends on:** D01. **Acceptance:** UIN-DA02.

**Interfaces:** Preserve `Text`, `PendingText`, `Value`, `Profile`, `ReadOnly`, `ValidationState`, `BeginEditSession`, `ProjectAcceptedValue`, `TryParsePending`, `TryCommitPending`, `ShowCalendar` and existing events. Add only a justified internal composition/rendering seam if required. An icon-only mode is proposed behavior; it is not a current public property.

- [x] **Add observable rendering/layout and interaction regressions** in the existing DateInput and CalendarPicker test owners:
  - `DateInputCorrection_Embedded_trigger_never_renders_date_summary`: empty, 12th/22nd dates, direct pointer opening, keyboard opening, preview, acceptance and cancel. Observe the actual rendered trigger/layout; testing only an internal mode flag is insufficient.
  - `DateInputCorrection_Unified_surface_survives_theme_font_and_dpi`: one exterior border, full date/icon readable, preferred height stable around docking, validation and focus coherent, pending text/selection retained through Light/Dark and font/layout changes.
  - `DateInputCorrection_Popup_and_standalone_picker_remain_compatible`: F4/Alt+Down, Enter/Tab, Escape, readonly/disabled, accessibility action, disposal and standalone date summary/focus behavior.
- [x] **Run before the visual repair** and retain the genuine clipped-caption/duplicate-chrome failures. Existing compatibility cases may pass as characterization.
- [x] **Implement one outer surface and an embedded icon-only trigger** in the shared UI. Reuse the calendar engine, shared icon renderer, theme metrics and DPI helpers. Suppress child chrome only in this composition. Preserve raw input, popup snapshot/clock semantics and event order; do not hide date captions by assigning empty Text or manipulating private popup controls from the application.
- [x] **Build both UI targets and run affected DateInput/CalendarPicker cases.** Exercise actual Light/Dark native100/native125 plus larger-font layout. Recheck D01 and standalone picker behavior. If a shared TextBox rendering seam changes, add the narrowly affected default-TextBox regression selection.
- [x] **Update UI component/compatibility documentation**, review any public API change explicitly and run the existing API baseline gate. Do not change an API fingerprint merely to hide an unexplained mismatch. Commit the visual slice separately, e.g. `Unify DateInput surface and calendar trigger`.

## UIN-D03 — Pin, integrate and verify the running candidate

**State:** VERIFIED in bounded scope at exact final pins with physical native100/native125 package checks, owned cleanup and dated Owner visual acceptance. **Depends on:** D01/D02. **Acceptance:** UIN-DA03/UIN-DA04.

**Interfaces:** Consume the reviewed, pushed UI commit; BSG produces a gitlink update; the application consumes the resulting pushed BSG commit. Required header dates and nullable From/To profiles keep their current app-owned admission semantics.

- [x] **Commit/push the UI changes on PR #73's branch**, then update/commit/push BSG's nested UI pin on PR #18's branch. Inspect/build the affected BSG projects under its policy; report pre-existing restore conflicts honestly rather than modifying unrelated SourceGrid dependencies. Keep the SourceGrid pin unchanged.
- [x] **Update the app's BSG pin on PR #45's branch** only after the referenced commits are fetchable. Recheck the nested revision chain from the app checkout.
- [x] **Add hosted native regressions** in the existing app date test owners. Proposed method `DateInputCorrection_Sales_filter_arrows_survive_state_projection` covers both From/To, repeated Month/Year arrows, an unrelated ApplyState, no query until Apply, accepted filters after Apply and nullable clearing. Add a Goods/Sales required-header regression for caret, validation and Save/Post buffer admission; keep permission/busy/recovery and signout scrubbing anchors.
- [x] **Build app Release net48/x86 and run the focused hosted/date cases.** Inspect all current DateInput consumers before selecting additional host layout cases; Stock Card's native UTC date-time control is outside the change.
- [x] **Build one identifiable running package and execute bounded actual-Login DateInput smoke** for Sales From/To and Sales/Goods required dates, including field clipping, calendar open/cancel and keyboard steps. Use the existing GUI-owned bounded process/resource fixtures. Any necessary SQL uses the designated non-production test instance and existing ownership/cleanup; this does not select the full SQL suite or a complete SAL round.
- [x] **Observe the new visual in Light/Dark at measured native100/native125.** Record monitor DPI and window DPI separately, source/test/package hashes and actual client bounds. Obtain a dated Owner disposition for the corrected running visual; if unavailable, report that acceptance item as pending instead of declaring D03 complete.
- [x] **Write actual results** to `docs/ui-next/evidence/20261024-dateinput-unified-compact-correction.md` when execution occurs (use the real execution date if later). Do not create a placeholder PASS ledger. Update UIN-D/DA statuses, the relevant entry points and PR descriptions with exact correction revisions and limits; preserve prior evidence and failures. Commit/push the docs and app changes to PR #45. Do not merge PRs as part of the handoff.

## Focused command guidance

Commands below are templates verified against current project/fixture names, not claimed executions. Use interactive Windows, repository-required restore and the actual SDK/targeting packs. Inspect the upstream `AGENTS.md`, test project and lock configuration before restore. Build the current source before any `--no-build`; use locked restore when lock files are prescribed. Run GUI commands sequentially and check `$LASTEXITCODE` after each command. Distinct framework/architecture outputs must not reuse stale assemblies.

From the **UI repository root**, after successful restore:

```powershell
$uiTests = 'tests/MyDmsVn.Bootstrap5WinFormUI.Tests/MyDmsVn.Bootstrap5WinFormUI.Tests.csproj'
$uiFilter = 'FullyQualifiedName~BootstrapDateInput|FullyQualifiedName~BootstrapCalendarPicker'
dotnet test $uiTests -c Release -f net48 -p:PlatformTarget=x86 --filter $uiFilter --list-tests -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'UI discovery failed.' }
dotnet test $uiTests -c Release -f net48 -p:PlatformTarget=x86 --filter $uiFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=dateinput-net48.trx' --results-directory TestResults/dateinput-correction-net48 -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'UI net48 tests failed.' }
dotnet test $uiTests -c Release -f net8.0-windows -p:PlatformTarget=x64 --filter $uiFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=dateinput-net8.trx' --results-directory TestResults/dateinput-correction-net8 -- RunConfiguration.TargetPlatform=x64
if ($LASTEXITCODE -ne 0) { throw 'UI net8 tests failed.' }
```

Inspect discovery and each TRX for required new methods, Theory/TestCase rows and skips. Repeat the affected native cases at the measured scales/themes with distinct result directories; labels alone do not establish the environment. The DateInput filter includes existing reference-clock and formatter regressions; add an actual parser/API fixture filter only when needed and source-verified.

From the **MyDmsVn repository root**, after successful restore:

```powershell
$appTests = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$dateFilter = 'FullyQualifiedName~DesktopDateInputIntegrationTests|FullyQualifiedName~Goods_required_date_native_characters_keep_model_until_enter|FullyQualifiedName~Required_date_keeps_raw_edit_through_state_and_blocks_save_post_and_back_until_valid|FullyQualifiedName~Required_date_error_clears_after_native_enter_then_successful_save|FullyQualifiedName~Goods_authoritative_date_change_cancels_old_pending_candidate|FullyQualifiedName~Nullable_sales_date_filters_commit_clear_and_reject_invalid_range_without_query|FullyQualifiedName~Pending_sales_date_filter_survives_unrelated_state_projection_and_rejected_apply|FullyQualifiedName~DateInputCorrection_'
dotnet test $appTests -c Release -f net48 -p:PlatformTarget=x86 --filter $dateFilter --list-tests -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'App date discovery failed.' }
dotnet test $appTests -c Release -f net48 -p:PlatformTarget=x86 --filter $dateFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=dateinput-app.trx' --results-directory TestResults/dateinput-correction-app -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'App date tests failed.' }
dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86
if ($LASTEXITCODE -ne 0) { throw 'App build failed.' }
```

`DesktopDateInputIntegrationTests.cs` contains both a partial `GoodsReceiptControlTests` and the `DesktopDateInputIntegrationTests` class. Filtering only the latter misses the Goods methods in the same file. `SalesReceiptControlTests` supplies additional filter/validation anchors. The proposed `DateInputCorrection_` methods must actually exist and be discovered before they count as coverage. Select the actual bounded package method after source inspection; the hosted commands above do not prove actual Login.

## Completion and handoff

D01/D02 can be recorded as implemented/verified in their own bounded scopes while D03 remains pending. Complete the correction only when UIN-DA01–DA04 have truthful evidence/disposition on the final candidate. Report exact commits in all three repos, current pins, tests actually run, skips/blockers, package identity, Owner disposition and any cleanup limits. Merge order, after separate review/merge authorization, remains UI #73 → BSG #18 → MyDmsVn #45.
