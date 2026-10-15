# Test harness

P0 tests require Windows, the .NET 8 SDK selected by `global.json`, and the .NET Framework 4.8 targeting pack/runtime. Run from the repository root:

```powershell
dotnet restore MyDmsVn.sln --locked-mode
dotnet build MyDmsVn.sln -c Release --no-restore
dotnet test MyDmsVn.sln -c Release --no-build --no-restore
```

WinForms tests create and dispose controls on an STA thread through `StaTest`. Each action receives a cancellation token; a timeout cancels the action and waits for cleanup before returning. Actions that cannot cooperate with cancellation, including fatal-process scenarios, must run in a bounded subprocess.

`WinFormsTestGuard` captures `Application.ThreadException` and `DataGridView.DataError`, subscribes to `AppDomain.UnhandledException` as last-chance diagnostics, and enables the Windows thread error mode that suppresses critical-error and general-protection-fault dialogs. Unconsumed captured exceptions fail the test when the guard is disposed; a test that intentionally raises an expected exception must consume it with `DrainExceptions()` and assert it. An `AppDomain.UnhandledException` notification cannot prevent process termination, so tests must catch/await worker failures before they become unhandled and use subprocess isolation when intentionally testing fatal behavior.

## P1 SQL integration tests

P1 SQL tests require a reachable SQL Server instance and a test administrator login that can create and drop databases. The connection string must be supplied explicitly through `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` and must target the `master` database. There is no fallback connection string.

The P5.3 SQL-concurrency gates also observe blocked requests and their lock resources through `sys.dm_exec_requests` and `sys.dm_tran_locks` from a separate SQL session. Their test login therefore requires the server-level `VIEW SERVER STATE` permission on SQL Server 2019 (15.x) and earlier, or `VIEW SERVER PERFORMANCE STATE` on SQL Server 2022 (16.x) and later. The tests preflight the version-specific permission and fail immediately with the required `GRANT` when it is absent. Grant this monitoring permission only on the disposable-test SQL Server instance.

SQLPERF-T02 concurrency tests use a unique test-only `sp_getapplock` release gate instead of a fixed SQL delay. A controller session acquires the session-owned gate before the holder starts; the trigger waits transaction-owned while retaining its business locks. A separate observer must identify the holder's APP wait and expected granted business-table lock, then the contender's wait on the expected business resource, before releasing the controller. Cancellation or failure releases the gate and drains every started task before database disposal. Do not replace this sequence with `WAITFOR`, `Task.Delay` or an uncommitted signal-table read.

SQL test database mutation now also requires a separately maintained approval file. The file is local/operator-managed and must never be committed. It binds an explicit non-production attestation to the exact live SQL instance fingerprint and expiration/configuration metadata. Both variables are required; missing or mismatched approval fails closed before `CREATE DATABASE`, including direct IDE/VSTest runs.

For an approved local Windows-authenticated development instance:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING = 'Server=localhost;Database=master;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
$env:MYDMSVN_SQL_TEST_APPROVAL_FILE = 'C:\local-secrets\sql-test-approval.json'
dotnet test tests\MyDmsVn.Server.Infrastructure.IntegrationTests\MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release
```

Environment variables saved at Windows `User` scope are inherited only by processes started after the change. Restart the terminal/Codex host, or load the values into the current PowerShell process without printing them:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING = [Environment]::GetEnvironmentVariable('MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING', 'User')
$env:MYDMSVN_SQL_TEST_APPROVAL_FILE = [Environment]::GetEnvironmentVariable('MYDMSVN_SQL_TEST_APPROVAL_FILE', 'User')
dotnet run --project tests\tools\MyDmsVn.SqlTestRunner\MyDmsVn.SqlTestRunner.csproj -c Release --no-build -- preflight
```

Preflight exit code `0` confirms that the effective `master` connection, approval identity, expiration and version-specific monitoring permission are valid. Exit code `2` is blocked configuration and must stop the run before DDL. Do not echo either environment variable or commit the approval file.

The repeatable SQLPERF runner builds separately, then runs TFMs sequentially under a Windows Job Object and requires complete TRX output:

```powershell
dotnet build MyDmsVn.sln -c Release --no-restore
tests\tools\Invoke-SqlIntegrationBenchmark.ps1 `
  -RepositoryRoot (Get-Location) `
  -TestProject tests\MyDmsVn.Server.Infrastructure.IntegrationTests\MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot .sqlperf\run-001 `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
```

Raw TRX, journals and timing JSONL stay ignored/local. Timing is off by default; the runner sets `MYDMSVN_SQL_TEST_TIMINGS=1`, a unique run ID and a local output path. A timeout, missing/corrupt TRX, nonzero exit, failed/skipped mandatory case, inventory mismatch, ownership ambiguity or approval failure makes the run incomplete.

For bounded development iteration, run one explicit SQL responsibility category on both TFMs:

```powershell
tests\tools\Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location) `
  -TestProject tests\MyDmsVn.Server.Infrastructure.IntegrationTests\MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot .sqlperf\focused-fastsql `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category FastSQL
```

Valid categories are `FastSQL`, `MigrationSchema`, `SecurityProvisioning` and `SqlConcurrency`. `FastSQL` contains only tests that access SQL Server; pure tests remain in the reviewed method-level non-SQL exemption manifest. Exemption fingerprints cover reviewed integration-test, harness and application source while excluding generated build output, so async/generated or helper changes require review and ordinary rebuild/commit metadata does not create drift. The helper runs discovery/execution under the owned-process-tree watchdog, reports selected methods plus discovered/executed/skipped cases per TFM, reconciles theory rows and ownership after every launched execution, and fails closed on a mismatch or timeout. A focused green run never replaces the unfiltered benchmark/release gate.

The harness creates a unique database named `MyDmsVn_Test_<32 hex digits>`, disables pooling for its database connection, and only drops the exact generated name it owns. Name validation rejects production-like names and incomplete test prefixes. Teardown does not call global `SqlConnection.ClearAllPools()`: owned database and production migrator connections are non-pooled, and the one test that deliberately enables a migration pool clears that exact pool itself. `SINGLE_USER WITH ROLLBACK IMMEDIATE` remains the guarded cleanup boundary for positively owned databases; unrelated pools must remain intact. Never point the variable at production or grant the test principal access to production data.

When either required variable is absent, SQL-dependent tests are reported as skipped with the missing-variable reason; guardrail tests still run. Skips are convenient for local non-SQL work but are invalid for SQL acceptance. The repository acceptance gate sets both variables and runs both `net48` and `net8.0` targets sequentially.

The deployment migrator is a separate executable and is not run by desktop startup. P6 replaced the original shared setting with the migration-only `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING` or `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE`; see `docs/CONFIGURATION_AND_SQL_SECURITY.md`. The target must be an already-created application database. The migrator does not create or drop databases and rejects missing catalogs, SQL Server system databases, unmanaged databases, future/forked history and partial schema. The `dbo.P1TestProbe` fixture is embedded only in this test project and is never loaded by the deployment migrator.
