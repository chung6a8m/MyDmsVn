# P1 persistence verification

Date: 2026-10-08

## Scope

P1 adds SQL Server connection ownership, an explicit UnitOfWork, explicit repository registration, RepoDb command access, Dapper reads, a controlled DbUp migrator, and an isolated SQL integration-test harness. The only schema in this phase is `dbo.P1TestProbe`, an intentionally test-only foundation table. No P3/P5 identity, catalog, receipt, ledger, or balance schema is implemented.

## Compatibility notes

- `MyDmsVn.Server.Application`, `MyDmsVn.Server.Infrastructure`, `MyDmsVn.Server.DbMigrator`, and SQL integration tests compile for `net48` and `net8.0`.
- RepoDb remains pinned at `1.16.0`; RepoDb.SqlServer remains pinned at `1.16.1`.
- RepoDb.SqlServer `1.16.1` initializes through `GlobalConfiguration.Setup().UseSqlServer()`. The older public `SqlServerBootstrap.Initialize()` example is not available in this pinned assembly.
- RepoDb.SqlServer.BulkOperations remains pinned at `1.16.2` centrally but is not referenced because P1 has no approved bulk operation. Bulk verification is deferred until a feature requires it.
- Dapper remains the explicit read path and receives the UoW connection and transaction directly.
- Microsoft.Data.SqlClient command cancellation can surface as `OperationCanceledException` or a cancellation `SqlException`; tests assert safe transaction disposal and subsequent connectivity rather than promising one exception type across TFMs/providers.

## SQL safety and migration evidence

- Tests require `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` targeting `master`; no default or developer database fallback exists.
- The harness creates only `MyDmsVn_Test_<guid>` databases and validates that exact pattern before drop.
- DbUp embeds numbered immutable scripts and journals them in `dbo.SchemaVersions`.
- Applying `001_TestFoundation.sql` to a clean database creates one history row; applying it again executes zero scripts and preserves one history row.
- The migrator is a separate command using `MYDMSVN_SQLSERVER_CONNECTION_STRING`; desktop composition does not invoke it.

## Covered SQL behaviors

- fresh open connection per UoW and pre-canceled open;
- legal UnitOfWork state transitions and nested-begin guard;
- commit across two explicitly registered repositories;
- rollback after the second repository operation fails;
- rollback on dispose without commit;
- RepoDb write visible to Dapper inside the same uncommitted transaction;
- parallel UoWs use different connection and transaction objects;
- repository and Dapper calls use the same SQL Server session;
- canceled command cleanup leaves a new connection usable;
- architecture reflection test rejects static `AsyncLocal<T>` storage in persistence infrastructure.

## Final command results

- `dotnet restore MyDmsVn.sln --locked-mode`: passed.
- `dotnet build MyDmsVn.sln -c Release --no-restore`: passed with 0 warnings and 0 errors for all solution targets, including `net48`, `net8.0`, and `net8.0-windows`.
- `dotnet test MyDmsVn.sln -c Release --no-build --no-restore` with the explicit local SQL test connection: passed 77/77 target-specific test executions with 0 failures and 0 skips.
- SQL integration subset: 25/25 passed on `net48` and 25/25 passed on `net8.0`; disposable database creation/drop, failure-retry cleanup, scoped repository disposal, exceptional UoW cleanup, and DbUp no-op replay were exercised.

The local verification instance reported SQL Server `14.0.2130.4`. No GUI smoke session was required because P1 changes no UI behavior; existing unattended WinForms tests remained green on both desktop targets.
