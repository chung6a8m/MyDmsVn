# Test projects and commands — v1a

**Assessed:** 2026-10-18, `c7ad32a4b0040f87417973eabd6c12757c54f7d5`. This is a command reference. [TEST_POLICY](../docs/TEST_POLICY.md) owns execution scope and [TEST_STRATEGY](../docs/TEST_STRATEGY.md) owns coverage. The two active plans are [RESP then L2DB](../docs/plans/README.md).

## Current implementation versus plan

The current `SqlTestDatabase.CreateAsync` helper creates a fresh, uniquely owned database per call; Respawn is not installed. SQL module projects and the shared harness already exist. RESP will add reusable fixtures and adapt lifecycle accounting before moving eligible tests. New RESP/L2DB task IDs are not yet in `test-gates.v1.json`.

| Target | Owners |
|---|---|
| `net48` | Application, Architecture, Desktop, Desktop.Session, SQL modules, SqlTestHarness.Tests |
| `netstandard2.0` | SqlTestHarness shared library, not a runnable test project |
| `net8.0` | tools/MyDmsVn.SqlTestRunner and its tests only |
| Parked | Product/application `net8.0` and Desktop `net8.0-windows` |

The six active SQL projects are `MyDmsVn.Server.Infrastructure.IntegrationTests`, `.Foundation.Tests`, `.Catalog.Tests`, `.Inventory.Tests`, `.Security.Tests` and `.Migrations.Tests`, each under `tests/` with the matching `.csproj`. Some infrastructure classes are classified non-SQL; inspect source and the current inventory rather than inferring behavior from an assembly name.

Use Windows with the SDK selected by `global.json` and .NET Framework 4.8 targeting/runtime support. The checked-in SDK request is `8.0.400` with `rollForward: latestFeature`; do not infer an exact installed patch. Run examples from the repository root and stop on a nonzero command exit. Restore/build the changed revision before `--no-build` or `--no-restore`.

## Focused non-SQL example

For a Catalog Application behavior change, the existing source contains `CatalogApplicationTests`:

```powershell
$testProject = 'tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj'
dotnet restore $testProject --locked-mode
dotnet test $testProject -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~CatalogApplicationTests'
```

This is an example for that behavior, not a required check for unrelated changes. Select a real source/discovered class and reject an empty selection. Pure ViewModel/session cases belong in their actual project; controls/focus tests use the GUI safety path below.

## SQL configuration and preflight

The existing harness requires both `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` (effective database `master`) and a valid local operator-maintained `MYDMSVN_SQL_TEST_APPROVAL_FILE`. The approval binds a non-production attestation to the live instance and expiration/configuration metadata. The test principal must have the required database create/drop and version-specific DMV permissions. Missing/mismatched approval fails before DDL, including direct IDE runs. Do not fabricate approvals, print their contents or silently select a database.

If the values are already stored at Windows User scope, load them without printing:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING = [Environment]::GetEnvironmentVariable('MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING', 'User')
$env:MYDMSVN_SQL_TEST_APPROVAL_FILE = [Environment]::GetEnvironmentVariable('MYDMSVN_SQL_TEST_APPROVAL_FILE', 'User')
$runnerProject = 'tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj'
dotnet restore $runnerProject --locked-mode
dotnet build $runnerProject -c Release -f net8.0 --no-restore
dotnet run --project $runnerProject -c Release -f net8.0 --no-build -- preflight
```

Preflight exit `0` validates the current SQL environment; `2` means blocked configuration. It is not test execution. Source of the approval contract: [SqlTestHarness](MyDmsVn.SqlTestHarness/SqlTestHarnessEnvironment.cs) and related types in that project. Preserve the operator-managed local file; this documentation reset does not replace the executable approval mechanism.

## Focused SQL by actual module/category

The current wrapper accepts **one TestProject and one Category**. Categories are `FastSQL`, `MigrationSchema`, `SecurityProvisioning`, `SqlConcurrency`; there is no class/method `-Filter` parameter. Explicitly pass `-Frameworks @('net48')`: this wrapper still defaults to both net48/net8.0 if omitted.

After building the runner above, a Catalog SQL change can use:

```powershell
$sqlProject = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
dotnet restore $sqlProject --locked-mode
dotnet build $sqlProject -c Release -f net48 --no-restore
$artifactRoot = '.sqlperf/cat-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location).Path -TestProject $sqlProject `
  -ArtifactRoot $artifactRoot -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category FastSQL -Frameworks @('net48')
```

The runner owns classification/discovery/Theory/TRX reconciliation, watchdog and post-run ownership cleanup. A focused run validates only its selected category/project. Do not route SQL through unguarded `dotnet test` to avoid a failing runner check. Use a new short artifact path for each run to preserve net48 Windows path headroom.

## Strict one-project SQL acceptance

When the plan requires every case in a module, the existing lower-level wrapper takes no category/filter. It requires a clean committed tree and binds the run to HEAD. Restore/build the runner and chosen SQL project first; its exact parameters are:

```powershell
$sqlProject = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
$artifactRoot = '.sqlperf/cat-full-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
& ./tests/tools/Invoke-SqlPrAcceptance.ps1 `
  -RepositoryRoot (Get-Location).Path -TestProject $sqlProject `
  -ArtifactRoot $artifactRoot -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Frameworks @('net48')
```

This runs one unfiltered pass for that project/TFM, without a warm-up or measured repetitions. It is not all six SQL projects and is not the complete RESP/L2DB acceptance by itself. The owning plan must bind every required project and its new fixture-aware inventory.

## Existing manifest runner

[Invoke-TestGate.ps1](tools/Invoke-TestGate.ps1) accepts `Mode` (`Task`/`Final`), exact `PlanId`, `TaskId`, `ArtifactRoot`, and optional `RepositoryRoot`, `ManifestPath`, `ApprovalFile`, `PreflightOnly`. It has **no project, framework or filter override**.

- Task currently supports registered non-SQL/non-GUI scopes. Do not classify SQL or controls as NonSql to get through it.
- Final requires a clean committed candidate and SQL approval; it runs the envelope actually registered in [test-gates.v1.json](test-gates.v1.json).
- Final with `-PreflightOnly` allows a dirty tree and no SQL approval because it checks source/fingerprints/ownership/discovery metadata without SQL test execution. A successful preflight is not a Final PASS.
- Old plan IDs remain valid configuration even when their Markdown has been removed. They do not reopen the old roadmap. RESP/L2DB must register/review their own tasks before any command claims those IDs are runnable.
- Exit codes: `0` complete, `2` resource/preflight denial, `3` incomplete execution/reconciliation, `64` invalid usage.

[sqlperf-t01-migration-invocations.json](tools/sqlperf-t01-migration-invocations.json), [sql-test-nonsql-exemptions.json](tools/sql-test-nonsql-exemptions.json), logical-case entries and source fingerprints are executable inputs, not obsolete documentation. RESP must update fixture-level migration/timing/ownership attribution with its runner tests. Do not delete or mechanically refresh these files to silence drift.

## GUI safety and actual launcher

[Invoke-GuiTestAcceptance.ps1](tools/Invoke-GuiTestAcceptance.ps1) takes `TestProject`, `ArtifactRoot`, exact 40-character `Revision` matching HEAD, optional `RepositoryRoot` and `TimeoutSeconds` (1–900, default 900). It runs active `net48`, requires a clean committed tree and has no framework/filter parameter. Build the runner and Desktop test project for the candidate before invoking it.

Use that guarded full-project path only when the change/plan requires it. Existing `StaTest` and `WinFormsTestGuard` arrange STA, cancellation/draining, exception/DataError capture and dialog suppression. Unhandled fatal-process probes require bounded subprocess isolation; an AppDomain notification cannot prevent process termination. Keep GUI process/resource serialization.

## Reuse transition and diagnostics

Current SQL assembly guards and global SQL leases remain. xUnit collection coordination does not extend across processes; every future reusable allocation still has a unique run/assembly/TFM/collection owner. Real concurrency probes retain separate sessions and verified lock handshakes; they must drain before reset or disposal.

The planned Respawn 4 API uses `Checkpoint.Reset(openDbConnection)` and immutable per-fixture reset configuration, with version-specific table-name filters. The explicit fresh/reuse comparison mode, reset profiles, per-fixture accounting and new task IDs are future RESP deliverables. Do not use those flags or assert reuse before they exist.

The old `Invoke-SqlIntegrationBenchmark.ps1` multi-pass baseline remains available for an explicitly requested measurement. It is not the routine command for either per-task checks or every PR. RESP's matched comparison has its own bounded protocol in the plan.

Keep raw TRX, timing, theory values, approval/ownership journals, SQL identifiers and paths local. Publish reviewed sanitized summaries with actual scope/results and cleanup. Timeout, malformed/missing results, unexplained missing cases or ambiguous ownership is incomplete/failed evidence, never a green acceptance.
