# Direct Sales — approved business and architecture design

**Status:** ACTIVE ROADMAP / BOUNDED DEVELOPMENT CHECKPOINTS / FULL SAL INCOMPLETE. **Updated:** 2026-10-22.
**Target:** `v1a-pro`. **Inspected source baseline:** `35ba2e8a3f37fb07095701eb94f4b56f7941c491` (merged PR #41).

The Owner selected direct sales receipts and moving weighted average costing, and requested the necessary documents, an active roadmap and a PR. This document records that decision and implementation rules. Approval of the design does not establish runtime, migration, UI or production acceptance.

The sole queue is [SAL-T00–T08](plans/20261021-002-direct-sales-and-moving-average-costing.md), indexed by [plans/README](plans/README.md). Read this design with [costing and persistence](DIRECT_SALES_COSTING.md), [query and desktop interaction](DIRECT_SALES_QUERY_AND_UI.md), [acceptance](DIRECT_SALES_ACCEPTANCE.md) and [ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md). Those documents define one roadmap. T00 foundations and T01 schema/bootstrap source are implemented ([T00](evidence/sal/20261022-t00-foundation.md), [T01](evidence/sal/20261022-t01-schema-bootstrap.md)); T02 receipt/reconciliation/stock reads are implemented ([evidence](evidence/sal/20261022-t02-valued-inventory.md)); T03 Draft Create/Update/Get has [reviewed evidence](evidence/sal/20261022-t03-sales-draft.md); T04 implements Sales Post; T05 implements List/keyset; T06 implements Local/VM; native UI remains incomplete. See the execution index for the active task; the checkpoint is not deployable.

T04 source checkpoint (2026-10-22, `7eeafa4`): [reviewed evidence](evidence/sal/20261022-t04-atomic-sales-post.md) records atomic Post on one owned UoW, Shared-before-header and all sorted stock locks before writes, exact duplicate/gift allocation, immutable line cost, paired quantity/value movements and authoritative status/version materialized before commit. Receipt Post and Exclusive reconciliation use the same protocol. Native UI, frozen T08, package and Owner acceptance remain open; this checkpoint is not deployable. T01 R3 remains deferred Minor.

## 1. Outcome and scope

An authorized user creates a sales Draft, selects a customer, warehouse and responsible employee, enters product lines and selling prices, then Posts the receipt. Successful Post deducts stock, freezes its moving-average cost and makes the document read-only. The user can find receipts by Product as well as the other header/search filters and navigate by cursor.

| In this roadmap | Explicitly outside this roadmap |
|---|---|
| Direct sales receipt, Draft → Posted | Sales orders, quotations, deliveries against orders and POS/till workflow |
| Manually entered selling price per line | Price lists, automatic pricing, discounts, tax and fiscal/e-invoice integration |
| Gift flag, duplicate Product lines | Gift programs, bundles and promotions engine |
| Stock deduction and perpetual moving-average cost | Payments, receivables/debt, returns, reversal/cancellation of Posted documents |
| Read-only posted cost and gross profit | General ledger/accounting journals, periodic average, FIFO, lots/serials and backdated cost recalculation |
| Filtered receipt list with keyset pagination | Numbered-page jumps, snapshot exports, printing and reporting subsystem |
| Quantity/value reconciliation and schema9 → schema10 preparation | Multi-currency, unit conversion, reservations, transfers, negative stock and offline/HTTP mode |

The inspected planning baseline has Goods Receipts and quantity-only inventory under scripts001–009. Development source at `81e8aab` adds schema10, Sales persistence shapes and valuation/bootstrap; T02 adds valued receipt posting, the common coordinator, Exclusive reconciliation and stock projections; T03 adds authorized Draft persistence/detail with stable line identity and current Unit snapshots; T04 implements Sales Post and completes the receipt/sale/reconciliation source protocol; Native UI and frozen deployment acceptance remain incomplete. Goods Receipt duplicate-Product restrictions remain specific to Goods Receipts. Preserve the existing net48 product/test graph, Windows x86 Desktop.App and separate DbMigrator, Local adapter and vendor pins. AppCore/.NET 8 remains parked.

## 2. Sales receipt behavior

### Header and identity

- Required: `SaleDate`, `CustomerId`, `WarehouseId`, `EmployeeId`. One customer and warehouse apply to the whole receipt. Note is optional, at most 1,000 characters, with NUL rejected.
- `SaleDate` is a business calendar date; normalize the Local request to `DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified)` without UTC/local conversion. Its JSON wire value is exactly an invariant `yyyy-MM-dd` string, with the same rule for optional FromDate/ToDate. The T00 Sales-only property converter rejects timestamps, offsets and impossible dates and writes date strings; it must bypass the global ApiJson UTC conversion for these properties only. Posting/audit timestamps keep the existing UTC convention. Golden Local/JSON round-trips must preserve the day across cultures, DateTime kinds and client time zones.
- Create assigns an immutable positive `bigint` `SaleId` and server-generated `SaleNo`: `SA` followed by a zero-padded ten-digit sequence value. Sequence starts at 1, does not cycle and stops at 9,999,999,999; gaps are valid. Clients never allocate or predict numbers.
- Save validates required references and all line Products as active. Posted history remains readable when a catalog item later becomes inactive. Queries use current catalog labels/availability and the persisted line Unit snapshot; this is not a fiscal customer/product-description snapshot feature.
- Opaque Base64 rowversion is returned as `Version` and required as `ExpectedVersion` for Update and Post. Clients never compare or synthesize its numeric contents.

### Exactly seven line columns

| Position | Business label | Behavior |
|---|---|---|
| 1 | Số thứ tự / STT | Server assigns `LineNo` 1..N from submitted order; read-only |
| 2 | Sản phẩm | Active Product lookup for editable lines; **the same Product may appear more than once** |
| 3 | Đơn vị tính | Read-only Product Unit snapshot; no conversion |
| 4 | Số lượng | `Qty` > 0, fits `decimal(18,4)`; at most four effective fractional digits |
| 5 | Đơn giá bán | User enters `UnitPrice` ≥ 0, fits `decimal(19,4)`; no price lookup or pricing engine |
| 6 | Tặng | `IsGift` checkbox; retains the entered UnitPrice when toggled |
| 7 | Thành tiền | `LineAmount = IsGift ? 0 : round4(Qty × UnitPrice)`; server authoritative |

Require 1–200 non-null lines. A zero selling price is permitted on an ordinary line; it does not implicitly set the gift flag. Gifts consume quantity and cost exactly like ordinary lines. Duplicate Products remain separate rows with separate identity, quantity, price and gift state; there is no unique `(SaleId, ProductId)` constraint and no duplicate-Product validator.

Each saved line has `SaleLineId`; a new unsaved row additionally needs a stable client row key, because several new lines have database ID 0. Update accepts each positive SaleLineId at most once and only if it belongs to the locked Draft. A submitted zero ID creates a new line; omitted existing IDs remove those Draft lines. Reordering only changes LineNo. Validate all identity changes before any replacement, and update/resequence transactionally without transient uniqueness failures.

The server derives `Unit nvarchar(32) NOT NULL` from Product when saving, preserving the accepted catalog value and its required/nonblank/no-NUL bounds; it does not trust a submitted unit. Post compares the saved Unit with the locked Product Unit using ordinal equality. A mismatch requires explicit reload/review and Save; it must not silently reinterpret Quantity or convert units. Posted Unit snapshots are immutable transaction labels, not a conversion model or proof of historical base-unit changes.

Line amounts and receipt totals use `decimal(28,4)`, round midpoint away from zero, and reject out-of-range results. Do not round input values into eligibility or rely on unchecked `decimal` multiplication. The [costing specification](DIRECT_SALES_COSTING.md) defines fixed-point arithmetic, overflow and conservation rules for both revenue and inventory value.

### State transitions and failure behavior

| Operation | Required state and permission | Result |
|---|---|---|
| Create | `Sales.Receipts.Write`, valid body | Persist Draft, rows and server totals; no inventory effect |
| Update | Draft, matching version, Write | Persist edits and return current detail/version; no inventory effect |
| Get/List | `Sales.Receipts.Read` | Return authorized detail or cursor page; Posted is read-only |
| Post | Draft, matching version, `Sales.Receipts.Post` | Atomically freeze costs, append movements, change balances and mark Posted |
| Post again | Already Posted | `SalesReceipt.AlreadyPosted` conflict; reload authoritative detail; no duplicate effect |
| Update Posted | Already Posted | Conflict; no mutation |

There is no Posted → Draft transition, delete/cancel command, automatic reversal or silent retry of a mutating command. A Draft does not reserve stock; availability may change before Post. Create is not request-idempotent: after an uncertain create outcome, locate/reload the possible saved receipt before creating another.

Current actor, active-account and code-defined permission checks occur in Application, not only in the UI. Keep safe `ApiResponse<T>` mapping and field paths relative to the public JSON request. Proposed business errors include `SalesReceipt.NotFound`, `SalesReceipt.AlreadyPosted`, `SalesReceipt.ConcurrencyConflict`, `SalesReceipt.InsufficientStock`, `SalesReceipt.UnitChanged` and validation details for precision/reference/cursor failures. Preserve distinct validation, authorization, conflict, cancellation and unexpected-failure semantics. Do not expose SQL parameters or exceptions.

## 3. Cost and stock decision

Cost pool is **WarehouseId + ProductId**. Let Q be locked on-hand quantity and V its authoritative inventory value before a Post. A receipt adds quantity and the sum of its individually rounded line costs. A sale groups its repeated Product lines, validates their combined quantity S against Q, and removes group value C. For full depletion C = V; otherwise C is round4(V × S / Q), bounded to [0,V]. Allocate C deterministically across the original lines by cumulative quantity in LineNo order.

Each posted line stores a common pre-issue `PostedUnitCost` snapshot (`decimal(28,8)`) and its authoritative allocated `CostAmount` (`decimal(28,4)`). A rounded unit-cost display is not a substitute for CostAmount. Gifts have LineAmount 0 and positive/zero cost according to the pool. Draft cost/profit fields are null, not misleading zeros or promises of a future Post price.

`TotalAmount = sum(LineAmount)`, `TotalCost = sum(CostAmount)` and `GrossProfit = TotalAmount − TotalCost`. The latter two exist only for Posted receipts. This is gross profit before taxes/overheads, not accounting net profit. Cost and profit are read-only detail/summary information outside the required seven-column grid; the selected line may expose its posted cost in an inspector.

Cost follows the actual serialized Post order for each stock key. Backdating SaleDate or ReceiptDate never recalculates an existing posted sale. The full [costing specification](DIRECT_SALES_COSTING.md) owns arithmetic examples, lock order, ledger conservation, migration admission and bootstrap; implement and test it as one coherent inventory protocol.

### Atomic boundary

One explicit UoW and current SQL transaction must contain Post validation, reference/header/stock locks, persisted cost snapshots, negative `StockLedger` rows, companion valuation rows, balance quantity/value updates and status/audit fields. A failure anywhere rolls them all back. Marking Posted early inside that same transaction is permissible only if no observer can see a partial commit.

Acquire the transaction-owned `MyDmsVn.InventoryValuation.v1` application lock at the start: Shared for all receipt/sale stock writers, Exclusive for reconciliation. Writers then acquire document/reference locks and every affected stock key in the same sorted order **before** inserting ledger rows. Protect missing balance keys with range locks; validate negative/overflow states. Receipt posting and reconciliation must adopt this protocol alongside sales posting. Do not ship a mixed protocol or claim a deadlock-free design merely because sales keys are sorted.

Same-document concurrent Post produces one economic effect. Different receipts competing for insufficient combined stock cannot both succeed. A deadlock/timeout may yield a safe conflict/failure, with rollback and explicit reload; the UI must not auto-replay Post. In an uncertain transport/cancellation outcome, retain the recovery lock until a successful authorized reload determines the committed state.

Business audit fields are transactional. Keep existing safe post-commit security/diagnostic logging semantics: logging failure cannot undo a committed sale or invite a second Post. Extend the existing audited-action definitions deliberately when implementing sales; do not invent a second independent security audit subsystem.

## 4. Persistence model and remaining workflow obligations

| Object | Key fields and constraints |
|---|---|
| `SalesReceipts` | `SaleId bigint` PK; unique `SaleNo`; `SaleDate date`; required Customer/Warehouse/Employee FKs; Draft/Posted status; Note; UTC created/updated/posted actor fields; rowversion |
| `SalesReceiptLines` | `SaleLineId bigint` PK; SaleId FK; unique `(SaleId, LineNo)`; Product FK; Unit snapshot; positive Qty18,4; nonnegative UnitPrice19,4; IsGift; LineAmount28,4; nullable Draft/required Posted cost snapshots |
| `dbo.SalesReceiptNumberSequence` | `bigint`, start/min/increment 1, max 9,999,999,999, NO CYCLE; separate from Goods Receipt numbering |
| `StockBalances.InventoryValue` | Nonnegative value28,4 paired with existing quantity and rowversion; zero quantity implies zero value |
| `StockValuationEntries` | Append-only value companion keyed/FK by existing LedgerId; one value delta per quantity movement; receipt value ≥ 0, sale value ≤ 0, including required zero-value companions |
| Existing `StockLedger` | Preserve historical rows and append-only trigger; sale document type `SalesReceipt`, one negative movement per SaleLineId |

Use SQL CHECK/FK/unique constraints for row-local rules and transactional repository validation for cross-table state/snapshot invariants. Persist server-calculated LineAmount; derive header totals from all lines, avoiding a second independently editable header-total source. Validate total precision before Save/Post and in query mappings. No direct client write API for balances, quantity ledger or valuation ledger.

Extend the existing stock read models without replacing their paging contract: `StockBalanceRow` adds InventoryValue and nullable AverageUnitCost (round8(V/Q) when Q > 0, null when Q = 0); invalid balance states or unrepresentable results fail closed. `StockCardRow` adds ValueChange and resolves sales DocumentNumber from SaleNo as well as existing Goods Receipt numbers. Existing Inventory read permissions apply. T02 implements these value fields and sales-number resolution with [bounded query/contract evidence](evidence/sal/20261022-t02-valued-inventory.md). Existing inventory paging remains unchanged; this does not implement the Sales writer/query/UI.

Indexes must support SaleId seek order, selective header filters and Product membership; the [query specification](DIRECT_SALES_QUERY_AND_UI.md) defines candidate indexes and actual SQL/plan evidence. Do not claim an index eliminates all filtered scanning or create every permutation of filters.

T01 implements schema10 through one new immutable `010_DirectSalesAndValuation.sql` migration plus code that admits **only an artifact-validated complete schema9 baseline** for this specific upgrade. Empty install and already-compatible no-op remain supported. Prefixes001–008, unknown/partial/future schemas remain rejected. Old scripts001–009 are not rewritten.

Before enabling sales, bootstrap value companions and balances from validated historical posted Goods Receipts under the offline admin migration boundary. Verify complete one-to-one receipt-line/quantity-ledger correspondence and quantity reconciliation; fail on unsupported movements, mismatches, negative states or arithmetic overflow. Do not silently assign zero to unknown stock or rewrite historical ledger entries. An atomic migration includes bootstrap and the DbUp journal entry; no partially valued schema10 may open in Desktop. The [costing document](DIRECT_SALES_COSTING.md) defines the exact recovery and arithmetic proof obligations.

## 5. Implemented and planned application boundaries

| Layer | Responsibility and current boundary |
|---|---|
| Contracts | T00 `SalesContracts.cs`: save/update/post/detail DTOs, slim list rows, independent cursor request/page and numeric limits; `SalesCalendarDateJsonConverter.cs` scopes date-only wire handling to Sales calendar properties |
| Domain | T00 `Sales/SalesReceipt.cs`, `SalesReceiptLine.cs`, statuses; inventory fixed-point moving-average calculator/value types without SQL/UI dependencies |
| Server.Application | T03 Draft Create/Update/Get, repository/detail interfaces, validators and errors; T00 permission definitions; T04 Post and T05 List are implemented; T06 Local/VM is implemented; native UI remains later work |
| Infrastructure | T01 mappings, T02 valuation coordinator and T03 Draft/Get LINQ repositories; owned connection/transaction and sequence boundary; T04 Post and T05 List are implemented |
| Local | `LocalSalesReceiptApiClient` using existing actor/session-generation dispatch and response mapper |
| Desktop.Application | `ISalesReceiptApiClient`; sales list/editor/line ViewModels, cursor state, raw-edit and recovery admission |
| WinForms | Sales navigation/factory, compact List/Detail control and seven-column grid using established pinned inline sessions |

`SaveSalesReceiptRequest` carries SaleDate, CustomerId, WarehouseId, EmployeeId, Note and ordered Lines. `UpdateSalesReceiptDraftRequest` additionally carries SaleId and ExpectedVersion. `SaveSalesReceiptLineRequest` carries SaleLineId (0 for new), ProductId, Qty, UnitPrice and IsGift; it does not accept LineNo, Unit, amount, cost, status, number or actor fields. `PostSalesReceiptRequest` carries only SaleId and ExpectedVersion. `PostSalesReceiptResponse` contains SaleId, Posted status, PostedAtUtc and the authoritative new Version; Get reloads full detail.

DTOs use `SaleNumber` for SQL `SaleNo`, and `SaleLineId`/`LineNo` for persisted line identity/order. Preserve current immutable transport-neutral result envelopes, cancellation and JSON conventions. Do not expose Domain entities, IQueryable, LINQ provider types, connections or transactions. Update architecture/contract tests and DI coverage rather than adding another composition root.

Use LINQ to DB for new ordinary reads/writes, predicates, joins, `Any`, aggregates, ordering and paging. Keep raw SQL limited to DbUp/metadata, sequence allocation, SQL application locks and a narrowly justified lock operation the pinned provider cannot express safely. All exception SQL is parameterized and uses the same owned connection/transaction. No EF/Dapper switch, hidden query connection, interpolated user SQL or global persistence refactor belongs here.

## 6. List, interface and security

The list filters inclusive SaleDate range, Customer, Warehouse, Employee, Draft/Posted, **Product**, and bounded free-text Search. Search is a literal substring over SaleNo, Note and customer code/name under the existing database collation. Product membership uses a correlated `Any`; repeated Product lines do not multiply receipt rows, and the displayed receipt total always covers all lines.

Order is unique immutable **SaleId descending**. Next/Previous use seek predicates and PageSize+1; page size defaults to50, maximum200. First/Refresh and filter/page-size changes clear the cursor. The opaque cursor binds version, direction, anchor, order and normalized filters/page size; validate it before SQL. No OFFSET, Skip, mandatory total count or numbered-page jump. This is live navigation across requests, with one short consistent read transaction per response. See the [query specification](DIRECT_SALES_QUERY_AND_UI.md) for exact reverse paging, concurrent-change and empty-page behavior.

Preserve BCF's compact shell and guarded List/Detail navigation, native per-key lookup behavior, pending versus committed identity, stable row keys, strict numeric parsing, cancellation and disposal. Product, Customer, Warehouse, Employee and list-filter lookups need their own new evidence. Save/Post must resolve raw input before deciding whether a Draft is clean. Posted/busy/denied/recovery states prohibit edits through keyboard, checkbox and accessibility paths alike.

Add three **code-defined Active** permissions: `Sales.Receipts.Read`, `.Write`, `.Post`. Read includes persisted cost and gross-profit information; no separate costing permission is introduced. Read opens the UI and queries, Write authorizes Create/Update, Post authorizes Post. Each service operation enforces its own key; returned command DTOs follow existing authorized-write semantics. Unknown/deprecated deny before superuser, ordinary DirectDeny wins and current database binding remains mandatory. No permission-table sync or automatic role grant.

Update schema object inventories, explicit least-privilege provisioning/validation, mapping registration, audit-action definitions and owned SQL reset profiles with the new objects. Preserve the distinction between code permissions and SQL principal grants. Polymorphic StockLedger document links require deliberate reset verification; FK discovery alone is insufficient.

## 7. Delivery and evidence

T00 foundations and the [reviewed T01 checkpoint](evidence/sal/20261022-t01-schema-bootstrap.md) are complete; [T02 valued inventory](evidence/sal/20261022-t02-valued-inventory.md) is also complete. [T03 Draft/detail](evidence/sal/20261022-t03-sales-draft.md) is complete; the execution index records T04 IN PROGRESS. T04 Post is implemented; full workflows, frozen T08 and Owner acceptance remain incomplete. The [plan](plans/20261021-002-direct-sales-and-moving-average-costing.md) gives dependencies, exact proposed locations, test-first behaviors and focused commands. Intermediate task commits are development checkpoints; schema10 is not deployable until all stock writers, reconciliation, guards and packaging agree on the new contract.

The [acceptance ledger](DIRECT_SALES_ACCEPTANCE.md) requires arithmetic/conservation, migration/bootstrap/recovery, atomicity/concurrency, LINQ/keyset, authorization/session, native GUI and frozen package evidence. SQL tests use only explicitly designated non-production infrastructure and verified owned database lifecycle. GUI tests require interactive Windows/net48, serialized guarded STA and bounded cleanup. Preserve the removed runner/approval-file boundary and record actual discovery/results rather than inherited counts.

BCF/BCI/GUI-P5/L2DB/RESP remain completed historical baselines with exact revisions, failures and exceptions. Their acceptance does not certify sales. [Operational release prerequisites](DEFERRED_WORK.md#release-prerequisites) stay UNACCEPTED / UNSCHEDULED; the feature's scoped schema9 migration tests do not waive a production rollout/recovery decision. The T01 source checkpoint includes schema changes and bounded SQL evidence; it does not establish packaged startup/upgrade, feature/Owner or production acceptance.
