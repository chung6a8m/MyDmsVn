# P5.2-T01 verification evidence

Date: 2026-10-10

## Scope

This evidence covers only the P5.2-T01 Goods Receipt tables and models: the immutable DbUp migration, SQL invariants, Domain entities, RepoDb mappings, opaque concurrency-token contracts and the corresponding data-model documentation.

P5.2-T02 command handlers, validators, server-issued receipt-number allocation, transactions and aggregate locking are not implemented here. P5.2-T03 queries and UI, and P5.3 posting/stock behavior, also remain open.

## Automated results

- `dotnet build MyDmsVn.sln -m:1 -c Debug --no-restore` — succeeded with 0 warnings and 0 errors.
- `dotnet build MyDmsVn.sln -m:1 -c Release --no-restore` — succeeded with 0 warnings and 0 errors.
- `dotnet test MyDmsVn.sln -m:1 -c Debug --no-build --no-restore --logger "console;verbosity=minimal"` — 523/523 target executions passed.
- `dotnet test MyDmsVn.sln -m:1 -c Release --no-build --no-restore --logger "console;verbosity=minimal"` — 523/523 target executions passed.

Both test commands used one MSBuild worker. The `net48`, `net8.0` and `net8.0-windows` test targets therefore ran sequentially rather than concurrently. SQL integration tests used an isolated disposable `SqlTestDatabase`.

Per configuration:

- Server Application: 82/82 on `net48` and 82/82 on `net8.0`.
- Desktop: 112/112 on `net48` and 112/112 on `net8.0-windows`.
- Architecture: 5/5 on `net8.0`.
- SQL Server integration: 65/65 on `net48` and 65/65 on `net8.0`.

## Evidence mapping

- Migration rerun/no-op and ordered history include `005_GoodsReceipt.sql`.
- SQL integration tests verify required columns, decimal precision/scale, all Goods Receipt foreign keys, unique receipt number, unique line number and mandatory unique product per receipt.
- Direct invalid SQL verifies duplicate keys, FK and NOT NULL failures, invalid state, nonpositive line number/quantity and negative unit cost.
- Updating a header advances its eight-byte SQL `rowversion`.
- RepoDb integration tests load both aggregate entities through the registered mappings and verify SQL column names and CLR value types.
- Contract golden JSON verifies Base64 `expectedVersion` on UpdateDraft/Post and the current `version` on `GoodsReceiptDto`; desktop client methods require those versioned request DTOs.

## Remaining risks and next task

The schema constrains the only stored states to Draft and Posted and requires complete posting audit values, but the legal one-way Draft-to-Posted transition is enforced by the future command paths. The next roadmap task is P5.2-T02, including validators, explicit UoW transactions, the shared header-lock protocol, rowversion conflict mapping and server-side receipt-number allocation.

Locked-mode restore currently reports an existing Desktop.WinForms lock-file dependency mismatch for `Microsoft.Extensions.DependencyInjection`; a normal restore succeeds. No package dependency or lock-file content change is part of P5.2-T01.
