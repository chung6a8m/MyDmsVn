# MyDmsVn — AI context

## Purpose
A modular, desktop-first SME business application, with a future transition from direct in-process services to ASP.NET Core HTTP APIs. Language for human-facing project discussions: Vietnamese. C# identifiers and core code documentation: English.

## Current state
**As of 2026-10-16:** P0/P1/P2/P3/P3.1/P4 implementation and automated verification are complete. P4.1 production Login/session is **accepted complete by the product owner** based on automated L3 and attended packaged UI, with the unfinished full idle-expiry/relogin observation explicitly recorded as a **P4.1-only evidence exception**, not a passed manual test. P5 Sales & Inventory functionality and automated gates exist (catalogs, Goods Receipt Draft/Post, ledger/balances/stock card), but **P5.1/P5.4 interactive UI acceptance remains open**; P6 operational/release G1–G5 gates remain open. SQLPERF-T01–T06 and TESTGATE-T01–T03 are verified complete; T01–T03's plan stays active for Codex context. The **next implementation work is NET8-PAUSE**, then modular test projects (T04/T05), SEC-REBUILD, remaining P5/P6 acceptance, and P7. This file is a quick orientation; the canonical status/order is [the active plans index](docs/plans/README.md) and [Development Plan](docs/DEVELOPMENT_PLAN.md). Do **not** restart P1 or assume future plans are implemented.

## Non-negotiable choices
- **Current checked-in baseline** still declares `net48` and `net8.0` (`net8.0-windows` for modern WinForms); developer uses Visual Studio 2022. The [reversible NET8-PAUSE plan](docs/plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) is **proposed, not implemented**. `main` and unrelated PRs require the current dual-TFM gates until the dedicated implementation merges. **Only the T01-authorized NET8-PAUSE candidate code PR** may run its fully guarded Stage B active-`net48` Final after actual target/runner changes; historical net8 is `PARKED / NOT RUN`, not PASS. Tool-only `net8.0` may remain necessary for the SQL runner; restore application net8 before P7 unless explicitly redesigned.
- Clean Architecture, feature-oriented modular monolith; MediatR + FluentValidation + ErrorOr.
- Explicit UoW; RepoDb 1.16.0, SqlServer 1.16.1, BulkOperations 1.16.2; Dapper; DbUp.
- Preserve `Users.PasswordSalt` during legacy migration.
- WinForms + MVVM Toolkit + Bootstrap5WinFormUI + BootstrapSourceGrid.
- Local adapter v1 / HTTP adapter v2; HTTP transport is postponed but contracts are designed now.

## P5 scope
`Products`, `Warehouses`, `Employees`, `Customers`, `GoodsReceipts` + `GoodsReceiptLines`, `StockLedger` and `StockBalances`. Core scenario: create draft receipt → post once atomically → ledger and current balances update → stock card reports. Customers remain standalone until sales orders are in a later phase.

## Mandatory invariants
- Domain/Application cannot depend on UI or SQL implementation.
- Explicit UoW owns single connection/transaction; never leak connection, repository or ErrorOr over transport.
- Posting must be all-or-nothing, idempotent against retries/concurrency, and immutable afterward.
- No write API for balance/ledger; only posting creates inventory movements.
- **Current implemented authorization (before SEC-REBUILD):** UserPermission overrides RolePermission and unknown keys deny. [SEC-REBUILD](docs/plans/20261016-003-permission-system-rebuild.md) is a future **breaking proposed replacement**, not the current runtime; it introduces code-owned permission definitions and a single configured superuser with carefully scoped bypass.
- Legacy hash migration is algorithm-aware, tested with fixtures, never guessed.

## Navigation
Read [AGENTS.md](AGENTS.md) first, then the [Development Plan](docs/DEVELOPMENT_PLAN.md) and the [six ordered active plans](docs/plans/README.md); [archived plans](docs/plans/archive/README.md) are historical, not new Codex tasks. The [first active TESTGATE plan](docs/plans/20261016-001-task-gates-and-modular-tests.md) stays as verified context; implement NET8-PAUSE next, not P1. See [README.md](README.md) for the documentation index.
