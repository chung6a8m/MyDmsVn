# P5.1 verification evidence

Date: 2026-10-10

## Scope

This evidence covers P5.1 master data for Products, Warehouses, Employees and Customers: SQL constraints, backend authorization and historical queries, shared desktop ViewModels, debounced filtering, same-process catalog notifications, WinForms controls and both desktop hosts.

`CatalogChangedMessage` is intentionally process-local. It does not notify another desktop process or observe direct SQL changes. Catalog and lookup activation always requery the server as the fallback; authoritative server-side validation remains mandatory.

Interactive Goods Receipt lookup smoke remains a P5.2 dependency and was not performed. The P5.1 manual-smoke checkbox therefore remains open.

## Automated results

- `dotnet build MyDmsVn.sln --no-restore` and `dotnet build MyDmsVn.sln -c Release --no-restore` — both succeeded with 0 warnings and 0 errors. The solution built the `net48`, `net8.0` and `net8.0-windows` target families.
- `dotnet test MyDmsVn.sln --no-build --no-restore --logger "console;verbosity=minimal"` and the same command with `-c Release` — 491/491 target executions passed in each configuration:
  - Architecture: 5/5 (`net8.0`).
  - Server Application: 81/81 on `net48` and 81/81 on `net8.0`.
  - Desktop: 100/100 on `net48` and 100/100 on `net8.0-windows`.
  - SQL Server integration: 62/62 on `net48` and 62/62 on `net8.0`; every test used its disposable `SqlTestDatabase`.
- `dotnet run --project src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -f net48 --no-restore -- --smoke-test` — exited 0.
- `dotnet run --project src/MyDmsVn.Desktop.AppCore/MyDmsVn.Desktop.AppCore.csproj -c Release -f net8.0-windows --no-restore -- --smoke-test` — exited 0.

## Gate mapping

- Uniqueness/collation: `DatabaseMigrationTests.Every_catalog_code_is_unique_case_and_accent_insensitively` exercises all four catalog tables at the SQL boundary.
- Employee.UserId: `Catalog_migration_creates_empty_tables_and_enforces_code_uniqueness_and_employee_user_fk` verifies FK rejection; `Employee_user_link_allows_many_nulls_but_only_one_employee_per_user` verifies nullable and one-user-per-employee-link behavior.
- Historical inactive references: `Inactive_catalog_rows_remain_historically_queryable_but_are_excluded_from_lookups` verifies Get/List retain inactive rows while all four Lookup queries exclude them.
- Authorization: `Explicitly_denied_catalog_write_leaves_business_tables_unchanged` verifies an explicit user deny overrides a role grant and does not insert a Product.
- Debounce and stale results: deterministic desktop tests cover the 300 ms default, final-call-only execution, page reset, immediate refresh, cancellation, generation-based stale-response rejection and disposal.
- Messenger lifetime: deterministic tests cover success-only typed publication, failure suppression, relevant-only two-recipient refresh, burst coalescing, disposal and activation refresh. An inactive selected lookup preserves its ID and label while becoming unavailable for a new selection.
- UI safety: STA tests cover catalog layout/tab order, commands, inline validation, active/inactive display, UI-thread marshaling, tab reuse and disposal. Desktop test parallelization is disabled because WinForms message loops and the Bootstrap theme manager are process-wide state.

## Remaining manual evidence

No human-observed interactive GUI session was performed. Before closing the roadmap-level P5.1 item, manually exercise rapid filtering and create/edit/activate/deactivate in all four catalog screens on both hosts. The open-Goods-Receipt propagation scenario cannot be exercised until the P5.2 receipt UI exists.
