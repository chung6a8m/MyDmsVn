# SQL data model — baseline and schema10 checkpoint

**Status:** Implemented schema at `v1a` commit `c7ad32a4b0040f87417973eabd6c12757c54f7d5`, refreshed from source on 2026-10-18. The baseline descriptions are retained below; the reviewed T01 schema10 additions and bounded SQL evidence are identified separately.

The application now uses LINQ to DB and typed native Microsoft.Data.SqlClient behind Application interfaces. [RESP](plans/20261018-002-respawn-fixture-reuse.md) established the accepted test-fixture baseline; [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) completed its provider conversion and T07 correctness/package/Local smoke acceptance with verified temporary-resource cleanup. [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) is complete in its approved scope. BCI is implemented/accepted historical UI work. The completed [BCF roadmap](plans/20261021-001-business-compact-lookup-and-visual-parity.md) is COMPLETE / OWNER ACCEPTED in its [final bounded scope](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21); it preserves this supported schema, DbUp history and business semantics. [Production release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain **UNACCEPTED / UNSCHEDULED**. See [PERSISTENCE](PERSISTENCE.md) for provider/transaction ownership and [CONTRACTS](CONTRACTS.md) for public DTO behavior.

T04 source checkpoint (2026-10-22, `7eeafa4`): [reviewed evidence](evidence/sal/20261022-t04-atomic-sales-post.md) records atomic Post on one owned UoW, Shared-before-header and all sorted stock locks before writes, exact duplicate/gift allocation, immutable line cost, paired quantity/value movements and authoritative status/version materialized before commit. Receipt Post and Exclusive reconciliation use the same protocol. Native UI, frozen T08, package and Owner acceptance remain open; this checkpoint is not deployable. T01 R3 remains deferred Minor.

T05 query checkpoint (2026-10-22, `f9149a6`): [reviewed evidence](evidence/sal/20261022-t05-sales-keyset-queries.md) records independently Read-authorized List, strict normalized-filter-bound cursors, typed LINQ Product membership and all-line slim totals, retained-endpoint two-way keyset paging, and page/aggregate/probe on one short owned SERIALIZABLE transaction. Navigation is live across requests; the cursor grants no authority. Native UI and frozen T08/package/Owner acceptance remain open.

## Reviewed schema10 checkpoint and baseline inventory

The schema9 inventory below records the pre-SAL baseline; T01 development source at `81e8aab` implements the schema10 additions below ([evidence](evidence/sal/20261022-t01-schema-bootstrap.md)). The receipt/sale/reconciliation source protocol is implemented through T04; full workflow acceptance remains incomplete and this checkpoint is not deployable. [Direct Sales](DIRECT_SALES.md) and [costing](DIRECT_SALES_COSTING.md) define the schema10 requirements under [SAL](plans/20261021-002-direct-sales-and-moving-average-costing.md):

| T01 object/change | Storage and remaining workflow invariant |
|---|---|
| SalesReceipts | SaleId bigint, unique server SaleNo, date/customer/warehouse/employee, Draft/Posted, audit and rowversion |
| SalesReceiptLines | SaleLineId and unique (SaleId, LineNo); duplicate Product allowed; Qty18,4, UnitPrice19,4, Unit snapshot, IsGift and persisted LineAmount28,4 |
| Posted line cost | PostedUnitCost28,8 and CostAmount28,4 have paired nullable/nonnegative storage; T03 guards Draft save/read and rejects Posted updates; T04 freezes Post costs; T06 implements bounded Local/VM recovery; native UI/frozen lifecycle acceptance remains open |
| Sales number sequence | Separate bounded non-cycling bigint sequence; T03 assigns immutable SA + ten-digit numbers on the owned transaction, with gaps allowed and no exhaustion fallback |
| StockValuationEntries | LedgerId PK/FK and signed ValueChange28,4; bootstrap supplies a complete append-only companion for existing ledger rows; ongoing completeness requires the writer cutover |
| StockBalances.InventoryValue | Value28,4 paired atomically with Quantity; nonnegative quantity/value; zero quantity requires zero value |

T01 exact9 preflight validates the baseline definitions; `010_DirectSalesAndValuation.sql` proves historical data, bootstraps value from Posted Goods Receipts and commits atomically with its journal entry. Unknown source movement, mismatched quantity/source, partial state or overflow fails; no historical quantity-ledger rewrite or guessed zero value. Old scripts remain immutable. Sales source links remain polymorphic in StockLedger, so reconciliation and fixture reset must verify them explicitly.

The baseline table descriptions below remain useful for001–009;010 adds the objects above and nonnegative quantity/value plus zero-quantity/zero-value balance checks. LineAmount is server-persisted, not a SQL computed column. [ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md) and the [migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) distinguish accepted design from executable upgrade support.

## 1. Schema ownership and migration inventory

The authoritative definitions are the embedded [migration scripts](../src/MyDmsVn.Server.DbMigrator/Scripts) and [DatabaseMigrationRunner](../src/MyDmsVn.Server.DbMigrator/DatabaseMigrationRunner.cs). DbUp journals applied scripts in `dbo.SchemaVersions`, serializes migration through the existing application lock and applies each script in its own transaction. The journal is infrastructure metadata, not an application permission catalog.

| Script | Current contribution |
|---|---|
| `001_PersistenceFoundation.sql` | Wiring/journal foundation; the script executes `SELECT 1` and creates no business tables. |
| `002_Identity.sql` | Users, roles, memberships, role/user permission assignments and singleton superuser binding. |
| `003_Catalog.sql` | Products, Warehouses, Employees and Customers. |
| `004_Catalog_Nul_Constraints.sql` | `dbo.CatalogTextContainsNul` and required catalog text NUL constraints. |
| `005_GoodsReceipt.sql` | Receipt headers/lines, audit and rowversion/uniqueness constraints. |
| `006_GoodsReceipt_Number_Sequence.sql` | Non-cycling server receipt-number sequence. |
| `007_Inventory_Stock.sql` | StockLedger, StockBalances, indexes and append-only ledger trigger. |
| `008_Security_Administration.sql` | User/role assignment revisions, SecurityAuditEvents and ApplySecurityAdministration. |
| `009_Security_Permission_Import.sql` | ReadSecurityPermissionImportTarget and ApplySecurityPermissionImport. |
| `010_DirectSalesAndValuation.sql` | Validated historical bootstrap, Sales headers/lines/sequence, append-only valuation companions and InventoryValue checks. |

Development runtime requires complete schema10. Migration preflight admits Empty/exact10, plus separately artifact-validated exact9 only under the supported SQL-login/restricted-maintenance boundary; prefixes001–008, newer, partial and unknown-history states remain refused. The earlier unreleased security redesign does not authorize another reset or script rewrite. RESP resets only explicitly owned disposable test data under its reviewed fixture contract; L2DB does not change schema, grants, journal or user assignments. See the [migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md).

## 2. Identity and security tables

| Table | Current key and stored data | Constraints and role |
|---|---|---|
| `Users` | `UserId int IDENTITY`; Username/NormalizedUsername/DisplayName `nvarchar(100)`; optional Email `nvarchar(254)`; Source `nvarchar(16)`; PasswordHash/PasswordSalt `nvarchar(255)`; PasswordAlgorithm `nvarchar(32)`; directory/image metadata; IsActive, audit, RowVersion and AssignmentRevision. | NormalizedUsername is unique. Preserve legacy password columns; stored credential format is identified by PasswordAlgorithm. |
| `Roles` | `RoleId int IDENTITY`; RoleName/NormalizedRoleName `nvarchar(100)`; IsActive, audit, RowVersion and AssignmentRevision. | NormalizedRoleName is unique; Administrator is an ordinary role name, without implicit grants. |
| `UserRoles` | `UserRoleId bigint IDENTITY`; UserId, RoleId and creation audit. | Required user/role FKs; unique `(UserId, RoleId)`. |
| `RolePermissions` | `RolePermissionId bigint IDENTITY`; RoleId, PermissionKey `nvarchar(100)` and creation audit. | Required RoleId FK; unique `(RoleId, PermissionKey)`. A row is a role Allow grant; there is no role Granted column. |
| `UserPermissions` | `UserPermissionId bigint IDENTITY`; UserId, PermissionKey `nvarchar(100)`, Granted `bit`, creation/update audit and RowVersion. | Required UserId FK; unique `(UserId, PermissionKey)`. Granted distinguishes direct Allow/Deny; absence means inherit. |
| `SystemSecuritySettings` | `Id tinyint`, SuperuserUserId, BoundNormalizedUsername `nvarchar(100)`, CreatedAtUtc and optional UpdatedAtUtc. | PK/CHECK constrains Id to 1; unique SuperuserUserId with a required non-cascading FK to Users. Only maintenance binds/changes this identity. |
| `SecurityAuditEvents` | `AuditEventId bigint IDENTITY`; ActorUserId, TargetType `nvarchar(8)`, TargetId, Operation `nvarchar(32)`, optional BeforeState/AfterState `nvarchar(1000)`, OccurredAtUtc and CorrelationId `uniqueidentifier`. | Required actor FK; TargetType is User/Role and TargetId is positive. `(TargetType, TargetId)` is a polymorphic logical target, not a user/role FK. |

Normalized user/role names and the stored bound username use `Latin1_General_100_CI_AI`. Assignment PermissionKey columns use `Latin1_General_100_BIN2`, with canonical key validation in Application and the supported procedure paths. These collations have different purposes; do not replace them with a single global comparison convention during DAL migration.

There is no `dbo.Permissions` catalog or permission-synchronization state in the supported schema. Code owns registered Active/Deprecated definitions; SQL stores explicit assignments. Authorization validates the current account and Active key, then the bound superuser, direct-user decision, active-role grants and default deny. Unknown/deprecated keys remain denied. [SECURITY](SECURITY.md) and [ADR 0007](adr/0007-code-defined-permissions-and-single-superuser.md) define the complete behavior.

Users and Roles receive `AssignmentRevision bigint NOT NULL DEFAULT (0)` in script 008. Administration/import update relevant revisions and audit in the same transaction. Import remains additive with stale-revision/catalog-digest revalidation and the existing 1 MiB UTF-8/5,000-entry application limits. Runtime SQL calls the three named procedures; it has no generic RBAC/binding write authority or direct SecurityAuditEvents data access. See [configuration and SQL security](CONFIGURATION_AND_SQL_SECURITY.md).

## 3. Catalog tables

| Table | Key and business fields | Current constraints |
|---|---|---|
| `Products` | ProductId `int IDENTITY`; Code `nvarchar(32)`, Name `nvarchar(256)`, Unit `nvarchar(32)`, IsActive. | Unique Code; required Code/Name/Unit reject blank and NUL values. |
| `Warehouses` | WarehouseId `int IDENTITY`; Code `nvarchar(32)`, Name `nvarchar(256)`, optional Address `nvarchar(500)`, IsActive. | Unique Code; required Code/Name reject blank and NUL values. |
| `Employees` | EmployeeId `int IDENTITY`; Code `nvarchar(32)`, Name `nvarchar(256)`, optional Phone `nvarchar(64)`, optional UserId, IsActive. | Unique Code; required Code/Name reject blank and NUL values. Nullable UserId has an FK and a filtered unique index for non-null assignments. |
| `Customers` | CustomerId `int IDENTITY`; Code `nvarchar(32)`, Name `nvarchar(256)`, optional Address `nvarchar(500)`, Phone `nvarchar(64)`, TaxCode `nvarchar(32)`, IsActive. | Unique Code; required Code/Name reject blank and NUL values. No current sales document relationship. |

All four Code columns explicitly use `Latin1_General_100_CI_AI`, giving case- and accent-insensitive uniqueness. The SQL nonblank checks remove ordinary spaces, ASCII whitespace controls 9–13 and non-breaking space before testing content. NUL constraints apply to the required fields listed above; do not claim that every optional text field has the same SQL constraint. Catalog migrations seed no production rows.

Catalog audit fields are exactly `CreatedAtUtc datetime2(7) NOT NULL` with a UTC default, `CreatedByUserId int NULL`, `UpdatedAtUtc datetime2(7) NULL` and `UpdatedByUserId int NULL`. These actor columns have no physical FKs in script 003. Catalog tables have no RowVersion column. Keep operation-specific write allowlists and do not invent a catalog concurrency token or generic mandatory actor FK during L2DB.

## 4. Receipt and inventory tables

| Table/object | Key and stored data | Current SQL boundary |
|---|---|---|
| `GoodsReceipts` | ReceiptId `bigint IDENTITY`; ReceiptNo `nvarchar(32)`; ReceiptDate `date`; WarehouseId, EmployeeId; Status `varchar(16)`; optional Note `nvarchar(1000)`; creation/update/posting audit; RowVersion. | Unique nonblank/no-NUL ReceiptNo. Warehouse/Employee and actor FKs. Canonical Draft/Posted status with exact byte length and posting-audit consistency. |
| `GoodsReceiptLines` | ReceiptLineId `bigint IDENTITY`; ReceiptId, LineNo `int`, ProductId; Quantity `decimal(18,4)`, UnitCost `decimal(19,4)`; creation/update audit. | Required receipt/product/creator FKs; optional updater FK. Unique `(ReceiptId, LineNo)` and `(ReceiptId, ProductId)`; LineNo > 0, Quantity > 0, UnitCost >= 0. No line RowVersion. |
| `GoodsReceiptNumberSequence` | SQL sequence `bigint`, start/minimum 1, increment 1, maximum 9,999,999,999, NO CYCLE. | Used only by the server to allocate receipt numbers; allocation is not a table relationship. |
| `StockLedger` | LedgerId `bigint IDENTITY`; WarehouseId, ProductId; DocumentType `varchar(32)`; DocumentId/DocumentLineId `bigint`; QuantityChange `decimal(18,4)`; PostedAtUtc `datetime2(7)` and PostedByUserId. | Required warehouse/product/posting-actor FKs. Unique `(DocumentType, DocumentLineId)`; nonblank/no-NUL DocumentType and nonzero QuantityChange. Append-only trigger rejects UPDATE/DELETE. |
| `StockBalances` | Composite PK `(WarehouseId, ProductId)`; Quantity `decimal(18,4) NOT NULL DEFAULT (0)`; RowVersion. | Required warehouse/product FKs. No generic creation/update audit columns and no SQL nonnegative-quantity constraint. |

ReceiptNo uses `Latin1_General_100_CI_AI`; receipt Status and ledger DocumentType use `Latin1_General_100_BIN2`. The status check also checks DATALENGTH so only exact `Draft`/`Posted` forms are accepted. Draft requires null PostedAtUtc/PostedByUserId; Posted requires both values.

Receipt headers and lines require `CreatedByUserId`; their `UpdatedAtUtc`/`UpdatedByUserId` are nullable. Headers additionally have nullable posting audit, paired by status. Unlike catalog audit actor columns, these receipt actor fields have physical Users FKs. StockLedger records required `PostedAtUtc`/`PostedByUserId`; it does not carry the catalog's generic four-column audit pattern. StockBalances stores the materialized quantity and rowversion only.

The schema permits a nonzero ledger movement with a generic DocumentType, including negative QuantityChange. The currently implemented business workflow writes positive `GoodsReceipt` movements only. No stock issue, reversal, transfer or costing workflow is implied by the generic storage shape.

## 5. Physical foreign keys and logical links

All physical FKs below use the default non-cascading behavior in the current scripts.

| Child/reference | Target | Enforcement and meaning |
|---|---|---|
| `UserRoles.UserId` / `UserRoles.RoleId` | `Users.UserId` / `Roles.RoleId` | Required FKs; unique membership pair. |
| `RolePermissions.RoleId` / `UserPermissions.UserId` | `Roles.RoleId` / `Users.UserId` | Required FKs to the assignment owner; PermissionKey has no FK to a SQL catalog. |
| `SystemSecuritySettings.SuperuserUserId` | `Users.UserId` | Required, unique FK plus singleton settings row. |
| `Employees.UserId` | `Users.UserId` | Optional FK; filtered uniqueness gives at most one Employee per non-null UserId. |
| `GoodsReceipts.WarehouseId` / `EmployeeId` | `Warehouses` / `Employees` | Required FKs; SQL existence does not itself prove current active status. |
| Receipt header `CreatedByUserId`, `UpdatedByUserId`, `PostedByUserId` | `Users.UserId` | Creator required; updater/posting actor nullable with posting-state constraints. |
| `GoodsReceiptLines.ReceiptId` / `ProductId` | `GoodsReceipts` / `Products` | Required FKs; receipt/product uniqueness rejects duplicate product lines. |
| Receipt line `CreatedByUserId` / `UpdatedByUserId` | `Users.UserId` | Creator required, updater optional. |
| `StockLedger.WarehouseId`, `ProductId`, `PostedByUserId` | `Warehouses`, `Products`, `Users` | Required physical FKs. |
| `StockBalances.WarehouseId` / `ProductId` | `Warehouses` / `Products` | Required physical FKs. |
| `SecurityAuditEvents.ActorUserId` | `Users.UserId` | Required physical FK. |
| Sales header customer/warehouse/employee and actor IDs | Catalogs / Users | T01 required reference/creator and optional updater/poster FKs, plus posting-audit check. |
| Sales line SaleId/ProductId and actor IDs | SalesReceipts / Products / Users | T01 physical FKs; unique SaleId/LineNo allows repeated Product. |
| StockValuationEntries.LedgerId | StockLedger.LedgerId | T01 companion PK/FK; append-only UPDATE/DELETE protection. |
| `StockLedger.DocumentId` / `DocumentLineId` | Receipt header/line when DocumentType is GoodsReceipt | **Logical source identity only: no physical receipt/header-line FK.** Application posting and unique ledger source identity enforce the intended linkage. |
| `SecurityAuditEvents.TargetType` + `TargetId` | User or Role | Logical target identity; no polymorphic FK. |
| Catalog and identity/RBAC creation/update actor columns | User identity | Stored audit values without actor FKs, except the explicit receipt/ledger/audit-event FKs listed above. |

A receipt line produces one ledger movement when successfully posted; this is a transaction/business invariant plus unique source constraint, not a relationship enforced by a GoodsReceiptLines-to-StockLedger FK. Test fixtures must account for these logical links when resetting data; an FK-only deletion order is not a complete isolation contract.

## 6. Posting, numbering and concurrency

T02 implements the valued receipt writer described below ([evidence](evidence/sal/20261022-t02-valued-inventory.md)); T04 implements sale issues; remaining workflow/frozen acceptance keeps the development checkpoint nondeployable.

Draft receipts are editable and have no posted stock movement. Posting requires an active Warehouse, Employee and Products, a valid nonempty line set and the current opaque receipt version. The public contract accepts 1–200 unique product lines with quantities/costs fitting their SQL decimal types. SQL constraints enforce per-row precision/sign, foreign keys and duplicates; Application validates the aggregate line count and active-state/business rules.

GoodsReceipt Post uses one explicit UoW transaction. It takes Shared MyDmsVn.InventoryValuation.v1 immediately after BEGIN, then locks the header, Warehouse, Employee and sorted Products. All sorted composite stock keys/ranges are locked through typed LINQ before the first movement. It validates line/document/result bounds before the conditional Draft/expected-rowversion transition, appends each quantity ledger row and its individually rounded value companion, and updates Q/V together with affected-row checks. Missing balance insertion requires its held range lock. An exception rolls status/version, ledger/value and balances back together. Duplicate posting cannot create a second movement; catalog deactivation waits on held master-data locks. No client contract directly mutates StockLedger or StockBalances.

The server formats numbers as `GR` plus ten zero-padded digits, from `GR0000000001` through `GR9999999999`, using `dbo.GoodsReceiptNumberSequence`. Sequence gaps after rollback are valid; the application does not recycle allocated values or let clients choose/predict numbers. The unique ReceiptNo index remains the final uniqueness boundary.

`GoodsReceipts.RowVersion` is the aggregate edit/post concurrency token. Contracts expose opaque Base64 `version`, and UpdateDraft/Post send `expectedVersion`. Clients do not interpret token bytes or expose SQL/provider entities. StockBalances.RowVersion does not replace atomic balance updates. T04 implements Sales Post using exact T00 issue math and immutable snapshots. No Cancelled/Reversed workflow is introduced.

T03 [Sales Draft/detail](evidence/sal/20261022-t03-sales-draft.md) assigns independent IDs to new duplicate-Product rows, validates retained ownership/canonical order before mutation, removes omitted rows and preserves retained IDs/Created audits while resequencing. Save captures current active Product Unit and exact revenue; Draft costs/profit stay null and stock/value tables are unchanged. Update requires a matching opaque version and rejects Posted/stale changes. Write response construction completes before commit; Get uses one short owned SERIALIZABLE read with historical labels/persisted Unit/cost. T04 Post, T05 List and T06 Local/VM are implemented; native UI remains later work.

## 7. Existing indexes and read models

| Area | Important implemented index/ordering |
|---|---|
| Identity and catalog | Unique normalized user/role names, catalog Code indexes, assignment/membership uniqueness and filtered Employee.UserId uniqueness. |
| GoodsReceipts | Unique ReceiptNo; `(ReceiptDate, ReceiptId)`; `(WarehouseId, ReceiptDate)`; EmployeeId. |
| GoodsReceiptLines | Unique `(ReceiptId, LineNo)` and `(ReceiptId, ProductId)`; ProductId. |
| StockLedger | Unique source identity; `(WarehouseId, ProductId, PostedAtUtc, LedgerId)` including document/quantity/actor columns; ProductId. |
| StockBalances | Composite PK `(WarehouseId, ProductId)`; ProductId. |
| SecurityAuditEvents | `(TargetType, TargetId, AuditEventId DESC)`. |
| T01 SalesReceipts | Unique SaleNo; `(SaleDate,SaleId)`; `(WarehouseId,SaleDate)`; EmployeeId. |
| T01 SalesReceiptLines | Unique `(SaleId,LineNo)`; nonunique `(ProductId,SaleId)` membership index. |

Current query services materialize DTOs with parameterized SQL, `COUNT_BIG` totals and long paging offsets. Balance rows include warehouse/product codes, names, active flags, Quantity, InventoryValue and nullable checked AverageUnitCost, ordered by warehouse Code/ID then product Code/ID. StockCardRow includes ledger identity, warehouse/product projections, PostedAtUtc, DocumentType, DocumentId, DocumentNumber, QuantityChange and required ValueChange, ordered by `PostedAtUtc, LedgerId`. Its optional UTC date filters apply to posting time. DocumentNumber resolves GoodsReceipt ReceiptNo or SalesReceipt SaleNo. Missing valuation companions and invalid Q/V/average states fail closed; current stock fields are mandatory constructor inputs, not default-zero shims.

Master-data codes/names and active flags are joined from current catalog rows; the model does not store immutable historical name snapshots. StockCardRow currently has no starting/running-balance field. Provider migration must preserve current filtering, ordering, materialization and result/error semantics without inventing reporting features. See [SqlInventoryQueryService](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlInventoryQueryService.cs) and [Inventory contracts](../src/MyDmsVn.Contracts/InventoryContracts.cs).

## 8. Reconciliation and scope boundaries

T02 implements the Exclusive valuation reconciliation below. It verifies existing source history without repair; T04 separately implements Sales Post and verifies real sale-source conservation.

[SqlStockReconciliationQueryService](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlStockReconciliationQueryService.cs) owns one explicit `SERIALIZABLE` transaction, takes Exclusive MyDmsVn.InventoryValuation.v1 before table reads, then materializes ledger → valuation → balances → receipt/sale sources. It compares exact signed Q/V totals, counterpart/source identity, receipt rounded values and stored sale cost/gift coherence. Source lines are indexed once; cooperative cancellation spans indexing, grouped analysis and completion while preserving numeric stock-key output order. The gate and owned connection are disposed on cancellation.

Reconciliation reports ledger-side and balance-side key presence separately before comparing coalesced quantities. A ledger-only key or an extra zero balance is therefore a defect even if both numerical expressions would otherwise become zero. LedgerValue/BalanceValue and Issues also report missing/extra valuation counterparts, invalid source/sign/range and value differences; no automatic repair or recosting occurs.

Direct sales, no-negative-stock posting and moving-average valuation are now approved SAL work, separate from the implemented schema9/BCF baseline. Reversal, transfers and other unapproved document/costing workflows remain excluded. [TEST_POLICY](TEST_POLICY.md) determines focused/shared-change/final execution scope; this model supplies invariants without adding a separate test queue.
