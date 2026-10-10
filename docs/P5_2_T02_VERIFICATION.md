# P5.2-T02 verification evidence

Date: 2026-10-10

## Scope

This evidence covers only P5.2-T02: Goods Receipt CreateDraft and UpdateDraft Application commands, aggregate validation, authorization boundaries, explicit transaction ownership, shared header locking, optimistic concurrency and server-issued receipt numbers.

P5.2-T03 Dapper List/Get handlers and WinForms UI remain open. P5.3 must reuse the shared header-lock primitive for Post and adds ledger/balance behavior; no posting or stock capability is claimed here.

## Implemented behavior

- CreateDraft and UpdateDraft use one explicit UoW, one connection and one SQL transaction for header and lines.
- Application validation rejects empty/null lines, more than 200 lines, invalid IDs, values outside the `decimal(18,4)` Quantity / `decimal(19,4)` UnitCost shapes, nonpositive quantity, negative unit cost, duplicate products, oversized/invalid note text and malformed/non-eight-byte Base64 version tokens before database access.
- Command-time reference checks reject missing or inactive Warehouse, Employee and Product rows with field-addressable validation errors. Distinct Product states are resolved with one parameterized set query while using the command transaction.
- UpdateDraft obtains `UPDLOCK, HOLDLOCK` on the GoodsReceipts header before reference checks or line changes, then verifies Draft state and the expected rowversion. It updates the header on every successful edit, including lines-only edits, so rowversion advances.
- The shared `LockHeaderAsync` repository primitive is the required lock entry point for the future P5.3 Post handler.
- `dbo.GoodsReceiptNumberSequence` issues non-cycling values formatted as `GR` plus 10 digits. Rollback gaps are allowed and the existing unique receipt-number index remains the final guard.
- `Inventory.GoodsReceipts.Write` protects CreateDraft/UpdateDraft. List/Get Application request boundaries declare `Inventory.GoodsReceipts.Read`; their Dapper handlers remain P5.2-T03. Post remains a separate P5.3 request protected by `Inventory.GoodsReceipts.Post`.

## Automated evidence

Application tests cover permission declarations, structural validators (including null lines, the 200-line limit and SQL decimal precision/scale on both CreateDraft and UpdateDraft), malformed versions, inactive references, explicit deny before UoW creation, aggregate commit and deterministic stale/Posted conflicts.

SQL integration tests cover migration/sequence creation, server-number format and uniqueness, a 25-product reference/persistence batch, header-plus-lines persistence, lines-only rowversion advancement, stale-token rejection without mutation, complete rollback after an injected second-line failure, two concurrent UpdateDraft calls with one expected version, inactive references and denied writes leaving receipt tables empty.

Release verification ran each target framework in a separate command; no two TFMs were tested in parallel:

- `dotnet restore MyDmsVn.sln --disable-parallel` — succeeded.
- `dotnet build MyDmsVn.sln -m:1 -c Release --no-restore` — succeeded with 0 warnings and 0 errors.
- Server Application: 95/95 on `net48`, then 95/95 on `net8.0`.
- Desktop: 112/112 on `net48`, then 112/112 on `net8.0-windows`.
- Architecture: 5/5 on `net8.0`.
- SQL Server integration: 70/70 on `net48`, then 70/70 on `net8.0` against isolated disposable databases.

Total: 559/559 target executions passed, 0 failed and 0 skipped.

## Remaining work and risk

- P5.2-T03 must implement authorized Dapper List/Get projections and the desktop master-detail UI.
- P5.3 must use the same header lock before reading lines and must revalidate active catalog references under its stronger posting/deactivation locking protocol.
- Full solution/WinForms verification requires the recursive `vendor/MyDmsVn.BootstrapSourceGrid` submodule. It was initialized at the pinned commits in this worktree before the successful Release build and test matrix.
