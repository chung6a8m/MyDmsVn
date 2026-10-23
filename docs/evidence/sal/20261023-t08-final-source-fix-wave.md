# SAL — final source fix wave, 2026-10-23

Status: IMPLEMENTED / FOCUSED VERIFICATION PASS / SCOPED REREVIEW PENDING. This is the single authorized FR-1/FR-2 wave before the final source freeze. It closes no frozen integration, full T07, native125, attended, Owner or release acceptance obligation.

## Baseline and changes

Parent/source baseline: `942b63cc64ef46d4df4b1f88ac24a22a5e231910`, initially clean on `codex/sal-direct-sales`. The official whole-branch report required exactly FR-1 / carried T01 R3 and FR-2 / carried T07 R4; its SHA256 is `CEC5A50B55046EB5E253D904B5B34495ECC19A4707146B3E5608B9E97C60F6A8`. No Critical/Important finding remained. This wave is committed with this evidence; executions used the parent plus the recorded dirty source/test edits, not a later clean-commit rebuild.

Pinned vendor remains `MyDmsVn.BootstrapSourceGrid@2bd7874f7e80b22ef181763749c2a2938ab7f16e`. Scripts001–009, dependencies, permissions, business scope and maintenance admission remain unchanged. Active product/tests remain net48.

FR-1: script010 reports an exact bounded `COUNT_BIG` and the minimum offending numeric ledger/receipt/line identity or first ordered Warehouse/Product key. Correspondence, Posted line count, counterpart cardinality and balance/key predicates are unchanged. Line range failure is fatal51015; aggregate value range failure is fatal51016. An indexed temporary line-value proof retains the exact product/rounding; nullable checked conversion detects target-range failure and cannot become a zero/default. Maintenance-only ordered key sums retain widened decimal arithmetic and identify the failing stock key even if the widened SUM itself overflows; that fatal catch reports `Count>=1`. Ordinary target-range failures report the exact number of failing keys. Table locks, the restricted exact9 boundary, DbUp transaction/journal and rollback remain in force.

The existing22 corruption rows now require bounded, safe diagnostic characters/counts/IDs and no fixture-private name/note sentinel. They compare original source/ledger/audit/tokens, balances, exact journal and ownership marker after rejection, plus schema9 artifacts/compatibility. `unknown` now corrupts two rows to require Count2/minimum LedgerId1. Missing/duplicate counterparts, invalid Posted counts, missing/extra/mismatched keys and line/aggregate target overflow remain covered. The physically impractical decimal38 SUM-overflow catch was inspected but not forced with billions of rows; line and aggregate decimal28 refusal paths executed against real SQL.

FR-2: the grid adapter remains the sole owner of inline validation. Command admission no longer copies its error into `_message`; it still retains focus on rejection. Footer/ErrorProvider consume the adapter error and resolve on cancel/successful commit. Independent operation feedback remains intact; server/list/error precedence and all existing admission/access paths remain unchanged. Two control rows preserve unrelated operation feedback through cancellation/correction. Four Light/Dark native rows type each numeric character, observe raw text/caret/range/focus, reject Save without writes, then use actual Escape or corrected successful Save; they assert footer/ErrorProvider, original rejected-write counts and saved2.125 quantity.

## Actual discovery and execution

All results are ordinary direct `dotnet test` TRX. Raw logs/discovery/TRX and retained artifacts are ignored under `.local/t08-final-fix-wave/`. No blanket SQL/GUI suite, matched six-owner pair, package publish or frozen run occurred.

| Invocation | Discovered / selected / executed | P / F / S | Actual meaning |
|---|---:|---|---|
| `migrations-red` |22 /22 /22 |0 /22 /0 | Original embedded010:20 missing bounded-diagnostic assertions and2 native8115 versus required fatal diagnostic categories. Source/balance/journal/schema assertions completed first; no setup/cleanup failure. |
| `gui-red` |6 /6 /6 |0 /6 /0 | All fail at resolved-footer assertion: old numeric error survives. Native4 + control2; ErrorProvider and write/value assertions pass before the stale-footer assertion. |
| `migrations-green` |42 /42 /42 |42 /0 /0 | Bootstrap33, maintenance7 (six SQL/one pure cleanup boundary), fresh1, exact9 upgrade/no-op1. |
| `migrations-final-bom` |2 selected /2 executed |2 /0 /0 | Final BOM build: actual fresh install and exact9 restricted upgrade/no-op. Same two source-verified cases previously discovered in42. |
| `gui-green` |109 /109 /109 |109 /0 /0 | NativeInput64, Control21, Admission10, Grid8, Accessibility6. Includes all six new rows. |

The exact new selectors are `Resolved_numeric_validation_clears_footer_and_provider_without_clearing_operation_feedback` (false/true) and `Numeric_save_validation_resolves_after_native_Escape_or_corrected_Save` (Light/Dark × false/true).

Both projects restored with `dotnet restore <project> --locked-mode`. Discovery used the same project/configuration/filter and `--list-tests`. Migration commands use `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj -c Release -f net48 --no-restore`. RED filter: `FullyQualifiedName~DirectSalesBootstrapTests.Invalid_history`. GREEN filter: `FullyQualifiedName~DirectSalesBootstrapTests|FullyQualifiedName~DirectSalesMigrationTests|FullyQualifiedName~DirectSalesSchemaCompatibilityTests.Exact9_runtime|FullyQualifiedName~DirectSalesMaintenanceTests`. Final-BOM filter: `FullyQualifiedName~DirectSalesMigrationTests|FullyQualifiedName~DirectSalesSchemaCompatibilityTests.Exact9_runtime`, with `--no-build` after the successful direct final build (0warnings/errors).

GUI commands use `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86`, and `-- RunConfiguration.TargetPlatform=x86`. RED filter: `FullyQualifiedName~Resolved_numeric_validation|FullyQualifiedName~Numeric_save_validation_resolves`. GREEN filter: `FullyQualifiedName~SalesReceiptControlTests|FullyQualifiedName~SalesReceiptControlAdmissionTests|FullyQualifiedName~SalesReceiptGridTests|FullyQualifiedName~SalesReceiptAccessibilityTests|FullyQualifiedName~SalesReceiptLookupNativeInputTests`. `MYDMSVN_SAL_EXPECTED_SCALE=100` was explicit. Each invocation used `--logger 'trx;LogFileName=<invocation>.trx' --results-directory .local/t08-final-fix-wave` (GUI RunConfiguration arguments follow these options).

## Exact artifact boundaries

C#/SQL retain their original UTF-8 BOM/CRLF; Markdown is UTF-8 without BOM/CRLF. The42-row migration GREEN embedded the same new SQL text before its original BOM was restored. That pre-restoration executable is archived separately. After restoration, the actual DbUp `EmbeddedScriptProvider` produced ordinal-identical `Script.Contents` from both assemblies: decoded UTF-8 SHA256 `B64CCDF8E749EAE9505D2F92CAB6B1D87436E4E6A374D64002F2976B4C92FD61`. The final-BOM build/smoke then passed2/2. This preserves the distinct executed DLL/script byte identities rather than relabeling42 as a final-byte run.

Final raw source/artifact SHA256 values:

| File | SHA256 |
|---|---|
| `src/MyDmsVn.Server.DbMigrator/Scripts/010_DirectSalesAndValuation.sql` | `E901E81CB9DEE4544DC6BA338DEA9C895565B1356F14305A5856156DB51527EA` |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs` | `2FBAB91929F3FA5874A55E4E96BA94B9C6385C5E63ED6E1B7F33D9947379E4C3` |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/DirectSalesBootstrapTests.cs` | `C972DEFDB391F139407057B8A197CA307F44964550E03ABBDE140C44F19E3311` |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptControlTests.cs` | `2F350D4F03DE7F38457F08E7F79F1F7B09046071CF3EF49F1DB49794AD0D638D` |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptLookupNativeInputTests.cs` | `F6106F0E5F55D5F274D8A429C1D7F68DB304EFE7FDB77B10301C32BCC3F070BC` |
| `tests/MyDmsVn.Desktop.Tests/GuiInlineEditorProbe.cs` | `FDFE1E91931FFC45EC1BAA6A3545C0B493EBFD095B5C0E54D825789023DBA1EF` |
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.Tests.dll` | `6CD9DB2E7EEF5E5A693B9A2CFC2094BB377AE4FE1DC2F107706FD1E38A4C548B` |
| `tests/MyDmsVn.Desktop.Tests/bin/Release/net48/MyDmsVn.Desktop.WinForms.dll` | `2D91EB9955A2F293CFA5C96BDAB1397FB3F7BE1496F5C06CDD82C0B6B855D87F` |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/bin/Release/net48/MyDmsVn.Server.Infrastructure.Migrations.Tests.dll` | `15483090CD0AF8E6EA0ADC7F6008BD4D1784664F6C46CABE398A4E39AF478C19` |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/bin/Release/net48/MyDmsVn.Server.DbMigrator.exe` | `28B5C3613A5B4545ED33971C2A822FF4C0519AD5AFF9FAD8F41F5E2A6AC36F5D` |

Final artifacts were archived after the runs in `final-artifacts/`; RED DLL/source and pre-BOM GREEN artifacts have separate retained directories/manifests. These are not contemporaneous full dependency pre/post archives. The temporary native observer executables were deleted by the existing fixture after verified drain; no executable hash is inferred. The unchanged observer source hash and embedded test-DLL identity above identify its source boundary, while actual measurement/cleanup output remains in each TRX. No package was built from this wave.

TRX SHA256 values:

| Original TRX | SHA256 |
|---|---|
| `gui-green.trx` | `3AAB3B7B1947F51F26D74D70E247A2C60149BE5C37AFA697C1D92E6DF440BFCE` |
| `gui-red.trx` | `6E2220866201C192951E0F4E58065F236FCD04D7D0B6167FF0EADB08E73BA165` |
| `migrations-final-bom.trx` | `AACCBEDC408DA7AFCA501FDD768EC0473F6C12D8FD365CC31A1013E7C3F6D03A` |
| `migrations-green.trx` | `A2239310F9CFFBB7C3558DBCB0188B1396FB0D03CA4129B05945F1A60E2F7DCE` |
| `migrations-red.trx` | `01B8C22E44CEB88F50D56F27532119C096D41829134C59421AD23F3D3F097513` |

## Environment, ownership and limits

Observed Windows10.0.26200, SDK8.0.426, .NET Framework Release533509. A bounded read-only metadata query after all tests confirmed designated non-production effective-master SQL Server14.0.2140.2, Developer Edition64bit; its connection/instance identifiers remain local. SQL owners ran sequentially using the explicitly designated environment. The executed fixture bodies account for22 RED +41 GREEN +2 final-smoke dedicated database allocations and two maintenance-owned SQL logins. All awaited ownership/token/instance-verified cleanup and worker/connection disposal completed; maintenance SID/drain checks and its injected cleanup-failure recovery passed. No uncertainty/quarantine remains from these invocations. This is fixture cleanup evidence, not an independent instance-wide orphan inventory.

GUI ran one command at a time on interactive Windows x86, serialized guarded STA, finite waits and disposal. RED native4 and GREEN native64 each record actual observer-process cleanup and post-disposal task/source/form cleanup PASS. Observers measured physical native100, window96DPI, sole display1920×1080/workarea1920×1032; these are hosted test controls with injected test identity. They do not prove actual Login. Final process observation found no testhost, Desktop app, inline/package probe remaining. No new screenshots or visual acceptance were generated.

Self-review: unchanged rejection predicates and source/ledger/journal preservation were checked against the baseline diff; diagnostic values are bounded numeric identifiers only; no unrelated `_message` assignment is removed or cleared; schema9/10 fingerprints and maintenance/no-op checks passed. Git diff/encoding/local links were checked before commit. One scoped independent rereview remains before source freeze.

Full T07/native125, all required native125 size/theme contexts, attended observations, final frozen Application/Architecture/SQL fresh-reuse/Session/GUI/package evidence, dated Owner disposition and operational release prerequisites remain OPEN. All original T01/T07 evidence, development package failures, attempt15 oracle typo, and the two unresolved/nonreproduced UI transients remain historical facts. No complete GUI, full19 package PASS, T08 completion or production readiness is claimed.
