# ADR 0004 — P5 receipt posting and inventory ledger

Status: **Accepted and implemented; current schema is defined by the checked-in migrations** · Date: 2026-10-08

## Context

P5 must prove a real transaction involving draft receipt, stock movements and stock balances. It must remain minimal so that future Sales Orders and Stock Issues can reuse the stock model.

## Decision

- Four catalogs: Product, Warehouse, Employee, Customer.
- Inventory workflow: GoodsReceipt Draft → Posted; immutable when Posted.
- Append-only StockLedger (one positive movement per receipt line), and current StockBalances materialized by Warehouse+Product.
- Explicit UoW encloses status change, ledger insertion and atomic balance increment.
- SQL conditional status transition + UNIQUE document movement identity prevent duplicate postings; concurrency-safe balance update prevents lost increments.
- Stock Balance/Stock Card are read models, with typed native SqlClient after L2DB-T04; neither exposes direct ledger/balance write APIs.
- P5 excludes outbound stock / Sales Orders; customers exist as standalone catalog.

## Consequences

Reconciliation and SQL Server integration tests for retry, rollback and parallel posting are mandatory. Reversal/returns/valuation require later ADRs; never mutate historic ledger to implement them ad hoc.

See [Data model](../DATA_MODEL.md), [Persistence](../PERSISTENCE.md) and [History](../HISTORY.md).
