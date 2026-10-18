# Test projects and commands — v1a

**Updated:** 2026-10-18 after the owner's SQL test framework removal. [TEST_POLICY](../docs/TEST_POLICY.md) owns execution scope and [TEST_STRATEGY](../docs/TEST_STRATEGY.md) owns coverage. The two active plans remain [RESP, then L2DB](../docs/plans/README.md).

## Current implementation

Run test projects directly with `dotnet test` or Test Explorer. The standalone SQL framework, its projects/tests, gate and invocation manifests, and command wrappers have been removed. The local [SqlTestDatabase](MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs) helper creates a fresh uniquely owned database per call. RESP-T01 pins Respawn 4.0.0 and verifies the test-only application reset profile on net48. RESP-T02 implements exclusive collection lifecycle and the fixture-mode setting; RESP-T03 adopts all five Catalog business methods in one collection with its adapter/contamination probes. RESP-T04 also adopts seven Inventory and eight Identity/Security data-only cases; reviewed lifecycle/schema/principal/trigger/contention exceptions remain fresh.

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

## Accepted reuse and evidence

RESP-T01 provides the compatible Respawn package and owned full-schema reset profile. RESP-T02 adds exclusive scopes, tracked workers/cleanup and xUnit collection adapter probes inside existing owners. Set `MYDMSVN_SQL_TEST_FIXTURE_MODE` to `fresh` or `reuse` for participating fixtures; absent/blank defaults to reuse, matching is case-insensitive with surrounding whitespace ignored, and any other value fails. T03–T04 adopt all 20 original eligible methods (Catalog 5, Inventory 7, Identity/Security 8); default reuse is now implemented. Dedicated exceptions continue using their own fresh helper in either mode. Keep the original behaviors/Theory rows and report ordinary TRX results plus lightweight allocation/migration/reset/seed/drop observations; do not recreate the removed framework.

Report exact revision, commands, project/TFM/filter, actual counts/outcomes and unresolved cleanup. Missing Windows, targeting packs, SQL or GUI resources is BLOCKED / NOT RUN. Keep connection data, raw TRX, Theory values, SQL identifiers and detailed diagnostics local; publish sanitized summaries. No old test totals or benchmark result proves either active plan has passed.

The RESP-T01 reset probes run directly in Foundation using `FullyQualifiedName~ReusableSqlFixtureIntegrationTests` (11 SQL Facts); the T01 snapshot had 27 Foundation cases; after T02 the complete project has 41 cases. They always allocate fresh owned databases to challenge reset/ownership/schema/failure boundaries. See [case map](../docs/RESPAWN_CASE_MAP.md#resp-t01-additions) and [verification](../docs/RESPAWN_VERIFICATION.md).

RESP-T02 local scope/parser tests use IntegrationTests with `FullyQualifiedName~ReusableSqlFixtureTests` (14 cases after T04); combine with `FullyQualifiedName~SqlTestDatabase` for 36 selected cases. T04 renames the parser test to `Mode_parser_uses_reuse_default_and_accepts_explicit_modes` and adds a local regression that rejects a seed assertion masquerading as the expected scenario failure. Foundation lifecycle probes use `FullyQualifiedName~ReusableSqlFixtureLifecycleIntegrationTests` (14 cases, including one local probe-cleanup regression). Catalog adapter probes use `FullyQualifiedName~SqlFixtureCollectionAdapterTests` (one Fact and two Theory rows). After T03, the Catalog `Category=FastSQL` selection and complete project each contain ten cases: five original business cases, three adapter cases and two contamination Theory rows. Every Theory row resets and seeds independently.

Participating fixtures migrate/prepare once per allocation, reset/seed/verify before every admitted body, then retain exclusive admission until tracked workers and resource cleanup drain. Register every background worker and owned resource before returning the scope. A cleanup/reset/seed/boundary failure poisons reuse; a body assertion failure with successful drain allows the next reset. Fresh mode uses that same contract on a new database per case. Catalog initialization and all final-drop diagnostics use xUnit's fixture message sink; lazy Inventory/Security initialization is reported once in the first participating member's output. Per-case observations use test output. Do not count shared migration once per test or double-count runner summaries.

`MYDMSVN_SQL_TEST_ADAPTER_PROBE` is an opt-in fault seam for the selected Catalog collection consumers: `initialize-failure`, `reset-failure`, `seed-failure`, `schema-failure`, `trigger-failure`, `cleanup-failure`. Leave it unset for normal execution. In explicit reuse initialization failure affects every selected test. For the original five business methods plus three adapter rows, other fault selections must report eight failed cases, no skips; cleanup-only failure permits exactly the first body before terminal poison. The deliberate scenario-failure challenge is excluded from those eight-member fault selections. These expected failing development runs are recorded separately from passing verification. Restore the caller's prior process environment after changing either setting. See [T03 evidence](../docs/RESPAWN_VERIFICATION.md#resp-t03-execution-record--catalog-adoption).

The Catalog collection uses the complete APP reset profile, followed by an explicit empty catalog/User baseline; the three adapter cases additionally seed/assert one User. Employee linkage remains seeded inside the original CRUD scenario. All application-owned DI providers/scopes, UoWs and direct scenario connections register cleanup with the exclusive test scope; disposal failure poisons reuse. No business UoW or transaction is shared across cases.

`FullyQualifiedName~CatalogFixtureReuseIntegrationTests` selects the two reordered contamination rows (`reverse: False` / `reverse: True`). Each row deliberately commits catalog/User sentinels, catches the expected assertion failure after successful cleanup, then calls all five original business bodies in a controlled order and verifies a final empty baseline. This is **two xUnit cases / fourteen scenario scopes**, not fourteen results. A complete Catalog pass is **ten cases / twenty-two scopes**: reuse allocates/migrates/drops once; explicit fresh does so twenty-two times with identical business assertions. Per-case reset/seed/verify/cleanup counts cover every nested scope. Use the existing Foundation `Outstanding_worker_blocks_reset_until_drained` and `Reset_cancellation_drains_before_rejecting_the_waiting_scope` methods for the dedicated multi-session/drain check; business contention cases remain fresh.

RESP-T04 uses the linked `ApplicationSqlCollectionFixture` in Inventory and Security, with one independent APP allocation per participating owner/collection. Its xUnit initialization is lazy: selecting only a fresh exception in a mixed class does not evaluate the fixture mode/fault setting or allocate a reusable database. Existing assembly serialization remains. Query `SeedAsync` runs in the seed callback, followed by cached-boundary verification; other data scenarios start from an explicitly asserted empty 15-table APP baseline and retain their own deterministic arrangement. Returned IDs and actual rowversion reads remain the authority; database-wide counters do not rewind.

`FullyQualifiedName~ApplicationDataFixtureReuseIntegrationTests` selects one new SQL Fact in each owner. Each Fact checks complete 15-table contamination (including receipts/ledger/balances, permissions/revisions/audit/binding and sequence advancement), then an exact expected assertion failure, all eligible business scenarios in forward/reverse order and final empty baselines. It is one xUnit result with 18 Inventory or 20 Security scopes. The complete eligible selections therefore have **Inventory 8 cases / 25 scopes**, **Security 9 cases / 28 scopes**. Other methods in mixed classes remain fresh; select exact eligible methods from the [case map](../docs/RESPAWN_CASE_MAP.md#resp-t04-adoption-and-frozen-candidate) when checking reuse alone.

`MYDMSVN_SQL_TEST_APPLICATION_PROBE` is the opt-in Inventory/Security fault seam: `initialize-failure`, `reset-failure`, `seed-failure`, `schema-failure`, `trigger-failure`, `cleanup-failure`. Leave it unset for ordinary runs. Select the seven/eight original eligible methods (exclude contamination probes) for failure evidence; all selected members must fail, no skips. The seed fault fills all 15 mutable tables before throwing. Lazy setup means the first participating member exposes an initialization error and later members reject poison. Cleanup-only failure allows exactly one body. A focused fresh-exception selection must still pass with this fault set to initialize-failure, with no reusable allocation/observation.

The accepted RESP-T05 candidate has **257 cases** across the six SQL owners: Foundation 41, Catalog 10, Inventory 36, Security 53, Migrations 65, IntegrationTests 52. One complete sequential explicit fresh pass and one explicit reuse pass each passed all 257 with zero failed/skipped cases and successful cleanup. Focused parser/default adapters also passed. See [matched acceptance and exact baseline](../docs/RESPAWN_VERIFICATION.md#resp-t05-acceptance--matched-complete-owners), including wall-time/phase observations and their limits. Keep both fault settings unset for ordinary runs. Preserve this test baseline during L2DB. See [T04 evidence](../docs/RESPAWN_VERIFICATION.md#resp-t04-execution-record--frozen-data-fixtures).
