# P5.1-T02 catalog backend verification

Date: 2026-10-09

## Scope

P5.1-T02 adds backend use cases for Product, Warehouse, Employee and Customer: Create, Update, SetActive, List, GetById and active-only Lookup. It adds validation, deterministic conflict/not-found codes, Application authorization, user audit propagation, Dapper read DTOs, RepoDb command writes and Local `ApiResponse<T>` adapters. WinForms catalog screens, debouncing and same-process catalog-change messaging remain P5.1-T03/T04 work.

## Implementation decisions

- Every catalog request implements `IAuthorizedRequest` with its exact `Catalog.*.Read` or `Catalog.*.Write` key. Authorization runs before validation/handlers; a denied mutation never creates a UnitOfWork.
- Every mutation uses one explicit UnitOfWork and SQL transaction. Create stores `CreatedByUserId`; Update and SetActive store `UpdatedByUserId` plus a UTC timestamp.
- SQL unique/FK violations are translated at the Infrastructure boundary into deterministic catalog conflicts. Employee distinguishes duplicate code, duplicate non-null UserId and missing UserId.
- Dapper list queries use parameterized substring filters and stable `Code, Id` ordering. Lookup is parameterized, bounded and excludes inactive rows. Get/List preserve inactive rows for historical display.
- Update responses reload the persisted DTO after commit so an inactive row is never reported as active merely because its editable fields changed.
- Local clients only dispatch Application requests and map `ErrorOr<T>` to transport-neutral `ApiResponse<T>`.

## TDD evidence

- Application tests were observed failing to compile before catalog request/query contracts existed, then passing after the minimal Product slice.
- SQL integration was observed failing first because RepoDb Update/SetActive was absent, then because Warehouse/Customer handlers were absent, before the corresponding implementation was added.
- The inactive-update regression was observed returning `IsActive = true`; reloading the persisted DTO after commit made it return the actual inactive state.
- Local composition was observed failing because `IProductApiClient` was unregistered; explicit registration of all four catalog clients made the adapter test pass.

## Automated evidence

- Targeted Application catalog suite: 10/10 passed on `net48` and 10/10 passed on `net8.0`.
- Targeted SQL catalog backend suite: 1/1 passed on `net48` and 1/1 passed on `net8.0`, using isolated disposable databases configured by `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`.
- Targeted Local catalog adapter suite: 1/1 passed on `net48` and 1/1 passed on `net8.0-windows`.
- `dotnet restore MyDmsVn.sln --locked-mode`: passed.
- `dotnet build MyDmsVn.sln -c Release --no-restore`: passed with 0 warnings and 0 errors across `net48`, `net8.0` and `net8.0-windows` targets.
- `dotnet test MyDmsVn.sln -c Release --no-build --no-restore`: passed 383/383 target-specific test executions with 0 failures and 0 skips.

## Remaining work

P5.1-T03 is next: catalog list/editor ViewModels and WinForms screens. P5.1-T04 then adds the shared debouncer, catalog-change messaging and open-lookup refresh behavior. The overall P5.1 gate remains unchecked until those UI, messenger, STA and manual-smoke criteria have evidence.

## Review follow-up

PR review identified that unbounded or NUL-containing List/Lookup search text could reach SQL Server `CHARINDEX` and be reported as an internal error. All eight catalog List/Lookup validators now reject search text over 256 characters or containing NUL before invoking a query service. The regression test exercises both invalid forms across Product, Warehouse, Employee and Customer request types and asserts zero query-service calls.
