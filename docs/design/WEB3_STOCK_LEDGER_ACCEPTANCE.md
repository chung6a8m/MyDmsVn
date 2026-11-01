# WEB3 — independent SQL stock/value/COGS acceptance

**Applies to:** backend `WEB3-B03/B04/B05` and Web `WEB3-F06`. **State:** Test design / NOT RUN. Business source remains [Data model](../DATA_MODEL.md), [Direct Sales costing](../DIRECT_SALES_COSTING.md), [ADR 0004](../adr/0004-p5-ledger-and-posting.md) and [ADR 0011](../adr/0011-direct-sales-and-moving-average-costing.md). Execution authority is **only** [TEST_POLICY](../TEST_POLICY.md) plus the selected backend plan. This document does not create another test-gate framework or claim SQL PASS.

## 1. Ownership and safety

- Use the existing six SQL test owners selectively; Inventory for receipt/stock/valuation, Security for hosted HTTP and restricted principal, Foundation/Migrations only if shared DB helper/schema/grants change, Application/HttpHost.Tests for pure handler/wire behavior. Do **not** resurrect removed `tests/test-gates.v1.json`, SQL runner, general suite launcher or old approval file.
- Tests require Windows .NET Framework 4.8 runtime with SDK from `global.json` and an operator-designated **non-production** SQL Server. Explicit `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` must point to effective database `master`. Missing = required test BLOCKED/NOT RUN, not success. SQL test projects run sequentially, not concurrently.
- Every SQL allocation must use the helper's own generated name, positive ownership token/instance/database ID verification, Respawn per reviewed profile or dedicated fresh helper for exceptions, bounded drain and safe cleanup; no ad hoc prefix-based DROP and no cross-process shared database.
- Host pilot uses exact schema11 and **restricted Web runtime principal** with permission-provisioning tests; Desktop/default exact schema10 admission remains separate. Do not weaken SQL/login privileges, disable ledger trigger or choose an unvalidated schema version to pass.
- Evidence must distinguish physical SQL row assertions from API JSON/UI assertions and record web SHA, backend SHA, test method, run ID, datetime, fixture cleanup and skips.

## 2. Physical invariants to prove

For every warehouse/product stock key, after a successfully Posted document:

1. `StockBalances.Quantity Q` is equal to signed sum of `StockLedger.QuantityChange` for that stock key under the known seed/baseline; `StockBalances.InventoryValue V` equals sum of matching `StockValuationEntries.ValueChange` plus seeded initial value.
2. Every posted Goods Receipt line has **one** positive quantity ledger source (`DocumentType=GoodsReceipt`, `DocumentLineId`), exactly one corresponding valuation companion and a correctly rounded individual receipt value; no duplicate unique source.
3. Every Posted Sales line has exactly one negative quantity ledger and one valuation companion, with `ValueChange == -CostAmount`, including Gift and duplicate-Product Sales rows. Group allocation equals total frozen COGS, not an independently recomputed per-row average.
4. `Receipt/SALE status + audit actor/time + ledger rows + valuation rows + balances` commit or rollback within the same explicitly owned transaction. Draft changes never modify inventory Q/V/ledger.
5. Concurrent Post of the same receipt does **not** add a second economic effect; existing `GoodsReceipt.AlreadyPosted`/`ConcurrencyConflict` response remains authoritative and need not be 200 idempotent success. Do not invent exactly-once HTTP delivery.
6. Inventory reconciliation sees consistent snapshots under the existing Exclusive writer protocol. StockCard queries show both sources ordered by `PostedAtUtc,LedgerId` with real document number; **no running balance** is asserted because it is not in existing DTO.

## 3. Required scenario matrix (proposed IDs, no tests created by this document)

| ID | Setup/action | Independent checks |
|---|---|---|
| WEB3-SQL-01 | Create/Update Draft with 1 and 200 unique Product rows | Real stored ordered lines; correct no-ledger/no-QV side effects; actor/rowversion and returned ID/version |
| WEB3-SQL-02 | Duplicate Product / 0,201 lines / invalid precision | 400 field errors, no mutated SQL; length and precision source limits |
| WEB3-SQL-03 | Post GoodsReceipt after valid Draft | One positive ledger and valued entry per line, Q/V increase, Posted audit, read-only |
| WEB3-SQL-04 | Same-Receipt concurrent Post, stale versions | Exactly one stock/ledger effect, one 409; no duplicate source |
| WEB3-SQL-05 | Force Post write/valuation failure | Entire status, ledger, valuation, balance rollback; deterministic injection, no persistent test-only trigger |
| WEB3-SQL-06 | Receipt → Sales with existing stock | Pairwise exact Q/V deltas, persisted Sales COGS and Gift/duplicate allocation, no negative forbidden state |
| WEB3-SQL-07 | Receipt Post **after** Sales Post | Earlier Sale CostAmount/PostedUnitCost, COGS/profit and ledger valuation unchanged |
| WEB3-SQL-08 | Separate warehouses/products and interleaving Posts | Per-key moving average and sorted stock locks; no key contamination/deadlock under deterministic handshakes |
| WEB3-SQL-09 | Inventory Balances and Stock Card query | Real valuation value, nullable average, signed quantity/value, source doc ID/number, filters/ordering/paging |
| WEB3-SQL-10 | Read/Write/Post denied independently, active/revoked actor | 401/403 corresponding permission; no SQL side effects; server actor used for audit |
| WEB3-SQL-11 | Restricted runtime principal, schema11 vs desktop schema10 | Required grants only; Web cannot DDL/privileged admin or operate with desktop SQL principal |
| WEB3-SQL-12 | Browser HTTPS pilot, two actors, commit-then-response-loss | Original command committed once, UI outcome unknown until authorized evidence; no replay after refresh/timeout |

**Method/class names above are scenario IDs, not runnable selectors.** Implement in appropriate existing owners, discover actual tests and record nonzero execution. Some scenarios require more than one test and negative/fault cases; avoid a false "12/12" just because 12 lines exist here.

## 4. Example arithmetic oracle — fixture design, not test result

Seed a positively owned stock key with an actual consistent source history such that opening `Q=10.0000` and `V=100.0000`. Post Goods Receipt `10.0000 @20.0000`:

- Physical post state: `Q=20.0000` and `V=300.0000`; stock-card receipt `QuantityChange=+10.0000`, `ValueChange=+200.0000`.
- Post Sales `4.0000` at arbitrary entered selling price: pre-issue moving average `V/Q=15`, `COGS=60.0000`; `Q=16.0000`, `V=240.0000`, sale stock-card `QuantityChange=-4.0000`, `ValueChange=-60.0000`.
- Post **later** receipt (possibly with earlier business `ReceiptDate`) at another unit cost. Assert the original Posted Sale's persisted cost and historical movement remain fixed. The new receipt only affects subsequent moving-average state by actual **Post** order.

The test should independently query physical `StockLedger`, `StockValuationEntries`, `StockBalances`, `SalesReceiptLines` and receipt/sale headers; compare source keys, amounts and status, then compare HTTP/UI read projections as a secondary check. Do not obtain expected values from production query service under test. Use exact fixed-point/rational expectations; no JS `Number` or C# display-rounded average as independent oracle.

Add a fractional/tie case with Goods Receipt line-level rounded value, duplicate Sales rows including gift, full depletion (Q and V become zero), no-history nullable average and maximum-range validation. If rounding requirements differ from initial worked example, source `DIRECT_SALES_COSTING.md` wins.

## 5. Failure/lock/session cases

- Distinguish pre-admission known 400/401/403 from dispatched network failure/timeout. After dispatch, a lost response is not proof of rollback. Test controls should abort **delivery after server success** (as WEB2 pilot did via a test-owned interception), not fake a database transaction.
- Post ack without read permission is still authoritative Posted; GET 403 after ack must not produce another HTTP Post or reverse stock UI. Unknown Post must retain the original snapshot/actor fence and no blind duplicate. Unknown Create without idempotency key cannot be uniquely proven merely by same-content list search.
- Revoke grants after a successful login; token validity does not override new permission decisions. Two actors must be isolated in server request scopes, sessions, audit and UI stores. Explicitly check SQL remains unchanged on denial.
- Concurrent stock-key Post cases require observable lock handshakes, finite timeouts and deterministic disposal; avoid `Thread.Sleep` as the sole race proof. Exhausted/reset-poisoned SQL helper retains allocation and reports failure rather than deleting uncertain resources.

## 6. Test execution, evidence and STOP

Use the current [test command owner](../../tests/README.md) and [sole policy](../TEST_POLICY.md). Examples only (actual new filters must be discovered in code):

```powershell
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore
```

For SQL, run the focused **Inventory** then **Security** owners sequentially after locked restore, with genuine configured SQL and unique TRX directories. Add Migrations/Foundation owners only if their affected contract changed. Existing WEB2 IIS Express browser wrapper requires explicit publish, certificate thumbprint and browser worktree variables; a WEB3 pilot should extend this owned fixture, not fork an unreviewed runner.

**Evidence template:** `docs/evidence/web3/README.md`. Include exact source hashes, command/case counts, tested browser and SQL versions, principal/permissions, schema, Q/V/COGS equations and physical queries/assertions, failure and cleanup status, skipped reasons, limits. No credentials/raw tokens/TRX/HAR/trace in repository.

**STOP:** ledger/cost algorithm, schema migration, runtime principal escalation, unsupported Local/desktop compatibility, full IIS or production release, operation journal and automatic replay, or a new test infrastructure framework all require explicit scope decision. If optional Windows/SQL full IIS environment is missing, mark the corresponding requirement NOT RUN; never call IIS Express a full IIS test.
