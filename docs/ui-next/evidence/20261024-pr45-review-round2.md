# PR #45 review round 2 corrections

Date: 2026-10-24. Scope: two remaining P2 findings in the local second review of PR #45. This is bounded correction evidence; prior UI-NEXT acceptance and production-release boundaries remain separate.

## Candidate

| Owner | Exact source revision | Change |
|---|---|---|
| Bootstrap5WinFormUI | `c91993f4f44a60210f4c6c4106e82ebc69931a0a` | Capture a fresh quick-date reference date when a successful Up/Down segment step starts a pending candidate, then retain that reference through later text input and commit. The public API fingerprint is unchanged. |
| BootstrapSourceGrid | `02888dec75878a081f2caac9b22829ae3a2f7d72` | Pin the UI correction. No SourceGrid source change. |
| MyDmsVn | `af5fa93715b618585b340e062cb470bf1d0c7f9e` | Pin BootstrapSourceGrid. After a corrected Sales cell commit clears a paste issue, refresh cell validation colors and notify the full host so summary/ErrorProvider update synchronously. Other cell issues remain. |

## Verification

Interactive Windows, Release net48/x86 app and UI net48/x86 plus net8.0-windows/x64. Tests use actual TRX and serialized WinForms STA.

| Scope | Local TRX or command | Outcome |
|---|---|---|
| Native Up arrow across year rollover, before UI fix | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/review2-date-red/review2-date-red.trx` | Expected regression failure: parsing `5` after an Up-started candidate returned 05/12/2026 instead of 05/01/2027. |
| Same date regression after UI fix | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/review2-date-green/review2-date-green.trx` | 1/1 PASS. |
| Full hosted Sales paste correction before app fix | `TestResults/review2-paste-red2/review2-paste-red2.trx` | Expected 2/2 regression failures: Quantity cell view stayed red after native correction (`1→1` and `1→2`). |
| Same Sales regressions after app fix, native keyboard/Enter | `TestResults/review2-paste-native/review2-paste-native.trx` | 2/2 PASS. Asserts corrected cell color, remaining UnitPrice color/summary, cleared LineGrid ErrorProvider, and accepted model value. |
| Affected UI DateInput tests, net48 and net8 | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/review2-ui-date-net48-125/review2-ui-date-net48-125.trx` and corresponding `review2-ui-date-net8-125` TRX | 19/19 PASS per target, no skips. These runs had no independent DPI probe; the subsequent app probe measured 100%, so the directory names do not establish native125 evidence. |
| Affected app Sales control/grid/native input and monitor probe at 100% | `TestResults/review2-app-sales-125/review2-app-sales-125.trx` | 112/112 PASS, no skips. Despite the directory name, PerMonitorV2 observations in the TRX show `MONITOR_SCALE=100;MONITOR_DPI=96;WINDOW_DPI=96`. |
| App Release x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, zero warnings/errors. Executable SHA-256 `DE63084E8E2BE45E7101CD472A37C75447BCF19FB553D0C2E579B48580972FB0`. |

Focused test DLL SHA-256: Desktop net48 `E7837FECACC1BB9FC1BCE121BDA4ED351B8236B427147C44CEA096E583EF7E50`; UI net48 `A8622F375574A910F49704D3956F7E77F6016B0824E9B39EF309035E6F30D8F3`; UI net8 `A7C01BDF2399B86D6793A9018FF555D419CDA740ECB64AF3B3FF6FB68812D610`.

## Boundary

Native125 recheck on this correction revision is pending a measured 120-DPI interactive display. No new packaged Login, SQL posting, or production-release acceptance is claimed. PR #45 and dependency PRs #18/#73 remain open for review and merge decision.
