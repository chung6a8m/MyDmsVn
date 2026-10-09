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
- [ ] Keyboard-friendly compact layout; provide Warehouse, Employee and Product lookups for receipt entry (only active items selectable). Customer lookup supports later Sales workflows and is not used by Goods Receipts.
- [ ] Reuse ViewModels and client interfaces for both desktop hosts.

### P5.1 gate
- [ ] Integration tests: uniqueness/collation, FK, deactivated historical reference, permissions.
- [ ] Manual smoke and safe STA UI smoke for each catalog across `net48` and `net8.0-windows`.

## P5.2 — Goods Receipt Draft

### P5.2-T01 — Tables and models
- [ ] Add GoodsReceipts and GoodsReceiptLines DbUp migrations with required FK constraints; unique ReceiptNo, unique (ReceiptId,LineNo), and **required unique (ReceiptId,ProductId)**. Reject duplicate products at both Application and SQL boundaries; update `docs/DATA_MODEL.md` to reflect the mandatory index before freezing the migration.
- [ ] Add SQL CHECK constraints: `Status IN ('Draft','Posted')`, `LineNo > 0`, `Quantity > 0`, `UnitCost >= 0`; use NOT NULL for required fields and compatible decimal types (`decimal(18,4)` quantity, `decimal(19,4)` unit cost). Application validators provide field errors; SQL remains the final invariant guard.
- [ ] Add a `rowversion` column to GoodsReceipts for aggregate-level optimistic concurrency; expose an opaque expected version token in UpdateDraft/Post request DTOs and the current token in Get response DTOs. Never serialize an ADO.NET/RepoDb entity as a token.
- [ ] Receipt states `Draft` / `Posted` with legal transition Draft → Posted only; enforce state rules in commands and SQL constraints.
- [ ] Add created/updated/posting identity and UTC timestamp audit fields.

### P5.2-T02 — Commands
- [ ] CreateDraft / UpdateDraft (header plus lines) atomically with **one explicit UoW and one SQL transaction** per command; rollback the entire aggregate on any failure.
- [ ] Require `Inventory.GoodsReceipts.Write` for CreateDraft/UpdateDraft and `Inventory.GoodsReceipts.Read` for List/Get, enforced in Application (not only UI). Post uses the separate `Inventory.GoodsReceipts.Post` permission and does not implicitly grant Read or Write; enforce explicit user-deny precedence.
- [ ] Validators: warehouse/employee/product active, >=1 line, Quantity > 0, UnitCost >= 0, duplicate product line rejected, codes not blank; mirror appropriate invariants with SQL constraints.
- [ ] **Shared aggregate locking protocol:** UpdateDraft and Post begin a transaction, acquire an update/held lock on the same GoodsReceipts header row **before reading or changing its lines**, and hold it to commit/rollback. Use consistent lock order across both paths; do not load lines on a separate connection or before obtaining the header lock.
- [ ] UpdateDraft requires the caller's expected GoodsReceipts `rowversion`, verifies `Status = Draft` and token under the header lock, and atomically updates the header plus replaces/modifies lines. Even a lines-only edit must update the header so its `rowversion` advances; return the new version. A stale token returns deterministic `GoodsReceipt.ConcurrencyConflict` (409 in v2); an already Posted receipt returns `GoodsReceipt.AlreadyPosted` (409).
- [ ] Issue ReceiptNo on the server using a DB SEQUENCE (or equivalent transactional-safe, collision-resistant allocation), with a unique SQL index as final protection. Never trust a client-generated receipt number; gaps caused by transaction rollback are acceptable, reuse is not. Document the exact format before coding (the `GR0001` walkthrough value is illustrative).

### P5.2-T03 — Queries and UI
- [ ] List and Get receipt with header/lines via Dapper read models; Get includes the current opaque `rowversion` token for optimistic editing/posting and query permissions are enforced.
- [ ] Master-detail WinForms form with BootstrapSourceGrid, Product lookups and totals for display only.
- [ ] Inline validation and progress/errors; avoid long-running database calls on UI thread.

### P5.2 gate
- [ ] SQL tests for rollback of header+lines; enforced FK/unique/CHECK constraints (including direct invalid SQL inserts), duplicate receipt numbers/product lines, state guard, and server-issued number uniqueness.
- [ ] SQL integration tests with separate UoWs: concurrent UpdateDraft vs UpdateDraft (one stale-token conflict), UpdateDraft vs Post (never mixed header/lines or post-edit mutation), lines-only edit advances version, and failed UpdateDraft rolls back completely.
- [ ] Authorization tests for CreateDraft/UpdateDraft (Write), List/Get (Read), and Post (Post), including explicit deny and Post-only user attempting Write/Read; denied requests must not mutate business tables.
- [ ] UI smoke: new Draft → edit → reload → inspect details; both hosts.

## P5.3 — Posting and stock invariants (critical)

### P5.3-T01 — Data storage
- [ ] Add append-only StockLedger with UNIQUE (DocumentType,DocumentLineId) and stock-card index.
- [ ] Add StockBalances with PK (WarehouseId,ProductId), Quantity decimal(18,4), RowVersion.
- [ ] Provide reconciliation SQL/test utility using a `FULL OUTER JOIN` between ledger totals grouped by (WarehouseId,ProductId) and StockBalances. Flag missing keys **on either side**, even if the stored balance is zero; compare quantities with `COALESCE` only after checking key presence (never use INNER JOIN, which hides orphan rows).

### P5.3-T02 — PostGoodsReceiptCommandHandler
- [ ] Verify authenticated identity and `Inventory.GoodsReceipts.Post` permission before mutation; enforce permission in the Application pipeline/handler, never only in WinForms.
- [ ] Create **one** explicit UoW and begin a SQL transaction. Acquire the same held GoodsReceipts header update lock as UpdateDraft **before** reading receipt lines; serialize aggregate edits and posting under a documented consistent lock order.
- [ ] Under that transaction, check `Status = Draft` and the expected receipt `rowversion` to reject stale Post requests; load all header/line data via the same UoW. Before changing status, **revalidate** warehouse and employee active status, every product's active status, >=1 line, positive quantities, nonnegative unit costs and no duplicate products.
- [ ] Protect active-state checks against concurrent master-data deactivation until posting commits (e.g. acquire and retain `UPDLOCK, HOLDLOCK` on the relevant master rows, in consistent key order; SetActive must use normal SQL updates that honor these locks). Neither a stale preflight check nor UI lookup filtering is sufficient.
- [ ] Conditionally update receipt status from Draft to Posted with the validated expected version, capturing who/when; affected rows must be exactly one, otherwise return a deterministic conflict.
- [ ] Insert one positive StockLedger movement per receipt line with stable unique source identity.
- [ ] Atomically add grouped deltas to balances using safe row locks + unique-key handling in the **same transaction**. Apply (WarehouseId,ProductId) keys in deterministic order and handle unique-key races without unsafe `MERGE`.
- [ ] Commit; any failure rolls back status/ledger/balance together. Never allow an update of a Posted receipt; concurrent or retried Posts must never post twice.
- [ ] Map a second Post to `GoodsReceipt.AlreadyPosted` (409 in v2) and a stale Draft version to `GoodsReceipt.ConcurrencyConflict` (409); use identical `ApiResponse<T>` semantics in Local and future HTTP adapters.

### P5.3-T03 — Concurrency and idempotency testing
- [ ] Concurrent Post same receipt using separate UoWs: exactly one succeeds; no duplicate ledger/balance. Test stale expected versions and deterministic error mappings.
- [ ] Concurrent UpdateDraft vs Post using separate UoWs: no lost updates, no mixed line snapshot, never edit after Posted; test both lock acquisition orders.
- [ ] Create a valid Draft, then deactivate its Warehouse, Employee or any Product before Post: Post must fail without status/ledger/balance changes; test deactivation racing with Post to verify locks/serialization.
- [ ] Concurrent different receipts same warehouse/product: final balance = sum of both quantities.
- [ ] Fault injection mid-post (after status change/ledger insert) rolls back all changes.
- [ ] Unique movement constraint prevents accidental duplicate source-line insertion independently from handler checks.
- [ ] Reconciliation after parallel load yields zero differences. Deliberately create a missing balance row, an extra zero/nonzero balance row, and a mismatched quantity **in isolated test data**; every defect must be detected by the reconciliation query.

### P5.3 gate
- [ ] All SQL Server integration scenarios above pass in reproducible isolated test database.

## P5.4 — Inventory read/UI + full slice testing

- [ ] Implement StockBalance query (warehouse/product filters, readable names, deterministic sort).
- [ ] Implement StockCard query (warehouse+product, posting timestamp, document identity, quantity delta).
- [ ] Permissions `Inventory.Balances.Read` and `Inventory.StockCard.Read`.
- [ ] WinForms "Tồn kho" and "Thẻ kho", filters, totals, loading/empty/error states.
- [ ] LocalApiClient contract tests for all P5 operations.
- [ ] GUI/manual walkthrough: create catalog items → Draft → Post → balance increases → stock card shows movement → double Post rejected.
- [ ] Test inactive Products, Warehouses and Employees remain visible historically but cannot be used for new receipt posting; a Draft created before deactivation must fail Post until references become valid again.
- [ ] No direct Create/Update/Delete client API for balances/ledger.

## Reusable acceptance walkthrough

**Given:** Product SP001 active; Warehouse KHO01 active; Employee NV001 active; sufficient permissions.
**When:** create Draft receipt (illustrative server-issued number GR0001) containing SP001 Qty=100 and Post using its current concurrency token.
**Then:** receipt becomes Posted, exactly 1 ledger movement +100, StockBalances(KHO01,SP001) = 100, stock card shows source GR0001.
**When:** Post again, or Post concurrently from two clients.
**Then:** no second movement/balance delta; defined conflict; reconciliation remains exact.
**When:** inject exception after ledger insert before commit.
**Then:** Draft remains, no movement and no balance change.

Add more than one product in a separate case to prove multi-row atomicity. Add a stale-token UpdateDraft/Post race, a master-data deactivation-before-Post case, and a deliberately inconsistent ledger/balance reconciliation fixture. Customers CRUD is independent from this receipt flow; it exists for subsequent Sales modules.

## Definition of Done

All task checkboxes based on evidence, dual-runtime desktop smoke, documented SQL migration/version compatibility, no modal test hangs, architecture references clean, tests green or explicitly documented environmental blockers. Confirm `docs/DATA_MODEL.md` and contract documentation reflect the finalized required uniqueness, rowversion and receipt-number policy before implementing the relevant migrations/DTOs. **Do not mark P5 complete if concurrency, deactivation-race, authorization or rollback test results are unavailable.**
