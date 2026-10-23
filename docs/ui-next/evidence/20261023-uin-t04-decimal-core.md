# UIN-T04 — lossless decimal parser and projection

Date: 2026-10-23. Task UIN-T04 / acceptance UIN-A05. The application branch is `codex/uin-t04`; the source/test implementation is `ab92661f2d5044ec53b77a3450996e8b39033670`, followed by final BSG pin commit `01300b2d6d8ca68c65426a60f3967fec78ce9de7`. The UI vendor has separate reviewable T04a parser commit `12df307ed1526744fc93078cc5b3ab7cb504f2ac` and T04b projection commit `59ec6b3a7c741f716c05c9f2f4166e48706f0273`. The final BSG pin is `7aec7bdd2e5ec89ed86d80c1ebd1fc3df5a12dca`; SourceGrid stays `f4e457b43582bf01892f50bdc74aa480531e5944`. The previous UI/BSG pins were `2be9a4d778fda7016207b75470020be6126f2f3f` / `2bd7874f7e80b22ef181763749c2a2938ab7f16e`.

## Behavior and review

The UI library now exports `DecimalInputProfile`, `DecimalInputParser.TryParsePending` and a pure `DecimalInputProjection.Project` result. The profile freezes a cloned culture, nullability, scale and caller bounds. Parsing checks literal ASCII lexical grammar, meaningful precision and representability before constructing `decimal`; it does not silently round, clamp, strip punctuation or commit a model. Projection retains the exact candidate, generated grouping provenance and reversible caret/selection boundary maps. Invalid, intermediate or ambiguous display cases use identity projection. An external grouped paste can display identically to generated grouping but retains its external origin and is rejected by the parser. Existing Numeral formatting, caret mapper and history behavior were not changed. The app adds one differential characterization against its existing `ReceiptNumericInput` policy for en-US/vi-VN quantity, cost and price. Independent UI tests cover fixed expected values, including literal custom sign/decimal tokens and far-tail precision.

The additive public API fingerprint changed from `95796bc67c04af789782cd20399c958dcda2d24083816c3df3cffc4b18b6499d` through T04a `aa03c5193f587ce2182b410621d05896c4acd165cebcccfd3e387821106e07c4` to T04b `a275ddba930aa8d07d73db5fc6f32f7568c926e9d08df7a5bd33bbf554a5b36b`. The UI `COMPONENTS`, `COMPATIBILITY` and `PUBLIC_API_BASELINE` documents and API gate were updated; compiled exports were reviewed without changing existing signatures or assembly version. Read-only independent review found no blocking defect. The projection conservatively keeps a trailing-decimal candidate such as `1234.` as identity display.

## Actual verification

Interactive Windows NT 10.0.26300.0, 64-bit OS, .NET SDK 8.0.426, VSTest 17.11.1, Release. Pure UI tests ran on net48 x86 and net8.0-windows x64. No native DPI/theme observation was required for this pure core. UI test restore needed `-p:ManagePackageVersionsCentrally=false` because the containing app's central props otherwise apply to the nested UI test project; net48 needed `-- RunConfiguration.TargetPlatform=x86` for nonempty NUnit discovery. An initial default net48 invocation discovered zero tests and is not counted as PASS.

| Scope / local TRX | Selected and executed | Outcome |
|---|---:|---|
| App existing `ReceiptNumericInputTests`, `TestResults/uin-t04-app-oracle` | 36 / 36 | PASS before change |
| New parser tests against stub, UI `TestResults/uin-t04a-red` | 13 / 13 | Intended RED: 11 failed, 2 passed |
| Projection tests against identity stub, UI `TestResults/uin-t04b-red` | 6 / 6 | Intended RED: 5 failed, 1 passed |
| Parser, projection, legacy Numeral/caret/history and API gate, UI `TestResults/uin-t04-final-net48` | 54 / 54 | PASS, 0 failed/skipped |
| Same selected UI tests, `TestResults/uin-t04-final-net8` | 54 / 54 | PASS, 0 failed/skipped |
| BSG complete integration, `TestResults/uin-t04-bsg-net48` | 243 / 243 | PASS, 0 failed/skipped |
| BSG complete integration, `TestResults/uin-t04-bsg-net8` | 243 / 243 | PASS, 0 failed/skipped |
| App final `ReceiptNumericInputTests`, `TestResults/uin-t04-app-final` | 37 / 37 | PASS, 0 failed/skipped |

UI source built both `net48` and `net8.0-windows` in Release with zero warnings/errors. BSG solution restored and built both targets with zero warnings/errors. For BSG validation, a temporary untracked `Directory.Packages.props` at the BSG root disabled inherited central package management for its inline-version test project while SourceGrid retained its own nearest central props; it was removed after the runs. The app final run rebuilt its referenced net48 source and tests. Runs used ordinary `dotnet test` and actual ignored TRX, not the retired gate runner. BSG GUI tests used bounded hang collection. No SQL allocation, package build or external process remained.

Saved-byte SHA-256: `DecimalInputParser.cs` `d3c1569d0704773bfba1220b326578cfab3b21da19bf27215dfffcf1973e7f97`; `DecimalInputProjection.cs` `01b3146b675ea0ee49304b1e21efa715685dc584ec8dcd3cb4e8bfe981b30cbb`; app `ReceiptNumericInputTests.cs` `b5932b36e5de764e9745f7b27ee0c734f05ff19092e1211a1598a58be6f1c2db`; built UI net48 DLL `6a38ad17e4af5ab94e2419358d3bf764c765d8cb83667760de70b72612b078e7`; built app test DLL `254f879fef3f6e07b25fb8e51586bc2e1c235d0f75939532b383023793b9684d`.

UIN-A05 is VERIFIED for the pure T04 core and current application numeric policy. T05 must prove native per-character edit, paste, IME, undo, BSG admission and Sales/Goods Save/Post behavior on its own candidate. T13 still owns package/actual-Login and dated Owner disposition. No production or archived SAL acceptance is inferred. T05 is the next ready task.
