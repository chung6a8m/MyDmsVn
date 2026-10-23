# ADR 0011 — Direct sales and moving weighted average costing

- **Date:** 2026-10-21
- **Status:** Accepted design decision; source implemented, execution queue archived (2026-10-23)
- **Original decision baseline:** `v1a-pro@35ba2e8a3f37fb07095701eb94f4b56f7941c491` (PR #41)
- **Scope:** Local net48 sales and inventory value; source rules remain binding, with no production release acceptance

Archive note (2026-10-23): Sales/schema10, shared receipt/sale/reconciliation valuation, keyset queries and Local/WinForms source are merged at `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` (PR #43). R3/R4 source fixes were reviewed/approved at `f5039cb`. [T08 integration evidence](../evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS. Native125, attended observations and dated Owner acceptance remain OPEN.

The [archive boundary](../archive/20261023-pre-ui-next/README.md) retires the SAL queue and excludes its unfinished work from the new [UI-NEXT roadmap](../ui-next/20261023-active-roadmap.md), without completing or waiving it. This ADR's accepted business/transaction rules remain binding. The original decision context below describes the pre-SAL schema9 baseline, not current source.

## Context

At the decision baseline, the application recorded Goods Receipts, positive quantity movements and quantity balances under schema9. Customers were a catalog without a sales workflow. The Owner selected direct sales receipts, manually entered prices, duplicate Product lines, gift lines and cost tracking. Payments, debt and returns are excluded. The list must add Product filtering, prefer LINQ over SQL strings and use keyset pagination.

The Owner selected moving weighted average from the costing options. This requires a persisted value pool and changes to receipt posting and reconciliation; adding only a negative quantity writer would not establish reliable cost. Existing append-only ledger, explicit UoW, rowversion, authorization and desktop session boundaries remain applicable.

## Decision

1. Add direct sales Draft → Posted with required Customer, Warehouse and Employee. The grid is exactly STT, Product, Unit, Quantity, UnitPrice, Gift and Amount. Prices are manually entered. Duplicate Products stay separate; gifts preserve entered price, produce zero revenue and consume full quantity/cost.
2. Calculate moving weighted average per `(WarehouseId, ProductId)` at actual serialized Post time. Persist the pre-issue unit-cost snapshot and authoritative allocated cost on every Posted line. Do not retroactively revalue earlier Posts or use the business date as the cost sequence.
3. Keep quantity StockLedger append-only and add an append-only one-to-one `StockValuationEntries` companion. Add authoritative InventoryValue to StockBalances. Sale lines generate negative quantity and nonpositive value deltas; receipt value deltas are nonnegative. A zero-value movement still requires a companion. Group duplicate Product quantities for stock validation and cost allocation; retain line-level movement identity.
4. Preserve exact value with scale4 midpoint-away rounding, fixed-point integer intermediates, full-depletion residual consumption and cumulative line allocation. Draft cost is unknown/null. Do not use rounded displayed unit cost to reconstruct posted CostAmount.
5. Make receipt/sale Post and reconciliation share one lock protocol. Acquire a transaction-owned application lock before inventory locks (Shared writers, Exclusive reconciliation); acquire all stock keys in stable order before ledger insertion. Keep all document/cost/ledger/balance mutations in one explicit SQL transaction. Reject insufficient stock and preserve one economic effect under concurrent Post.
6. Add one atomic schema10 migration, validating and bootstrapping historical schema9 receipts without rewriting quantity history. Specifically admit only verified complete schema9 for this upgrade, plus normal Empty/Compatible cases; unknown/partial/older prefixes stay rejected. Old clients are quiesced, no partial zero-valued bootstrap is accepted, and mixed writer protocols are not deployable.
7. Build new business queries primarily with LINQ to DB on the existing owned connection. Use SaleId DESC keyset pages, Product `Any` membership, all-line totals and bounded opaque filter-bound cursors. Navigation is live; no cross-request snapshot or numbered-page contract is promised. Existing offset-paged modules keep their contracts.
8. Add explicit Sales Read/Write/Post code permissions, preserve least-privilege SQL/admin separation, and extend Local/WinForms through existing layers. Keep net48, Windows x86 and pinned vendors. No automatic permission grants or framework/provider replacement.

## Alternatives considered

| Alternative | Reason not selected |
|---|---|
| Sales order followed by fulfillment | Adds another lifecycle; Owner selected direct receipts |
| POS/cashier checkout | Requires till/payment interaction outside the selected scope |
| Periodic weighted average | Needs period closure/recalculation and does not finalize cost at the selected Post boundary |
| FIFO | Requires layers and allocation history beyond the selected average-cost model |
| Recalculate past costs by document date | Would mutate already posted costs and require an explicit revaluation workflow |
| OFFSET pagination or date-only cursor | Does not meet keyset requirement; mutable/non-unique dates complicate stable navigation |
| Reuse receipt duplicate-Product constraint | Conflicts with the Owner's explicit repeated-line requirement |

## Consequences and boundaries

Sales cannot ship independently of valued receipt posting, value reconciliation, schema validation and a verified bootstrap. The initial feature requires more inventory work than a simple sales table. It avoids historical ledger rewriting by deriving immutable value companions from supported source documents.

Amounts are bounded and rounded deliberately; extremely large historical values may block migration rather than silently truncate. Gifts can make gross profit negative. A backdated sale uses the current pool when posted, and live keyset navigation can reflect concurrent edits/inserts. These behaviors must be visible in documentation and acceptance evidence.

The database application lock coordinates existing writers/reconciliation but is not a substitute for sorted stock-key locking or independent concurrency tests. Explicit SQL seams for DbUp/metadata, sequences and application locks remain legitimate; ordinary query strings are not the default implementation path.

There is no payment, receivable, return, tax, print, multi-unit, multi-currency, FIFO or periodic-cost feature in this decision. Production release prerequisites, framework reactivation and HTTP remain outside the roadmap.

## Implementation and validation

The normative [business design](../DIRECT_SALES.md), [costing specification](../DIRECT_SALES_COSTING.md) and [query/UI specification](../DIRECT_SALES_QUERY_AND_UI.md) retain the implemented rules. The [archived SAL plan](../archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md), [acceptance ledger](../DIRECT_SALES_ACCEPTANCE.md) and T08 evidence retain candidate-specific implementation/verification history and unresolved acceptance. They supply no active tasks. UI-NEXT uses its own [scoped validation and handoff](../ui-next/20261023-validation-and-handoff.md); it does not inherit the full SAL integration round or production release work.

This ADR extends the inventory decision in [ADR 0004](0004-p5-ledger-and-posting.md) and preserves the current framework/provider decisions in [ADR 0009](0009-linq2db-local-net48-persistence.md). Check the ADR index for historical statuses. No product code or schema is implemented by accepting this document.
