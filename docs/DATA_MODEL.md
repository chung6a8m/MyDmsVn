# Current SQL data model — Identity, Catalog and Inventory

**Status:** Implemented schema at `v1a` commit `c7ad32a4b0040f87417973eabd6c12757c54f7d5`, refreshed from source on 2026-10-18. This is a model of current SQL objects, not a new schema proposal or fresh SQL acceptance result.

The application now uses LINQ to DB and typed native Microsoft.Data.SqlClient behind Application interfaces. [RESP](plans/20261018-002-respawn-fixture-reuse.md) first established the accepted test-fixture baseline using the pre-migration implementation; [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) then delivered LINQ to DB plus justified native SqlClient through T06; T07 package/manual acceptance remains pending. Both plans preserve this supported schema, DbUp history and business semantics. See [PERSISTENCE](PERSISTENCE.md) for provider/transaction ownership and [CONTRACTS](CONTRACTS.md) for public DTO behavior.

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

Current migration preflight accepts an empty or compatible database and refuses unsupported older, newer, partial or unknown-history states through its documented codes. The earlier unreleased security redesign does not authorize another reset or script rewrite. RESP resets only explicitly owned disposable test data under its reviewed fixture contract; L2DB does not change schema, grants, journal or user assignments. See the [migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md).

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
| `StockLedger.DocumentId` / `DocumentLineId` | Receipt header/line when DocumentType is GoodsReceipt | **Logical source identity only: no physical receipt/header-line FK.** Application posting and unique ledger source identity enforce the intended linkage. |
| `SecurityAuditEvents.TargetType` + `TargetId` | User or Role | Logical target identity; no polymorphic FK. |
| Catalog and identity/RBAC creation/update actor columns | User identity | Stored audit values without actor FKs, except the explicit receipt/ledger/audit-event FKs listed above. |

A receipt line produces one ledger movement when successfully posted; this is a transaction/business invariant plus unique source constraint, not a relationship enforced by a GoodsReceiptLines-to-StockLedger FK. Test fixtures must account for these logical links when resetting data; an FK-only deletion order is not a complete isolation contract.

## 6. Posting, numbering and concurrency

Draft receipts are editable and have no posted stock movement. Posting requires an active Warehouse, Employee and Products, a valid nonempty line set and the current opaque receipt version. The public contract accepts 1–200 unique product lines with quantities/costs fitting their SQL decimal types. SQL constraints enforce per-row precision/sign, foreign keys and duplicates; Application validates the aggregate line count and active-state/business rules.

GoodsReceipt Post uses one explicit UoW transaction. It locks the receipt header, then Warehouse, Employee and sorted Products, makes the conditional Draft/expected-rowversion transition, appends ledger rows and applies StockBalances in sorted `(WarehouseId, ProductId)` order. Preserve atomic increments and lock-safe creation of a missing balance row. An exception rolls status, ledger and balances back together. Duplicate posting cannot create a second movement; catalog deactivation waits on held master-data locks. No client contract directly mutates StockLedger or StockBalances.

The server formats numbers as `GR` plus ten zero-padded digits, from `GR0000000001` through `GR9999999999`, using `dbo.GoodsReceiptNumberSequence`. Sequence gaps after rollback are valid; the application does not recycle allocated values or let clients choose/predict numbers. The unique ReceiptNo index remains the final uniqueness boundary.

`GoodsReceipts.RowVersion` is the aggregate edit/post concurrency token. Contracts expose opaque Base64 `version`, and UpdateDraft/Post send `expectedVersion`. Clients do not interpret token bytes or expose SQL/provider entities. StockBalances.RowVersion does not replace atomic balance updates. No Cancelled/Reversed state or COGS algorithm exists in the current model.

## 7. Existing indexes and read models

| Area | Important implemented index/ordering |
|---|---|
| Identity and catalog | Unique normalized user/role names, catalog Code indexes, assignment/membership uniqueness and filtered Employee.UserId uniqueness. |
| GoodsReceipts | Unique ReceiptNo; `(ReceiptDate, ReceiptId)`; `(WarehouseId, ReceiptDate)`; EmployeeId. |
| GoodsReceiptLines | Unique `(ReceiptId, LineNo)` and `(ReceiptId, ProductId)`; ProductId. |
| StockLedger | Unique source identity; `(WarehouseId, ProductId, PostedAtUtc, LedgerId)` including document/quantity/actor columns; ProductId. |
| StockBalances | Composite PK `(WarehouseId, ProductId)`; ProductId. |
| SecurityAuditEvents | `(TargetType, TargetId, AuditEventId DESC)`. |

Current query services materialize DTOs with parameterized SQL, `COUNT_BIG` totals and long paging offsets. Balance rows include warehouse/product codes, names, active flags and Quantity, ordered by warehouse Code/ID then product Code/ID. StockCardRow includes ledger identity, warehouse/product projections, PostedAtUtc, DocumentType, DocumentId, DocumentNumber and QuantityChange, ordered by `PostedAtUtc, LedgerId`. Its optional UTC date filters apply to posting time. Receipt number is projected through the logical GoodsReceipt join, with a type/ID fallback when no header is found.

Master-data codes/names and active flags are joined from current catalog rows; the model does not store immutable historical name snapshots. StockCardRow currently has no starting/running-balance field. Provider migration must preserve current filtering, ordering, materialization and result/error semantics without inventing reporting features. See [SqlInventoryQueryService](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlInventoryQueryService.cs) and [Inventory contracts](../src/MyDmsVn.Contracts/InventoryContracts.cs).

## 8. Reconciliation and scope boundaries

[SqlStockReconciliationQueryService](../src/MyDmsVn.Server.Infrastructure/Inventory/SqlStockReconciliationQueryService.cs) groups ledger quantities and compares them with balances using a FULL OUTER JOIN inside one explicit `SERIALIZABLE` transaction. It first acquires shared `TABLOCK, HOLDLOCK` locks on StockLedger and then StockBalances, matching the ledger-before-balance posting order. This preserves a consistent state during concurrent Post and avoids an optimizer-selected join order reversing lock acquisition.

Reconciliation reports ledger-side and balance-side key presence separately before comparing coalesced quantities. A ledger-only key or an extra zero balance is therefore a defect even if both numerical expressions would otherwise become zero. Preserve these checks, not only the final quantity comparison.

Additional document types, negative-stock policy, reversal, costing, sales documents, warehouse transfers or schema changes need separate approved work. They are outside the two active plans. [TEST_POLICY](TEST_POLICY.md) determines focused/shared-change/final execution scope; this model supplies invariants without adding a separate test queue.
