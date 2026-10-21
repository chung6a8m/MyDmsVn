# Direct Sales — query contracts and desktop interaction

**Status:** APPROVED DESIGN / NOT IMPLEMENTED. **Updated:** 2026-10-21.
**Source baseline:** `MyDmsVn/v1a-pro@35ba2e8a3f37fb07095701eb94f4b56f7941c491`.
This specification supports the sole [SAL execution plan](plans/20261021-002-direct-sales-and-moving-average-costing.md); it is not runtime, UI or release acceptance.
Read the [Direct Sales design](DIRECT_SALES.md), [costing rules](DIRECT_SALES_COSTING.md) and [acceptance ledger](DIRECT_SALES_ACCEPTANCE.md) together.
All new names, signatures, paths, SQL shapes and test classes below are proposed until implemented and verified on their recorded candidate.

## 1. Scope and source boundaries

Direct Sales introduces one warehouse, one customer and one responsible employee per receipt, with Draft and Posted states.
The line grid has exactly seven columns; duplicate Product lines remain independent throughout editing, persistence, queries and posting.
Payments, receivables, returns, tax, discounts and printing are outside this design.
Active application/test targets remain `net48`; the shipped desktop remains Windows x86 with the existing Local adapter.
No HTTP host, framework upgrade, grid replacement or broad shared-receipt refactor is implied.

The existing [Goods Receipt contracts](../src/MyDmsVn.Contracts/InventoryContracts.cs), [API interfaces](../src/MyDmsVn.Desktop.Application/FeatureApiClients.cs), [query service](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlGoodsReceiptQueryService.cs) and [grid adapter](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs) are implementation references.
Their offset paging, complete-detail list DTO and duplicate-Product validation must not be copied into Direct Sales.
Existing [PagedResult](../src/MyDmsVn.Contracts/PagedResult.cs) remains an offset contract for its current callers; the sales cursor result is independent.
Use the established ownership and mapping seams in [PERSISTENCE](PERSISTENCE.md), current [CONTRACTS](CONTRACTS.md) and [SECURITY](SECURITY.md).

### Proposed implementation locations

| Path under the repository root | Responsibility |
|---|---|
| `src/MyDmsVn.Contracts/SalesContracts.cs` | Sales request/detail/summary/page DTOs and shared limits |
| `src/MyDmsVn.Server.Domain/Sales/SalesReceipt.cs` | `SalesReceipt` header; `SalesReceiptLine` in adjacent `SalesReceiptLine.cs` |
| `src/MyDmsVn.Server.Application/Sales/ISalesReceiptQueryService.cs` | Materialized read interface |
| `src/MyDmsVn.Server.Application/Sales/SalesReceiptQueries.cs` | Authorized Get/List requests, normalization and validation |
| `src/MyDmsVn.Server.Application/Sales/SalesReceiptCursor.cs` | Fixed-schema cursor codec and normalized-filter binding |
| `src/MyDmsVn.Server.Infrastructure/Sales/SqlSalesReceiptQueryService.cs` | LINQ to DB header/detail/summary queries |
| `src/MyDmsVn.Server.Infrastructure/Sales/SalesLinqToDbMapping.cs` | Explicit mappings for `SalesReceipts` and `SalesReceiptLines` |
| `src/MyDmsVn.Desktop.Application/FeatureApiClients.cs` | Add `ISalesReceiptApiClient` alongside existing feature interfaces |
| `src/MyDmsVn.Desktop.Infrastructure.Local/LocalSalesReceiptApiClient.cs` | Existing authenticated Local dispatch and response mapping |
| `src/MyDmsVn.Desktop.Application/SalesReceiptListViewModel.cs` | Applied filters, cursor navigation and generation guards |
| `src/MyDmsVn.Desktop.Application/SalesReceiptEditorViewModel.cs` | Draft/detail state, per-line identity and command admission |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs` | Sales List/Detail workspace and header lookups |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGrid.cs` | Seven-column grid and accessibility surface |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs` | Sales-specific Product/numeric/checkbox editing sessions |

Keep provider types, connections, transactions and `IQueryable` inside Infrastructure.
Proposed tables are `SalesReceipts(SaleId, SaleNo, SaleDate, ...)` and `SalesReceiptLines(SaleLineId, SaleId, LineNo, ...)`; DTO `SaleNumber` maps to SQL `SaleNo`.
Exact remaining schema and command ownership follow the main design.

## 2. Public list and detail contracts

| Proposed type | Required shape |
|---|---|
| `SalesReceiptListRequest` | `FromDate`, `ToDate`, `CustomerId`, `WarehouseId`, `EmployeeId`, `Status`, `ProductId`, `Search`, `PageSize`, nullable `Cursor` |
| `SalesReceiptListPage` | `IReadOnlyList<SalesReceiptListRow> Items`, nullable `NextCursor`, nullable `PreviousCursor` |
| `SalesReceiptListRow` | `SaleId`, `SaleNumber`, `SaleDate`, customer/warehouse/employee IDs and display names, `Status`, `LineCount`, `TotalAmount` |
| `SalesReceiptDto` | Complete header, opaque `Version`, ordered `SalesReceiptLineDto` values and the approved read-only costing summary |
| `SalesReceiptLineDto` | `SaleLineId`, `LineNo`, `ProductId`, Product display/availability, `Unit`, `Qty`, `UnitPrice`, `IsGift`, `LineAmount`, approved cost snapshots |

`PageSize` defaults to 50 and accepts 1–200; reject an out-of-range value before SQL rather than silently clamping it.
There is no required `TotalCount`, `PageNumber`, numbered-page jump or inherited `PagedResult<T>` base type.
The list materializes a slim summary only; opening a selected `SaleId` explicitly requests its complete detail.
For every retained header, `LineCount` and `TotalAmount` include all its lines, including duplicate Products and gift lines with zero revenue.
Product-filtered results must never display a total calculated only from matching Product lines.
Names and availability follow the existing catalog/historical-reference conventions; selecting an unavailable historical row does not silently reactivate it.
Client monetary values are previews: server validation and recomputation remain authoritative for saved amounts and Post cost snapshots.

Proposed signatures, retaining current transport-neutral `ApiResponse<T>` and cancellation conventions:

```csharp
// ISalesReceiptApiClient in Desktop.Application
Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken);
Task<ApiResponse<SalesReceiptDto>> GetAsync(long saleId, CancellationToken cancellationToken);
Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest request, CancellationToken cancellationToken);
Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest request, CancellationToken cancellationToken);
Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest request, CancellationToken cancellationToken);

// ISalesReceiptQueryService in Server.Application; no provider objects escape.
Task<SalesReceiptListPage> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken);
Task<SalesReceiptDto?> GetByIdAsync(long saleId, CancellationToken cancellationToken);
```

Proposed Active keys are `Sales.Receipts.Read`, `Sales.Receipts.Write` and `Sales.Receipts.Post`; this documentation does not register or automatically grant them.
List/Get require Read, Create/Update require Write, and Post requires Post; the UI requires Read to open and the relevant action key to mutate.
Read includes the approved persisted cost/profit fields; no separate costing permission is introduced, and command return DTOs follow current authorized-write result semantics.
Recheck current actor/account and permission state on every request; use the `SalesReceipt.*` prefix for sales business errors.
The cursor does not carry a trusted actor, grant, rowversion, edit permission or posting authority.
Get and list results retain established error envelopes, validation paths, missing-row semantics and Local session-generation fencing.

## 3. Filtering and summary semantics

| Input | Normalization and server predicate |
|---|---|
| `FromDate`, `ToDate` | Optional inclusive calendar dates against `SaleDate`; normalize `.Date` with `DateTimeKind.Unspecified`, perform no timezone conversion, validate supported SQL date range and normalized `FromDate <= ToDate` |
| Customer/Warehouse/Employee IDs | Optional positive integer equality on the header; absence means no restriction |
| `Status` | Absent, `Draft` or `Posted`; reject other values and use one canonical representation |
| `ProductId` | Optional positive ID; header predicate `lines.Any(line => line.SaleId == sale.SaleId && line.ProductId == productId)` |
| `Search` | Trim; blank becomes null; at most 256 characters; reject NUL; Unicode literal substring matching |
| `Cursor` | Null for the first page; otherwise validate as specified below before query execution |

Search matches `SaleNo` OR `Note` OR customer Code/Name; Warehouse, Employee and Product are separate ID filters.
Keep search parameterized, with literal `%`, `_` and `[` behavior; use proven LINQ translation or escaping rather than user-defined wildcard SQL.
Do not case-fold or alter Unicode silently; matching follows the documented database collation, while cursor binding uses the exact normalized input.
Combine filters with AND; a search match may come from any approved search field.
`ProductId` is a receipt-membership test; it does not restrict the lines returned by Get or used in totals.
The proposed Sales-only `SalesCalendarDateJsonConverter.cs` in Contracts writes SaleDate/FromDate/ToDate as invariant `yyyy-MM-dd` JSON strings and reads only valid date strings (or null for optional filters), rejecting timestamps, offsets and impossible dates. Normalize Local DateTime values to their date component with unspecified kind; never call UTC/local conversion for these fields. Apply the converter to these properties only: current ApiJson globally uses UTC DateTime handling and has no date-only converter. Preserve existing audit timestamp and other module contracts. Golden tests must cover request/result/filter round-trips, cultures and DateTime kinds; no full-text engine is introduced.

Build a filtered header query first, using `Any`/SQL `EXISTS` for line membership instead of a one-to-many header join.
A receipt containing three matching Product lines appears once, and its `LineCount` remains three plus any other lines.
Do not recover header duplication with client-side `Distinct`, grouping, filtering or sorting after taking a page.
Apply authorization, all filters, cursor comparison, ordering and `Take(PageSize + 1)` in SQL before materialization.
For retained page IDs, obtain all-line aggregates with a bounded server-side grouped projection or equivalent correlated aggregate.
Do not fetch every line merely to build a list total; avoid per-header N+1 queries and an unbounded all-receipt materialization.
Nullable SQL aggregate results map explicitly to the valid empty aggregate value; decimal overflow remains a validation/data error.

## 4. Stable ordering and cursor validation

The sole list order is **`SaleId DESC`**. `SaleDate` is an independent filter, not a hidden primary or secondary sort key.
`SaleId` is a unique, immutable positive `bigint`; receipt numbering, dates and Product IDs are not paging anchors.
Identity allocation does not imply transaction commit order or chronological business-date order.
This is live navigation: inserts, delayed commits, Draft edits and status changes may alter membership between requests.
It does not promise a cross-request database snapshot, an export-grade frozen result set or an identical page after navigating Back.

Proposed cursor format: unpadded base64url containing bounded UTF-8 JSON with this exact schema:

```json
{"v":1,"order":"SaleIdDesc","direction":"next","anchor":123,"pageSize":50,"filterHash":"<base64url SHA-256>"}
```

`direction` is exactly `next` or `previous`; `anchor` must be a positive signed 64-bit integer.
`filterHash` binds a canonical, fixed-order UTF-8 serialization of normalized dates, all five optional ID/status filters, Search and PageSize.
Spell the canonical filter fields explicitly in the codec: FromDate, ToDate, CustomerId, WarehouseId, EmployeeId, Status, ProductId, Search, PageSize.
Use ISO calendar dates, explicit nulls and invariant numeric formatting; do not rely on dictionary enumeration or current culture.
The request's normalized filters and PageSize must match the cursor binding; each changed value requires a fresh first-page request.

Validation obligations before opening the query connection:

1. Limit the encoded cursor to 4,096 characters and decoded payload to 3,072 bytes; enforce parser depth and string bounds.
2. Reject malformed/noncanonical base64url, invalid UTF-8/JSON, duplicate or unknown fields, missing fields and wrong primitive types.
3. Accept only supported version/order/direction, valid positive anchor and bounded matching PageSize.
4. Recompute and compare the canonical filter binding; return a stable field-level validation error on mismatch.
5. Disable polymorphic/type-name deserialization and never interpolate decoded content into SQL identifiers or query text.

Base64url and the digest provide encoding and consistency checking, not secrecy, authenticity or access security.
A cursor may be inspected or reconstructed by a caller; all request filters and current authorization are independently enforced.
An anchor need not still exist or still match the filters: a key comparison remains valid after membership changes.
Do not require an anchor-row lookup, serialize provider expressions, or accept arbitrary client sort fields.

## 5. Forward and reverse navigation

Let `F` be the complete authorized, normalized filtered header query and `n` the validated PageSize.

| Request | Query before materialization | Retained rows and display order |
|---|---|---|
| First | `F.OrderByDescending(x => x.SaleId).Take(n + 1)` | Retain the first `n`; already descending |
| Next | `F.Where(x => x.SaleId < anchor).OrderByDescending(x => x.SaleId).Take(n + 1)` | Retain the first `n`; already descending |
| Previous | `F.Where(x => x.SaleId > anchor).OrderBy(x => x.SaleId).Take(n + 1)` | Retain the first `n` nearest newer rows, then reverse those retained rows |

For Previous, trim the extra ascending row **before** reversing; otherwise navigation skips the nearest boundary row.
Generate a Next cursor from the last retained/displayed `SaleId`, and a Previous cursor from the first retained/displayed `SaleId`.
Never use the extra look-ahead row as the next anchor, and never use a Product ID or mutable date as a boundary.
The extra row establishes existence in the requested direction; when the opposite direction is not already established, issue a bounded `Any`/`EXISTS` key-range probe against `F`.
Return null for a direction with no matching row; do not infer availability solely from having received an incoming cursor or from `Items.Count == n`.
All endpoints and availability checks derive from the retained result and the same normalized predicates.

Materialize the header page, retained-page aggregates and any direction probe in one short explicit transaction on one owned connection.
The proposed baseline is `SERIALIZABLE`, consistent with current receipt queries; verify lock ordering, blocking and generated plans before acceptance.
Use the owned LINQ session and its transaction; sequential queries share that connection, and cancellation/failure disposes the transaction and wrapper.
Do not open hidden secondary connections, parallelize commands on that connection, or hold the transaction while awaiting UI input.
Do not silently switch to statement-level READ COMMITTED while claiming a consistent multi-query page; a different isolation design needs recorded proof and disposition.
No transaction survives the API call, and no cursor represents an open database transaction.

An empty initial request is a successful empty result with null cursors.
An empty later request can result from live membership changes; it has no retained boundary from which to manufacture a new cursor.
The VM retains the last successful page while attempting at most one first-page reload for the current filter generation, then publishes a successful result or a recoverable message.
Never recurse through empty pages indefinitely; if that recovery also fails, keep the previous successful view and offer explicit Refresh.

## 6. LINQ to DB and index obligations

Use the pinned `linq2db` **6.3.0** from [Directory.Packages.props](../Directory.Packages.props).
Maximize typed LINQ to DB for sales application queries, aggregates, inserts and explicit update/delete allowlists.
Preserve the existing Infrastructure-only session factory and explicit UoW ownership for command participation.
Raw parameterized SQL is limited to proven requirements the pinned provider cannot safely express, such as a specific locking seam; use LINQ for conditional updates when supported. Record file, method, reason and evidence for every escape. DbUp/metadata, sequence allocation and application locks retain their explicitly scoped SQL responsibilities.
The existing native Goods Receipt list is a reference for semantics, not permission to make the new sales query native by default.
Inspect generated SQL for server-side `EXISTS`, key comparisons, ordering, bounded selection, precision and parameterization.

| Proposed index or constraint | Purpose and limit |
|---|---|
| Header primary key on `SalesReceipts.SaleId` | Unique keyset order; SQL Server can traverse a suitable B-tree in either direction |
| Unique `(SaleId, LineNo)` on lines, plus primary key `SaleLineId` | Stable line identity/order; does not prohibit duplicate Products |
| Nonunique `IX_SalesReceiptLines_ProductId_SaleId (ProductId, SaleId)` | Product membership seeks; repeated `(ProductId, SaleId)` values are valid |
| Line access by `(SaleId, LineNo)` | Detail ordering and all-line page aggregates; assess INCLUDE columns from actual plans |
| Candidate header `(CustomerId, SaleId)`, `(WarehouseId, SaleId)`, `(EmployeeId, SaleId)` | Assess selective header equality filters and descending key traversal before adding |
| Candidate date/status access | Measure combined selectivity and sorting costs; do not claim one index optimizes every filter combination |

Never add uniqueness on `(SaleId, ProductId)` or reuse Product as an entity/grid dictionary key.
Keyset paging avoids increasing OFFSET skips; it does not make arbitrary substring searches or every residual filter constant-cost.
Record representative deep-navigation and selective-filter plans/reads, including duplicate-heavy receipts and both directions, before index acceptance.
Schema, schema preflight, migration admission and runtime-principal changes belong to the main SAL plan; this document does not deploy them.

## 7. Seven-column receipt editor

The input grid's exact ordered columns are:

| Position | Column | Behavior |
|---|---|---|
| 1 | `STT` | Read-only displayed ordinal; not a persistent identity |
| 2 | `Product` | Inline catalog lookup; independent choice per line, duplicates allowed |
| 3 | `Unit` | Read-only unit of the chosen Product; no unit-conversion editor |
| 4 | `Quantity` | Strict positive decimal input, SQL `decimal(18,4)` |
| 5 | `UnitPrice` | Manually entered nonnegative selling price, SQL `decimal(19,4)` |
| 6 | `IsGift` | Editable checkbox using the same write/admission guards as other editable cells |
| 7 | `LineAmount` | Read-only server-consistent amount, SQL `decimal(28,4)` |

Use the shared exact fixed-point arithmetic in the costing design: a non-gift amount is Quantity × UnitPrice rounded to four places with `MidpointRounding.AwayFromZero`.
Reject invalid precision, unrepresentable inputs and amount/receipt-total overflow; do not silently coerce an invalid raw buffer.
For `IsGift == true`, LineAmount is exactly zero; keep the manually entered UnitPrice and restore its non-gift calculation when unticked.
A gift still consumes its complete Quantity and incurs moving-average inventory cost; it is not omitted from stock checks or cost totals.
Do not populate selling price from purchase cost, average cost or an unapproved price list.
Cost/average/margin information belongs in a clearly read-only detail or summary panel; do not add an eighth input column.
Draft costs remain null and display as not posted; Posted costs are required frozen snapshots under [DIRECT_SALES_COSTING](DIRECT_SALES_COSTING.md).

The displayed `Quantity` column binds to proposed contract/entity `Qty`; the UI caption does not rename the approved data field.
Require 1–200 lines and active Customer, Warehouse, Employee and selected Products at Save/Post; retain historical Posted display when references later become inactive.
`SaveSalesReceiptLineRequest` carries `SaleLineId`, `ProductId`, `Qty`, `UnitPrice` and `IsGift`; it accepts no client amount, cost or Unit.
`SaleLineId == 0` means a new line; a positive ID must belong to the same editable Draft and must not repeat in the submitted list.
Submitted order is authoritative; the server assigns `LineNo` 1..N without grouping by Product.
Save snapshots Unit from the chosen Product; Post compares that snapshot with the current catalog Unit.
A changed Unit produces a conflict/validation response requiring reload and Save for quantity review; Post does not silently change the Unit or convert quantities.
Posted lines retain the approved Unit snapshot.

Maintain a stable client line key/object identity for every unsaved row, including several rows with `SaleLineId == 0` and the same Product.
Persisted lines use `SaleLineId`; `LineNo` represents order, while STT reflects the displayed sequence.
Reconcile save/reload results by the command's per-line mapping/order contract, preserving the correct selection and editing target; never infer identity from Product or price.
Adding, removing or renumbering one duplicate must not edit, merge, collapse or transfer the raw buffer to another.
Any per-Product stock-demand aggregation is an internal posting calculation and must not merge stored sales lines or line-level ledger identity.

## 8. Lookup, navigation and command lifecycle

Use the pinned BootstrapSourceGrid lookup/formatted adapters and existing app-owned parser, session and accessibility patterns.
Build sales-specific adapters around those proven seams; only extract shared helpers when a concrete behavior and focused regression justify it.
Keep established English UI text; `STT` is the approved column label, not authorization for broad localization.
Customer, Warehouse, Employee and inline Product lookups preserve separate pending text and committed identities.
List lookups remain optional active-only selectors; historical header/detail selections remain displayable under current catalog rules.

On ordinary data-source rebinding, do not call `SelectValue` for an unchanged committed key or restore only `.Text`.
Preserve native text, caret, selection range and focus between each typed character, including late completion of a canceled preceding query.
Query-only text must not change the committed model, dirty state or applied list filter until a valid selection is committed.
Fence every lookup/list/detail response by operation, document and desktop-session generation; cancellation alone does not suppress a late completion.
Cancel admitted boundary queries explicitly and detach event handlers, timers, editor sessions and cancellation sources on actual disposal/document replacement.
Changing List/Detail presentation alone reuses the same control/editor/adapter and preserves list state.

Before Save/Post decisions, resolve pending header choices and commit/validate the active Product, Quantity, UnitPrice or checkbox buffer.
Do not trust a stale `IsDirty` or model value while a native editor still owns uncommitted input.
Apply Posted, busy, access-denied and `RequiresPostOutcomeReload` guards to buttons, keyboard shortcuts, checkbox toggles, accessibility setters and callbacks.
Uncertain Post locks mutation until a successful authoritative reload resolves the outcome; an unsuccessful or denied reload never unlocks it.
Never retry Post automatically after timeout, cancellation, logout or an ambiguous response.
Back admission occurs before focus-loss validation; rejected dirty/invalid/recovery navigation preserves raw input and the current document.
Admitted query-only Back cancels that query and returns to List; clean Posted inspection can return without pretending it is editable.

On filter or PageSize changes, increment generation, cancel preceding work and clear its cursor chain before issuing a new first-page request.
Publish Items, applied filters and both cursors atomically only when a current successful response arrives.
Transport/validation failures retain the prior successful page, selection and cursors; show which criteria are applied versus still pending.
Authorization/session failures follow the existing security boundary: clear or invalidate protected view state and block commands rather than retaining an active inaccessible document.
Give first/previous/next/refresh controls clear accessible names, state and keyboard behavior; no misleading total-page count.
Expose read-only Unit/LineAmount, editable cells and IsGift checked state correctly to accessibility clients, including validation messages and row identity.

## 9. Future verification and acceptance

These are future obligations, not executed tests; [TEST_POLICY](TEST_POLICY.md), [TEST_STRATEGY](TEST_STRATEGY.md) and [tests/README](../tests/README.md) govern their actual execution.

| Proposed file in an existing test project | Required evidence |
|---|---|
| `tests/MyDmsVn.Server.Application.Tests/SalesReceiptQueryContractTests.cs` | Cursor bounds/schema/version/binding, date/ID/search validation, Local DTO/error parity and current authorization |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/SalesReceiptQueryTests.cs` | Real-SQL both-direction pages, ties/date edits/delayed commits, all filter combinations, duplicate Product membership and complete totals |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/SalesReceiptQueryOwnershipTests.cs` | Single connection/transaction, cancellation/disposal, consistent aggregates/probes, blocking and generated-plan observations |
| `tests/MyDmsVn.Desktop.Session.Tests/SalesReceiptListViewModelTests.cs` | Cursor reset, stale completions, ordinary failure retention, auth invalidation and bounded empty-page recovery |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptGridTests.cs` | Seven columns, duplicate/ID-zero identity, gift-price restoration, strict decimals, commit-before-command, all locked states and accessibility |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptLookupNativeInputTests.cs` | Customer/Warehouse/Employee/Product per-key text/caret/range/focus, canceled predecessor, keyboard selection and disposal |

Include PageSize 1, exact/full/partial/empty pages, valid anchors no longer present, malformed cursors and reverse-page trim-before-reverse boundaries.
Use independent SQL assertions and inspect generated LINQ SQL; in-memory mocks cannot prove translation, locks, transaction consistency or index behavior.
Require interactive Windows/net48/x86 at native100/native125 in Light/Dark for affected per-key/grid workflows, plus the plan's actual-Login packaged and attended checks.
Record exact source/test/vendor/package hashes, actual discovered/executed rows, failures, cleanup and limitations; historical BCF passes do not certify these new controls.
For this documentation-only change, verify diff, local links, proposed/current distinctions and UTF-8 without BOM with CRLF working-tree lines; do not run SQL/GUI solely for Markdown.
