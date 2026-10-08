# MyDmsVn — AI context

## Purpose
A modular, desktop-first SME business application, with a future transition from direct in-process services to ASP.NET Core HTTP APIs. Language for human-facing project discussions: Vietnamese. C# identifiers and core code documentation: English.

## Current state
The repository was initialized with `project-idea.md` and Git/editor settings. The present work creates architecture/planning documents; there is no finished solution, database or runnable feature yet.

## Non-negotiable choices
- Keep `net48` and `net8.0` (`net8.0-windows` for WinForms); developer uses Visual Studio 2022.
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
- UserPermission overrides RolePermission; unknown permission denies.
- Legacy hash migration is algorithm-aware, tested with fixtures, never guessed.

## Navigation
Read `AGENTS.md` first; then `docs/DEVELOPMENT_PLAN.md` and the appropriate `docs/plans` document. See `README.md` for the complete documentation index.
