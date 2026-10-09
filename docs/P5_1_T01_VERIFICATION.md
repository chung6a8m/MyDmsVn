# P5.1-T01 catalog schema and mapping verification

Date: 2026-10-09

## Scope

P5.1-T01 adds only the Product, Warehouse, Employee and Customer persistence foundation: an additive DbUp migration, Domain entities, explicit RepoDb mappings and SQL integration coverage. Commands, queries, permissions, Local adapters and WinForms screens remain for P5.1-T02 and later tasks.

## Schema and mapping decisions

- Catalog primary keys are `int IDENTITY` values and codes are `nvarchar(32)` with explicit `Latin1_General_100_CI_AI` collation and unique indexes.
- Mutable catalog rows carry `IsActive` plus nullable user audit identifiers and UTC `datetime2(7)` timestamps consistent with the Identity schema.
- `Employees.UserId` is a nullable FK to `dbo.Users(UserId)` and has a filtered unique index to preserve the documented zero-or-one employee relationship for a user.
- The production migration seeds no Product, Warehouse, Employee or Customer rows.
- `CatalogRepoDbMapping` explicitly maps every Domain property, SQL `DbType`, table, primary key and identity column, and is registered once through SQL persistence composition. Audit timestamps use `DbType.DateTime2` so RepoDb preserves `datetime2(7)` precision.

## Automated evidence

- The catalog migration test was observed failing with zero catalog tables before `003_Catalog.sql` was added, then passing after implementation.
- The RepoDb round-trip test was observed failing against the inferred `Product` table before mapping registration, then passing after explicit mappings were added.
- A mutation that changed `UX_Customers_Code` from unique to non-unique made the catalog schema test fail with 3 rather than 4 unique code indexes; restoring the constraint returned the test to green.
- The final review's timestamp regression was observed failing for all four entities because RepoDb inferred legacy SQL `datetime`; explicit `DbType.DateTime2` mappings made the fractional-tick audit assertions pass.
- SQL integration suite: 54/54 passed on `net48` and 54/54 passed on `net8.0`, using disposable `MyDmsVn_Test_<guid>` databases configured by `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`.
- `dotnet restore MyDmsVn.sln --locked-mode`: passed.
- `dotnet build MyDmsVn.sln -c Release --no-restore`: passed with 0 warnings and 0 errors across `net48`, `net8.0` and `net8.0-windows` targets.
- `dotnet test MyDmsVn.sln -c Release --no-build --no-restore`: passed 351/351 target-specific test executions with 0 failures and 0 skips.

No manual GUI smoke was required because P5.1-T01 changes no desktop behavior.

## Remaining work

P5.1-T02 is next: catalog commands, validators, deterministic Dapper queries, authorization/audit behavior and Local `ApiResponse<T>` adapters. P5.1 UI and gate scenarios remain unchecked.
