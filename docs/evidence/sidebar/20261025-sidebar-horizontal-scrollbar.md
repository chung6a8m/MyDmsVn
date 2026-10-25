# Sidebar horizontal scrollbar — candidate evidence

**Date:** 2026-10-25 (Asia/Bangkok). **Scope:** SBN-T00/T01 and focused SBN-T02 hosted native100/native125 checks. **Status:** implementation candidate; focused hosted native100/native125 checks passed. Running-application observation remains OPEN.

## Candidate and environment

- Base product `46e3dd7438fe869fc49277650cc5b2076d2b36a8` on `v1a-pro-ui-next`; product source commit `bc8918c`, test commit `a93e3665abd18350348a9f6fe5bf5fb9e361894d` on `codex/sidebar-horizontal-scrollbar`.
- Consumer toolkit gitlink `c232a4c09dc0386a5bd8bb484a5c991acae827d5`; nested SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Both unchanged.
- Windows interactive desktop, SDK `8.0.426`, `net48`, x86 test target. The first run used physical monitor `DISPLAY1`, 1920×1080, work area 1920×1032, 96×96 DPI/100%. The second used the same monitor at physical 120×120 DPI/125%, work area 1920×1020. Both were confirmed by the PerMonitorV2 observer. Shell navigation `DeviceDpi` was 96 in both contexts. `MYDMSVN_UIN_EXPECTED_SCALE` was explicitly set to the corresponding actual scale for each run and restored afterward.
- No SQL, package, actual Login, running application or attended observation was performed. The test shell uses an authenticated fake session and a panel catalog factory.

## Original failure and measured cause

The unchanged product source passed the three existing selectors (4 executed rows, 0 failed/skipped). The new Light/Dark hosted navigation theory failed on the original horizontal-scroll invariant before selection (2 failed rows, 0 skipped; local TRX `TestResults/sidebar-t00-red-geometry/sidebar-t00-red-geometry.trx`). At the settled failure point the host client was 207×419, its virtual display rectangle was 216×1124, and its first label/button had already been corrected to width 199. The horizontal bar was visible while `AutoScrollPosition.X` was 0. This demonstrates a stale horizontal virtual extent after initially oversized rows; it is an earlier form of the reported geometry fault, not a captured Products-tab transition on the unchanged source. The hosted row path used `Button.PerformClick()`; it was not a physical mouse click. The selection-preservation test separately failed against the unchanged selection path because the first button instance was disposed/recreated.

The source now sizes labels and buttons from the actual host client viewport before resumed layout, uses one width rule for subsequent changes, and performs a settling layout after normalization. Selection changes update existing buttons' descriptions/palette/accent without structural rebuild. No toolkit, shell production, public API or package change was made.

## Focused verification

Command: locked restore of `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj`, followed by `dotnet test` Release/net48/x86 with a filter selecting `BusinessCompactNavigationTests`, `FoundationShellFormTests.Products_navigation_keeps_sidebar_vertical_only_through_open_close_and_reopen`, and the three existing plan selectors. The native100 TRX is local at `TestResults/sidebar-native100-final/sidebar-native100-final.trx`; the native125 TRX is local at `TestResults/sidebar-native125/sidebar-native125.trx`.

- Final native100 hosted run: 11 executed rows, 11 passed, 0 failed, 0 skipped. New cases: Light/Dark initial and post-selection horizontal fit (2), Light/Dark viewport/vertical reachability (2), selection focus/metadata/event count (1), Light/Dark Products route/close/reopen (2). Existing selected cases: 4 rows passed.
- Native125 hosted run on the same source/test candidate: 11 discovered and executed rows, 11 passed, 0 failed, 0 skipped. The observer reported monitor 120×120 DPI/125%, physical normal window 1500×950, physical client 1480×901, work area 1920×1020, window DPI 96; the navigation control reported `DeviceDpi=96`. Requested and actual logical outer sizes were 1200×760 and 900×600 in both themes; no required viewport was clamped.
- Shell observation in both themes at native100 and native125: normal logical outer 1200×760, client 1184×721, navigation width 224, host client 207×545, footer `Y=609 Height=88`; constrained logical outer 900×600, client 884×561, navigation width 224, host client 207×385, footer `Y=449 Height=88`. At native100 the observer measured normal physical window 1200×760 and physical client 1184×721; at native125 it measured 1500×950 and 1480×901. The horizontal-fit assertion passed at zero tab, Products, second tab, close and reopen; vertical overflow was present at constrained size.
- The Products test verified one `catalog-product` tab, factory count 1 on reuse, page disposal/selection clear on close, and factory count 2 on reopen. The separate selection test verified stable button instance/focus, accessible descriptions and event counts; the existing nested-keyboard/removal test passed. The viewport test covered collapse/expand, long nested row, vertical last-row scrolling, short/tall host, row removal/addition and return to vertical overflow. Test guard captured no UI exceptions in new cases.

## Open verification

- The running application screenshot sequence, attended mouse/keyboard/theme review and matched before/after captures were **NOT RUN**. Hosted fake-shell observations do not establish actual Login or package behavior.
- The unchanged-source RED was captured at zero selection rather than the exact Products transition. Initial/final state and the distinct selection rebuild failure were measured, but the requested staged diagnostic timing (suspended/resumed/focus event order) was not instrumented.
- These gaps keep the remaining SBN-T02 observations and full plan acceptance open. No archived acceptance or production-readiness conclusion is inferred.
