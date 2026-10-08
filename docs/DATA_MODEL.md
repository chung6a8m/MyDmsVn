# P5 Sales & Inventory — proposed SQL data model

**Status:** schema blueprint. P1/P5 DbUp scripts are the implementation source of truth once written. The business ID strategy is a reversible proposal; confirm it before SQL baseline migration is frozen.

## 1. Core entities

| Table | Suggested key / fields | Constraints |
|---|---|---|
| `Products` | ProductId int IDENTITY, Code nvarchar(32), Name nvarchar(256), Unit nvarchar(32), IsActive bit | Code unique and not blank |
| `Warehouses` | WarehouseId int IDENTITY, Code nvarchar(32), Name nvarchar(256), Address nvarchar(500) null, IsActive bit | Code unique |
| `Employees` | EmployeeId int IDENTITY, Code nvarchar(32), Name nvarchar(256), Phone nvarchar(64) null, UserId int null, IsActive bit | Code unique; nullable FK to Users |
| `Customers` | CustomerId int IDENTITY, Code nvarchar(32), Name nvarchar(256), Address nvarchar(500) null, Phone nvarchar(64) null, TaxCode nvarchar(32) null, IsActive bit | Code unique; future sales relationship |
| `GoodsReceipts` | ReceiptId bigint IDENTITY, ReceiptNo nvarchar(32), ReceiptDate date, WarehouseId int, EmployeeId int, Status varchar(16), Note nvarchar(1000) null, PostedAtUtc datetime2 null | ReceiptNo unique, FK, Draft/Posted status |
| `GoodsReceiptLines` | ReceiptLineId bigint IDENTITY, ReceiptId bigint, LineNo int, ProductId int, Quantity decimal(18,4), UnitCost decimal(19,4) | unique (ReceiptId,LineNo), optionally unique (ReceiptId,ProductId) |
| `StockLedger` | LedgerId bigint IDENTITY, WarehouseId int, ProductId int, DocumentType varchar(32), DocumentId bigint, DocumentLineId bigint, QuantityChange decimal(18,4), PostedAtUtc datetime2 | unique (DocumentType,DocumentLineId), FK, immutable |
| `StockBalances` | WarehouseId int, ProductId int, Quantity decimal(18,4), RowVersion rowversion | composite PK (WarehouseId,ProductId), FK |

All mutable business tables carry `CreatedAtUtc datetime2`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy` (types consistent with identity model). Server-generated times stored UTC. Consider consistent column names across modules rather than mixing `InsertDate`/`CreatedAtUtc` without an explicit compatibility map.

`StockLedger` is append-only: business operations do not update/delete historical rows. `StockBalances` is a transactional materialization of ledger totals, **not** an independent source editable by UI.

## 2. Relationship outline

```text
Users 0..1 <--- Employees.UserId (optional)
Warehouses 1 --- N GoodsReceipts
Employees 1 --- N GoodsReceipts
GoodsReceipts 1 --- N GoodsReceiptLines
Products 1 --- N GoodsReceiptLines
Warehouses 1 --- N StockLedger N --- 1 Products
Warehouses 1 --- N StockBalances N --- 1 Products
GoodsReceiptLines 1 --- 0..1 StockLedger (when Posted)
Customers (standalone in P5)
```

## 3. States and invariants

Receipt state:
- `Draft`: editable; ledger must have no movement for it.
- `Posted`: immutable; posting generates exactly one movement per detail line and adjusts balances in the same DB transaction.
- No Cancelled/Reversed state in P5. Reversal is future design.

Posting preconditions:
- Header warehouse and employee exist and are active at time of posting (historical names/IDs remain stored after deactivation).
- Receipt has one or more lines; products valid and active; quantity > 0, unit cost >= 0.
- Duplicate product lines: **reject** in P5 unless an ADR permits aggregation; still unique source line movement identity.
- Atomic conditional status transition; posted concurrency conflicts cannot produce duplicate movement.
- Ledger quantity for goods receipt is positive. Stock balance starts at zero for missing key and adds quantities under safe SQL locking.
- No direct insert/update/delete UI commands for ledger or balance.
- If a product/warehouse is referenced historically, deactivate instead of deleting.

## 4. Indexing and concurrency

Recommended indexes: GoodsReceipts (ReceiptDate, ReceiptId), GoodsReceipts (WarehouseId, ReceiptDate), GoodsReceiptLines (ReceiptId, LineNo), StockLedger (WarehouseId,ProductId,PostedAtUtc,LedgerId), StockBalances PK (WarehouseId, ProductId), unique Ledger source identity; indexes on FKs.

Stock card order: `PostedAtUtc, LedgerId`; document date retained for user display but posting time defines deterministic movement order. Do not use dates alone as unique movement keys.

`rowversion` supports optimistic checks on balance/editable documents where useful; missing balance-row creation needs explicit lock/unique constraint handling. Monetary valuation is **not** implemented solely by storing UnitCost; no COGS algorithm in P5.

## 5. Reporting DTOs

- Product / warehouse lookup: Id, Code, Name, IsActive.
- Receipt detail: header, line collection, Status, PostedAtUtc.
- StockBalanceRow: ProductId, WarehouseId, Code/Name projections, Quantity.
- StockCardRow: LedgerId, date/time, document number/type, QuantityChange, running balance (optional read-side projection).

Dapper queries have filters by warehouse/product/date, paging where appropriate, and authorization. User input is parameterized; do not concatenate SQL order-by expressions from arbitrary client strings.

## 6. Future extensibility boundaries

Additional ledger `DocumentType` values for stock issue, transfer, stock correction or reversal require **separate approved workflows** and must preserve append-only evidence and reconciliation with balances. Customers do not create sales invoices/order tables in P5.

## 7. Data reconciliation

Provide an integration-test/reconciliation query that compares each (WarehouseId,ProductId) StockBalances.Quantity with SUM(StockLedger.QuantityChange) and flags drift. Initially only posted GoodsReceipts contribute; no stock issue/negative movements are in scope.
