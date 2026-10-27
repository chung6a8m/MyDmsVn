# Product requirements — MyDmsVn

**Scope reference:** Local v1 on net48, inspected 2026-10-23 at historical commit `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` (merged PR #43 and migrated assessment). Current scheduling follows the [execution index](plans/README.md); the [2026-10-27 main archive](archive/20261027-main-roadmap-reset/README.md) preserves the previous queues. [UI-NEXT](ui-next/README.md) retains later UI design/implementation evidence; the A0 routing update adds no business or production-release scope.

The [archive boundary](archive/20261023-pre-ui-next/README.md) retires previous plans and excludes unfinished acceptance without waiving it. Sales and schema10 are implemented in source. [T08 evidence](evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific runnable automation PASS; native125, attended observations and dated Owner acceptance remain OPEN. BCF/BCI/GUI-P5/L2DB/RESP retain their recorded signed scopes. [Production release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.

## 1. Product goal

Deliver a practical Windows desktop SME platform that grows by business module. Release Local Mode first, using in-process Application handlers and SQL Server. A future HTTP adapter will reuse business handlers and transport-neutral contracts.

The foundation proves modularity, authorization, transaction consistency, reproducible migrations and a responsive dense WinForms interface. Local direct SQL remains a trust limitation; it is not a trusted independent middle tier.

## 2. Users and supported workflows

| User/job | Workflow |
|---|---|
| Authorized security operator | Manage ordinary users, roles and explicit assignments; inspect the code-defined permission catalog |
| Maintenance operator | Migrate schema and bootstrap/recover the configured superuser through the separate administrator package |
| Master-data operator | List/filter/create/edit/activate/deactivate Products, Warehouses, Employees and Customers |
| Warehouse operator | Create/edit Goods Receipt Drafts and post when authorized |
| Sales operator | Create/edit direct Sales Drafts and post when independently authorized |
| Manager/auditor | Inspect current Stock Balance, Stock Card and ledger/balance reconciliation |

One account may hold several roles. For ordinary users, an explicit user decision overrides role grants. The configured database-bound superuser is a separately protected identity; Administrator is an ordinary role name.

## 3. Local v1 scope

- Active .NET Framework 4.8 desktop and separate net48 administrator migrator; AppCore/modern product targets remain parked under ADR 0008.
- SQL Server local/intranet deployment, Local adapter, Application authorization, Login/logout/idle session handling, current configured-superuser bootstrap and deliberate security administration.
- Four catalogs; Goods Receipt and direct Sales Draft/Posted lifecycles; quantity/value ledgers, derived Stock Balances and Stock Card.
- UTC audit fields, correlated/redacted diagnostics, DbUp migrations and explicit schema/deployment verification.
- Existing password hash/salt columns and an algorithm-aware legacy boundary; legacy account support requires approved vectors or a separately accepted release exclusion.

Product persistence now uses LINQ to DB and typed native SqlClient after L2DB-T03–T06, preserving workflows and contracts. T07 frozen-revision correctness/package/Local smoke acceptance and later GUI-P5 acceptance are complete in their recorded scopes. BCI inline UI acceptance is complete in its recorded scope; BCF has its separate accepted lookup/presentation evidence and explicit exceptions; production release prerequisites remain unaccepted and unscheduled. Respawn changes test infrastructure only.

## 3.1 Implemented source scope — Direct Sales

The Owner selected [direct sales receipts](DIRECT_SALES.md) with Draft → Posted, customer/warehouse/employee, manual selling prices, duplicate Product rows and gifts. The exact grid is STT, Product, Unit, Quantity, UnitPrice, Gift checkbox and Amount. Gift revenue is zero while quantity and cost are fully consumed. Preserve entered price on gift toggle.

Cost is [moving weighted average](DIRECT_SALES_COSTING.md) per warehouse/product at actual Post, with immutable posted line costs, no negative stock and atomic document/quantity/value effects. Valued receipt posting, reconciliation and the validated schema9 → schema10 bootstrap share that contract. Draft cost remains unknown/null. Read-only posted cost/profit stays outside the seven-column grid.

The [list contract](DIRECT_SALES_QUERY_AND_UI.md) uses date/customer/warehouse/employee/status/Product/search filters, LINQ queries and SaleId DESC keyset navigation. Product membership returns each sale once; totals cover the complete document. Development source requires schema10 while historical schema9 packages retain separate provenance. [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) records the archived incomplete native125/attended/Owner boundary; implementation is not full acceptance or production readiness.

## 3.2 Active UI infrastructure scope

UI-NEXT improves tab-owned feedback, field/cell validation, close/busy behavior and semantic theme surfaces under the [design and control policy](ui-next/20261023-design-and-control-policy.md). Shared UI owns reusable visual/native behavior; MyDmsVn owns data/session identity, dirty-close decisions, command admission and recovery. Preserve pending buffers, keyboard/focus/accessibility, security-denial cleanup and the existing business workflows. Use source and the historical UI-NEXT candidate records to distinguish implemented controls from original proposals; this specification does not copy task status.

## 4. Acceptance behavior

**Catalogs:** enforce stable unique codes and server validation. New-document lookups show eligible active records; historical references remain readable. Filtering is debounced, cancellable and resistant to stale responses; explicit Refresh is immediate. Same-process catalog-change messages refresh already-open lookups without silently losing a historical selection.

**Draft receipt:** one warehouse, an employee and at least one valid product line; positive quantity and nonnegative cost. Duplicate products are rejected by the implemented unique constraint. Save/edit validates the opaque expected-version token; stale edits fail without replacing accepted state.

**Posting:** authorized Post changes receipt status, writes each ledger movement and updates balances within one explicit transaction. Sequential/concurrent retries cannot duplicate posting. A failure rolls back all changes. Posted receipts are immutable; no reversal workflow is currently included.

**Stock inquiry:** balance/card queries apply authorization, filters, deterministic paging/order, UTC semantics and historical entity visibility. Users do not edit balances or ledger directly. Reconciliation compares them against one consistent SQL state.

**Security:** authenticated active account and Active registered permission are required. Unknown/deprecated keys deny, including for the superuser. Current binding protects superuser bypass; other users receive access only through explicit assignments. Unauthorized operations leave business data unchanged.

**Session:** real Login establishes the process-local session; logout/expiry requires reauthentication and prevents stale UI results from becoming another user's state. Actor snapshots preserve already admitted operation attribution. No P7 bearer session is emulated in Local Mode.

## 5. Business invariants

Use the checked-in migrations and [Data model](DATA_MODEL.md) for exact types, constraints and indexes. Quantities use decimal(18,4), line unit costs decimal(19,4), and server UTC audit values. Receipt numbers are generated by the non-cycling SQL sequence; gaps are allowed, reuse is not.

Posting must be all-or-nothing and idempotent within its SQL boundary. Concurrency tests use independent sessions. Ledger is append-only and balances are derived. Deactivate referenced catalog records instead of deleting history.

## 6. Quality and release requirements

Keep DB work off the UI thread, with bounded timeouts/cancellation, loading/empty/error states and field validation. Headless GUI failures cannot leave modal dialogs blocking a process. Do not log passwords, hashes, salts, connection strings, raw SQL parameters or authentication material.

Product completion requires behavioral evidence under [TEST_POLICY](TEST_POLICY.md). [GUI-P5](archive/20261023-pre-ui-next/plans/20261019-001-full-winforms-gui-p5-acceptance.md) is complete within its approved scope, retaining the explicit T05 Down NOT TESTED exception. BCI retains its accepted [historical UI evidence](BUSINESS_COMPACT_ACCEPTANCE.md); BCF retains candidate-specific accepted evidence under [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md), including native per-key lookup behavior before visual implementation and owner comparison against all three references. Clean install, upgrade/recovery, privilege/credential, diagnostics/audit and release sign-off remain [unaccepted, unscheduled release prerequisites](DEFERRED_WORK.md#release-prerequisites). A successful developer build is not installed-host evidence. The release schema-metadata discrepancy is already closed **narrowly** (schema9/scripts001–009 from the shipped policy); full packaged install, recovery and G1–G5 are still unaccepted.

## 7. Deferred scope

HTTP Server.Api/HttpApiClient, bearer authentication, Local/HTTP parity and product .NET 8 reactivation remain [out of scope](DEFERRED_WORK.md#out-of-scope-directions). They require a separate decision and new approved plan; none belongs to UI-NEXT.

Sales orders, POS, fiscal/e-invoicing, payments, debt, returns/reversal, automatic pricing/discounts, accounting/tax, FIFO/periodic/revaluation, transfers, reservations, lots/serials, multiple units, multi-currency, multi-tenancy, offline synchronization and distributed transactions remain excluded. Direct sales, their outbound stock and moving-average valuation are already implemented source contracts; Customers participate in that workflow. Their incomplete historical acceptance remains archived and excluded from UI-NEXT.

The future SQL/Windows support topology, legacy-account support or exclusion, signing/distribution and runtime lifecycle decisions belong to release planning. Codex should use the implemented contracts now, rather than reopening resolved P0/P5 design questions.

