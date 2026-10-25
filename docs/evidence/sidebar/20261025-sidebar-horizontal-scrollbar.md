# Sidebar horizontal scrollbar — candidate evidence

**Date:** 2026-10-25 (Asia/Bangkok). **Scope:** SBN-T00/T01 and Owner-selected hosted-shell SBN-T02 checks at native100/native125. **Status:** pre-fix staged diagnosis and focused hosted verification completed within this scope.

## Candidate and environment

- Base product `46e3dd7438fe869fc49277650cc5b2076d2b36a8` on `v1a-pro-ui-next`; product source commit `bc8918c`, hosted capture test revision `0664a880d5d5a3359efb93d92ef51768dd72d587` and final focused test revision `664e3af47771f6dcf7a14a198e870f3657bb0439` on `codex/sidebar-horizontal-scrollbar`.
- Consumer toolkit gitlink `c232a4c09dc0386a5bd8bb484a5c991acae827d5`; nested SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Both unchanged.
- Windows interactive desktop, SDK `8.0.426`, `net48`, x86 test target. The first run used physical monitor `DISPLAY1`, 1920×1080, work area 1920×1032, 96×96 DPI/100%. The second used the same monitor at physical 120×120 DPI/125%, work area 1920×1020. Both were confirmed by the PerMonitorV2 observer. Shell navigation `DeviceDpi` was 96 in both contexts. `MYDMSVN_UIN_EXPECTED_SCALE` was explicitly set to the corresponding actual scale for each run and restored afterward.
- The Owner selected the SQL-free hosted shell for this follow-up. No SQL, package, actual Login or running Desktop.App observation was performed. The real `FoundationShellForm` uses an authenticated fake session and a panel catalog factory.

## Original failure and measured cause

The unchanged product source passed the three existing selectors (4 executed rows, 0 failed/skipped). A test-only diagnostic in a temporary worktree at exact base `46e3dd7` then captured the reported **selection-only** transition at physical100: zero-selection settled with host client 207×436, virtual display 207×1124, vertical bar visible, horizontal bar hidden and `AutoScrollPosition.X=0`. Selecting another item without opening a workspace made the host client 207×419, virtual display 216×1124 and horizontal bar visible. The assertion failed as intended (1 failed, 0 skipped; local `TestResults/sidebar-baseline-diag/sidebar-baseline-exact-red.trx`). Direct `OpenWorkspace` without changing selection passed its no-horizontal-bar control (1 passed, 0 skipped; local `TestResults/sidebar-baseline-diag/sidebar-baseline-direct-control.trx`). The temporary test and diagnostic hook were not added to the candidate source.

The staged trace recorded `ControlAdded` width 216 for buttons and 208 for section labels while the viewport was 207. `ResumeLayout` produced `Layout → ClientSizeChanged (height 436→419) → Layout`, with virtual width 216 and the horizontal bar visible. After width normalization, rows were 199 wide, but the virtual width and horizontal bar remained 216/visible before focus restore, after focus restore and at the settled turn. Focus returned to `Item 0`; navigation/form width remained 224/916 outer, and scroll X stayed 0. The local stage trace is `TestResults/sidebar-baseline-diag/sidebar-baseline-staged.txt`; the temporary staged probe failed the same horizontal assertion. This isolates stale virtual extent from the content host and focus restoration.

The earlier Light/Dark hosted navigation theory also failed the horizontal invariant against unchanged product source (2 failed, 0 skipped; local `TestResults/sidebar-t00-red-geometry/sidebar-t00-red-geometry.trx`), though that trial failed at its initial preselection checkpoint. The hosted row path used `Button.PerformClick()`; it was not a physical mouse click. The selection-preservation test separately failed against the unchanged selection path because the first button instance was disposed/recreated.

The source now sizes labels and buttons from the actual host client viewport before resumed layout, uses one width rule for subsequent changes, and performs a settling layout after normalization. Selection changes update existing buttons' descriptions/palette/accent without structural rebuild. No toolkit, shell production, public API or package change was made.

## Focused verification

Command: locked restore of `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj`, followed by `dotnet test` Release/net48/x86 with a filter selecting `BusinessCompactNavigationTests`, `FoundationShellFormTests.Products_navigation_keeps_sidebar_vertical_only_through_open_close_and_reopen`, and the three existing plan selectors. The native100 TRX is local at `TestResults/sidebar-native100-final/sidebar-native100-final.trx`; the native125 TRX is local at `TestResults/sidebar-native125/sidebar-native125.trx`.

- Final native100 hosted run: 11 executed rows, 11 passed, 0 failed, 0 skipped. New cases: Light/Dark initial and post-selection horizontal fit (2), Light/Dark viewport/vertical reachability (2), selection focus/metadata/event count (1), Light/Dark Products route/close/reopen (2). Existing selected cases: 4 rows passed.
- Native125 hosted run on the same source/test candidate: 11 discovered and executed rows, 11 passed, 0 failed, 0 skipped. The observer reported monitor 120×120 DPI/125%, physical normal window 1500×950, physical client 1480×901, work area 1920×1020, window DPI 96; the navigation control reported `DeviceDpi=96`. Requested and actual logical outer sizes were 1200×760 and 900×600 in both themes; no required viewport was clamped.
- Shell observation in both themes at native100 and native125: normal logical outer 1200×760, client 1184×721, navigation width 224, host client 207×545, footer `Y=609 Height=88`; constrained logical outer 900×600, client 884×561, navigation width 224, host client 207×385, footer `Y=449 Height=88`. At native100 the observer measured normal physical window 1200×760 and physical client 1184×721; at native125 it measured 1500×950 and 1480×901. The horizontal-fit assertion passed at zero tab, Products, second tab, close and reopen; vertical overflow was present at constrained size.
- On capture test revision `0664a88`, the complete focused selection passed 11/11, 0 failed/skipped at native100 (`TestResults/sidebar-native100-complete/sidebar-native100-complete.trx`) and 11/11, 0 failed/skipped at native125 (`TestResults/sidebar-native125-complete-v3/sidebar-native125-complete-v3.trx`). Each physical context also ran the separate foreground mouse/capture selector: 2/2 Light/Dark passed, 0 failed/skipped (`TestResults/sidebar-capture-native100/sidebar-capture-native100.trx` and `TestResults/sidebar-capture-native125/sidebar-capture-native125.trx`). The latter used a real pointer hit check and mouse input on the Products button; the regular focused run used the programmatic setter so other windows cannot invalidate the geometry test.
- Review follow-up `664e3af47771f6dcf7a14a198e870f3657bb0439` strengthened the selection test to assert that a negative vertical scroll offset survives change and clear while a workspace input retains focus. This exact revision passed 11/11, 0 failed/skipped at native100 (`TestResults/sidebar-native100-reviewed/sidebar-native100-reviewed.trx`) and 11/11, 0 failed/skipped at physical125 (`TestResults/sidebar-native125-reviewed/sidebar-native125-reviewed.trx`). The product source remains `bc8918c`; the earlier capture results remain applicable because this follow-up changed only the separate selection test.
- The hosted Products test verified one `catalog-product` tab, factory count 1 on reuse, page disposal/selection clear on close, factory count 2 on reopen, second-tab close and final zero tabs. Security administration was scrolled fully into view; native posted End/Home keys reached Security/Products. Collapse/expand and two theme toggles preserved fit and footer bounds. The separate selection test verified stable button instance/focus, workspace-input focus, hidden nested selection, accessible descriptions and event counts; the existing nested-keyboard/removal test passed. The viewport test covered long nested rows, vertical last-row scrolling, short/tall host, row removal/addition and return to vertical overflow. Test guard captured no UI exceptions in new cases.

## Reviewed hosted captures

The following captures show the zero-tab and Products-selected states at the same constrained logical shell size (900×600) in each theme/physical scale. All use the owned-window physical screen capture path and contain only the fake `Operator` identity. The original Owner screenshots are report references, not geometrically matched captures from this candidate.

| Context | Zero tabs | Products selected |
|---|---|---|
| Native100 Light | [capture](captures/hosted-zero-tabs-native100-Light.png) | [capture](captures/hosted-products-native100-Light.png) |
| Native100 Dark | [capture](captures/hosted-zero-tabs-native100-Dark.png) | [capture](captures/hosted-products-native100-Dark.png) |
| Native125 Light | [capture](captures/hosted-zero-tabs-native125-Light.png) | [capture](captures/hosted-products-native125-Light.png) |
| Native125 Dark | [capture](captures/hosted-zero-tabs-native125-Dark.png) | [capture](captures/hosted-products-native125-Dark.png) |

At native100 the capture client was 884×561 pixels with 96 DPI. At native125 it was 1105×701 pixels with paint/monitor DPI 120; the WinForms shell still reported control DPI 96. Reviewed images show the vertical bar, selected accent, full sidebar width and fixed footer without a horizontal bar. The test-local capture directory also retains second-tab, reopened Products and final-zero-tab frames, which were not committed.

SHA-256 for the eight reviewed, committed captures, in the table order above (zero/Products per row):

| Context | Zero tabs | Products selected |
|---|---|---|
| Native100 Light | `45BD5B6E9FBEB8FE5AD982DE5D7507F4C89571C5DB1C55B58A547A629D63525D` | `AA0CAFCEE4448A0A0EC2C54B645F781990B4511F8F3389D60F0F01F6CCFDF172` |
| Native100 Dark | `69B78CFC7088BFB89E0ACE437E320E91F684179977AE0C2AF63A025DC13A8D8A` | `5D41270680AF158E1DE16EBC4CD3A993E6DB09D101927C8F29694DA0F4224ED9` |
| Native125 Light | `4B6EE636DC790AC0D02BBA5BC5296A981940DDA3EA3C0A08E94A8143A2446B38` | `2AC1E6CCD49429A7388BA4E388D58A03A1C4C200A013B9ED7A56C9CD5C7668F4` |
| Native125 Dark | `D3A6B1CC1CDB4DFC6BE9DC65613CC74C7B389202CD60709822FA31944654DDB8` | `43C7141628F3E1FE6D20E726DFF7926E0E979163D0409DED31D6D30A188C08B4` |

Two intermediate full-filter attempts failed in both theme rows because the foreground physical-capture probe refused a window occluded by another process, then because the physical mouse hit check found an occluded target. These were environment-sensitive capture failures, not horizontal-fit failures. The capture and mouse path was therefore isolated into its own opt-in foreground selector (`MYDMSVN_SIDEBAR_CAPTURE=1`); both final focused and capture invocations passed at the recorded candidate.

## Open verification

- Running Desktop.App, SQL-backed Login and package behavior were outside the Owner-selected hosted-shell scope; no result is inferred for them.
- The precise pre-fix RED was selection-only on a synthetic 24-row menu, not a Products route against a SQL-backed Desktop.App. The hosted Products route was verified on the fixed candidate. This is the Owner-selected UI boundary; no actual-Login, SQL, package or release acceptance is inferred.
