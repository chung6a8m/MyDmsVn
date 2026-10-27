# UIN-T00–T02 — bounded task completion on native100/native125

Date: 2026-10-23. Task-owned acceptance: UIN-A01, UIN-A02 and UIN-A03. Product source baseline is commit `d5d13c532250403564bb224f1f938df30152527b` on `codex/uin-t02`, including the earlier [T00](20261023-t00-field-errors.md), [T01](20261023-uin-t01-workspace-feedback.md) and [T02](20261023-uin-t02-validation-pilot.md) records. This follow-up adds test-only native observation and a T01 Light/Dark host case; the implementation source, transport, SQL and vendor pins are unchanged. The final test/evidence revision is the commit containing this record and the hashes below.

Pinned BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, nested Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No package was built and no SQL allocation occurred. Package/actual Login, attended observations and dated Owner disposition remain UIN-T13/UIN-A14, rather than being inferred from these hosted tests.

## Current-candidate execution

Interactive Windows, `.NET Framework 4.8`, Debug GUI net48/x86, Release Application/Session net48; VSTest 17.11.1. The same Product/Sales/feedback implementation was tested at both physical monitor scales. The helper launches the existing bounded owned x86 observer in a separate PerMonitorV2 process, checks target process ownership and verified cleanup, and optionally rejects a declared scale mismatch. Native GUI tests use serialized STA, `WinFormsTestGuard`, finite waits and disposal. Raw TRX is local, with no credentials or SQL identifiers committed.

| Run and local TRX | Selected / executed | Result |
|---|---:|---|
| `TestResults/uin-t00-current-at/uin-t00-current-at.trx` — Catalog application and API mapper | 41 / 41 | PASS; 0 fail, 0 skip |
| `TestResults/uin-t00-t02-current-session/uin-t00-t02-current-session.trx` — Catalog/base, feedback scope and Sales editor VMs | 80 / 80 | PASS; 0 fail, 0 skip |
| `TestResults/uin-t00-t02-native100-final/uin-t00-t02-native100-final.trx` — Product, Sales and host/shell GUI | 172 / 172 | PASS; 0 fail, 0 skip |
| `TestResults/uin-t00-t02-native125-final/uin-t00-t02-native125-final.trx` — same GUI selector | 172 / 172 | PASS; 0 fail, 0 skip |

The GUI selector includes `FieldValidationBinderTests`, `CatalogControlTests`, `CatalogAccessibilityAcceptanceTests`, `InlineValidationPresentationTests`, `SalesReceiptGridTests`, `SalesReceiptControlTests`, `SalesReceiptAccessibilityTests`, `SalesReceiptLookupNativeInputTests`, `WorkspaceFeedbackHostTests` and `FoundationShellFormTests`. Six Light/Dark invocations per GUI TRX cover Product duplicate/unbound errors, Sales cell/row summary and T01 A/B feedback host; each carries an independent observer record. Other selected cases are focused regressions, not additional theme-matrix rows. Counts from different runs are not added into one case total.

```powershell
dotnet test tests/MyDmsVn.Server.Application.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~CatalogApplicationTests|FullyQualifiedName~ApiResponseMapperTests' --logger 'trx;LogFileName=uin-t00-current-at.trx' --results-directory TestResults/uin-t00-current-at
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~SalesReceiptEditorViewModelTests|FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~DesktopViewModelBaseTests|FullyQualifiedName~WorkspaceFeedbackScopeTests' --logger 'trx;LogFileName=uin-t00-t02-current-session.trx' --results-directory TestResults/uin-t00-t02-current-session
$env:MYDMSVN_UIN_EXPECTED_SCALE='100' # repeat in a separately measured 125% session with '125'
dotnet test tests/MyDmsVn.Desktop.Tests -c Debug -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~FieldValidationBinderTests|FullyQualifiedName~CatalogControlTests|FullyQualifiedName~CatalogAccessibilityAcceptanceTests|FullyQualifiedName~InlineValidationPresentationTests|FullyQualifiedName~SalesReceiptGridTests|FullyQualifiedName~SalesReceiptControlTests|FullyQualifiedName~SalesReceiptAccessibilityTests|FullyQualifiedName~SalesReceiptLookupNativeInputTests|FullyQualifiedName~WorkspaceFeedbackHostTests|FullyQualifiedName~FoundationShellFormTests' --logger 'trx;LogFileName=uin-t00-t02-native100-final.trx' --results-directory TestResults/uin-t00-t02-native100-final -- RunConfiguration.TargetPlatform=x86
```

TRX review found exactly six `MONITOR_SCALE=100;MONITOR_DPI=96` records at 100% and six `MONITOR_SCALE=125;MONITOR_DPI=120` records at 125%. The WinForms test host reports `DeviceDpi=96` and `WINDOW_DPI=96` in both contexts because its window coordinates are DPI virtualized; this was **not** used to infer monitor scale. The independent observer recorded physical client `940×700` for Product/host and `944×700` for Sales at 100%, and `1175×875` / `1180×875` respectively at 125%. The logical Product inspector stayed 340 pixels wide, Sales grid `912×330` retained its last column, and host alert/toast stayed inside the workspace. The 125% context was supplied by the user and measured before execution; the later 100% context was measured again before its run.

## Behavior and boundary

The current candidate retains T00 duplicate-Code Conflict/`code` metadata, attempted text and stale-response denial; T01 A/B originating-workspace feedback, hidden-tab retention and recovery; T02 current active editor border/help/provider, inactive cell/row indicators, accessible summary/navigation, stable submitted-line mapping and raw input. T01's added Light/Dark case explicitly checks A error while B is selected, B success toast without focus theft, retained A alert on return, and compact bounds. Previous T00/T01/T02 RED and repair histories remain in their original evidence; this run verifies the combined candidate and does not rewrite those histories.

UIN-T00–T02 are VERIFIED in their bounded task scope, and UIN-A01–A03 have the prescribed focused Application/Session/native evidence on this candidate. UIN-T03 is next. UIN-A14 final package, actual Login and dated Owner review remain NOT RUN; neither this record nor the user's availability to participate is an Owner acceptance. Archived SAL/native125/attended obligations and production release prerequisites remain separate.

## Test source identity and cleanup

SHA-256 of changed C# working-tree bytes (UTF-8 BOM/CRLF); product source identity is the unchanged `d5d13c5` commit above.

| File | SHA-256 |
|---|---|
| `tests/MyDmsVn.Desktop.Tests/UiNextNativeScaleObservation.cs` | `3ab1e206a2dea2ddaede9f92cccdc9b4c7a3a11a93f0fadafe54aa861939f8bd` |
| `tests/MyDmsVn.Desktop.Tests/WorkspaceFeedbackHostTests.cs` | `21be5545566878bff5ee841296518760615952664cf090085576ca9138261f5a` |
| `tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs` | `9d2087aed24272087f3e8f89adfc48d0083855b5837e0c2b3b5ca65c3ea58d81` |
| `tests/MyDmsVn.Desktop.Tests/InlineValidationPresentationTests.cs` | `e0bb72f25b9c7d1803c6f00cdbe16e70c972b0dd0d8c3a11ca540d21e6f26f50` |

The native observer and compiler processes exited with verified owned cleanup; selected GUI tests disposed their forms, scopes and controls. No screenshot or raw TRX is checked in. Missing final package/Owner evidence is recorded under T13, without reviving the archived SAL or release gates.
