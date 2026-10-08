# ADR 0002 — Explicit UnitOfWork

Status: **Accepted** · Date: 2026-10-08

## Context
Commands must atomically change receipt status, ledger rows and warehouse balances, using RepoDb and sometimes Dapper. Prototype ambient `AsyncLocal<IDbTransaction>` creates difficult nested ownership and leakage risks.

## Decision
Use **Option A: Explicit UoW**. A UoW owns one opened `IDbConnection` and one optional local `IDbTransaction`. Repository instances share this scope. Dapper and RepoDb are always passed the current connection/transaction explicitly. Command handlers own transaction begin/commit/rollback for multi-table operations.

No static/global ambient transaction provider. No implicit nested transactions; `BeginTransaction` when one is already active fails clearly. Dispose rolls back uncommitted work.

## Alternatives
Ambient AsyncLocal accessors — rejected for MVP; may be revisited with explicit suppression/nesting ADR and tests.

## Consequences
Handlers have visible transaction ownership and slightly more plumbing. Correct disposal, transaction state machine and SQL Server concurrency tests are required before P5.

See `docs/PERSISTENCE.md` and `docs/TEST_STRATEGY.md`.
