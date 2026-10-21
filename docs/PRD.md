# Product requirements — MyDmsVn

**Current scope:** Local v1 on net48 · **Updated:** 2026-10-21 for `v1a-pro` at merged PR #40 baseline `6040d7bcfca698fc9daba8268bb94e4454e8eacb`. The initial idea is consolidated into this PRD and current architecture/contracts. RESP, L2DB, GUI-P5 and BCI are implemented/accepted in their approved scopes. [BCF](plans/20261021-001-business-compact-lookup-and-visual-parity.md) is the sole active roadmap: repair three Goods Receipt lookups first, then refine the shared shell, Products catalog and receipt against all three owner references. BCF implementation is **NOT STARTED** and new runtime obligations are **NOT RUN**; no runtime/API/schema or business-contract change is included. [Production release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain **UNACCEPTED / UNSCHEDULED**.

## 1. Product goal

Deliver a practical Windows desktop SME platform that grows by business module. Release Local Mode first, using in-process Application handlers and SQL Server. A future HTTP adapter will reuse business handlers and transport-neutral contracts.

The foundation proves modularity, authorization, transaction consistency, reproducible migrations and a responsive dense WinForms interface. Local direct SQL remains a trust limitation; it is not a trusted independent middle tier.

## 2. Users and supported workflows

| User/job | Workflow |
|---|---|
| Authorized security operator | Manage ordinary users, roles and explicit assignments; inspect the code-defined permission catalog |
| Maintenance operator | Migrate schema and bootstrap/recover the configured superuser through the separate administrator package |
| Master-data operator | List/filter/create/edit/activate/deactivate Products, Warehouses, Employees and Customers |
| Warehouse operator | Create/edit receipt Drafts and post when authorized |
| Manager/auditor | Inspect current Stock Balance, Stock Card and ledger/balance reconciliation |

One account may hold several roles. For ordinary users, an explicit user decision overrides role grants. The configured database-bound superuser is a separately protected identity; Administrator is an ordinary role name.

## 3. Local v1 scope

- Active .NET Framework 4.8 desktop and separate net48 administrator migrator; AppCore/modern product targets remain parked under ADR 0008.
- SQL Server local/intranet deployment, Local adapter, Application authorization, Login/logout/idle session handling, current configured-superuser bootstrap and deliberate security administration.
- Four catalogs; Goods Receipt header/lines with Draft/Posted lifecycle; Stock Ledger, derived Stock Balances and Stock Card.
- UTC audit fields, correlated/redacted diagnostics, DbUp migrations and explicit schema/deployment verification.
- Existing password hash/salt columns and an algorithm-aware legacy boundary; legacy account support requires approved vectors or a separately accepted release exclusion.

Product persistence now uses LINQ to DB and typed native SqlClient after L2DB-T03–T06, preserving workflows and contracts. T07 frozen-revision correctness/package/Local smoke acceptance and later GUI-P5 acceptance are complete in their recorded scopes. BCI inline UI acceptance is complete in its recorded scope; BCF requires new lookup/presentation evidence; production release prerequisites remain unaccepted and unscheduled. Respawn changes test infrastructure only.

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

Product completion requires behavioral evidence under [TEST_POLICY](TEST_POLICY.md). [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) is complete within its approved scope, retaining the explicit T05 Down NOT TESTED exception. BCI retains its accepted [historical UI evidence](BUSINESS_COMPACT_ACCEPTANCE.md); BCF requires new candidate-specific evidence under [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md), including native per-key lookup behavior before visual implementation and owner comparison against all three references. Clean install, upgrade/recovery, privilege/credential, diagnostics/audit and release sign-off remain [unaccepted, unscheduled release prerequisites](DEFERRED_WORK.md#release-prerequisites). A successful developer build is not installed-host evidence. The release schema-metadata discrepancy is already closed **narrowly** (schema9/scripts001–009 from the shipped policy); full packaged install, recovery and G1–G5 are still unaccepted.

## 7. Deferred scope

HTTP Server.Api/HttpApiClient, bearer authentication, Local/HTTP parity and product .NET 8 reactivation remain [out of scope](DEFERRED_WORK.md#out-of-scope-directions). They require a separate decision and new approved plan; none belongs to the active net48 BCF roadmap.

Sales orders/invoices, outbound stock, returns/reversal, pricing, accounting/tax/valuation, transfers, reservations, lots/serials, multiple units, multi-tenancy, offline synchronization and distributed transactions require future approved scope. Customers remain a standalone catalog in this foundation.

The future SQL/Windows support topology, legacy-account support or exclusion, signing/distribution and runtime lifecycle decisions belong to release planning. Codex should use the implemented contracts now, rather than reopening resolved P0/P5 design questions.

