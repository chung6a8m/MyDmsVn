# P5 — Sales & Inventory Foundation (detailed implementation plan)

Status: **Not started**. Requires P0–P4 exit gates and SQL Server integration-test infrastructure. Product requirements: `docs/PRD.md`; data model: `docs/DATA_MODEL.md`; contract and security rules in linked documents.

## Goal / explicitly excluded

Implement **Products, Warehouses, Employees, Customers, Goods Receipts, Stock Ledger, Stock Balances and Stock Card** in WinForms. This is the **foundation** for future Sales; **no Sales Order, sales invoice, outbound stock, pricing, debt, COGS or accounting in P5**.

## P5.1 — Master Data

### P5.1-T01 — Schema + mappings
- [ ] Add DbUp migration(s) for Products, Warehouses, Employees, Customers; unique Code indexes, IsActive, audit fields and FK Employee.UserId nullable.
- [ ] Implement Domain entities and RepoDb column mappings verified against SQL names and types.
- [ ] Seed **only test fixtures** in test setup; no production default customer/employee without a requirement.

### P5.1-T02 — Backend features
- [ ] Create/Update/SetActive commands with validators and conflict codes.
- [ ] List/GetById/Lookup Dapper query services; deterministic order, paging and parameterized filtering.
- [ ] Apply Catalog.*.Read/Write permissions and user identity audit fields.
- [ ] Return `ApiResponse<T>` via Local adapter; no raw entity/RepoDb object escapes.

### P5.1-T03 — UI
- [ ] WinForms catalogs: list/filter, create/edit, activate/deactivate, async busy/cancel, field-level validation.
- [ ] Keyboard-friendly compact layout; provide employee/customer lookups for receipts.
- [ ] Reuse ViewModels and client interfaces for both desktop hosts.

### P5.1 gate
- [ ] Integration tests: uniqueness/collation, FK, deactivated historical reference, permissions.
- [ ] Manual smoke and safe STA UI smoke for each catalog across `net48` and `net8.0-windows`.

## P5.2 — Goods Receipt Draft

### P5.2-T01 — Tables and models
- [ ] Add GoodsReceipts and GoodsReceiptLines migrations; FK, unique ReceiptNo, (ReceiptId,LineNo), suggested (ReceiptId,ProductId).
- [ ] Receipt states `Draft` / `Posted` with legal transition Draft → Posted only.
- [ ] Add created/updated/posting identity and timestamp audit fields.

### P5.2-T02 — Commands
- [ ] CreateDraft / UpdateDraft (header plus lines) with explicit UoW.
- [ ] Validators: warehouse/employee/product active; >=1 line; Quantity >0; UnitCost >=0; duplicate product line rejected; codes not blank.
- [ ] Concurrent edit prevention (document rowversion or equivalent optimistic guard).
- [ ] Draft-only edit; changes on Posted fail with deterministic business conflict. Unique ReceiptNo should be server-issued or DB-enforced.

### P5.2-T03 — Queries and UI
- [ ] List and Get receipt with header/lines via Dapper read models.
- [ ] Master-detail WinForms form with BootstrapSourceGrid, Product lookups and totals for display only.
- [ ] Inline validation and progress/errors; avoid long-running database calls on UI thread.

### P5.2 gate
- [ ] SQL tests for rollback of header+lines; state guard, duplicate lines/codes, permission denial and concurrency.
- [ ] UI smoke: new Draft → edit → reload → inspect details; both hosts.

## P5.3 — Posting and stock invariants (critical)

### P5.3-T01 — Data storage
- [ ] Add append-only StockLedger with UNIQUE (DocumentType,DocumentLineId) and stock-card index.
- [ ] Add StockBalances with PK (WarehouseId,ProductId), Quantity decimal(18,4), RowVersion.
- [ ] Provide reconcile SQL/test utility: grouped ledger sum vs materialized balance.

### P5.3-T02 — PostGoodsReceiptCommandHandler
- [ ] Verify identity and `Inventory.GoodsReceipts.Post` permission before mutation.
- [ ] Create ONE explicit UoW; BeginTransaction.
- [ ] Conditionally update Receipt status from Draft to Posted, capturing who/when; affected rows must be exactly one.
- [ ] Insert one positive StockLedger movement per receipt line with stable unique source identity.
- [ ] Atomically add grouped deltas to balances using safe row locks + unique-key handling in the SAME transaction.
- [ ] Commit; any failure rolls back status/ledger/balance together.
- [ ] Do not allow update of Posted document; retries/concurrent posts must never post twice.
- [ ] Ensure deterministic behavior/error code for second Post (default: `GoodsReceipt.AlreadyPosted`, HTTP 409 in v2).

### P5.3-T03 — Concurrency and idempotency testing
- [ ] Concurrent Post same receipt using separate UoWs: exactly one succeeds; no duplicate ledger/balance.
- [ ] Concurrent different receipts same warehouse/product: final balance = sum of both quantities.
- [ ] Fault injection mid-post (after status change/ledger insert) rolls back all changes.
- [ ] Unique movement constraint prevents accidental duplicate source-line insertion independently from handler checks.
- [ ] Reconciliation after parallel load yields zero differences.

### P5.3 gate
- [ ] All SQL Server integration scenarios above pass in reproducible isolated test database.

## P5.4 — Inventory read/UI + full slice testing

- [ ] Implement StockBalance query (warehouse/product filters, readable names, deterministic sort).
- [ ] Implement StockCard query (warehouse+product, posting timestamp, document identity, quantity delta).
- [ ] Permissions `Inventory.Balances.Read` and `Inventory.StockCard.Read`.
- [ ] WinForms "Tồn kho" and "Thẻ kho", filters, totals, loading/empty/error states.
- [ ] LocalApiClient contract tests for all P5 operations.
- [ ] GUI/manual walkthrough: create catalog items → Draft → Post → balance increases → stock card shows movement → double Post rejected.
- [ ] Test inactive products remain visible historically but unavailable for new receipt posting.
- [ ] No direct Create/Update/Delete client API for balances/ledger.

## Reusable acceptance walkthrough

**Given:** Product SP001 active; Warehouse KHO01 active; Employee NV001 active; sufficient permissions.
**When:** create Draft receipt GR0001 containing SP001 Qty=100 and Post.
**Then:** receipt becomes Posted, exactly 1 ledger movement +100, StockBalances(KHO01,SP001) = 100, stock card shows source GR0001.
**When:** Post again, or Post concurrently from two clients.
**Then:** no second movement/balance delta; defined conflict; reconciliation remains exact.
**When:** inject exception after ledger insert before commit.
**Then:** Draft remains, no movement and no balance change.

Add more than one product in a separate case to prove multi-row atomicity. Customers CRUD is independent from this receipt flow; it exists for subsequent Sales modules.

## Definition of Done

All task checkboxes based on evidence, dual-runtime desktop smoke, documented SQL migration/version compatibility, no modal test hangs, architecture references clean, tests green or explicitly documented environmental blockers. **Do not mark P5 complete if concurrency / rollback test results are unavailable.**
