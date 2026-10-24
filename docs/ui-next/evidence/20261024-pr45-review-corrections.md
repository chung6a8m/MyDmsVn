# PR #45 review corrections

Date: 2026-10-24. Scope: nine findings in the review of PR #45, bounded to the UI-NEXT workspace, receipt inputs, inventory feedback, and the pinned UI library. This record is a correction to the PR candidate, not a new production acceptance or a replay of archived gates.

## Candidate and changes

| Owner | Revision | Correction |
|---|---|---|
| Bootstrap5WinFormUI | `1bf0956b7fd637ff976f08c12be25f456addccfc` | Whole-value decimal input retains raw punctuation; quick-date reference is captured when a new candidate begins; DGV Tab navigation includes row boundary cells. Public API gate deliberately reviewed and approved the additive `SetRawCandidate(string)` fingerprint `5016acec10c7efdafe03387de2c8f182a2e96c49f473f1bcb8bc6e5932dfa6cb`. |
| BootstrapSourceGrid | `56dcfffb31c81ebd065724a70231c849a77c52d9` | Pins the UI revision; SourceGrid source remains unchanged. |
| MyDmsVn | `6b1a83155c63002dc66f3e8c42c032ecc24188b7` | Pins BootstrapSourceGrid and corrects accessibility decimal entry, Security close admission, navigation reselection, field-specific numeric validation, paste error clearing, successful inventory-query alert cleanup, and Goods date validation cleanup. |

The Security tab has no safe dirty-close admission, so its close affordance and close request are disabled. This preserves its in-progress edits until that admission is implemented. Inventory alerts clear only after a current authoritative successful query; late/stale operations cannot clear a newer alert.

## Bounded verification

Interactive Windows 10.0.26300.0, Release `net48` x86 for the app and `net48`/`net8.0-windows` for the UI library. Ordinary `dotnet test` produced the local TRX paths below. Test-session injection in GUI tests does not prove packaged Login.

| Scope | TRX or command | Result |
|---|---|---|
| UI decimal/date/DGV and public API gate, net48 x86 | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/review45-ui-net48-final/review45-ui-net48-final.trx` | 33/33 PASS, no skips. |
| Same UI scope, net8 x64 | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/review45-ui-net8-final/review45-ui-net8-final.trx` | 33/33 PASS, no skips. |
| Desktop workspace, Sales grid, Goods date, inventory query feedback, net48 x86 at native100 | `TestResults/review45-desktop-focus2/review45-desktop-focus2.trx` | 55/55 PASS, no skips. The measured monitor is 96 DPI / 100% with PerMonitorV2 observer context. |
| App accessibility decimal grouping regression | `TestResults/review45-accessible/review45-accessible.trx` | 1/1 PASS; invalid `1,2345` remains pending after accepted `1,234`, without changing the model. |
| Numeric draft patch/session | `TestResults/review45-session-final/review45-session-final.trx` | 6/6 PASS, no skips. |
| Shipped-form app build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, zero warnings and errors. Executable SHA-256 `AF25B006A1D364ADBBC8A29202185FE41E959B3EF2B1B550CA1A29484A5B667E`. |

The UI API gate first failed against the old T10 fingerprint and printed the new exported surface; after review, the approved fingerprint was updated and the focused gate passed. An attempted net8 x86 test host could not load the installed x64-only `hostfxr.dll`; the net8 x64 run above passed. A stale test build briefly discovered zero rows after the fingerprint edit; cleaning that net8 test target restored discovery, and the final TRX contains 33 executed rows. Neither attempted run is counted as PASS.

Final focused test DLL SHA-256: Desktop net48 `46AAAC017625164ABBB852C49415169AE7828AEA2FABA13C41A70A29D4B65583`, Session net48 `B327C5DD65A7A8A974714D24A16E82EFA154CEFBE7BC81C5B0889537ABBD680A`, UI net48 `514354D0B7C66EEA21A2FD34F81B3BF44F28C006B4A1A49A1BC6429D03BB3AD9`, and UI net8 `3B8E4C73E32ADD5D69827018F522D9C8E50A631AEAF310A36975D7F681A8D138`.

## Remaining boundary

Native125 recheck of this correction revision remains pending an interactive 125% display. This correction scope contains no SQL posting, production package Login, or release authorization. PR #45 and its two submodule PRs remain for review and merge decision.
