# Test projects and commands — v1a

**Updated:** 2026-10-18 after the owner's SQL test framework removal. [TEST_POLICY](../docs/TEST_POLICY.md) owns execution scope and [TEST_STRATEGY](../docs/TEST_STRATEGY.md) owns coverage. The two active plans remain [RESP, then L2DB](../docs/plans/README.md).

## Current implementation

Run test projects directly with `dotnet test` or Test Explorer. The standalone SQL framework, its projects/tests, gate and invocation manifests, and command wrappers have been removed. The local [SqlTestDatabase](MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs) helper creates a fresh uniquely owned database per call. Respawn and reusable fixtures remain planned; no fixture-mode setting exists yet.

All ten active test projects target **net48**:

| Responsibility | Project under `tests/` |
|---|---|
| Application/contracts | `MyDmsVn.Server.Application.Tests` |
| Architecture | `MyDmsVn.Architecture.Tests` |
| Pure Desktop/session | `MyDmsVn.Desktop.Session.Tests` |
| Desktop/WinForms | `MyDmsVn.Desktop.Tests` |
| SQL foundation | `MyDmsVn.Server.Infrastructure.Foundation.Tests` |
| SQL catalogs | `MyDmsVn.Server.Infrastructure.Catalog.Tests` |
| SQL inventory | `MyDmsVn.Server.Infrastructure.Inventory.Tests` |
| SQL security | `MyDmsVn.Server.Infrastructure.Security.Tests` |
| SQL migrations/schema | `MyDmsVn.Server.Infrastructure.Migrations.Tests` |
| Shared SQL helper and remaining Local scenarios | `MyDmsVn.Server.Infrastructure.IntegrationTests` |

Each directory contains the matching `.csproj`. The last six are separate SQL owners; the original IntegrationTests project no longer means the entire SQL suite. Some classes in those projects are pure code. Inspect source before selecting them; an assembly name alone does not classify a case.

Use Windows with the SDK selected by `global.json` and .NET Framework 4.8 targeting/runtime support. The SDK request is `8.0.400` with `rollForward: latestFeature`; it is a build tool, not a modern product/test target. AppCore and historical modern product targets are PARKED / NOT RUN. Run commands from the repository root. Stop on failed restore/build/test; restore/build the current revision before using `--no-restore` or `--no-build`.

## Focused non-SQL example

For a Catalog Application change, the current source contains `CatalogApplicationTests`:

```powershell
$project = 'tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~CatalogApplicationTests'
if ($LASTEXITCODE -ne 0) { throw 'Application tests failed.' }
```

Select a real source/discovered class or method, then review the executed cases; an empty selection is not a successful check. Pure ViewModel/session tests use their actual project. Controls/focus tests use the Desktop section below.

## SQL configuration

Set only `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` for an operator-designated **non-production** SQL Server, with explicit effective database `master`. The account needs database create/drop rights and any privileges required by the selected principal/maintenance/DMV scenarios. Do not use a production connection or the application's runtime/migration configuration.

If the reviewed value is already stored at Windows User scope, load it without printing:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING = [Environment]::GetEnvironmentVariable('MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING', 'User')
if ([string]::IsNullOrWhiteSpace($env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING)) {
  throw 'Configure the designated SQL test instance before running required SQL coverage.'
}
```

The helper rejects blank instance/catalog input, non-master catalogs, `AttachDBFilename` and `UserInstance` before opening SQL. No approval JSON or external preflight command is used. `SqlServerFact` skips when the connection is absent; those cases remain NOT RUN for mandatory SQL evidence. Invalid configured input or failed live ownership checks fail the test. The helper never silently chooses an instance.

## Focused SQL project/category

The retained `SqlServerFact` trait uses `Category`: `FastSQL`, `MigrationSchema`, `SecurityProvisioning` and `SqlConcurrency`. Standard VSTest `--filter` syntax can select one category or a source-verified class/method. A case can carry several categories, so category totals are not necessarily disjoint.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
$results = 'TestResults/cat-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=catalog.trx' `
  --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'Catalog SQL tests failed.' }
```

Omit `--filter` for a complete project pass. Substitute the exact owner and category for another affected scope. A focused pass proves only the selected cases. `dotnet test <project> -c Release -f net48 --list-tests --filter <filter>` can inspect discovery after restore; it does not prove SQL execution or cleanup.

## Complete SQL milestone

Only use all six owners when required by TEST_POLICY or a plan milestone. Execute them sequentially, with one unfiltered pass per project and separate results:

```powershell
$owners = @('Foundation.Tests', 'Catalog.Tests', 'Inventory.Tests',
  'Security.Tests', 'Migrations.Tests', 'IntegrationTests')
$results = 'TestResults/sql-' + [guid]::NewGuid().ToString('N').Substring(0,8)
foreach ($owner in $owners) {
  $name = 'MyDmsVn.Server.Infrastructure.' + $owner
  $project = 'tests/' + $name + '/' + $name + '.csproj'
  dotnet restore $project --locked-mode
  if ($LASTEXITCODE -ne 0) { throw ('Locked restore failed: ' + $name) }
  dotnet test $project -c Release -f net48 --no-restore `
    --logger ('trx;LogFileName=' + $owner + '.trx') `
    --results-directory $results
  if ($LASTEXITCODE -ne 0) { throw ('SQL project failed: ' + $name) }
}
```

Review each TRX's required cases, Theory rows, pass/fail/skip counts and cleanup failures. A zero exit with SQL skipped is not SQL acceptance. Use the current source and plan case map to explain additions/replacements/omissions; there is no separate gate registry, fingerprint approval or automated case-reconciliation service.

This loop does not automatically run Application, Architecture, Desktop.Session or Desktop. Add those project commands only when their affected scope or milestone requires them. Use a new short local results directory for each run; raw artifacts are not committed evidence.

## SQL helper lifecycle and interruption

The shared source stays inside the existing IntegrationTests project. Catalog references that assembly; Foundation, Inventory, Security and Migrations compile linked helper copies. Keep that project ownership explicit when editing helpers.

The helper requires `master`, generates a `MyDmsVn_Test_` GUID name, and records the live instance, database ID/creation time and random `MyDmsVn.TestOwnership` marker. It verifies those values before destructive cleanup. Helper connections disable pooling; disposing them does not clear unrelated connection pools. A missing/changed marker or changed allocation identity refuses deletion. It never adopts an arbitrary existing database.

The helper disables connection retry, caps connection timeout at 30 seconds, uses 30-second SQL command timeouts and applies a two-minute cancellation budget to each create/dispose lifecycle. These are local operation bounds, not an external test-process timeout. A cleanup failure remains visible and can be retried where ownership still matches. Failure before ownership is confirmed leaves the allocation for manual review rather than guessing from its name. After a killed process, an operator reviews any leftover database on the designated test instance: there is no automatic orphan cleanup or durable allocation journal. Never bulk-drop by name prefix.

Keep existing SQL assembly serial guards, and run SQL projects sequentially. IntegrationTests, Foundation, Inventory, Security and Migrations have assembly guards; Catalog currently has none and uses independent fresh allocations. Its future shared fixture must explicitly collect and serialize all consumers. There is no cross-process lease. Concurrency scenarios retain independent SQL sessions and verified lock handshakes; their workers, transactions and readers must drain before database disposal or a future reset.

## Desktop / WinForms

Run Desktop cases directly from an interactive Windows session. Existing `StaTest`, `WinFormsTestGuard`, assembly serialization, bounded waits, dialog suppression and exception/DataError capture remain in the project. Execute one GUI command at a time; no external GUI launcher or global lease remains.

```powershell
$project = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --logger 'trx;LogFileName=desktop.trx' `
  --results-directory ('TestResults/gui-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed.' }
```

Add a real `FullyQualifiedName` filter for affected focused work; the example is unfiltered and is required only for an applicable broader scope. Future fatal or non-cooperative hang probes must implement their own bounded subprocess isolation. Current `ReleasePackagingTests` waits for its PowerShell child without a timeout; direct execution has no suite watchdog. Stop a hung test/child process explicitly and record incomplete evidence. Automated GUI results do not replace attended packaged workflows.

## Planned reuse and evidence

RESP will add its compatible Respawn package/reset profiles, xUnit collection adapters and explicit fresh/reuse comparison mode in the existing SQL test projects. Those APIs/settings are future deliverables. Keep the original behaviors/Theory rows and report ordinary TRX results plus lightweight allocation/migration/reset/seed/drop observations; do not recreate the removed framework.

Report exact revision, commands, project/TFM/filter, actual counts/outcomes and unresolved cleanup. Missing Windows, targeting packs, SQL or GUI resources is BLOCKED / NOT RUN. Keep connection data, raw TRX, Theory values, SQL identifiers and detailed diagnostics local; publish sanitized summaries. No old test totals or benchmark result proves either active plan has passed.
