# DateInput Unified Compact — correction evidence

**Execution date:** 2026-10-24. **Scope:** UIN-D01–D03 / UIN-DA01–DA04 within UI-NEXT. Owner approved option 01 as a design before execution; acceptance of this running candidate is tracked separately below.

## Candidate identity

| Layer | Revision / identity |
|---|---|
| Shared UI PR #73 | `57a64b75b0c02e36e9988310580da13cedd7cd6e` caret; `26880237f94ce76a82e93d624afc6b2112f668a6` compact visual; `463a04223fd6177a7077dcf386660cc3438d0074` popup selection/focus; final `3ed8b4afb4206dfddd2c3dadf4c9cd0932309729` F4-trigger focus |
| BSG PR #18 | Final `0eb1f85e8d7ac918e3b24ee7ca937235d7f8e6e5`, pinning UI `3ed8b4a`; SourceGrid unchanged at `f4e457b43582bf01892f50bdc74aa480531e5944` |
| App PR #45 product/package source | Final `0ed03156186885ba895546518cec9d7d6d0b56dc`, pinning BSG `0eb1f85`; preceding app commits `615aae1`, `3ea61e9`, `68f715f` |
| Package | `.local/dateinput-compact-final2/package`, manifest SHA-256 `0A1447923AEDD3339FC6A56C3E2D7F0FE2A87D9BB4804EE1C4661F6FC06C4919`, desktop EXE SHA-256 `5C1B1A7AD2F06FC67EA44C61831E5FAFCE8026EFDC4E94CE2229CCC9D6472393`; manifest records clean source tree and `0ed0315` |

## Behavioral and integration runs

| Check | Actual result |
|---|---|
| D01 old UI behavior RED | Three new net48/x86 native cases failed on caret/selection (including selection reset to 0 and readonly mutation). These were behavioral failures, not discovery/build errors. |
| D01 after repair | New cases 10/10 and DateInput group 36/36 on both net48/x86 and net8.0-windows/x64. |
| D02 old visual RED | Two rendered/surface cases failed on the clipped summary and duplicate child chrome; two larger-font cases failed before the native font/height repair. |
| D02 final UI selection | 104/104 net48/x86 (`TestResults/dateinput-f4-trigger-net48/dateinput-f4-trigger-net48.trx`) and 104/104 net8.0-windows/x64 (`TestResults/dateinput-f4-trigger-net8/dateinput-f4-trigger-net8.trx`), zero skipped; includes DateInput, CalendarPicker, TextBox accessory and public API baseline. An earlier `SendKeys` Alt+Down failure also reproduced on pristine D01 baseline; the final affected runs passed. |
| Popup review regressions | Old UI failed 2/4 new cases on selection range and pointer dismissal focus. The `463a042` candidate passed 103/103 on each target but its first clean package failed Light/Dark 2/2 at F4-open → pointer-dismiss focus (`TestResults/dateinput-package-final-100/dateinput-package-final-100.trx`); SQL cleanup was verified. Exact `PostMessage` reproduction then failed 1/1 before the final UI fix, passed 3/3 after; the final 104/104 matrix includes it. |
| BSG pin | Final focused TextBox/Lookup editor/sizing tests 50/50 net48/x86 and 50/50 net8.0-windows/x64 (`vendor/MyDmsVn.BootstrapSourceGrid/TestResults/dateinput-bsg-final-net48` and `dateinput-bsg-final-net8`). An initial nested restore with central package management disabled failed in unrelated SourceGrid package resolution; the documented temporary BSG-root `<Project />` props isolation allowed both runs, then was removed. |
| App hosted | Final DateInput/Goods/Sales selection 19/19 net48/x86, zero skipped (`TestResults/dateinput-app-hosted-pin-final/dateinput-app-hosted-pin-final.trx`); Release app x86 publish completed. The new Sales From/To regression first failed 2/2 on old caret behavior (expected Month caret 4, actual 0), then passed 2/2 after UI D01. The added Goods/Sales required-header cases passed 2/2, asserting invalid Post denial, native Month caret through state projection and model change only at Save. |

The UI checks use the upstream STA/NUnit fail-fast and bounded `--blame-hang` path. App hosted checks use serialized STA, `WinFormsTestGuard`, finite waits and real native key messages. These selections do not assert any SQL business acceptance or full GUI suite.

## Packaged actual Login, native100

The designated non-production SQL test instance was supplied through `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with the owned `GuiSqlEnvironment` allocation and runtime/migration principal split. The bounded GUI-owned subprocess logged `BITNESS=32`, `UIA_DPI_CONTEXT=PerMonitorV2`, `MONITOR_SCALE=100;WINDOW_DPI=96;MONITOR_DPI=96`. Final selected Light/Dark theory rows were **2/2 passed**, zero skipped (`TestResults/dateinput-package-final2-100/dateinput-package-final2-100.trx`). Both authenticated through actual Login, covered independent Sales From/To, Sales and Goods required dates, two native Month Up steps per field with caret/focus assertions, F4 and pointer calendar open/cancel with focus recovery, and no Save/Post. Each row logged `APPLICATION_DRAINED=PASS`; both resource records ended `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`.

Observed window geometry at 96 DPI was 198×32 for each filter date (native text width 150, trigger 28×28), 248×32 for Sale date (text width 200), and 298×32 for Receipt date (text width 250). Trigger and native editor bounds did not overlap or exceed the field. Package test assembly SHA-256 was `BF0437DB2078386EF3231D867240B6689F95133AD295008944D665C0DEB8B799` at the final native100 run.

The first package attempt failed 2/2 because its UIA selector treated the DateInput's outer `Edit` as the native TextBox and compared the outer bounds against its trigger. A diagnostic run measured the real native editor at 150×18; the corrected test resolves the single owned `WindowsForms10.EDIT` HWND inside the semantic DateInput. A later attempt reached Sales required date and failed because `Sale date` also names a label and pane; selecting the exact `Edit` role removed that test ambiguity. Compile and focused diagnostic failures remain failures at their own test revisions; they are not counted as product passes. Every failed owned SQL allocation inspected after these runs also recorded verified database/principal cleanup.

## Remaining acceptance

Native125 Light/Dark, window/monitor 120-DPI measurement and dated Owner visual disposition remain pending until executed on the corrected candidate. UIN-D01/D02 have bounded implementation/test evidence above. UIN-D03 and UIN-DA03/DA04 cannot be marked fully accepted from native100 alone. Historical T13 and PR review records remain candidate-specific and unchanged.
