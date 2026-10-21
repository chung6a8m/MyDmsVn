# Direct Sales — moving weighted average costing

**Status:** APPROVED DESIGN / BOUNDED CHECKPOINTS VERIFIED / SOURCE WRITER PROTOCOL IMPLEMENTED / FULL WORKFLOW ACCEPTANCE INCOMPLETE. **Updated:** 2026-10-22.
**Source baseline:** `v1a-pro@35ba2e8a3f37fb07095701eb94f4b56f7941c491`.
The Owner selected moving weighted average per `(WarehouseId, ProductId)`, using actual serialized Post order.
T00 implements exact math and T01 implements schema/bootstrap/admission/mappings/grants/reset ([evidence](evidence/sal/20261022-t01-schema-bootstrap.md)). T02 implements valued receipt posting, the shared coordinator, Exclusive reconciliation and stock projections ([evidence](evidence/sal/20261022-t02-valued-inventory.md)). T03 uses T00 revenue math for [reviewed Draft Save/detail](evidence/sal/20261022-t03-sales-draft.md), preserves independent line IDs/current Unit and keeps Draft costs/profit null with no stock movement. T04 Sales Post is implemented; later workflows remain under implementation in the [SAL roadmap](plans/20261021-002-direct-sales-and-moving-average-costing.md); the checkpoint is not deployable.
[Direct Sales](DIRECT_SALES.md) owns document scope; [query/UI design](DIRECT_SALES_QUERY_AND_UI.md) owns presentation; [acceptance](DIRECT_SALES_ACCEPTANCE.md) owns required evidence.

T04 source checkpoint (2026-10-22, `7eeafa4`): [reviewed evidence](evidence/sal/20261022-t04-atomic-sales-post.md) records atomic Post on one owned UoW, Shared-before-header and all sorted stock locks before writes, exact duplicate/gift allocation, immutable line cost, paired quantity/value movements and authoritative status/version materialized before commit. Receipt Post and Exclusive reconciliation use the same protocol. Local/VM/UI, frozen T08, package and Owner acceptance remain open; this checkpoint is not deployable. T01 R3 remains deferred Minor.

## 1. Existing implementation and change boundary

| Source fact at the baseline | Required SAL change |
|---|---|
| [Receipt schema](../src/MyDmsVn.Server.DbMigrator/Scripts/005_GoodsReceipt.sql) stores `Quantity decimal(18,4)` and `UnitCost decimal(19,4)`; receipt products are unique within a receipt. | Preserve these input types and the Goods Receipt duplicate rule. Sales separately permits duplicate products. |
| [Inventory schema](../src/MyDmsVn.Server.DbMigrator/Scripts/007_Inventory_Stock.sql) has append-only quantity `StockLedger` and quantity-only `StockBalances`. | Add immutable valuation companions and a derived value balance; preserve existing ledger identities and entries. |
| [Receipt Post](../src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs) appends ledger entries before [balance updates](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlGoodsReceiptWriteRepository.cs). | Lock every affected balance before either quantity or value writes, and calculate receipt value in the same transaction. |
| [Reconciliation](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlStockReconciliationQueryService.cs) takes ledger then balance table locks. | Introduce the common posting/reconciliation barrier before changing writer order. |
| [Migrator](../src/MyDmsVn.Server.DbMigrator/DatabaseMigrationRunner.cs) accepts Empty/Compatible; older prefixes are refused. | Add one narrowly verified schema-9 upgrade path; adding a script alone does not make that path work. |

Keep active `net48`, Windows x86, SQL Server, LINQ to DB 6.3.0 with typed native SqlClient, explicit UoW and DbUp.
No payment, receivable, return, reversal, transfer, tax, currency conversion or retrospective recosting is introduced.
Business date remains descriptive: a backdated receipt affects the next successful Post after it, never an already Posted sale.
There is no globally ordered posting queue; operations sharing a stock key serialize, while unrelated keys need no common valuation order.

## 2. Numeric contract and exact arithmetic

Use one application/domain arithmetic contract for Save validation, Post and read-only numeric projection; tests derive expected values independently from this specification.
SQL constraints and explicitly typed parameters provide additional enforcement; provider rounding is never the business rule.

| Value | Proposed storage / accepted range |
|---|---|
| Line quantity, stock quantity, per-key quantity total | `decimal(18,4)`; positive input, nonnegative balance; maximum `10^14 - 10^-4` |
| Manual sale unit price; existing receipt unit cost | `decimal(19,4)`; `0` through `10^15 - 10^-4` |
| Revenue, line/group cost, inventory value, document monetary totals | `decimal(28,4)`; nonnegative amounts through `M = 10^24 - 10^-4` |
| Ledger value change; profit | Signed `decimal(28,4)` within `[-M, M]` |
| `PostedUnitCost` snapshot | `decimal(28,8)`; `0` through `10^20 - 10^-8` |

Reject excess input scale, nonpositive quantity, negative price/cost and out-of-range values before SQL; never truncate, clamp or saturate.
Representable inputs do not imply a representable product, product-group sum or document total.
Validate every line amount, every per-key quantity/value aggregate, document revenue/cost totals and resulting balance before writing.
Perform aggregate checks with wide exact intermediates, including receipt `Q + Rq` and `V + Rv`; a valid line can still overflow its aggregate.
Validate an eight-place unit-cost snapshot before converting it to its declared type. Do not reduce its scale to make it fit.

### 2.1 Fixed-point calculation

All monetary rounding uses `MidpointRounding.AwayFromZero`, at four decimal places unless explicitly marked as an eight-place snapshot.
Do not use `double`, `float`, or `Math.Round`'s default midpoint behavior.
Use `System.Numerics.BigInteger` integer coefficients extracted exactly from `decimal` and their declared scales for multiplication, sums and rational division.
Convert back to `decimal` only after checking the target coefficient/range. Accepted endpoints fit .NET `decimal`; arbitrary intermediate products need not.
The implementation must add the required net48 assembly reference explicitly and keep this pure arithmetic independent of SQL/provider types.

For nonnegative integers define `R(n,d)` with `d > 0`: divide into quotient `a` and remainder `r`, then return `a + 1` when `2r >= d`, otherwise `a`.
This performs exactly one midpoint-away-from-zero rounding at the chosen output scale. Apply the sign after rounding a magnitude for signed outputs.
Let `q`, `p` and `v` denote quantity, input unit price/cost and amount coefficients at scale 4.

| Operation | Exact integer result |
|---|---|
| Receipt line value or nongift revenue, scale 4 | `R(q * p, 10^4)` |
| Partial issue value, scale 4 | `R(v * issuedQuantityCoefficient, availableQuantityCoefficient)` |
| Unit-cost display snapshot, scale 8 | `R(v * 10^8, availableQuantityCoefficient)` |

Do not divide to a rounded unit cost and multiply that rounded result to determine inventory value or cost.
SQL multiplication/division can change result precision/scale; any SQL implementation of an equivalent step must prove equivalence at boundaries rather than rely on an implicit cast.
See Microsoft's [decimal expression rules](https://learn.microsoft.com/en-us/sql/t-sql/data-types/precision-scale-and-length-transact-sql).

## 3. Proposed persistence model

| Object | Proposal |
|---|---|
| `SalesReceipts` | `SaleId` is the aggregate identity; SQL `SaleNo`/`SaleDate` map to DTO `SaleNumber`/`SaleDate`; lifecycle, audit and rowversion follow [the document contract](DIRECT_SALES.md). |
| `SalesReceiptLines` | `SaleLineId`, `SaleId`, `LineNo`; quantity, `UnitPrice`, unit snapshot, gift and revenue fields; nullable `PostedUnitCost decimal(28,8)` and `CostAmount decimal(28,4)`. Unique `(SaleId, LineNo)`; **no** unique `(SaleId, ProductId)`. |
| Existing `StockLedger` | One quantity entry per Posted source line. Sales uses `DocumentType = 'SalesReceipt'`, `DocumentId = SaleId`, `DocumentLineId = SaleLineId`, and negative `QuantityChange`. Existing source uniqueness remains. |
| New `StockValuationEntries` | `LedgerId bigint` is both PK and FK to `StockLedger.LedgerId`; `ValueChange decimal(28,4) NOT NULL`. One companion per quantity entry, including legitimate zero-value movements. |
| Existing `StockBalances` | Add `InventoryValue decimal(28,4) NOT NULL` with **no default that supplies a missing valuation**. Preserve composite PK and SQL-generated `RowVersion` type/ownership. |

Add enabled/trusted balance checks: `Quantity >= 0`, `InventoryValue >= 0`, and `Quantity <> 0 OR InventoryValue = 0`.
Positive quantity with zero value is valid, including zero-cost receipts and rounding outcomes.
Retain the balance row at zero quantity/value after depletion; its ledger history still exists.
Add an append-only UPDATE/DELETE trigger for `StockValuationEntries`, and restrict runtime privileges accordingly.
Its PK/FK establish at most one companion and an existing parent; they alone do not establish that every ledger row has a companion.
The atomic writer establishes completeness; reconciliation detects any missing companion, orphan, duplicate or unsupported source relationship.
Receipt value changes are nonnegative; sale value changes are nonpositive. Zero value never justifies omitting an entry.

Draft `PostedUnitCost`/`CostAmount` and cost/profit summaries are null. Draft cost estimation is not an endpoint or UI requirement in this roadmap.
Admit at most 200 sale lines. Save requests omit `LineNo`, revenue and cost; the server derives line numbers from submitted list order and computes the amounts.
Zero `SaleLineId` denotes a new line; a nonzero identity must belong to the locked Draft. Duplicate products retain their own line identities.
Save derives the `Unit nvarchar(32) NOT NULL` snapshot with the catalog's required/nonblank/no-NUL bounds; duplicate rows share the same unit. Post uses ordinal equality to validate that the current Product Unit still matches the saved snapshot. This preserves a transaction label, not a historical unit-conversion model.
A changed unit requires explicit reload/review/Save; Post never silently changes units or converts quantities. Posted unit snapshots are immutable.
On Post, every sale line receives nonnull cost fields, including gifts and zero-cost lines. They become immutable with the aggregate.
Persisted line `CostAmount` is authoritative for COGS and profit; `PostedUnitCost` explains the common pre-issue rate at display precision.
Consequently `round4(Qty * PostedUnitCost)` need not equal an allocated line `CostAmount`.
Do not add cost columns to the Owner's seven-column sales grid; cost/profit presentation follows the query/UI design.

## 4. Receipt value and moving average

For one locked stock key let available quantity/value be `(Q,V)`; absent receipt balance means `(0,0)` under a protected key range.
For each receipt line `i`, compute `receiptValue_i = round4(quantity_i * unitCost_i)`.
Then `Rq = sum(quantity_i)` and `Rv = sum(receiptValue_i)` for that key; round each line once before summing.
Post the new state `(Q + Rq, V + Rv)`, a positive quantity entry per line and its nonnegative value companion, atomically with receipt status.
Do not replace line rounding with `round4(sum(quantity_i * unitCost_i))`.
The next issue uses the exact rational average `V / Q` from the new locked state; no repeatedly rounded average is stored as the source of truth.
All Goods Receipt posting paths must use this contract after cutover.
Existing Goods Receipt quantity/cost validation and its unique-product rule remain, with additional amount/balance overflow validation.

## 5. Sale cost, duplicates and gifts

Group all persisted sale lines by `(WarehouseId, ProductId)` without collapsing the visible or stored lines.
For each group use the single pre-issue locked `(Q,V)` and `S = sum(line.Qty)`, including every gift line.
Require `Q > 0` and `0 < S <= Q`; missing stock or insufficient total stock rejects the whole Post with no partial issue.
Checking duplicate lines individually against the same available quantity is insufficient.

Calculate the group's authoritative issue cost:

```text
C = V                        when S == Q
C = round4(V * S / Q)         when 0 < S < Q
newQuantity = Q - S
newInventoryValue = V - C
```

Assert `0 <= C <= V` using exact intermediates. This is an invariant check, not a permission to clamp a bad calculation.
When all stock is issued, take all remaining value exactly; `(Q,V)` must become `(0,0)`.
When quantity remains, zero remaining value is permitted. No negative-stock or negative-value fallback is allowed.
Compute `PostedUnitCost = round8(V / Q)` once for the group and copy it to every duplicate line, paid or gift.
Never reduce stock and recompute an average between duplicate lines of the same sale.

### 5.1 Deterministic allocation to original lines

Within a product group order persisted lines by unique `LineNo`, preserving their `SaleLineId` identities.
Let `P_i` be cumulative quantity through line `i`, `A_0 = 0`, and `A_i = round4(C * P_i / S)`.
For the final line set `A_n = C` exactly; persist `CostAmount_i = A_i - A_(i-1)`.
Calculate this using the fixed-point integer rule above. Cumulative rounding is monotone, so every line cost is nonnegative and the sum is exactly `C`.
It also avoids assigning a negative residual to the last line after separately rounding every prior line.
Append each line's ledger quantity `-quantity_i` and valuation `-CostAmount_i`.
Line order determines the rounding allocation, not the group's total cost or its pre-issue unit-cost snapshot.

`Amount_i = 0` for a gift; otherwise `Amount_i = round4(quantity_i * manualUnitPrice_i)`.
Gift revenue is always zero regardless of retained manual price. Gift quantities and costs participate fully in stock, COGS and profit.
`Revenue = sum(Amount_i)`, `COGS = sum(CostAmount_i)`, and `GrossProfit = Revenue - COGS`.
A Draft does not reserve inventory or promise a future cost. Cost is established only by successful Post against the locked current pool.
Posted queries use stored amounts/costs and never substitute a current average or business-date reconstruction.

### 5.2 Worked examples

| Event, same warehouse/product | Quantity after Post | Value after Post | Economic result |
|---|---:|---:|---|
| Receive 10 at 10 | 10.0000 | 100.0000 | Receipt value 100.0000 |
| Receive 10 at 20 | 20.0000 | 300.0000 | Exact pre-sale average 15 |
| Sell 3 at price 25 plus a separate gift line of 2 | 15.0000 | 225.0000 | Group cost 75; paid-line cost 45, gift cost 30; revenue 75, profit 0 |
| Receive 5 at 30, even with an earlier business date | 20.0000 | 375.0000 | New average 18.75; prior sale cost stays 75 |

Rounding case: start `Q = 3`, `V = 0.0002`; issue two separate lines of quantity 1.
Group cost is `0.0001`; cumulative allocation is `0.0001` then `0.0000`; both snapshots are `0.00006667`.
The remaining `(1, 0.0001)` is fully consumed at cost `0.0001` by a later final issue, leaving exactly `(0,0)`.
These are design examples, not executed test results.

## 6. One transaction and one locking protocol

T02 receipt/reconciliation extends the existing explicit UoW; T04 Sales Post adopts the same coordinator. All participating repositories borrow its current connection/transaction; they never open a hidden connection.
LINQ to DB owns mapped reads, writes and guarded balance updates wherever the pinned provider can express the required operation safely. Application locks use typed native SqlClient on that same transaction; a key-range/locking escape requires recorded evidence that the required semantics cannot be safely expressed through the pinned LINQ provider.
Bind precision/scale explicitly, materialize/dispose readers before the next command, and never run parallel commands on a UoW connection.

### 6.1 Shared posting / exclusive reconciliation barrier

Use database application-lock resource **`MyDmsVn.InventoryValuation.v1`**, `@DbPrincipal = 'public'`, `@LockOwner = 'Transaction'`.
Every Goods Receipt/Sales Post takes `Shared` immediately after transaction begin and before any document/reference/inventory lock.
Reconciliation and authorized inventory maintenance take `Exclusive` immediately after transaction begin and before any table/document lock.
Acquire once, retain until commit/rollback, and never promote an already-held Shared lock to Exclusive during Post.
The identical database, principal and resource name are essential: they define the lock's identity.
Use an explicit finite lock timeout, cancellation and a bounded command timeout; T02 uses 15 seconds for the application lock and 30 seconds per SQL command.
Accept only nonnegative `sp_getapplock` return codes. Timeout, cancellation, deadlock or call failure terminates/rolls back the owned transaction; no continuation without the barrier.
Do not assume an application-lock deadlock result automatically rolled the transaction back.
Verify this path with the actual restricted runtime identity; no `db_owner` grant or alternate connection is permitted as a workaround.
These semantics follow Microsoft's [application-lock contract](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql).

The Shared lock permits concurrent writers; the balance key locks serialize their common stock keys.
The Exclusive lock waits for admitted writers to finish and blocks new ones before reconciliation touches tables.
This prevents the old cycle where a writer held a balance key waiting for ledger access while reconciliation held ledger access waiting for balances.
Changing only Sales, only Goods Receipt, or only reconciliation would leave an unsafe mixed protocol; deploy all consumers together.

T02 uses typed LINQ UPDLOCK/HOLDLOCK/composite-PK point reads and missing-key range protection; no native balance-lock escape was required. Its provider-free StockValuationState validates Q/V, exact nullable average and signed aggregates use checked coefficient arithmetic. Reconciliation takes Exclusive before ledger/valuation/balance/source reads, indexes receipt/sale lines once and cooperatively checks cancellation through materialized analysis and completion. The final-reader cancellation proof uses existing test tracing, without a production hook. Sales source fixtures verify reconciliation diagnostics, not a completed sale writer.

### 6.2 Required Post sequence

1. Authorize the current actor; start UoW transaction; acquire the Shared barrier.
2. Lock the document header, admit only Draft with the expected opaque rowversion, and load its persisted lines. An already Posted document causes no new economic effect.
3. Lock and validate required active references in the common order: warehouse, employee, customer for Sales, then distinct products by numeric ID. Validate saved unit snapshots and retain locks through commit; Posted history may display later-inactive references.
4. Acquire **all** distinct `(WarehouseId, ProductId)` balance locks in ascending tuple order before writing any ledger row. Use sequential point reads, not an assumption that an `ORDER BY` determines physical lock acquisition.
5. Point-read balances through their unique composite PK using `UPDLOCK, HOLDLOCK`; retain an absent-key range lock as well. Verify the actual SQL/index path and missing-row concurrency behavior. Never treat an unlocked missing row as zero.
6. Validate quantities, grouped availability, current value invariants and all numeric bounds; compute every new balance and sale cost allocation. Failure in any key aborts the whole document.
7. Apply the conditional Draft/expected-version transition and cost snapshots; append per-line quantity/value pairs. Capture each inserted ledger identity and use that exact identity for its companion; T02 replaces the quantity-only receipt APIs with gate/balance admission and `ApplyPostedInventoryAsync`; its coordinator captures ledger identity and immediately inserts the companion. Apply sorted quantity **and value** deltas together, checking affected-row counts; insert a missing receipt balance only under its held key-range protection.
8. Commit once. Dispose/rollback on precommit failure. Return persisted snapshots; later security-audit reporting follows the existing committed-operation semantics and never causes a second Post.

The logical guarded update is `Quantity = Quantity + quantityDelta, InventoryValue = InventoryValue + valueDelta` on the already locked row.
The balance `RowVersion` still advances naturally; it neither supplies valuation order nor replaces locks.
SQL Server documents update locks and [serializable key-range protection](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide).
Lock escalation and unrelated writers can still cause contention/deadlocks; the design does not promise a deadlock-free database.
Record bounded failures and verify rollback. Any later retry policy must restart the entire operation at a proven safe boundary.
An uncertain commit outcome requires authoritative reload (`RequiresPostOutcomeReload`), with no implicit client Post retry.
Keep existing Goods Receipt `AlreadyPosted` conflict semantics; “idempotent” here means one economic effect, not that repeat requests return success.

Post order for a common key is the order of successful transactions using the locked prior state.
Business dates, request arrival, identity allocation and timestamps do not choose that state. Failed/rolled-back transactions contribute no movement.
Persisted companions and line costs preserve the result; no later query reruns moving average against newly sorted history.

## 7. Schema-9 baseline and atomic valuation bootstrap

Propose one new DbUp script, **`010_DirectSalesAndValuation.sql`**, with schema-10 runtime support.
It contains the sales schema, companion table, balance value column, backfill and final constraints/indexes required for a usable state.
Scripts 001–009 remain immutable. The [current runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) describes complete schema10 and narrow exact9 maintenance admission. Baseline schema9 facts above remain historical.

### 7.1 Admission and deployment prerequisites

- Retain the existing administrative migration lock and separate maintenance identity. Admit only a genuinely Empty database, exact verified schema 9 for this transition, or exact schema 10 for no-op.
- Validate the full schema-9 journal and baseline artifact fingerprints/constraints/triggers; seeing a `009` entry is insufficient. Unknown, partial, newer or prefix-001–008 databases remain refused without mutation.
- Extend migrator admission explicitly; the current `TooOld` refusal cannot be bypassed by renaming history, editing the journal or broadly accepting all prefixes.
- Quiesce all clients/writers and verify they are drained before upgrade. Existing open schema-9 clients do not become safe because a new startup preflight exists. Prevent their reconnection/writes until only the matching runtime is deployed.
- Use a reviewed backup/recovery point and preserve sanitized validation evidence. Do not reset an existing database, replace balances, or seed zero value to make the migration pass.

### 7.2 Required data proof before backfill

Within the migration transaction and maintenance exclusion, produce counts and safe identifiers for failures, and reject any of these conditions:

1. A ledger `DocumentType` other than exactly `GoodsReceipt`, a nonpositive quantity, missing source header/line, or a source header that is not Posted.
2. A mismatch of source receipt/line identity, warehouse, product, quantity, posting actor or posting timestamp between a ledger row and its Posted receipt.
3. A Posted receipt with an invalid line count, a Posted receipt line with no ledger row, any extra ledger/source mapping, or cardinality other than one ledger row per Posted source line. Check both directions; an inner join alone hides defects.
4. A ledger key with no balance, an extra balance key even when zero, a quantity mismatch, negative quantity, invalid source cost/scale, or any aggregate/amount overflow.
5. A partially introduced valuation/sales artifact or conflicting column/constraint definition. Partial execution is an investigation state, not permission to fill gaps heuristically.

For each verified legacy ledger row calculate `round4(source.Quantity * source.UnitCost)` and insert one companion with that same `LedgerId`.
Per-line SQL bootstrap can use the exact `decimal(18,4) * decimal(19,4)` product (`decimal(38,8)`), explicit rounding and checked conversion to `decimal(28,4)`.
Use widened checked sums, compare against target ranges, and treat any SQL overflow as a fatal migration error; do not catch it and substitute zero.
Prove semantic parity with the fixed-point arithmetic using independent boundary cases before accepting this migration.
Set each existing balance's value to the sum of its verified companion values, while retaining its already-proved quantity.
Legacy schema 9 has only admissible receipt inflows, so this initial value is additive and does not require inventing an earlier sale order.
Unknown negative movements cannot be valued from today's average and are a hard rejection.
This establishes value from the stored, verified Posted receipt sources; it does not claim evidence of historical purchase prices beyond those sources.

### 7.3 Atomicity and compatibility proof

Add the value column for backfill without a misleading default, populate it, then enforce `NOT NULL` and the final trusted checks.
Do not UPDATE/DELETE existing `StockLedger` rows or disable their append-only trigger to rewrite history.
Balance value updates legitimately advance balance rowversions; preserve the rowversion column contract and existing document data/tokens.
Keep all script-010 DDL, companion inserts, balance value updates and its DbUp journal entry in the same transaction; inject failures to prove rollback to usable schema 9 with no 010 entry.
`WithTransactionPerScript()` does not make the entire clean install atomic: earlier scripts can already have committed. Document recovery for earlier-prefix failure separately.
Fresh install must complete 001–010; verified schema-9 upgrade must preserve data; a second run against exact schema 10 must be a no-op.
Old runtime preflight must refuse schema 10, new runtime must refuse schema 9, and partial/unrecognized states must fail before business work.
Update schema fingerprints/allowlists, column/constraint/FK/trigger checks, package manifest generation and both runtime/admin package verification together.

Review runtime grants for sales tables, valuation SELECT/INSERT and balance updates; keep valuation UPDATE/DELETE and schema administration unavailable.
Keep the documented Local direct-SQL trust limitation; new DTO permissions do not make a shared runtime database identity a server security boundary.
Extend owned disposable-database fixture reset/seed profiles for new dependencies and append-only triggers; delete valuation companions before parent ledger during authorized reset.
Preserve verified allocation ownership, exclusive fixture lifecycle, fresh migration exceptions and quarantine on uncertain cleanup. Never apply reset to an application database.

## 8. Quantity/value reconciliation and required proof

Reconciliation takes its own explicit serializable transaction, then the Exclusive barrier before any table locks.
Use a fixed table order `StockLedger -> StockValuationEntries -> StockBalances`, followed by source verification reads, with finite waits and complete disposal.
Compare key presence separately from numerical totals; an extra zero balance or an unmatched zero-value movement remains a defect.
For each key establish `sum(QuantityChange) == Quantity` and `sum(ValueChange) == InventoryValue`, with checked wide sums and target-range diagnostics.
Check one companion per ledger, source-line completeness, receipt/sale sign rules, supported document types and Posted source state.
For sales establish per-line `ValueChange == -CostAmount`, negative line quantity, complete cost snapshots and group/document sums; include gifts.
For receipts establish the companion equals the rounded source line value. A companion total matching balances alone cannot establish correct source valuation.
Require nonnegative balances and zero value at zero quantity. Report defects; reconciliation never repairs data silently.

The [acceptance ledger](DIRECT_SALES_ACCEPTANCE.md) must cover exact arithmetic/ties/overflow, duplicate/gift allocation, full depletion, receipt integration and backdated documents.
Real-SQL independent assertions must cover concurrent sale/sale and sale/receipt on a common key, reversed multi-key requests, absent balances, reconciliation during Post, rollback at each write stage, and repeated Post effects.
Migration evidence must cover clean install, exact schema-9 upgrade, no-op, invalid-source refusal, injected failure atomicity, old/new client refusal, restricted privileges and fixture isolation.
Preserve one connection/current transaction through mixed LINQ/native operations and demonstrate finite timeout/cancellation cleanup.
Plan approval supplies no implementation or acceptance evidence. This documentation change runs no SQL/GUI/product tests; future execution follows [TEST_POLICY](TEST_POLICY.md).
