# UIN-T08 — content-local busy presentation

Date: 2026-10-24. Scope: UIN-A09 in the UI-NEXT plan. Package Login, attended Owner review and release disposition remain in T13.

## Candidate and ownership

| Slice | Exact revision | Behavior |
|---|---|---|
| Bootstrap5WinFormUI | `d38f8bdb8da9e5d1727815925d214e2bcfadf76c` | `BootstrapBusyHost` wraps an unparented content control. Each `Begin(BusyPresentation)` returns a UI-thread-owned token. Overlay display waits 150 ms; newest live token supplies message/optional Cancel request. Existing spinner and shared debouncer own scheduling. Hidden/disposed/handle-recreated controls stop and restart presentation as appropriate. Reviewed additive API fingerprint: `22004ef5487e12d7e0cfc558c5a1cac7b822c16e205738bcea7ebcd32f6db8a5`. |
| BootstrapSourceGrid pin | `138971cc43fcb8934dc230e82532140baee9848d` | Pins the UI change. No BSG adapter or SourceGrid source change; SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. |
| App source and tests | `daf82eae59951d65d244b2213b2b14be88380cac` | `WorkspaceBusyBinder` projects aggregate VM state within originating workspace/document scope. Catalog, Sales and Goods wrap only their content region, leaving outer commands/recovery/status reachable. Existing VM and command admission remain authoritative. |

The local Release net48/x86 executable SHA-256 is `522D7D14B26249FBD5E21E9FFDA37A88ED8FADA0B28E6B6AE5CA98B3A8094A6C`; the GUI test DLL SHA-256 after the native100 candidate run is `0FCEAB1C52B18D11EDE628A1B7F7376D1D5E4F4F6406B3CD2E1671C9B0CE11F0`. This is a local build, not a packaged Login or release candidate.

## Verification

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. UI and BSG cover `net48/x86` and `net8.0-windows/x64`; app runs `net48/x86`. Actual TRX rows have zero skips. GUI tests use bounded STA and exception capture.

| Scope | Local TRX | Result |
|---|---|---|
| UI busy host, spinner, debouncer and public API gate | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/uin-t08-ui-final-net48/uin-t08-ui-final-net48.trx`; corresponding `uin-t08-ui-final-net8` TRX | 33/33 PASS on each target |
| BSG full compatibility after UI pin | `vendor/MyDmsVn.BootstrapSourceGrid/TestResults/t08-bsg-net48/t08-bsg-net48.trx`; corresponding `t08-bsg-net8` TRX | 256/256 PASS on each target; existing SourceGrid dependency warnings retained |
| Binder and Catalog/Sales/Goods GUI regression | `TestResults/t08-app-native100-final-candidate/t08-app-native100-final-candidate.trx` | 93/93 PASS on the final source commit, including Light/Dark native observer, Sales/Goods delayed overlay and admission assertions |
| Catalog native Light/Dark busy overlay | `TestResults/t08-native100-write-cancel/t08-native100-write-cancel.trx` | 2/2 PASS; independent PerMonitorV2 observer reported scale 100, monitor DPI 96 |
| Sales/Goods delayed overlays and admission | `TestResults/t08-sales-goods-overlay/t08-sales-goods-overlay.trx` | 2/2 PASS on current 100% session |
| App Release `net48/x86` build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, 0 warnings/errors before final evidence commit |

The first native125-labelled run was rejected by the independent observer because the physical display was still 100% (`MONITOR_SCALE=100`, `MONITOR_DPI=96`). Those two failed environment checks are excluded from native125 PASS. Final 125% measurement and same-candidate tests are pending a physical display switch.

Behavior covered: fast operation suppresses overlay; overlapping/out-of-order tokens retain the newest live message; Cancel invokes only the current callback and does not release the token; hide/dispose/handle recreation stop delayed scheduling; theme colors update while visible. Binder tests cover stale document completion, scope replacement and background invalidation dispatch. Catalog/Sales/Goods tests retain command locks before and during overlay and release it after completion. No code path restores caller-owned `Enabled` or focus, retries Post or treats Cancel as transaction rollback.

## Boundary

T08 source and native100 validation are complete; measured native125 remains pending. No SQL owner was needed because this task changes no persistence contract. Full package/Owner acceptance remains T13.
