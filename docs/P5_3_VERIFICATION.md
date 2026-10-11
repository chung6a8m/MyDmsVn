# P5.3 verification

Date: 2026-10-11

## Scope

- P5.3-T01/T02 storage, reconciliation and posting behavior remains covered by the existing migration, Application and SQL integration tests.
- P5.3-T03 adds separate-UoW concurrency evidence for same-receipt Post, UpdateDraft versus Post in both header-lock acquisition orders, catalog deactivation versus Post in both lock orders, and different receipts updating the same balance key.
- Mid-post failure injection occurs from a StockBalances trigger, after the handler has changed the receipt status and inserted its ledger movement; disposal rolls the transaction back to Draft with no ledger or balance row.
- The independent stock-ledger source uniqueness test proves duplicate source-line insertion is rejected by SQL. Reconciliation tests cover clean parallel posting plus ledger-only, extra zero balance, extra nonzero balance and mismatched-quantity defects in isolated test data.

The concurrency tests use a test-only signal table and SQL triggers inside each disposable database. Triggers expose the point at which a transaction owns the relevant locks and delay that transaction briefly; the competing operation then runs through a separate dependency-injection scope and UoW. No test hook or synchronization behavior is present in production code.

## Automated evidence

Commands were run sequentially by target framework against the disposable SQL Server test-database infrastructure configured by `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`.

- `dotnet restore MyDmsVn.sln --locked-mode --nologo`: succeeded after initializing the repository's declared UI submodules in the isolated worktree.
- `dotnet build MyDmsVn.sln -c Release --no-restore --nologo -m:1`: succeeded with 0 warnings and 0 errors, covering `net48`, `net8.0` and `net8.0-windows` projects.
- Server Infrastructure integration tests, Release `net48`: 90 passed, 0 failed, 0 skipped.
- Server Infrastructure integration tests, Release `net8.0`: 90 passed, 0 failed, 0 skipped.
- Focused Goods Receipt command integration tests: 19 passed on each server TFM.
- Server Application tests: 116 passed on `net48` and 116 passed on `net8.0`, with 0 failed/skipped.
- Desktop tests: 165 passed on `net48` and 165 passed on `net8.0-windows`, with 0 failed/skipped.
- Architecture tests, Release `net8.0`: 5 passed, 0 failed, 0 skipped.
- Mutation check: temporarily mapping the losing concurrent Post to `GoodsReceipt.ConcurrencyConflict` made `Concurrent_posts_for_same_receipt_commit_exactly_once` fail with the expected mapping mismatch; restoring `GoodsReceipt.AlreadyPosted` returned the suite to green.

## Remaining P5 work

- The interactive P5.1 manual smoke remains open.
- P5.4 Inventory read models, permissions, WinForms screens and full-slice testing are next.
