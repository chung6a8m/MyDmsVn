# UIN-T10 — decimal DataGridView parity

Date: 2026-10-24. Scope: UIN-A11, retaining UIN-A04. This is a library/demo consumer; the application receipt grids remain SourceGrid. Package Login, attended Owner review and release disposition remain T13.

## Candidate

| Owner | Exact revision | Change |
|---|---|---|
| Bootstrap5WinFormUI | source/demo/API `4a7fc668e538f9de02a8b6dbb62c3bf1e9eca7ca`; theme-test `91e566fffbf5b2885d974b8f03487e4bd87ff3d6`; editor reuse fix `910c19493b36d1db2fb0b92b7fc436f263ec564d` | Adds `BootstrapDecimalColumn`, internal decimal cell and DGV editing control. Cloned immutable profile, recycled-editor reset including validation state, lexical candidate parsing, nullable `DataRowView`/`decimal?` handling, native navigation and bound-model projection. Public API fingerprint `2e6616c512076a4eff3892039eb4eb41c6942da3f02d76be9c7de5da5361d530`. Demo Total is the consumer. |
| BootstrapSourceGrid | `a2c2cb749ac5656a56b1cfebfc7718b889d95152` | Explicit UI pin; no SourceGrid editor or registry source change. Existing T05 adapter passes focused parity and ownership cases. SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. |
| MyDmsVn | pin `d592202334edd7265c8bef4942be26f240662cf4` | Explicit BSG pin; no app source or business-grid editability change. |

Release net48/x86 executable SHA-256: `C5E75C89587685ADDAB538BF22C412D0635708556305E8DDE7AB8BA5CE09A6E5`. App GUI test DLL SHA-256: native100 build `E794A5B85003C30A349F067C861A95780EB2E22CD6BD1C5EFDDCC438547828EF`, native125 build `940C4467E3BDC94373E5BEE71694E580ED4ABA1B9DB3023D3705DFFD363EECE3`; source/test revision unchanged. Local build only; no packaged Login claim.

Release test DLL SHA-256: UI net48 `59B10D137CC9D70060631E02D8F09C43629CE88F3D537F51470701DA7253017B`, UI net8 `7EEE4CBAC6F199F7A44352D10C16E02F15FC7C26DF90B82EA21D9BAA239B59F2`; BSG net48 `0232947B04F9F5EA67E0C137A2C7EF5FDB8630EA744A10FB4D934915A9B6AB95`, BSG net8 `D858A4C31B709047B9981258A852D5B2BA37CD5D07D25F5CA875A3E78A82568D`.

## Bounded verification

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. UI/BSG tests use net48 and net8.0-windows. App uses net48/x86, guarded STA and bounded native probes. For each listed final TRX, test definitions, result rows, total and executed counters agree (UI 51, BSG 44, app 122); all rows passed with zero skips.

| Scope | Local TRX | Outcome |
|---|---|---|
| UI DGV decimal, lookup compatibility, demo and API at native100 | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/t10-ui-release-net48/t10-ui-release-net48.trx` and corresponding `t10-ui-release-net8` TRX | 51/51 PASS per target on final UI commit; includes Light/Dark invalid pending/caret/model transitions, recycled validation reset and shared decimal grammar. The app probe below independently measured the monitor at 96 DPI. |
| UI DGV decimal, lookup compatibility, demo and API at native125 | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/t10-ui-native125-net48/t10-ui-native125-net48.trx` and corresponding `t10-ui-native125-net8` TRX | 51/51 PASS per target, run sequentially on the same interactive session after the app monitor probe measured 120 DPI. |
| BSG T05 decimal and lifecycle/ownership/sizing on final UI pin at native100/native125 | `vendor/MyDmsVn.BootstrapSourceGrid/TestResults/t10-bsg-pin-net48/t10-bsg-pin-net48.trx`, `t10-bsg-pin-net8`, and corresponding `t10-bsg-native125-net48`/`t10-bsg-native125-net8` TRX | 44/44 PASS per target and scale on final UI pin; existing SourceGrid NU1602 warnings remain. No demonstrated parity gap required a BSG source fix. |
| App UIN-A04 Goods/Sales regressions at native100 | `TestResults/t10-app-final-native100/t10-app-final-native100.trx` | 122/122 PASS on final pin; four native monitor observations include `MONITOR_SCALE=100;MONITOR_DPI=96;WINDOW_DPI=96`. |
| App UIN-A04 Goods/Sales regressions at native125 | `TestResults/t10-app-final-native125/t10-app-final-native125.trx` | 122/122 PASS on final pin; four native monitor observations include `MONITOR_SCALE=125;MONITOR_DPI=120;WINDOW_DPI=96`. The test-host window DPI remains 96; this evidence establishes monitor scaling and tested physical layout, not 120-DPI awareness of that test-host window. |
| App Release x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, zero warnings/errors. |

The first app run (`t10-app-a04-pin.trx`) declared 125% after the display had moved to 100%; its four native observations correctly refused that mismatch (118 PASS, four FAIL). The corrected 100% run above passed without source changes. An optional full UI net48 run (`t10-ui-full-net48.trx`) had 2323 PASS and one failure in the unrelated DateInput `SendKeys` Alt+Down test; that exact test passed 1/1 when rerun alone (`t10-date-isolated-net48.trx`). This full-suite run is not claimed as PASS. Focused T10/UI compatibility runs passed on both targets.

The recycled-editor validation-state assertion was first run against the candidate without the reset and failed (three other selected rows passed); the one-line reset then passed with the complete 51-row UI scope on both targets. BSG's final-pin Release runs used a temporary local package-management props file to keep its explicit PackageReference versions separate from SourceGrid's nested central versions; that file was removed after both scales' runs. Earlier calls that forced one global property through the whole graph failed during restore/build and yielded no test outcome.

## Boundary

UIN-A11 is VERIFIED in this bounded source/demo and measured native100/native125 Light/Dark scope, retaining the Goods/Sales UIN-A04 regression. The DGV consumer is a library/demo host; application receipt grids remain SourceGrid. No SQL owner was needed because T10 changes no persistence contract. Test-host `WINDOW_DPI=96` at monitor125, physical Vietnamese IME typing, packaged Login and dated Owner review are not established by these runs; package/Owner acceptance remains T13.
