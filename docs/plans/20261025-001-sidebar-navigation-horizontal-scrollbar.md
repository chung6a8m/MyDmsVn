# Sidebar Navigation Horizontal Scrollbar — Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. Follow repository instructions and the focused gates below; checkbox state belongs in this file.

**Goal:** Keep the left navigation free of horizontal overflow when opening, switching, closing and reopening workspaces, while preserving vertical scrolling, keyboard focus and existing navigation behavior.

**Architecture:** Correct the application-owned `BusinessCompactNavigation` layout/render boundary. Confirm the failing transition first, then make row creation and subsequent layout use the same actual viewport; update selection presentation without rebuilding rows if the reproduction confirms that selection-only rebuild is the trigger.

**Tech stack:** C# / WinForms / .NET Framework 4.8 (`net48`), xUnit, existing serialized STA GUI harness; no new dependencies.

**Spec:** The reported behavior and acceptance contract in sections 1 and 3, with the [two reviewed Owner screenshots](../design-references/sidebar-horizontal-scrollbar/README.md).

**Status:** COMPLETE within the Owner-selected SQL-free hosted-shell scope (2026-10-25). The isolated `codex/sidebar-horizontal-scrollbar` candidate has an exact pre-fix selection-only RED, source fix and focused hosted physical100/physical125 Light/Dark passes, pointer/capture runs and reviewed captures in [the Sidebar evidence record](../evidence/sidebar/20261025-sidebar-horizontal-scrollbar.md). The Owner selected only the fake-service shell; Desktop.App, actual Login, SQL, package and release acceptance were not exercised or inferred. This completed Sidebar scope remains separate from the planned Security workstream.

**Inspected baseline:** `chung6a8m/MyDmsVn@b9fe4756b8ec68fabca74b1fd9a8fb1899a10c3d` on `v1a-pro-ui-next`. The consumer gitlink is `vendor/MyDmsVn.WinFormsKit@c232a4c09dc0386a5bd8bb484a5c991acae827d5`. Re-read the branch and gitlink before implementation; record any relevant drift.

## Global constraints

- Keep `net48`, the existing package architecture, toolkit pin, public navigation API, 224/64 logical-pixel expanded/collapsed widths and fixed brand/session areas.
- `BusinessCompactNavigation` owns this layout. Do not change Bootstrap/WinFormsKit, Catalog layout, workspace close admission, authentication or persistence without new evidence establishing an affected seam.
- Preserve `AutoScroll = true`, the single-column layout, vertical reachability, native buttons, icons, tooltips, accessible names, disabled-item/ancestor admission and item-removal behavior.
- A hidden scrollbar alone is not a fix. Do not clip rows, disable all scrolling, hard-code a scrollbar width, use Win32 `ShowScrollBar` to mask overflow, or replace the native focus/scroll behavior.
- Follow [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [test commands](../../tests/README.md). Run only affected cases; no inherited SQL, full Desktop, SAL, UI-NEXT, release or attended-package gate.
- Markdown is UTF-8 without BOM, CRLF in the working tree and LF in Git blobs. Existing C# encoding conventions remain binding. Preserve archived records and original failure evidence.

## Review focus

1. A vertical scrollbar already exists before selection: both row bounds and the virtual horizontal extent must fit after selection (SBN-T00/T01).
2. Short/long menus, collapse/expand and small/tall windows change the viewport: rows converge without reentrant layout loops, and the last enabled item remains reachable (SBN-T01).
3. Selection updates must preserve focus, event count, accessible description and the custom selected accent, including clearing selection and nested items (SBN-T01).
4. The real Products navigation route must open `catalog-product`, reuse it, close it and reopen it without changing shell geometry (SBN-T00/T02).
5. Light/Dark and physical 100%/125% displays must be recorded separately; a virtualized `DeviceDpi`, a resized screenshot or a passing unrelated wide-layout test is not native-DPI evidence (SBN-T02).

## 1. Reported behavior and reproduction

| Reference | Observed state |
|---|---|
| [Main-01.png](../design-references/sidebar-horizontal-scrollbar/Main-01.png) | Empty workspace; navigation already has a vertical scrollbar; no horizontal scrollbar. |
| [Main-02.png](../design-references/sidebar-horizontal-scrollbar/Main-02.png) | Products tab open and Products selected; a horizontal scrollbar appears above the Operator/session area. |

Both images are 1186 × 753 pixels. This is image geometry, not an assertion about `Form.ClientSize`, monitor scale or the exact executable revision; those were not supplied.

Reproduce with the current shell: show an authenticated session at a size that requires vertical navigation scrolling, capture the zero-tab state, activate Products without resizing, then inspect the one-tab state. Repeat by opening an existing tab, opening a second workspace, closing Products, closing the last tab and selecting Products again. Separately test navigation selection with no workspace creation, and direct `OpenWorkspace` with selection unchanged, to distinguish a navigation rebuild from content-host layout.

## 2. Source findings and working hypothesis

The relevant source is local to this repository:

| Source at the inspected baseline | Verified behavior |
|---|---|
| [BusinessCompactNavigation.cs](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactNavigation.cs), lines 33–46 | `_itemsHost` is a `FlowLayoutPanel`, `TopDown`, `WrapContents = false`, `AutoScroll = true`; client-size changes call `LayoutRows()`. |
| Same file, lines 54–75 | `SelectedItem` calls the full `Render()`; `LayoutRows()` uses the host's actual `ClientSize.Width - Scale(8)`. |
| Same file, lines 94–168 | `Render()` disposes/recreates rows, calls `ResumeLayout()`, then normalizes row widths and restores focus. Creation initially uses the outer navigation width: labels `Width - Scale(16)`, buttons `Width - Scale(8)`. |
| [FoundationShellForm.cs](../../src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs), lines 245–275, 542–546 | Products selection calls `OpenCatalog(CatalogKind.Product)`; it requires a catalog factory and uses the key `catalog-product`. |
| Same file, lines 788–808 | The menu contains ten fixed items. There is no permission-filtered-menu API in this baseline; preserve current enabled/ancestor and removal semantics. |

**Working hypothesis, not yet runtime-confirmed:** with vertical scrolling already active, rebuilding rows from the outer width temporarily creates wider content than the host viewport. The first resumed flow layout can observe that oversized content; subsequent width correction and focus/scroll processing may leave a horizontal scroll extent visible. Pure selection currently invokes this path unnecessarily.

`LayoutRows()` already accounts for the reduced viewport by using `ClientSize`; this is not simply a missing scrollbar-width subtraction. Microsoft's [ClientSize contract](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.clientsize) excludes nonclient elements such as scrollbars. Do not subtract the vertical scrollbar width a second time. [ScrollControlIntoView](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.scrollablecontrol.scrollcontrolintoview?view=netframework-4.8) is also part of expected vertical reachability.

The existing post-render correction should normally cause layout to settle. Therefore SBN-T00 must measure whether the failure is actual row overflow, stale virtual extent, focus restoration, or a parent/client-size transition. No Windows/net48 runtime was available during planning, and the screenshots do not prove the event sequence.

**Chosen planning direction:** a local viewport-sizing/render fix. A replacement scroll host would have a larger keyboard/accessibility/lifecycle footprint and is not justified yet; hiding the scrollbar would leave the geometry fault unproven. If SBN-T00 disproves the hypothesis, record the measured cause and revise this section before implementing a different fix.

## 3. Acceptance contract

At settled message-loop checkpoints, before and after every relevant transition:

- `HorizontalScroll.Visible == false` and `AutoScrollPosition.X == 0`.
- Each visible row's horizontal outer extent, including margins and normalized for any scroll offset, fits within the host's actual client viewport and padding. Icons, selected accents, focus indicators and hit targets are not cropped; long text may use the existing AutoEllipsis behavior.
- Where the menu exceeds available height, vertical scrolling remains enabled and the complete last enabled row can be brought into view. The fixed brand and Operator/Theme/Sign out area stay accessible.
- Selecting a different admitted item updates old/new `Selected` flags, palette, accent and `AccessibleDescription`, and raises `SelectedItemChanged` once. Reassigning the same item raises no extra event. Clearing or removing selection retains existing semantics.
- Keyboard Up/Down/Home/End and nested Left/Right behavior continue to work; disabled ancestors cannot admit children. Selection does not reset vertical position unnecessarily or steal focus from workspace inputs.
- The Products route preserves one workspace per key and valid close/reopen disposal. No geometry workaround depends on Products data or changes the workspace/content width.

## 4. Implementation tasks

### SBN-T00 — Capture a deterministic failing transition

**Files:** create `tests/MyDmsVn.Desktop.Tests/BusinessCompactNavigationTests.cs`; extend [FoundationShellFormTests.cs](../../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs). Create `docs/evidence/sidebar/20261025-sidebar-horizontal-scrollbar.md` when actual measurements exist.

**Interfaces:** use existing `Navigation.SelectedItem`, `ICatalogControlFactory.Create(CatalogKind)`, `Workspace.RequestTabClose(TabPage)`, `StaTest.Run` and `WinFormsTestGuard`. Obtain the current host through `Navigation.Controls.OfType<FlowLayoutPanel>().Single()`; there is no public `ItemsHost` API. New helper and test names below are proposals until implemented and discovered.

- [x] Add `BusinessCompactNavigationTests.Selection_with_vertical_overflow_keeps_horizontal_scrollbar_hidden` as a Light/Dark theory. Mount the real control with a real HWND and enough grouped rows to require vertical scrolling. Assert the zero-selection precondition, select a different item at the same geometry, pump bounded layout/messages, then assert section 3. Exercise the focused row click path as well as the programmatic setter; label hosted click-handler invocation accurately.
- [x] Add `FoundationShellFormTests.Products_navigation_keeps_sidebar_vertical_only_through_open_close_and_reopen` as a Light/Dark theory. Reuse its authenticated `TestDesktopSession`, `ReadyApiClient` and notification helpers; inject a proposed recording `ICatalogControlFactory` returning lightweight Panels. The existing `CreateShell()` has no catalog factory and would not open Products. Assert `catalog-product`, reuse without another factory call, close/dispose/selection-clear, and a second factory call on reopening the same item.
- [x] Measure before rebuild, after row creation while suspended, after resume, after width normalization, before/after focus restoration and at the next settled UI turn. Record host client/display rectangles, scroll positions/visibility/ranges, every row's bounds/margins, actual form/navigation bounds, focus identity, and `Layout`/`ClientSizeChanged` order. Temporary diagnostic hooks stay test-local or are removed after investigation.
- [x] Run only the new methods against unchanged product source. Retain an intended RED for the reported horizontal-scroll invariant; a missing factory, compilation failure, absent focus, impossible geometry or zero-test filter is not that RED. Run the selection-only and direct-workspace controls from section 1 to isolate ownership.
- [x] Record the measured cause, exact product/test/toolkit revisions, environment and original failure in the evidence file. If no deterministic reproduction is obtained, retain NOT REPRODUCED and continue targeted diagnosis; do not apply a speculative scrollbar workaround.

**Exit:** a reproducible failing assertion and a measured local cause, or an explicit blocked diagnosis. This task grants no PASS and does not change production code.

### SBN-T01 — Correct the confirmed navigation layout boundary

**Files:** modify [BusinessCompactNavigation.cs](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactNavigation.cs); extend the two SBN-T00 test files and its evidence record. Shell production changes are not expected; require measured evidence before adding them.

**Interfaces:** retain existing `private void LayoutRows()`, `private void Render()` and `private void AddRows(IEnumerable<BusinessCompactNavigationItem> items, int depth, bool enabled)`. Proposed private helpers, only if required by the confirmed approach: `int GetRowWidth(Control row)` and `void RefreshSelectionPresentation()`. No new public API or vendor pin.

- [x] Add `BusinessCompactNavigationTests.Viewport_changes_preserve_horizontal_fit_and_vertical_reachability` for Light/Dark. Cover expanded/collapsed/expanded, short/tall/short host, add/remove rows, a long label and a nested expanded section. Assert the actual presence/absence of vertical overflow in the arranged states, row fit and last-row visibility; keep time/layout bounds finite.
- [x] Use a single viewport-based rule for initial labels/buttons and later row sizing. Base it on current `_itemsHost.ClientSize.Width`, host padding, row horizontal margins and the existing scaled 8-pixel trailing inset; clamp to a valid positive width. Do not use virtual `DisplayRectangle.Width` or subtract scrollbar width again. Keep labels and buttons within that rule before resumed layout can measure them.
- [x] Balance layout suspension in `try/finally`; normalize geometry before the first resumed layout and re-evaluate on actual client-size changes. If measurements prove another settling pass is necessary, use a guarded, idempotent layout path with a finite convergence check. Do not add unbounded `Layout` recursion, repeated resize nudges, arbitrary timers or a perpetual `BeginInvoke` retry.
- [x] If SBN-T00 confirms the selection-only rebuild trigger, replace only the `SelectedItem` setter's structural render with the proposed presentation refresh. Preserve admission and old/new model flags; update accessible descriptions including section state, apply palette and invalidate the custom accent. Keep native button instances, current focus and scroll position. Structural collection/property/expand/DPI changes retain the corrected render path.
- [x] Add `BusinessCompactNavigationTests.Selection_preserves_focus_metadata_and_event_count`: first/change/clear/same-item cases, correct old/new descriptions/accent invalidation, selected nested item without a visible row, and no unexpected focus/vertical-position reset. Use the existing nested-keyboard/removal test in section 5 for its disabled-ancestor and collection-removal coverage.
- [x] Rerun the new tests and the three existing selectors in section 5. Confirm the original RED is GREEN on the same geometry and save actual TRX/candidate evidence. Review the source diff for unintended shell, session, public API or dependency changes; commit the focused code/tests together.

**Exit:** the reported transition and viewport/selection boundaries pass with no hidden content, no UI exception and verified disposal. If the source finding changes, update the plan/evidence before expanding scope.

### SBN-T02 — Verify native contexts and close the plan

**Files:** the SBN-T00 evidence record and this plan; update the [execution index](README.md) only when the scope is complete. Add a test fix only to address a concrete gap from the focused run.

**Interfaces:** use existing `UiNextNativeScaleObservation.Observe(Form, CancellationToken)` in the new hosted tests to record the physical monitor; it reads `MYDMSVN_UIN_EXPECTED_SCALE` and uses the existing bounded observer. No new runner or global test manifest.

- [x] Run the focused selection in section 5 sequentially on physical 100% and 125% displays. Each new theory explicitly exercises Light and Dark and restores the original theme. Record requested and actual outer/client sizes, working area, observer monitor DPI and control `DeviceDpi`; refuse a silently clamped required viewport. Missing native context is BLOCKED / NOT RUN, not simulated PASS.
- [x] Check the real shell at its normal `Size = 1200 × 760` and constrained `MinimumSize = 900 × 600` outer sizes, recording actual client bounds. Repeat the screenshot sequence: zero tabs → Products → another tab → Products again → close/reopen → zero tabs. At each point check no horizontal bar, stable sidebar width and footer, and vertical access to Security administration.
- [x] Check mouse selection, keyboard traversal to the bottom, collapse/expand and theme toggle. Also open enough vertical space to remove the vertical scrollbar, then reduce it again. These observations concern Sidebar only; do not perform CRUD/Post or infer actual-Login, SQL, full product or release acceptance.
- [x] Retain matched, reviewed before/after captures and sanitized geometry/TRX summaries with source/test/vendor hashes. State whether each observation came from a hosted fake shell or a running application; keep unavailable real-app observations explicit. Do not commit credentials, connection details or raw proprietary artifacts.
- [x] Reconcile all acceptance bullets and focused test rows. Update checkboxes only from actual evidence, document any unresolved limitation, and route completed status through the execution index without reactivating archived work.

**Exit:** focused automated evidence and the native-context sidebar checks meet section 3; any required unavailable check stays open. Historical acceptance is unchanged.

## 5. Focused verification commands

Run from repository root on interactive Windows with the SDK in `global.json`, .NET Framework 4.8 targeting/runtime support and the exact recursively initialized submodule pins. Use serialized STA, `WinFormsTestGuard`, bounded waits and deterministic disposal. The selected tests use fake services and require no SQL connection.

The following three selectors exist at the inspected baseline:

```powershell
$project = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$existing = @(
  'FullyQualifiedName~FoundationShellFormTests.Closing_selected_workspace_allows_the_same_sidebar_item_to_open_it_again'
  'FullyQualifiedName~BusinessCompactLayoutTests.Navigation_preserves_nested_keyboard_selection_and_clears_removed_identity'
  'FullyQualifiedName~BusinessCompactRefinementTests.Shell_has_grouped_icon_navigation_and_compact_session_actions'
) -join '|'
```

After implementing and discovering the new tests, use:

```powershell
$new = 'FullyQualifiedName~BusinessCompactNavigationTests|FullyQualifiedName~FoundationShellFormTests.Products_navigation_keeps_sidebar_vertical_only_through_open_close_and_reopen'
$filter = $existing + '|' + $new
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }

dotnet test $project -c Release -f net48 --no-restore -p:PlatformTarget=x86 `
  --list-tests --filter $filter -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'Sidebar discovery failed.' }
# Inspect discovery: every intended method and theme row must be present.

$resultPath = 'TestResults/sidebar-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet test $project -c Release -f net48 --no-restore -p:PlatformTarget=x86 `
  --filter $filter --logger 'trx;LogFileName=sidebar.trx' `
  --results-directory $resultPath -- RunConfiguration.TargetPlatform=x86
if ($LASTEXITCODE -ne 0) { throw 'Sidebar tests failed.' }
```

For SBN-T00, set `$filter = $new` after implementing only its two methods. Before each native-context invocation, set `$env:MYDMSVN_UIN_EXPECTED_SCALE = '100'` or `'125'` to match the actual display, and restore the previous setting afterward. This variable declares a measured context; it does not change Windows scale. Rebuild the graph when switching architecture; do not reuse stale x64/x86 outputs.

Inspect actual nonempty discovery and TRX outcomes; compile success, omitted/skipped rows and earlier counts are not PASS. Do not select the entire `BusinessCompactRefinementTests` class: it contains unrelated wide-viewport and capture gates. Add existing close/disposal methods only if the patch actually changes that shared behavior.

## 6. Planning-commit verification and handoff

This planning commit verifies screenshot identity, source anchors, existing versus proposed APIs/selectors, local links, encoding and consistent current-work routing. It does not run application/SQL/GUI tests or claim the bug fixed. Archived UI-NEXT/SAL/BCF evidence, current runtime behavior and the WinFormsKit gitlink stay unchanged.

Implementation handoff: read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), the [execution index](README.md), this plan and [TEST_POLICY](../TEST_POLICY.md); start with SBN-T00. Keep runtime evidence separate from the planning baseline and update only this plan's bounded task state.
