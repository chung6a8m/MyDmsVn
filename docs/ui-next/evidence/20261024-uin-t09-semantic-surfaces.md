# UIN-T09 — semantic surfaces and text

Date: 2026-10-24. Scope: UIN-A10 on the UI-NEXT plan. Package Login, attended Owner review and release disposition remain T13.

## Candidate

| Owner | Exact revision | Change |
|---|---|---|
| Bootstrap5WinFormUI | source `15ef4032e9420a04bb4c6279704406c8d7424859`; documentation `e4df594dacab9b2a9781057528fc3ced8a9d7f98` | Native `BootstrapPanel` with Body/Surface/Alternate roles and `BootstrapLabel` with Body/Muted/Success/Warning/Danger roles. Theme-owned colors/fonts, explicit concrete-property caller overrides, DPI/layout inheritance and deterministic disposal. Reviewed additive API fingerprint `453cd58ad8e1686781ad3b84d9c620fef43f4bde4037c97c65451db931841ba7`. |
| BootstrapSourceGrid | `ee86b183ce032f123d25ce62bf5306d1f639a03e` | Pins the UI candidate; no BSG editor code changed. SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. |
| MyDmsVn | app source `5719dc8ec60c9a8a271d9cf468b25bd482ea1c3b`, ownership review fix `8fd8de5a44a9b314465ffb16ccd2e5b51efe3300`, pin `62cc6efe71161106cad041fbba4e1617d224a3b3` | Feedback host, Catalog inspector, Sales/Goods detail surfaces and pagination/filter labels adopt semantic controls. `BusinessCompactAppearance` traverses semantic Panel public children and busy-host Content, excludes private vendor children, preserves semantic roles and detaches moved/disposed subtrees. |

Release net48/x86 executable SHA-256: `48238135CA9B954A594326C8E5AB1E06516C4B701FCB92691E8A959919DF53AA`. Native100 GUI test DLL SHA-256: `C32B491982B2288A8129A26AD461401613FFE44021D177CD57A099E55874C717`; native125 GUI test DLL SHA-256: `B1F54EC045AFB32002C254C784F423DFE6ED64243EED7629B926B9F875820E84`. Local build only; no packaged Login claim.

## Verification

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. UI/BSG tests run on net48/x86 and net8.0-windows/x64; app tests run on net48/x86 with guarded STA and bounded probes. Actual TRX rows below have zero skips.

| Scope | Local TRX | Outcome |
|---|---|---|
| UI semantic controls, theme manager, Card and public API | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/t09-ui-final-net48/t09-ui-final-net48.trx` and corresponding `t09-ui-final-net8` TRX | 34/34 PASS per target. Additive API gate was first observed RED, reviewed and updated to the fingerprint above. |
| BSG full compatibility after UI pin | `vendor/MyDmsVn.BootstrapSourceGrid/TestResults/t09-bsg-net48/t09-bsg-net48.trx` and corresponding `t09-bsg-net8` TRX | 256/256 PASS per target; existing SourceGrid NU1602 warnings remain. |
| App feedback, Catalog, Sales, Goods, appearance/layout and compatibility at native100 | `TestResults/t09-app-native100-review-final/t09-app-native100-review-final.trx` | 153/153 PASS, zero skips. PerMonitorV2 observations include `MONITOR_SCALE=100`, `MONITOR_DPI=96` across Light/Dark and compact/wide layouts. |
| App feedback, Catalog, Sales, Goods, appearance/layout and compatibility at native125 | `TestResults/t09-app-native125-supported/t09-app-native125-supported.trx` | 145/145 PASS, zero skips. PerMonitorV2 observations include `MONITOR_SCALE=125`, `MONITOR_DPI=120` across Light/Dark and supported compact layouts. The filter excludes eight rows with `DisplayName` containing `1680`: six archived BCF wide-reference rows fail because the 1920×1080 physical display offers only 1540×845 logical work area at 125%; two layout rows passed in the unfiltered attempt but were also excluded consistently. |
| UI semantic controls at native125 | `vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI/TestResults/t09-ui-native125-net48/t09-ui-native125-net48.trx` and corresponding `t09-ui-native125-net8` TRX | 34/34 PASS per target, zero skips. App probe supplies the physical monitor DPI measurement; the UI unit tests do not independently assert monitor DPI. |
| App Release x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, zero warnings/errors. |

The first pre-change app baseline had 4/61 failed rows in `BusinessCompactLayoutTests` because the T08 busy host's public content was hidden from appearance traversal, leaving a 9pt grid view where 10pt was required. A targeted RED test reproduced semantic-role clobbering; subsequent focused app adoption/layout tests passed 10/10. Review then found alert-color clobbering and stale appearance subscriptions on moved semantic content. Both received failing tests and focused GREEN tests before the 153-row final native100 run. Original failed TRX are retained locally and excluded from PASS.

At native125, the first unfiltered run (`t09-app-native125-final.trx`) had 133 PASS and 20 failures because the independent BCF probe defaulted `MYDMSVN_BCF_EXPECTED_SCALE` to `100`. Its failure output itself measured `DPI=120x120;SCALE=125`. After declaring both `MYDMSVN_UIN_EXPECTED_SCALE=125` and `MYDMSVN_BCF_EXPECTED_SCALE=125`, the unfiltered run (`t09-app-native125-corrected.trx`) had 147 PASS and six failures, all at the archived 1680-logical BCF reference size. No source changed between these attempts and the 145-row supported-size PASS. The UI-NEXT validation policy excludes the archived wide125 reference gate; these failed attempts are retained locally and do not count as a full-suite PASS.

The shared UI ownership contract is precise: explicit overrides assigned through `BootstrapPanel`/`BootstrapLabel` properties survive role/theme changes, including same-value assignments. Native base `Control`/`Label` color/font setters are nonvirtual, so a same-value assignment through a base-typed reference is not observable as a new override; the upstream component guide records this limit. App semantic roles are not replaced by generic Body/Text or Alert palette assignments. Native input, validation, focus and command state remain owned by their existing controls/view models.

## Boundary

UIN-T09/UIN-A10 are VERIFIED in their bounded source and measured Light/Dark native100/native125 scope, with supported compact sizes at native125. This does not close the archived BCF wide125 reference gate. No SQL owner was needed because this task changes no persistence contract. T13 retains attended/package/Owner acceptance and release disposition.
