# Direct Sales and Moving-Average Costing Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. Checkboxes record execution, not design approval.

**Goal:** Deliver a direct-sales Draft → Posted workflow with seven input columns, duplicate Products, gifts, exact moving-average inventory costing and guarded two-way cursor navigation.

**Architecture:** Keep the current Local → Application → explicit SQL UoW architecture and use typed LINQ to DB for new sales persistence. A shared valuation protocol coordinates Goods Receipt, Sales Receipt and reconciliation on one owned transaction; immutable ledger companions carry value without rewriting historical ledger rows. Desktop commands retain native edit-session, authorization, version and uncertain-Post boundaries.

**Tech Stack:** C#; all product and ten active test projects `net48`; Windows x86 WinForms; pinned BootstrapSourceGrid and existing vendor revisions; SQL Server/DbUp; `linq2db 6.3.0`; native SqlClient only for justified infrastructure/locking seams; xUnit and existing Respawn 4.0.0 fixtures.

**Spec:** [DIRECT_SALES](../DIRECT_SALES.md), [DIRECT_SALES_COSTING](../DIRECT_SALES_COSTING.md), [DIRECT_SALES_QUERY_AND_UI](../DIRECT_SALES_QUERY_AND_UI.md), [ADR 0011](../adr/0011-direct-sales-and-moving-average-costing.md) and [SAL acceptance](../DIRECT_SALES_ACCEPTANCE.md).

**Status:** IMPLEMENTATION IN PROGRESS — T00 foundation complete at `b5a2f3f`; [focused evidence](../evidence/sal/20261022-t00-foundation.md). T01 is complete at `81e8aab`; [bounded schema/bootstrap evidence](../evidence/sal/20261022-t01-schema-bootstrap.md) records actual SQL results and pre/post-BOM boundaries. T02 is complete at `47165ded` ([bounded evidence](../evidence/sal/20261022-t02-valued-inventory.md)); T03 is complete at `1164199` ([bounded evidence](../evidence/sal/20261022-t03-sales-draft.md)); T04 is complete at `7eeafa4` ([bounded evidence](../evidence/sal/20261022-t04-atomic-sales-post.md)); T05 is complete at `f9149a6` ([bounded evidence](../evidence/sal/20261022-t05-sales-keyset-queries.md)); T06 is complete at `5fbd6f5` ([bounded evidence](../evidence/sal/20261022-t06-local-session-viewmodels.md)); T07 source is implemented at `879ab7b` with [bounded native100 evidence](../evidence/sal/20261022-t07-native-sales-workspace.md), but full T07 remains IN PROGRESS / OPEN: native125 RESOURCE BLOCKED and R4 DEFERRED Minor. T08 feasible implementation/integration is IN PROGRESS under the 2026-10-23 continuation decision below; full T07/native125 and final acceptance remain OPEN. Frozen full GUI/package, attended review and Owner acceptance remain **NOT RUN**; focused actual packaged Login/Sales development observations are recorded separately below.
**Baseline:** `chung6a8m/MyDmsVn`, `v1a-pro@35ba2e8a3f37fb07095701eb94f4b56f7941c491`, after PR #41.
BCF-T00–T06 remain COMPLETE / OWNER ACCEPTED in their recorded scope. SAL is the next active roadmap; it does not reopen BCF or establish production readiness.

## Global Constraints

- Source at the baseline still implements schema **9**, scripts 001–009; T00 foundations and reviewed T01 schema **10** are now implemented; T02 receipt/reconciliation/projection interfaces are implemented; T03 Draft/Get interfaces are implemented; T04 Post interfaces are implemented; T05 List/keyset interfaces are implemented; T06 Local/VM interfaces are implemented; T07 native controls are implemented with bounded native100 evidence; complete matrix/frozen evidence remains open. Historical baseline facts are not rewritten.
- One warehouse, customer and responsible employee per receipt; 1–200 lines; Draft → Posted only; no payment, receivable, return, order/POS, tax, discount, multi-unit or printing workflow.
- Grid order is exactly `STT / Product / Unit / Quantity / UnitPrice / IsGift / LineAmount`; `Quantity` binds to `Qty`; costing stays outside these seven columns.
- `Qty decimal(18,4) > 0`; `UnitPrice decimal(19,4) >= 0`, including non-gift zero; amounts/value/cost `decimal(28,4)`; `PostedUnitCost decimal(28,8)`; round ties away from zero.
- Calculate with exact scaled `BigInteger` intermediates. Validate inputs, each stored result, Product-group totals and receipt totals; no decimal-intermediate overflow, clamping or silent precision loss.
- Duplicate Products are separate lines. Gifts retain UnitPrice, earn zero revenue and consume the same quantity/cost as other lines. Draft cost is null; Posted snapshots are required and immutable.
- Cost pools are `(WarehouseId, ProductId)`. Value at successful Post, independent of document date; aggregate demand before stock checks and allocate group cost by cumulative `LineNo`.
- Both receipt writers acquire transaction-owned Shared `MyDmsVn.InventoryValuation.v1` immediately after BEGIN; reconciliation acquires Exclusive before reading tables. Lock all stock keys in sorted order, including absent-key ranges, before inserting ledger rows.
- Use one connection/current transaction; explicit UoW ownership and cancellation/disposal; no ambient state, hidden connections, provider fallback or automatic Post retry.
- Preserve all dependency pins, architecture boundaries, historical evidence and removed runner/approval files. No modern-target reactivation or extra test project.
- Follow [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md): focused tests first; real SQL for storage/locks; interactive Windows for native control evidence.
- The original plan approval supplied no SQL/GUI evidence. Implementation evidence requires actual source/test/package revisions, discovered and executed rows, failures, owned-resource cleanup and limitations.
- No intermediate checkpoint is deployable before schema, both writers, reconciliation, grants and preflight enforce the same valuation protocol. Release prerequisites remain separate and UNACCEPTED / UNSCHEDULED.

## Review Focus

1. A valid maximum-scale input may overflow multiplication or a group/document sum: T00/T02/T04 must reject before mutation, with independent expected-value calculations.
2. A duplicate Product, gift or tiny residual may allocate rounded cost unevenly: T00/T04 must conserve exact group value and keep line identity and entered price.
3. A resumed migration or old running desktop may bypass new invariants: T01/T08 must validate exact schema-9 admission, rollback/resume and enforced old-client quiescence.
4. A canceled Post or lookup/list request may complete later: T04/T06/T07 must preserve one inventory effect, authoritative reload and session/generation fencing.
5. Reverse paging after deleted/moved boundaries and native edit buffers can contradict a stale model: T05/T06/T07 must verify retained-row anchors, live membership and commit/admission before commands.

---

## Execution order and checkpoints

| Task | Deliverable | Dependencies | Current status | Acceptance |
|---|---|---|---|---|
| SAL-T00 | Exact math, contracts, domain and permission definitions | None | COMPLETE — focused evidence | A01–A08, A20 |
| SAL-T01 | Atomic schema-10 bootstrap, compatibility, grants and reset metadata | T00 | COMPLETE — bounded evidence | A09–A16 |
| SAL-T02 | Valued Goods Receipt, shared locks, reconciliation and stock reads | T01 | COMPLETE — bounded evidence | A17–A19, A26–A28, A30 |
| SAL-T03 | Authorized sales Draft save/detail with stable line identity | T02; consumes T00/T01 | COMPLETE — bounded evidence | A20–A24 |
| SAL-T04 | Atomic Sales Post and cross-writer concurrency | T02, T03 | COMPLETE — bounded evidence | A25–A32 |
| SAL-T05 | LINQ summary queries and both-way keyset paging | T04; consumes T03 | COMPLETE — `f9149a6` | A33–A40 |
| SAL-T06 | Local API, session fencing and sales ViewModels | T04, T05 | COMPLETE — `5fbd6f5` | A41–A44 |
| SAL-T07 | Compact seven-column workspace, native keyboard and accessibility | T06 | IN PROGRESS / OPEN — bounded source `879ab7b`; native125 RESOURCE BLOCKED, R4 deferred | A45–A50 |
| SAL-T08 | Frozen integrated SQL/desktop/package evidence and Owner acceptance | T00–T07 | IN PROGRESS — feasible work authorized; missing native125/full T07, attended/Owner acceptance OPEN | A51–A55 |

On 2026-10-23 the Owner replied “Tiếp tục” after the explicit choice between feasible T08 continuation and waiting for native125. The execution controller interprets that reply as authorization to proceed with feasible T08 implementation, SQL, package and native100 work while full T07/native125, attended observations and final dated Owner acceptance remain OPEN. This changes the earlier serial execution order only; it waives no evidence or feature/release acceptance requirement.

SAL remains the sole active roadmap. T08 source/test additions and deferred T01 R3/T07 R4 triage must settle before the distinct frozen integrated candidate and one matched six-owner fresh/reuse pair.
Dependencies identify required inputs, and no partial writer cutover can be released.
Each task records meaningful RED → GREEN evidence, focused verification and a scoped commit after review.
A task exit verifies only that task's portion of an acceptance row. A row spanning tasks closes only after all its required portions have evidence on the accepted candidate.
Do not substitute a compile error, empty filter, missing dependency or skipped SQL case for the intended RED behavioral failure.
Implemented T00–T07 selectors and actual discovery are recorded in their task evidence; the anchor names below retain their original proposed spelling. Verify actual source names before execution. T08 anchors remain proposed.

## File map and ownership

Paths are relative to repository root; baseline paths are historical references. T00–T07 source exists as recorded in task evidence; the T08 evidence path remains proposed.
Read the adjacent current implementation before editing. Keep small feature files and avoid a general Goods Receipt/UI rewrite.

| Owner | Existing files to modify or use | New files |
|---|---|---|
| T00 contracts/domain | `src/MyDmsVn.Desktop.Application/FeatureApiClients.cs`; `src/MyDmsVn.Server.Application/Security/{PermissionKeys,PermissionRegistryComposition}.cs` | `src/MyDmsVn.Contracts/{SalesContracts,SalesCalendarDateJsonConverter}.cs`; `src/MyDmsVn.Server.Domain/Sales/{SalesReceipt,SalesReceiptLine,SalesReceiptStatuses}.cs`; `src/MyDmsVn.Server.Domain/Inventory/InventoryValuationMath.cs`; `src/MyDmsVn.Server.Application/Sales/SalesPermissionDefinitions.cs` |
| T01 schema | `deployment/sql/Provision-MyDmsVnPrincipals.sql`; `src/MyDmsVn.Server.DbMigrator/DatabaseMigrationRunner.cs`; `src/MyDmsVn.Server.Infrastructure/Operations/{SchemaCompatibility,SqlSchemaCompatibilityChecker,SqlRuntimePrincipalValidator}.cs` | `src/MyDmsVn.Server.DbMigrator/Scripts/010_DirectSalesAndValuation.sql` |
| T01/T02 fixture/mapping | `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/Fixtures/RespawnResetProfile.cs`; `src/MyDmsVn.Server.Infrastructure/Inventory/InventoryLinqToDbMapping.cs`; `src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs` | `src/MyDmsVn.Server.Infrastructure/Sales/SalesLinqToDbMapping.cs`; `src/MyDmsVn.Server.Domain/Inventory/{StockValuationEntry,StockValuationState}.cs` |
| T02 shared inventory | `src/MyDmsVn.Contracts/InventoryContracts.cs`; `src/MyDmsVn.Server.Application/Inventory/{IGoodsReceiptWriteRepository,GoodsReceiptFeatures,IStockReconciliationQueryService}.cs`; `src/MyDmsVn.Server.Infrastructure/Inventory/{SqlGoodsReceiptWriteRepository,SqlStockReconciliationQueryService,SqlInventoryQueryService}.cs` | `src/MyDmsVn.Server.Infrastructure/Inventory/InventoryValuationCoordinator.cs` |
| T03/T04 commands | Existing actor authorization, audit and UoW pipelines | `src/MyDmsVn.Server.Application/Sales/{ISalesReceiptWriteRepository,SalesReceiptFeatures}.cs`; `src/MyDmsVn.Server.Infrastructure/Sales/SqlSalesReceiptWriteRepository.cs` |
| T05 reads | Existing `ILinqToDbSessionFactory` and Infrastructure-only mapping/session ownership | `src/MyDmsVn.Server.Application/Sales/{ISalesReceiptQueryService,SalesReceiptQueries,SalesReceiptCursor}.cs`; `src/MyDmsVn.Server.Infrastructure/Sales/SqlSalesReceiptQueryService.cs` |
| T06 Local/session | `src/MyDmsVn.Desktop.Infrastructure.Local/DependencyInjection.cs`; `src/MyDmsVn.Desktop.Application/FeatureApiClients.cs` | `src/MyDmsVn.Desktop.Infrastructure.Local/LocalSalesReceiptApiClient.cs`; `src/MyDmsVn.Desktop.Application/{SalesReceiptListViewModel,SalesReceiptEditorViewModel,SalesReceiptLineViewModel}.cs` |
| T07 controls | `src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs`; current receipt lookup/grid/session/parser patterns | `src/MyDmsVn.Desktop.WinForms/{SalesReceiptControl,SalesReceiptControlFactory,SalesReceiptLinesGrid,SalesReceiptLinesGridAdapter}.cs` |
| T08 evidence | `deployment/release/Publish-LocalRelease.ps1`; `tests/MyDmsVn.Desktop.Tests/{PackagedGuiAcceptanceTests,GuiPackageProbe}.cs`; SAL acceptance | `docs/evidence/sal/20261021-t08-integrated-acceptance.md` when evidence actually exists |

Brace expressions above enumerate exact sibling filenames; they are not new directories or wildcard edit permission.
Update the existing `deployment/sql/Provision-MyDmsVnPrincipals.sql` object allowlist together with `SqlRuntimePrincipalValidator`; inspect current privileges before adding only required grants.
Add the framework `System.Numerics` reference to `src/MyDmsVn.Server.Domain/MyDmsVn.Server.Domain.csproj` for BigInteger; no package/provider change is implied.
Current `PagedResult<T>`, Goods Receipt contract naming and original migration files 001–009 remain compatibility references; sales uses its own contract.

## Verification commands and admission

Use Windows, the SDK selected by `global.json`, .NET Framework 4.8 targeting/runtime and the actual candidate checkout.
Map the task's proposed class to its existing test owner, inspect its assertions, then discover its implemented name before running.
A reusable focused command pattern is:

```powershell
$project = 'tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj'
$filter = 'FullyQualifiedName~SalesValuationMathTests' # Implemented T00 selector; inspect current source/discovery before execution.
$results = 'TestResults/sal-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore --list-tests --filter $filter
if ($LASTEXITCODE -ne 0) { throw 'Discovery failed.' }
# Reconcile actual selected Facts/Theory rows; stop if expected cases are absent.
dotnet test $project -c Release -f net48 --no-restore --filter $filter `
  --logger 'trx;LogFileName=sal.trx' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'Focused SAL verification failed.' }
```

Discovery is not execution. Record real TRX rows and outcomes; no fixed historical count is an acceptance target.
For SQL select the actual Inventory, Migrations, Foundation, Security or IntegrationTests owner and set the designated non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective `master`; never print it.
Use fresh owned allocations for migrations/principals/fault/concurrency cases; participating data fixtures retain reviewed fresh/reuse semantics.
Run SQL owners sequentially. Verify instance/allocation/token before cleanup; drain workers and connections; quarantine uncertain resources.
For GUI use the Desktop owner, interactive Windows, `-p:PlatformTarget=x86` and `-- RunConfiguration.TargetPlatform=x86`; one GUI command at a time.
Use current bounded `GuiOwnedProcess` for package/fatal probes; an STA timeout cannot prove a worker or process has stopped.
Each task's GREEN requires all selected mandatory rows to execute and pass with cleanup; failed/skipped/missing rows retain their actual outcome.
Build affected product graphs with locked restore. Do not add a runner, manifest, approval file, global lease or blanket solution test command.

## Proposed test anchors

These are new case names to implement, not claims about existing discovery. Arrange each case with the task's owned fixtures and independent observation paths.
The assertions identify minimum outcomes; implement the additional task cases below and inspect actual discovery before using a selector.

| Task | Proposed class.method | Required assertion after the stated arrangement |
|---|---|---|
| T00 | `SalesValuationMathTests.CalculateIssue_TinyResidual_ConservesGroupValue` | Q3/V0.0002, two qty1 lines: `Assert.Equal(0.0001m, result.CostAmount)`; `Assert.Equal(0m, result.LineCostAmounts[2])`; `Assert.Equal(0.00006667m, result.PostedUnitCost)` |
| T01 | `DirectSalesMigrationTests.Exact9Failure_RollsBackSchemaValuesAndJournal` | Inject before010 journal: `Assert.Equal(beforeLedger, afterLedger)`; `Assert.Equal(9, appliedScripts.Count)`; `Assert.False(hasValuationTable)`; successful rerun then `Assert.Equal(10, appliedScripts.Count)` |
| T02 | `InventoryValuationWriterTests.Receipt_PostsQuantityAndRoundedValueTogether` | Independently observe10@10 then10@20: `Assert.Equal(20m, balance.Quantity)`; `Assert.Equal(300m, balance.InventoryValue)`; `Assert.Equal(ledgerIds, valuationLedgerIds)` |
| T03 | `SalesReceiptDraftTests.DuplicateProductLines_KeepIdentityAndSubmittedOrder` | Two new duplicate rows: `Assert.NotEqual(lines[0].SaleLineId, lines[1].SaleLineId)`; `Assert.Equal(new[] { 1, 2 }, lineNumbers)`; `Assert.Empty(stockMovements)` |
| T04 | `SalesReceiptConcurrencyTests.SameSale_PostsOnceAndRequiresReload` | Two independent contenders: `Assert.Single(successes)`; `Assert.Equal("SalesReceipt.AlreadyPosted", loserError)`; `Assert.Equal(savedLineIds, movementLineIds)`; independent final Q/V equals one sale |
| T05 | `SalesReceiptQueryTests.Previous_TrimsBeforeReverse_UsesRetainedAnchors` | IDs1..11, PageSize3, Previous anchor7: `Assert.Equal(new long[] { 10, 9, 8 }, displayedIds)`; discarded look-ahead11 proves trim-before-reverse; next anchor8 and previous anchor10 come from retained rows |
| T06 | `SalesReceiptListViewModelTests.OlderGenerationCannotReplaceSuccessfulPage` | Complete newer request then older one: `Assert.Equal(newerIds, displayedIds)`; `Assert.Equal(newerCursor, nextCursor)`; old error remains unpublished |
| T07 | `SalesReceiptGridTests.GiftToggle_KeepsPriceAndSevenColumns` | Enter25, check/uncheck gift on qty3: `Assert.Equal(7, columnCount)`; `Assert.Equal(25m, enteredPrice)`; checked amount0, unchecked amount75 |
| T08 | `PackagedGuiAcceptanceTests.DirectSales_CostAndStockMatchIndependentOracle` | Actual Login and A05 sale: independently `Assert.Equal(15m, quantity)`; `Assert.Equal(225m, value)`; `Assert.Equal(75m, postedCost)`; positive process/SQL cleanup |

For T00 also pin `CalculateRevenue(0.0001m,0.5m,false) == 0.0001m` and the same gift calculation to0; these expose midpoint-away rounding independently of a larger worked example.
Assertions against lists/IDs must preserve intended ordering and cardinality; do not infer one-to-one movement conservation from a count alone.

### SAL-T00 — Exact math, contracts, domain and permission definitions

**Interfaces:** In `InventoryValuationMath.cs`, propose `decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift)`, `decimal CalculateReceiptValue(decimal qty, decimal unitCost)` and `InventoryIssueCost CalculateIssue(decimal quantityOnHand, decimal inventoryValue, IReadOnlyList<InventoryIssueLine> lines)`.
`InventoryIssueLine` contains `LineNo/Qty`; `InventoryIssueCost` contains `IssueQuantity/CostAmount/PostedUnitCost/LineCostAmounts` keyed by LineNo. These immutable value types live beside the calculator and represent one Product pool.
Propose SalesCalendarDateJsonConverter for Sales SaleDate/FromDate/ToDate properties only: invariant yyyy-MM-dd strings, strict date-only parsing, nullable optional filters and Local date-component normalization without timezone conversion. Keep global ApiJson UTC audit semantics unchanged.
Contracts use `SaleId/SaleNumber/SaleDate`, `SaleLineId/LineNo`, `Qty`, `Unit`, `UnitPrice`, `IsGift`, `LineAmount`, `PostedUnitCost` and `CostAmount`; SQL maps SaleNumber to SaleNo.

| Proposed command contract | Exact accepted fields |
|---|---|
| `SaveSalesReceiptRequest` | SaleDate, CustomerId, WarehouseId, EmployeeId, Note, ordered Lines |
| `UpdateSalesReceiptDraftRequest` | Save body plus SaleId and ExpectedVersion |
| `SaveSalesReceiptLineRequest` | SaleLineId0/new or positive/existing, ProductId, Qty, UnitPrice, IsGift |
| `PostSalesReceiptRequest` | SaleId, ExpectedVersion only |
| `PostSalesReceiptResponse` | SaleId, Posted status, PostedAtUtc, authoritative new Version; no full cost/detail payload |

Save bodies accept no client LineNo, Unit, amount, cost, number, status or actor. Header totals derive from all stored lines.
SaleNo is server-owned SA plus ten digits, separate sequence1..9,999,999,999 with NO CYCLE; Note is optional1000/noNUL.

**Test files (new):** `tests/MyDmsVn.Server.Application.Tests/{SalesValuationMathTests,SalesContractTests,SalesPermissionTests}.cs`.
**Existing test owner to extend:** `tests/MyDmsVn.Architecture.Tests/PermissionRegistryCoverageTests.cs`.

- [x] Add arithmetic tests with independent expected constants: receipts 10×10 and 10×20 produce Q20/V300; sale quantities 3 and gift2 cost 45/30, revenue75 at price25, remaining Q15/V225; later receipt5×30 leaves Q20/V375 without changing prior costs.
- [x] Add residual test Q3/V0.0002, two qty1 lines: issue0.0001, costs0.0001/0, common snapshot0.00006667; final remaining-unit sale drains Q/V exactly to zero.
- [x] Add tests for midpoint-away rounding, positive quantity, four-place input bounds, zero price, gift retained price, 200/201 lines, amount/group/document/snapshot overflow and intermediate products larger than `decimal`.
- [x] Add contract round-trip and permission tests: opaque version, date-only JSON across cultures/DateTime kinds and rejected timestamps/offsets, nullable Draft versus required Posted cost; separate Read/Write/Post; unknown/deprecated deny, active identity and direct Deny, no automatic grants.
- [x] Run implemented T00 selectors with the focused command; verify failures demonstrate missing required behavior.
- [x] Implement exact scaled-integer math and checked conversions. Compute C=V for S=Q, otherwise round4(V×S/Q); cumulative allocation uses round4(C×cumulativeQty/S), differencing adjacent totals in LineNo order.
- [x] Define contracts/domain types and register only the three code-defined Active sales keys. Do not change inventory permission grants or persistence behavior.
- [x] Run focused Application/Architecture cases and affected build; record actual RED/GREEN rows, then commit scoped T00 changes.

**Exit:** Pure arithmetic/contract portions of A01–A08 and the registry portion of A20 are verified; complete acceptance rows remain open for their later persistence/UI owners. No SQL migration or sales workflow is claimed.

### SAL-T01 — Schema 10, validated bootstrap, admission and reset profile

**Interfaces:** Add migration `010_DirectSalesAndValuation.sql` under existing `WithTransactionPerScript()`; schema/bootstrap/010 journal entry share one transaction.
T01 implements a separate migrator admission for the exact validated schema-9 manifest/artifacts under the supported SQL-login/restricted-maintenance boundary; runtime compatibility still requires complete schema10.
Tables use `SalesReceipts(SaleId,SaleNo,SaleDate,...)`, `SalesReceiptLines(SaleLineId,SaleId,LineNo,...)`, `StockValuationEntries(LedgerId PK/FK,ValueChange)` and `StockBalances.InventoryValue`.
Draft cost fields are null; Posted fields required; line identity and `(SaleId,LineNo)` unique, Product repeats allowed; preserve existing StockLedger bytes/IDs/audit. T01 proves paired nullable cost storage; cross-table Draft/Posted enforcement remains T03/T04.

**Test files (new):** `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/{DirectSalesMigrationTests,DirectSalesSchemaCompatibilityTests}.cs`.
**Existing files to extend:** `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/ReusableSqlFixtureIntegrationTests.cs` and applicable runtime-principal tests located by source inspection.

- [x] Add fresh-install, exact9-upgrade, schema10 no-op and schema9/runtime rejection tests; reject prefixes001–008, partial010, drift, unknown/future journals and insufficient migration privileges.
- [x] Add independent bootstrap assertions for every historical Posted receipt line, ledger identity, document/line key, audit, quantity and balance key in both directions; validate value equals sum of individually rounded historical purchase lines.
- [x] Add fail-closed cases for unknown/negative movements, orphan/duplicate/missing counterparts, extra zero balance keys, quantity mismatch, input/result overflow and missing required metadata; never invent zero opening values.
- [x] Add fault tests before/during/after backfill and before010 journal commit. Observe rollback to intact validated9 and successful rerun; preserve earlier-script fresh-install partial failure as recovery-required, not automatic prefix admission.
- [x] Discover and run the new Migrations selectors on fresh disposable databases; retain intended failures and independent before/after SQL observations.
- [x] Implement atomic010, append-only companion protection, required checks/FKs/indexes, new mappings, exact9 migration admission and complete10 runtime preflight.
- [x] Update runtime grant/deny allowlists, schema artifact/invariant detection and Respawn metadata/reset/sequence/trigger ownership together; restore and verify both append-only protections before fixture bodies.
- [x] Run focused migration, restricted-principal and fresh/reuse reset contamination cases; verify no orphan companion, journal drift or leaked rows; commit the reviewed T01 scope.

**Exit:** T01-owned schema/bootstrap/reset portions of A09–A16 are verified with exact recovery limits; packaged startup/upgrade and frozen comparison portions remain open. This checkpoint cannot run old inventory writers against schema10.

### SAL-T02 — Shared valuation protocol and valued Goods Receipt

**Interfaces:** New Infrastructure-internal `InventoryValuationCoordinator(ISqlExecutionContext, ILinqToDbSessionFactory)` borrows the caller's transaction.
Its implemented methods are `Task AcquirePostingGateAsync(CancellationToken)`, `Task<IReadOnlyDictionary<int,StockValuationState>> LockBalancesAsync(int warehouseId,IReadOnlyCollection<int> productIds,CancellationToken)`, `Task AppendMovementAsync(StockLedgerEntry entry,decimal valueChange,CancellationToken)` and `Task ApplyBalanceAsync(int warehouseId,int productId,decimal quantityChange,decimal valueChange,CancellationToken)`.
`StockValuationState` exposes Quantity/InventoryValue. AppendMovement captures LedgerId and inserts its one companion before returning.
The Goods Receipt write interface now exposes `AcquirePostingGateAsync`, `LockValuationBalancesAsync` and `ApplyPostedInventoryAsync`; the handler acquires Shared immediately after BEGIN. The former quantity-only write APIs and callers are removed.
Application handlers call repository interfaces only; the coordinator remains inaccessible outside Infrastructure.
Extend `StockBalanceRow` with InventoryValue and nullable AverageUnitCost: null when Q=0, checked round8(V/Q) otherwise.
Extend `StockCardRow` with ValueChange; DocumentNumber resolves SalesReceipt.SaleNo for sales and existing Goods Receipt numbering for receipts.
Invalid Q/V state or unrepresentable derived average fails closed; existing inventory PagedResult semantics remain unchanged.

**Test files (new):** `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/{InventoryValuationWriterTests,InventoryValuationReconciliationTests,InventoryValuationConcurrencyTests}.cs`.
**Existing coverage:** Goods Receipt command/stock query cases and Foundation ownership/rollback cases affected by the coordinator.

- [x] Add receipt-value tests for zero-cost stock, individually rounded lines, maximum legal values and overflow rollback; use native SQL oracles independent of calculator and mappings.
- [x] Add explicit lock-handshake tests proving Shared gate precedes document/reference/key access, all sorted point/key-range locks precede ledger insertion, and absent rows are protected.
- [x] Add reconciliation cases for quantity/value differences, missing/extra keys, missing/extra valuation counterparts, unknown movement types and overflow; verify it never silently repairs corruption.
- [x] Add post-versus-reconciliation and reversed-multi-key concurrency cases with independent sessions and finite waits; inspect blockers/order, drain all workers and preserve original failures.
- [x] Run discovered Inventory/Foundation selectors and verify intended RED observations.
- [x] Implement the common gate using resource `MyDmsVn.InventoryValuation.v1`, owner Transaction, principal public, 15-second acquisition timeout and 30-second command bound; negative return/cancel/deadlock rolls back with no retry.
- [x] Route Goods Receipt through shared valuation, lock warehouse/employee/products and all stock keys in canonical order, append quantity+value together and update Q/V atomically.
- [x] Acquire Exclusive reconciliation gate before table reads; use ledger → valuation → balances order. Extend stock read DTO/projections and independent value reconciliation under the same ownership contract.
- [x] Run focused receipt/ownership/reconciliation/race coverage and affected builds; record raw-SQL escape reasons and generated LINQ checks, then commit T02.

T02 uses typed LINQ 6.3.0 UPDLOCK/HOLDLOCK/composite-PK point/range locks; the only new native seam is same-transaction `sp_getapplock`. Exact nullable average/signed aggregates and cancellation-aware indexed reconciliation are implemented. GUI constructor adaptations have compile-only evidence.

**Exit:** T02-owned receipt/reconciliation/locking portions of A17–A19/A26–A28/A30 are verified; sale cross-writer and frozen-package portions remain open. Legacy quantity-only writer paths cannot bypass this protocol.

### SAL-T03 — Authorized sales Draft persistence and detail

**Interfaces:** `CreateSalesReceiptDraftCommand(SaveSalesReceiptRequest)` and `UpdateSalesReceiptDraftCommand(UpdateSalesReceiptDraftRequest)` return `SalesReceiptDto` through the existing authorized actor pipeline.
Implemented `ISalesReceiptWriteRepository` retains explicit UoW ownership with `Task<SalesReceipt> InsertDraftAsync(SalesReceipt,IReadOnlyList<SalesReceiptLine>,CancellationToken)`, `Task<SalesReceipt?> LockHeaderAsync(long,CancellationToken)`, `Task<SalesReceipt> ReplaceDraftAsync(SalesReceipt,IReadOnlyList<SalesReceiptLine>,CancellationToken)` and `Task AcquirePostingGateAsync(CancellationToken)`.
Implemented `Task<SalesReceiptReferenceSnapshot> GetReferenceSnapshotAsync(int customerId,int warehouseId,int employeeId,IReadOnlyCollection<int> productIds,CancellationToken)`; the immutable snapshot holds existing CatalogReferenceState values and Product Unit values.
Insert/Replace assign returned IDs to their supplied Domain lines and return the current header/version on the same transaction; materialize the complete response before commit. Do not perform a fallible post-commit fetch to construct a successful write response.
`ISalesReceiptQueryService.GetByIdAsync(long saleId,CancellationToken)` returns `Task<SalesReceiptDto?>`; implementation uses typed LINQ and materialized results.

**Test files (new):** `tests/MyDmsVn.Server.Application.Tests/SalesReceiptApplicationTests.cs`; `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/{SalesReceiptDraftTests,SalesReceiptMappingTests}.cs`.

- [x] Add create/reload/update tests for required active customer/warehouse/employee/products, duplicate Products, non-gift zero price, gifts and recomputed amounts; invalid buffers/precision/201 lines never write.
- [x] Add line-ownership tests: multiple SaleLineId0 rows remain distinct; positive IDs must belong to this Draft, cannot repeat and retain identity; server assigns LineNo1..N from submitted order.
- [x] Add Unit snapshot/review, missing/inactive references, malformed/stale versions, competing Save and attempted Posted mutation cases; no client Unit, amount or cost is trusted.
- [x] Add independent Read/Write/Post permission tests, inactive-session/direct-Deny behavior and historical Posted display; authorized Write response follows current result semantics without creating a new Read bypass.
- [x] Run implemented Application/Inventory selectors and record intended failures.
- [x] Implement explicit LINQ insert/update/delete allowlists and rowversion-guarded Draft replacement; allocate SA+ten-digit numbers from its separate bounded noncycling sequence, preserve line IDs and validate resequencing before mutation.
- [x] Derive Unit from current catalog at Save, calculate revenue using T00, keep Draft cost null and expose complete ordered detail with stable IDs.
- [x] Run focused contract/mapping/authorization/draft cases and affected builds; record results and commit T03.

T03 also exposes same-owned-transaction `LoadLinesAsync` for canonical stored-line/ownership checks. Reference snapshots include immutable labels/state/Unit. Replacement parks validated retained IDs at positive temporary LineNo 201–400 before final 1–N, preserving identity/Created audits within the same transaction; this does not expand the 200-line business limit. Native sequence allocation is the narrow SQL-specific exception. Post-grant isolation tests do not implement the Post endpoint; T05 separately implements List; T06 separately implements Local/VM; T07 has bounded native100 evidence; native125/full workflow acceptance remains open.

**Exit:** T03-owned Draft/identity/authorization portions of A20–A24 are verified; later T04 Post and T06 Local portions have their own evidence; native-UI portions remain open. Draft saves create no stock or valuation movement.

### SAL-T04 — Atomic Sales Post and concurrency

**Interfaces:** `PostSalesReceiptCommand(PostSalesReceiptRequest)` returns `PostSalesReceiptResponse`; request contains SaleId and opaque ExpectedVersion.
The provider-free Sales repository implements these methods:

- `Task<SalesReceiptPostSnapshot> LockPostSnapshotAsync(SalesReceipt,CancellationToken)`: locked ordered lines and current reference/Unit state.
- `Task<IReadOnlyDictionary<int,StockValuationState>> LockValuationBalancesAsync(int warehouseId,IReadOnlyCollection<int> productIds,CancellationToken)`: sorted locked Q/V snapshots.
- `Task ApplyPostedInventoryAsync(SalesReceipt header,IReadOnlyList<SalesReceiptLine> lines,IReadOnlyDictionary<int,InventoryIssueCost> costsByProductId,DateTime postedAtUtc,int postedByUserId,CancellationToken)`: persist line cost snapshots, append each quantity/value pair and apply balances using the internal coordinator.
- `Task<SalesReceipt?> TryMarkPostedAsync(long saleId,byte[] expectedVersion,DateTime postedAtUtc,int postedByUserId,CancellationToken)`: null on conditional conflict; on success reread/materialize the new header/version on the current transaction.

The Application handler consumes only its repository interface and T00 pure math; Infrastructure alone consumes T02's coordinator.
Materialize PostSalesReceiptResponse including the new Version before commit, then return after successful commit. Later authorized Get reloads full detail.

**Test files (new):** `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/{SalesReceiptPostTests,SalesReceiptConcurrencyTests}.cs`; extend `SalesReceiptApplicationTests.cs`.

- [x] Add successful/insufficient-stock tests aggregating all duplicate and gift demand per Product; each persisted line has one negative ledger movement and one nonpositive valuation companion (including a required zero-value entry).
- [x] Add equal-stock drain, zero-valued stock, tiny residual, cost/amount/stock overflow and Unit mismatch tests; finalized costs remain unchanged after later receipts and backdated documents.
- [x] Add same-document double Post and stale Save/Post races; one accepted Post has exactly one effect; a now-Posted header returns SalesReceipt.AlreadyPosted, a stale still-Draft version returns SalesReceipt.ConcurrencyConflict; both require reload without implicit retry.
- [x] Add competing different-sales, sale-versus-receipt, reversed-key and absent-key scenarios with deterministic session handshakes; observe legal serial outcomes, no negative stock/lost update and bounded deadlock handling.
- [x] Inject failures after status/line-cost/ledger/valuation/balance changes and before commit; verify every participating table, version and audit outcome independently.
- [x] Run discovered Inventory/Application selectors; RED must expose the intended invariant or missing feature, with cleanup intact.
- [x] Implement gate → header/version → warehouse/employee/customer/sorted Products → all sorted stock keys → validate/compute → persist snapshots/status/ledger/value/balances → commit.
- [x] Reject any stale Unit snapshot pending reload and reviewed Save; value at actual successful Post, freeze cost, preserve gift price and return stable conflict/validation outcomes.
- [x] Run focused rollback/race/fault coverage and affected ownership regressions, review real lock/SQL evidence, then commit T04.

T04 final candidate2 repairs a test-only unbounded session-publication wait and the existing receipt-read timing/cleanup fixture; product code remains unchanged in that repair. The candidate1 Inventory failure, missing pre-rebuild DLL hash and old reader-drain limitation remain explicit in evidence. Source-level writer/reconciliation coherence does not establish deployability or Local uncertain-outcome recovery.

**Exit:** T04-owned SQL Post/rollback/concurrency portions of A25–A32 and deferred Post portions of A01–A08/A23–A24 are verified; both writers/reconciliation form one coherent protocol. T06 separately verifies bounded Local uncertainty/recovery; native UI/runtime packaging remain incomplete.

### SAL-T05 — LINQ filters, slim summaries and two-way keyset pages

**Interfaces:** `ISalesReceiptQueryService.ListAsync(SalesReceiptListRequest,CancellationToken)` returns `Task<SalesReceiptListPage>`.
List request carries optional calendar FromDate/ToDate, CustomerId/WarehouseId/EmployeeId/Status/ProductId/Search, PageSize and Cursor.
Page contains only Items/NextCursor/PreviousCursor; each slim row includes whole-bill LineCount/TotalAmount. Existing PagedResult is unchanged.

**Test files (new):** `tests/MyDmsVn.Server.Application.Tests/SalesReceiptQueryContractTests.cs`; `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/{SalesReceiptQueryTests,SalesReceiptQueryOwnershipTests}.cs`.

- [x] Add normalization/validation tests: inclusive unspecified calendar dates, reversed dates, invalid IDs/status, Search256/NUL/literal wildcards and PageSize1/50/200/invalid.
- [x] Add fixed-schema cursor tests for canonical base64url/UTF-8/JSON, version/order/direction/positive anchor, 4096 encoded/3072 decoded limits, duplicate/unknown fields and every filter/PageSize fingerprint mismatch; reject before SQL.
- [x] Add real-SQL First/Next/Previous cases with size1, exact/full/partial/empty pages, deleted/nonmatching anchors, delayed commits, date/status/filter membership edits and live inserts.
- [x] Add duplicate-heavy Product filters proving EXISTS membership, no repeated headers and whole-bill totals; no full-line list payload, offset/count requirement or client-side recovery grouping.
- [x] Add single-connection SERIALIZABLE page/aggregate/probe consistency, cancellation/disposal and bounded blocking tests; inspect actual generated SQL/plans/reads for representative deep/selective pages.
- [x] Run implemented Application/Inventory selectors; preserve intended failures.
- [x] Implement Next `SaleId < anchor ORDER BY SaleId DESC Take(n+1)`; Previous `SaleId > anchor ORDER BY SaleId ASC Take(n+1)`, trim then reverse; anchor only retained displayed endpoints.
- [x] Compute opposite-direction availability by bounded same-filter probe when needed; no open transaction survives the request. Validate cursor hash as consistency only, never authority/security.
- [x] Run focused query/ownership tests and record index/translation evidence; commit T05.

**Exit:** T05-owned query/translation/ownership portions of A33–A40 are verified; frozen-candidate checks remain open. Live-membership limits and exact filter binding are documented.

### SAL-T06 — Local API and session-safe sales ViewModels

**Interfaces:** Add `ISalesReceiptApiClient` to existing FeatureApiClients with `ListAsync/GetAsync/CreateDraftAsync/UpdateDraftAsync/PostAsync` signatures specified in [query/UI §2](../DIRECT_SALES_QUERY_AND_UI.md#2-public-list-and-detail-contracts).
Each returns `Task<ApiResponse<T>>` and accepts CancellationToken; Local dispatch captures current actor/generation.
SalesReceiptListViewModel owns applied filters/Items/cursors; editor/line VMs retain stable per-row identity, command locks and read-only cost/profit summary.

**Test files (new):** `tests/MyDmsVn.Desktop.Session.Tests/{LocalSalesReceiptApiClientTests,SalesReceiptListViewModelTests,SalesReceiptEditorViewModelTests}.cs`.

- [x] Add Local result/validation/conflict parity and permission/session-expiry tests; already admitted commands retain their actor, late responses never populate a replacement session.
- [x] Add First/Refresh/filter/PageSize cursor reset and latest-generation tests; ordinary failures retain the prior applied view, selection/cursors, while authorization loss invalidates protected state.
- [x] Add empty-later-page recovery: at most one first-page reload per generation, no infinite recursion or manufactured anchors; pending criteria remain distinguishable from applied criteria.
- [x] Add duplicate unsaved/persisted row identity, gift toggle/revenue, read-only cost summaries, Posted/busy/denied guards and uncertain Post tests; failed reload leaves mutation locked and never retries Post.
- [x] Run discovered Session selectors and verify the intended behavioral failures.
- [x] Implement Local mapping/DI and VM operations around current session ownership, opaque versions, cancellation and generation fencing; publish list Items/criteria/cursors atomically after current success.
- [x] Implement command admission that asks the control to commit pending header/line buffers before deciding Save/Post; no stale IsDirty shortcut.
- [x] Run focused Session cases and affected existing Local/session regressions; commit T06.

**Exit:** T06-owned Local/VM portions of A41–A44 are verified with no cross-user publication or unreviewed Post retry; native-control command-admission proof remains open for T07.

### SAL-T07 — Seven-column compact UI and native interaction

**Interfaces:** SalesReceiptControl/Factory compose the T06 VMs; SalesReceiptLinesGrid/Adapter own Product/numeric/checkbox sessions with stable client row keys.
Retain current pinned lookup/formatted adapters, parser, accessibility and guarded List/Detail presentation; do not replace the grid or clone pending text into committed IDs.

**Implemented test files:** `tests/MyDmsVn.Desktop.Tests/{SalesReceiptGridTests,SalesReceiptLookupNativeInputTests,SalesReceiptControlTests,SalesReceiptControlAdmissionTests,SalesReceiptAccessibilityTests,SalesReceiptNativeEnvironmentTests}.cs`; List access-admission regressions also live in Desktop.Session.Tests.

- [x] Add seven-column order/read-only tests, repeated Product/ID0 row edits/removal, retained gift UnitPrice, strict decimals and stock/cost/profit summary outside the grid.
- [x] Add commit-before-Save/Post, dirty/invalid Back admission, canceled boundary queries and all Posted/busy/denied/RequiresPostOutcomeReload interaction routes, including checkbox/accessibility setters.
- [x] Add native per-character text/caret/range/focus tests for Customer, Warehouse, Employee and inline Product, plus optional list lookups; include canceled predecessor, middle insertion/replacement, slow responses and unchanged-key rebinding.
- [x] Add actual F4/Up/Down/Enter/Escape/Tab/Shift+Tab, paste/mouse/deactivation, historical reference display, accessibility names/checked state, theme changes and disposal/session replacement.
- [x] Run focused Desktop selectors on interactive net48/x86 and retain intended behavioral failures; whole Text/UIA SetValue is not per-key proof.
- [x] Implement compact List/Detail shell integration and the exact seven-column adapter with current ownership/admission patterns.
- [ ] Run the affected native100/native125 × Light/Dark matrix; measure actual monitor/window DPI and bounds at 1680×900, 1200×760 and 944×600 logical targets; capture usable focus/errors/popups and essential reachability.
- [x] Review bounded native100 same-data captures and keyboard observations; commit the source checkpoint `879ab7b` with the candidate3-tested/candidate4-format-only boundary recorded.
- [ ] Complete every mandatory native125 workflow and six size/theme contexts to close the full T07 exit. The 2026-10-23 continuation decision permits feasible T08 work while this remains OPEN; no inherited BCF exception or native125 evidence waiver applies.
- [ ] Keep R4 stale numeric validation footer DEFERRED Minor for final whole-branch triage/disposition before the T08 frozen candidate; A50 validation accuracy remains OPEN. This is not an earlier T08-start or Important-repair gate.

**Current checkpoint:** [Reviewed bounded evidence](../evidence/sal/20261022-t07-native-sales-workspace.md) proves implemented control/hosted/native100 portions only: 110 affected Sales, 55 shared, 174 complete Session, 13 Architecture; 67 measured native rows plus 43 hosted rows. The six native100 layout rows do not mean every keyboard workflow ran at every size. Native125 remains NOT RUN / RESOURCE BLOCKED on the sole 1920×1080 native 96DPI display/work area 1920×1032; strict wide125 requires 2100×1125 physical client plus chrome. R4 stale numeric footer stays DEFERRED Minor; A44–A50 full rows remain OPEN.

**Exit remains OPEN:** Full T07 requires its missing control/theme/scale portions; frozen-package/attended obligations stay with T08, whose feasible work is IN PROGRESS under the 2026-10-23 continuation decision. Screenshots alone cannot close interaction rows.

### SAL-T08 — Frozen integrated candidate and acceptance

**Files:** Update SAL acceptance and create the evidence path from the file map only when actual evidence is available.
**Interfaces:** The reviewed release script produces the existing Windows x86 desktop and separate admin migrator; use its inspected parameters and record the exact invocation rather than inventing flags.
T08 consumes the frozen T00–T07 candidate, implemented case inventory, identical package bytes and unchanged dependency pins.

**Development checkpoint:** [2026-10-23 packaged tests](../evidence/sal/20261023-t08-packaged-development-checkpoint.md) implement 19 new rows on the genuine clean BASE development package. The original complete19 run is 13PASS/6FAIL; four stale rows pass after a bounded driver repair and stronger version/line oracle, and two other UI failures pass in an isolated diagnostic rerun while their causes remain unresolved. Seven affected existing callers pass. This test/docs candidate awaits task review and the final source review/fix wave; no frozen integration or acceptance checkbox below is closed.

- [ ] Freeze and record source/test/vendor/package revisions and hashes, migration script/artifact schema, Windows/SQL/runtime environment, permissions and native DPI/theme matrix.
- [ ] Reconcile each SAL-A row to implemented Facts/Theory rows and independent or attended observations; resolve missing mandatory cases before declaring acceptance.
- [ ] Run complete Application and Architecture projects once; run the six SQL owners sequentially with explicit fresh using [tests/README complete SQL milestone](../../tests/README.md#complete-sql-milestone).
- [ ] Run the same six SQL owners sequentially with explicit reuse to complete one matched pair, because010 changes the full reset schema; preserve dedicated fresh exceptions and actual rows/resource phase outcomes. Do not add a third redundant complete SQL pass.
- [ ] Run complete Desktop.Session once and complete Desktop GUI at native100/native125; ensure required sales cases execute in both Light/Dark, and retain each context's outcomes independently.
- [ ] Build the frozen package with `deployment/release/Publish-LocalRelease.ps1`; verify actual Login, least-privilege runtime, admin-only migration, quiesced schema9 upgrade and schema10 startup refusal/admission behavior.
- [ ] Execute bounded packaged direct-sale create/save/reload/post, duplicate/gift, permission, stale/uncertain-Post and list workflows with independent quantity/value/ledger assertions and positive process/SQL cleanup.
- [ ] Obtain attended all-lookup/native-edit and cost/profit review at both native scales/themes; keep human observations separate from hosted/package automation and record actual missing resources.
- [ ] Preserve all failed attempts and focused repairs; a repair rerun does not rename an earlier full run PASS. Review every deviation and obtain dated Owner disposition of the exact frozen scope.
- [ ] Update acceptance/plan statuses only from that evidence, commit sanitized summaries, and hand off the accepted feature scope with production prerequisites still separate.

**Exit:** All A01–A55 required portions are reconciled on the frozen candidate and evidenced or explicitly dispositioned within the approved bounds; unresolved mandatory accounting/atomicity/ownership failures block SAL completion. Feature acceptance is not production release approval.

## Completion record and handoff

For each task attach revision, changed behavior, exact command/project/TFM/filter, discovered/executed Facts/Theory rows, pass/fail/skip/not-run, environment and cleanup.
Keep raw TRX, SQL identities, connection data and proprietary screenshots local; publish sanitized reviewable summaries and resource limitations.
The original baseline contains no Direct Sales runtime. T00 is complete at `b5a2f3f`; T01 is reviewed/complete at `81e8aab` ([evidence](../evidence/sal/20261022-t01-schema-bootstrap.md)). T02 is complete at `47165ded` ([evidence](../evidence/sal/20261022-t02-valued-inventory.md)); T03 is complete at `1164199` ([evidence](../evidence/sal/20261022-t03-sales-draft.md)); T04 is complete at `7eeafa4` ([evidence](../evidence/sal/20261022-t04-atomic-sales-post.md)); T05 is complete at `f9149a6` ([bounded evidence](../evidence/sal/20261022-t05-sales-keyset-queries.md)); T06 is complete at `5fbd6f5` ([bounded evidence](../evidence/sal/20261022-t06-local-session-viewmodels.md)); T07 source `879ab7b` has bounded native100 evidence; full T07 remains IN PROGRESS / OPEN with native125 RESOURCE BLOCKED and R4 deferred Minor. T08 feasible work is IN PROGRESS under the 2026-10-23 continuation decision; package/attended/Owner and multi-task SAL acceptance rows remain open. T01 is not deployable; R3 generic bootstrap diagnostics remains a deferred Minor.
The owner's approval selects this roadmap and costing design; it is not a claim that the application, SQL migration or future package has passed.
