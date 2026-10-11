# P5.3-T01/T02 verification

Date: 2026-10-11

## Scope

- DbUp migration `007_Inventory_Stock.sql` adds append-only `StockLedger`, `StockBalances`, source uniqueness, stock-card indexing, FKs, decimal storage and rowversion.
- `IStockReconciliationQueryService` uses a `FULL OUTER JOIN` in a `SERIALIZABLE` transaction with ledger-then-balance shared table locks, preserves key-presence flags and reports missing ledger/balance keys independently of quantity or concurrent Post timing.
- `PostGoodsReceiptCommandHandler` enforces `Inventory.GoodsReceipts.Post`, one explicit UoW/transaction, shared header locking, held master-data locks, aggregate revalidation, conditional posting, append-only movements and ordered atomic balance deltas.
- Local mode dispatches the Post command through the same `ApiResponse<T>` mapper used by other Goods Receipt operations.

## Automated evidence

All commands were run sequentially by TFM against the repository's disposable SQL Server test-database infrastructure.

- Server Application tests, Release `net48`: 116 passed, 0 failed, 0 skipped.
- Server Application tests, Release `net8.0`: 116 passed, 0 failed, 0 skipped.
- Server Infrastructure integration tests, Release `net48`: 83 passed, 0 failed, 0 skipped.
- Server Infrastructure integration tests, Release `net8.0`: 83 passed, 0 failed, 0 skipped.
- Desktop tests, Release `net48`: 165 passed, 0 failed, 0 skipped.
- Desktop tests, Release `net8.0-windows`: 165 passed, 0 failed, 0 skipped.

The focused SQL evidence includes migration shape and rerun, append-only enforcement, source uniqueness, reconciliation defects on either side (including an extra zero balance), enforced serializable reconciliation during concurrent Post, successful multi-line posting, stale and denied Post, line-order-correct inactive-product errors, rollback after ledger failure, second-Post rejection and Local adapter dispatch.

## Follow-up status

P5.3-T03 and the P5.3 gate were completed on 2026-10-11 with the full separate-UoW concurrency matrix, explicit mid-post fault injection and reconciliation evidence. See `docs/P5_3_VERIFICATION.md`. P5.4 remains open.
