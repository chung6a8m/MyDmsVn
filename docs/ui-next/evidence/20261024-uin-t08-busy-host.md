# UIN-T08 — content-local busy presentation

Date: 2026-10-24. Scope: UIN-A09 in the UI-NEXT plan. Package Login, attended Owner review and release disposition remain in T13.

## Candidate and ownership

| Slice | Exact revision | Behavior |
|---|---|---|
| Bootstrap5WinFormUI | `d38f8bdb8da9e5d1727815925d214e2bcfadf76c` | `BootstrapBusyHost` wraps an unparented content control. Each `Begin(BusyPresentation)` returns a UI-thread-owned token. Overlay display waits 150 ms; newest live token supplies message/optional Cancel request. Existing spinner and shared debouncer own scheduling. Hidden/disposed/handle-recreated controls stop and restart presentation as appropriate. Reviewed additive API fingerprint: `22004ef5487e12d7e0cfc558c5a1cac7b822c16e205738bcea7ebcd32f6db8a5`. |
| BootstrapSourceGrid pin | `138971cc43fcb8934dc230e82532140baee9848d` | Pins the UI change. No BSG adapter or SourceGrid source change; SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. |
| App source and tests | source `daf82eae59951d65d244b2213b2b14be88380cac`; final test revision `c711163f510314930f52043b42ff6b61390a87df` | `WorkspaceBusyBinder` projects aggregate VM state within originating workspace/document scope. Catalog, Sales and Goods wrap only their content region, leaving outer commands/recovery/status reachable. Existing VM and command admission remain authoritative. |

The local Release net48/x86 executable SHA-256 is `848A7BB9E092CB3A794958E9F3EA75D22CA36C834C0EC5D5BB5C97975D794902`. The GUI test DLL SHA-256 was `5F2B57ED5098A8344EEEB7740D8241AEC83C5D9C398DE586BFBDABB9FD293914` for native125 and `916D0BAE85A8C7A5E951FC0AADAE1E785E893875B6F57EB9C4B16C2BFE458F9D` after rebuilding the same test-source revision for native100. This is a local build, not a packaged Login or release candidate.

## Verification

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. UI and BSG cover `net48/x86` and `net8.0-windows/x64`; app runs `net48/x86`. Actual TRX rows have zero skips. GUI tests use bounded STA and exception capture.

| Scope | Local TRX | Result |
|---|---|---|
| UI busy host, spinner, debouncer and public API gate | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/uin-t08-ui-final-net48/uin-t08-ui-final-net48.trx`; corresponding `uin-t08-ui-final-net8` TRX | 33/33 PASS on each target |
| UI focused repeat in the measured 125% session | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/t08-ui-native125-net48/t08-ui-native125-net48.trx`; corresponding `t08-ui-native125-net8` TRX | 33/33 PASS on each target |
| BSG full compatibility after UI pin | `vendor/MyDmsVn.BootstrapSourceGrid/TestResults/t08-bsg-net48/t08-bsg-net48.trx`; corresponding `t08-bsg-net8` TRX | 256/256 PASS on each target; existing SourceGrid dependency warnings retained |
| Binder and Catalog/Sales/Goods GUI regression at 100% | `TestResults/t08-app-native100-verified/t08-app-native100-verified.trx` | 95/95 PASS, zero skips on final test revision. Ten measured windows report `MONITOR_SCALE=100`, `MONITOR_DPI=96`, including Catalog, Sales and Goods Light/Dark rows. |
| Binder and Catalog/Sales/Goods GUI regression at 125% | `TestResults/t08-app-native125-verified/t08-app-native125-verified.trx` | 95/95 PASS, zero skips on the same final test-source revision. Ten measured windows report `MONITOR_SCALE=125`, `MONITOR_DPI=120`, with Catalog, Sales and Goods Light/Dark rows. The x86 window API reports virtualized `WINDOW_DPI=96`; monitor measurement is authoritative for physical scale. |
| Catalog native Light/Dark busy overlay | `TestResults/t08-native100-write-cancel/t08-native100-write-cancel.trx` | 2/2 PASS; independent PerMonitorV2 observer reported scale 100, monitor DPI 96 |
| Sales/Goods delayed overlays and admission | `TestResults/t08-sales-goods-overlay/t08-sales-goods-overlay.trx` | 2/2 PASS on current 100% session |
| App Release `net48/x86` build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, 0 warnings/errors before final evidence commit |

The first native125-labelled run was rejected by the independent observer because the physical display was still 100% (`MONITOR_SCALE=100`, `MONITOR_DPI=96`). Those two failed environment checks are excluded from native125 PASS. Before each final run, an independent PerMonitorV2 P/Invoke measured the physical display at 125% / 120 DPI and then 100% / 96 DPI. The current test-source revision passed at both scales.

Behavior covered: fast operation suppresses overlay; overlapping/out-of-order tokens retain the newest live message; Cancel invokes only the current callback and does not release the token; hide/dispose/handle recreation stop delayed scheduling; theme colors update while visible. Binder tests cover stale document completion, scope replacement and background invalidation dispatch. Catalog/Sales/Goods tests retain command locks before and during overlay and release it after completion. No code path restores caller-owned `Enabled` or focus, retries Post or treats Cancel as transaction rollback.

## Boundary

T08 and UIN-A09 are VERIFIED in the bounded source and measured native100/native125 Light/Dark scope above. No SQL owner was needed because this task changes no persistence contract. Full package/Owner acceptance remains T13.
