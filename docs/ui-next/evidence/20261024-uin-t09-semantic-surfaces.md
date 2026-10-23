# UIN-T09 — semantic surfaces and text

Date: 2026-10-24. Scope: UIN-A10 on the UI-NEXT plan. Package Login, attended Owner review and release disposition remain T13.

## Candidate

| Owner | Exact revision | Change |
|---|---|---|
| Bootstrap5WinFormUI | source `15ef4032e9420a04bb4c6279704406c8d7424859`; documentation `e4df594dacab9b2a9781057528fc3ced8a9d7f98` | Native `BootstrapPanel` with Body/Surface/Alternate roles and `BootstrapLabel` with Body/Muted/Success/Warning/Danger roles. Theme-owned colors/fonts, explicit concrete-property caller overrides, DPI/layout inheritance and deterministic disposal. Reviewed additive API fingerprint `453cd58ad8e1686781ad3b84d9c620fef43f4bde4037c97c65451db931841ba7`. |
| BootstrapSourceGrid | `ee86b183ce032f123d25ce62bf5306d1f639a03e` | Pins the UI candidate; no BSG editor code changed. SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. |
| MyDmsVn | app source `5719dc8ec60c9a8a271d9cf468b25bd482ea1c3b`, ownership review fix `8fd8de5a44a9b314465ffb16ccd2e5b51efe3300`, pin `62cc6efe71161106cad041fbba4e1617d224a3b3` | Feedback host, Catalog inspector, Sales/Goods detail surfaces and pagination/filter labels adopt semantic controls. `BusinessCompactAppearance` traverses semantic Panel public children and busy-host Content, excludes private vendor children, preserves semantic roles and detaches moved/disposed subtrees. |

Release net48/x86 executable SHA-256: `48238135CA9B954A594326C8E5AB1E06516C4B701FCB92691E8A959919DF53AA`. Native100 GUI test DLL SHA-256: `C32B491982B2288A8129A26AD461401613FFE44021D177CD57A099E55874C717`. Local build only; no packaged Login claim.

## Verification

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. UI/BSG tests run on net48/x86 and net8.0-windows/x64; app tests run on net48/x86 with guarded STA and bounded probes. Actual TRX rows below have zero skips.

| Scope | Local TRX | Outcome |
|---|---|---|
| UI semantic controls, theme manager, Card and public API | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/t09-ui-final-net48/t09-ui-final-net48.trx` and corresponding `t09-ui-final-net8` TRX | 34/34 PASS per target. Additive API gate was first observed RED, reviewed and updated to the fingerprint above. |
| BSG full compatibility after UI pin | `vendor/MyDmsVn.BootstrapSourceGrid/TestResults/t09-bsg-net48/t09-bsg-net48.trx` and corresponding `t09-bsg-net8` TRX | 256/256 PASS per target; existing SourceGrid NU1602 warnings remain. |
| App feedback, Catalog, Sales, Goods, appearance/layout and compatibility at native100 | `TestResults/t09-app-native100-review-final/t09-app-native100-review-final.trx` | 153/153 PASS, zero skips. PerMonitorV2 observations include `MONITOR_SCALE=100`, `MONITOR_DPI=96` across Light/Dark and compact/wide layouts. |
| App Release x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, zero warnings/errors. |

The first pre-change app baseline had 4/61 failed rows in `BusinessCompactLayoutTests` because the T08 busy host's public content was hidden from appearance traversal, leaving a 9pt grid view where 10pt was required. A targeted RED test reproduced semantic-role clobbering; subsequent focused app adoption/layout tests passed 10/10. Review then found alert-color clobbering and stale appearance subscriptions on moved semantic content. Both received failing tests and focused GREEN tests before the 153-row final native100 run. Original failed TRX are retained locally and excluded from PASS.

The shared UI ownership contract is precise: explicit overrides assigned through `BootstrapPanel`/`BootstrapLabel` properties survive role/theme changes, including same-value assignments. Native base `Control`/`Label` color/font setters are nonvirtual, so a same-value assignment through a base-typed reference is not observable as a new override; the upstream component guide records this limit. App semantic roles are not replaced by generic Body/Text or Alert palette assignments. Native input, validation, focus and command state remain owned by their existing controls/view models.

## Boundary

Source, dual-target UI/BSG and measured native100 app verification are complete. Physical native125 Light/Dark app verification remains pending. No SQL owner was needed because this task changes no persistence contract. T13 retains package/Owner acceptance.
