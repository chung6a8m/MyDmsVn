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

For a local Windows-authenticated development instance:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING = 'Server=localhost;Database=master;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
dotnet test tests\MyDmsVn.Server.Infrastructure.IntegrationTests\MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release
```

The harness creates a unique database named `MyDmsVn_Test_<32 hex digits>`, disables pooling for its database connection, and only drops the exact generated name it owns. Name validation rejects production-like names and incomplete test prefixes. Never point the variable at production or grant the test principal access to production data.

When the variable is absent, SQL-dependent tests are reported as skipped with the missing-variable reason; guardrail tests still run. The repository acceptance gate sets the variable and runs both `net48` and `net8.0` test targets.

The deployment migrator is a separate executable and is not run by desktop startup. P6 replaced the original shared setting with the migration-only `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING` or `MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE`; see `docs/CONFIGURATION_AND_SQL_SECURITY.md`. The target must be an already-created application database. The migrator does not create or drop databases and rejects missing catalogs, SQL Server system databases, unmanaged databases, future/forked history and partial schema. The `dbo.P1TestProbe` fixture is embedded only in this test project and is never loaded by the deployment migrator.
