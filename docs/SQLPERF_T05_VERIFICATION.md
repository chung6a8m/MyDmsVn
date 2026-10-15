# SQLPERF-T05 focused SQL tiers verification

**Date:** 2026-10-15  
**Base:** `e53b5ff`  
**Scope:** Test classification, independent verification and focused developer runner only. No production behavior, migration, permission or assertion changed.

## Implemented

- `SqlServerFact` now emits a real xUnit `Category` trait. Its default is `FastSQL`; methods with migration/schema, security/provisioning or concurrency responsibility declare those categories explicitly and may overlap.
- The independent reflection inventory reads categories from the compiled assembly. A reviewed method-scoped non-SQL exemption manifest contains both TFM IL fingerprints, rationale and evidence for 60 pure or fail-closed tests. A changed method invalidates its exemption.
- The strict unfiltered benchmark gate now rejects missing, stale, duplicate or category-conflicting classification before accepting TRX results.
- `FocusedSqlTestResultGate` compares selected source methods with filtered discovery and expanded TRX cases, rejects missing/extra methods, theory-row mismatch, failures and skips, and reports selected/discovered/executed/skipped counts.
- `Invoke-FocusedSqlIntegrationTests.ps1` performs approval preflight, runs each requested TFM sequentially, uses unique TRX/timing/journal paths and reconciles database ownership. It warns that focused success is not a release gate.

## Classification inventory

Both `net48` and `net8.0` produced the same 208 source methods:

| Category | Methods per TFM |
|---|---:|
| `FastSQL` | 37 |
| `MigrationSchema` | 60 |
| `SecurityProvisioning` | 36 |
| `SqlConcurrency` | 17 |
| Reviewed non-SQL exemptions | 60 |

There are 148 SQL methods; two security methods also carry `SqlConcurrency`, so category counts intentionally overlap. `FastSQL` contains only live SQL methods and does not include the 60 non-SQL exemptions.

## Commands and results

```powershell
dotnet build tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release --no-restore
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release --no-restore

tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location).Path `
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot .sqlperf/focused-fastsql-t05-rerun `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category FastSQL
```

| Gate | Result |
|---|---|
| Integration project dual-TFM build | Passed, 0 warnings / 0 errors |
| SQL runner tests | 75 / 75 passed |
| `FastSQL` net48 | 37 selected, 37 discovered, 37 executed, 0 skipped; ownership complete; 1m46s |
| `FastSQL` net8.0 | 37 selected, 37 discovered, 37 executed, 0 skipped; ownership complete; 1m34s |

The first focused attempt correctly failed after the `net48` test execution because a single-TFM IL fingerprint was stale on the other TFM. The manifest format was tightened to retain both reviewed TFM fingerprints, a RED/GREEN test was added, and the complete dual-TFM rerun then passed. The invalid attempt was not accepted as evidence.

## Concurrency decision

No bounded parallel execution mode was introduced. T04 found no reusable fixture/reset boundary, the accepted SQL environment is capacity constrained, and the existing serial policy has deterministic ownership and cleanup evidence. Focused categories reduce iteration time without creating simultaneous full-suite or shared-database load. A future parallel experiment requires separate capacity and adversarial isolation evidence and a simple serial fallback.
