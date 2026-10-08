# Product Requirements Document — MyDmsVn

Status: **Approved direction / planning baseline**, 2026-10-08. Source: `project-idea.md` and follow-up decisions. Details labeled "proposed" may be refined through ADRs before coding.

## 1. Product goal

Deliver a practical, reliable Windows desktop SME platform that can grow by business module. Release **v1 Local Mode** first for rapid delivery; enable **v2 Remote HTTP API** later without rebuilding desktop views, workflows or core application handlers.

The architectural MVP proves: modularity, security boundaries, transactional consistency, shared contracts, reproducible migrations and a usable dense WinForms interface.

## 2. Users and jobs to be done

- **Administrator** maintains users, roles and permissions.
- **Master-data operator** maintains products, warehouses, employees and customers.
- **Warehouse operator** enters goods receipt drafts and submits postings.
- **Manager / auditor** reads current stock and stock-card transaction history.

A person may carry more than one role; direct user permission exceptions override role grants.

## 3. In scope by release

### v1 (P0–P6)
- Dual runtime desktop hosts: .NET Framework 4.8 and .NET 8 Windows.
- SQL Server local/intranet deployment; in-process Application execution via LocalApiClient.
- Login, authorization, safe legacy-password transition.
- P5 catalog: Products, Warehouses, Employees, Customers.
- Goods Receipt with header, lines, Draft/Posted lifecycle.
- Ledger-backed stock balance and stock card, concurrency-safe posting.
- Audit fields, log correlation, DbUp schema migration, deployment verification.

### v2 (P7)
- ASP.NET Core 8 Web API with secured endpoints.
- HttpApiClient corresponding to supported Desktop API interfaces.
- Matching request/response semantics, errors and permission checks.
- Contract and integration tests across Local and HTTP modes.

## 4. Explicitly out of scope for P5
Sales orders, sales invoices, stock issues, purchase ordering, payment, receivables/payables, accounting, tax calculation, cost valuation/COGS, multiple UoM conversion, reservations, returns, serial/lot tracking, multi-tenant data isolation, offline synchronization, distributed transactions, and warehouse transfers. They need future approved plans.

### Naming
"Sales & Inventory Foundation" describes a foundation for later sales workflows. **P5 does not implement outbound sales.**

## 5. End-user scenarios / acceptance

**Catalog CRUD:** operator can list/filter/create/edit/activate/deactivate each of the four catalogs (with stable unique codes, validation, permission checks); lookups exclude inactive items for new documents, without breaking historical references.

**Draft receipt:** operator creates a warehouse-specific receipt, sets employee and at least one product line, quantity > 0, nonnegative unit cost; can edit Draft. Server, not UI, validates constraints.

**Posting:** authorized user posts a Draft once. Posting atomically transitions document status, writes a ledger entry for each line and increments balances. Retries or simultaneous attempts create no duplicate postings. Posted documents are read-only in P5.

**Stock inquiry:** current balance per warehouse/product and chronological stock card are queryable using Dapper DTOs. Queries respect permissions and produce deterministic sorting and paging.

**Security:** unknown permission denies; explicit user Granted=false overrides all role grants. Unauthorized commands do not mutate data.

**Consistency:** if any posting step fails, receipt stays Draft and ledger/balance are unchanged. Concurrent receipts to same warehouse/product preserve total quantity.

## 6. Core business rules

- Single warehouse per receipt; one product and quantity per detail line; different lines may refer to the same product only if business rules explicitly allow and aggregation is transactionally correct (default: reject duplicate product per receipt).
- Code uniqueness case/collation behavior must be explicit in SQL and mirrored by validation; DB unique constraints are final arbiter.
- Receipt numbers cannot be reused; initial strategy may use server-generated unique numbers (not UI-generated sequential numbers).
- Quantities use `decimal(18,4)`; monetary unit costs `decimal(19,4)` (proposed baseline).
- Creation/update audit fields are UTC; client UI converts for display.
- Posted documents are immutable; reversal/cancellation will be a separate approved workflow.
- Balances are derived materializations, never edited directly by users; ledger is an append-only audit source.

## 7. Non-functional requirements / quality gates

- Runs on supported Windows environments with .NET 4.8 and .NET 8 desktop hosts.
- No modal WinForms exception dialogs block CI/headless runs.
- SQL Server integration tests cover transaction and concurrency; a clean database migrates reproducibly.
- Keep UI responsive: no blocking DB work on UI thread; cancellation, loading/error states.
- No hardcoded credentials or connection strings; logging avoids passwords, hashes and sensitive auth artifacts.
- Permission checks exist in Application/backend paths (and in remote endpoints at HTTP boundary).
- Avoid unstable "exactly-once" claims outside the database transaction; prove idempotent posting via unique constraints and conditional status transition.

## 8. Release gates

- P0–P4 foundational gates documented in `docs/DEVELOPMENT_PLAN.md`.
- P5 complete only when all scenarios above have automated evidence plus GUI smoke checks.
- P6 complete when scripted clean-install/upgrade/rollback-operational guidance exists and both desktop hosts are verified.
- P7 complete when local and HTTP adapter contract parity tests pass.

## 9. Open product decisions (not blockers for P0)

- Exact product/customer/employee names and nullable contact fields; ID type/seed policy.
- Initial SQL Server version/editions and installation topology.
- Whether users can select the warehouse after any lines exist (proposed: yes, before posting, with revalidation).
- Long-term financial valuation, warehouse transfers and multi-tenant needs (future phase).

Codex should not invent unapproved modules to resolve these; use narrow reversible defaults for P5 and record choices.
