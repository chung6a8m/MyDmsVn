# Codex instructions — MyDmsVn / v1a

Applies to the entire repository. Human-facing discussion is Vietnamese; identifiers and technical documentation are English unless an existing runbook uses Vietnamese.

## Start here

The owner reset the active roadmap on **2026-10-18**. There are exactly **two active implementation plans**, in this order:

1. [RESP — Integration-test fixture reuse with Respawn](docs/plans/20261018-002-respawn-fixture-reuse.md).
2. [L2DB — LINQ to DB persistence migration on net48](docs/plans/20261018-001-linq2db-net48-persistence-migration.md).

RESP-T00–T05 are complete and accepted on the local net48 working-tree candidate recorded in [verification](docs/RESPAWN_VERIFICATION.md#pr-36-review-follow-up). Next is L2DB-T00; L2DB is not implemented. Bind the accepted fixture bytes to the actual commit/worktree before DAL changes, preserve uncommitted work, and keep fixture boundaries fixed. The prior removal of the old SQL test framework is a baseline simplification, not RESP implementation. Historical TESTGATE, completed modularization, NET8-PAUSE and SEC-REBUILD work is context, not an execution queue. Pending P5/P6/P7 work is in [Deferred work](docs/DEFERRED_WORK.md), not a third active plan.

## Read order and authority

1. This file and [AI_CONTEXT.md](AI_CONTEXT.md).
2. [Active plans](docs/plans/README.md), the specific task, and [TEST_POLICY.md](docs/TEST_POLICY.md).
3. Relevant contracts: [Architecture](docs/ARCHITECTURE.md), [Persistence](docs/PERSISTENCE.md), [Contracts](docs/CONTRACTS.md), [Security](docs/SECURITY.md), [Data model](docs/DATA_MODEL.md).
4. [PRD](docs/PRD.md), [Tech stack](docs/TECH_STACK.md), and applicable [ADRs](docs/adr/README.md).
5. [History](docs/HISTORY.md) only when a past decision or evidence is needed.

Explicit owner decisions supersede older documents. The active-plan index owns work order; TEST_POLICY owns test execution; TEST_STRATEGY describes coverage; tests/README describes direct test commands. Historical plans and old command examples in Git are not current instructions. Flag a real contradiction instead of silently changing architecture or claiming planned behavior already exists.

## Current implementation and approved destination

| Area | Current v1a code | Approved direction |
|---|---|---|
| Runtime | Product, SQL, Desktop and all ten active test projects target `net48` | Retain `net48`; no runtime upgrade in RESP/L2DB |
| Test execution | Direct `dotnet test` / Test Explorer; local owned helper and accepted test-only Respawn collections | Preserve accepted fixture baseline; no external framework/manifest restoration |
| Modern product | AppCore source / historical .NET 8 cases parked; AppCore outside solution/release | Reactivation is deferred and separately scoped |
| Persistence | RepoDb commands, Dapper queries, explicit UoW, SQL Server, DbUp | LINQ to DB behind existing contracts after RESP acceptance |
| SQL fixtures | Default reusable collections for 20 original Catalog/Inventory/Identity/Security data cases; explicit fresh and dedicated exceptions; serial guards | Preserve accepted reset/seed/ownership/drain contract and dedicated lifecycle/schema/principal/trigger/contention databases during L2DB |
| Permissions | Code-defined registry, explicit assignments, one configured database-bound superuser | Preserve current implementation and invariants |

## Architecture and business invariants

- Local v1 dispatches Application in process; future HTTP uses the same transport-neutral DTO/result semantics.
- Domain/Application do not depend on SQL-provider, HTTP or WinForms implementations. Desktop views/ViewModels depend on desktop abstractions and Contracts.
- Explicit UoW owns one open connection and optional transaction. Scope-bound repositories and queries share them explicitly. No ambient/static/AsyncLocal transaction, implicit nested transaction, or hidden second connection for transactional work.
- Repository resolution stays within the UoW scope and remains explicit; preserve the rule in PERSISTENCE and ARCHITECTURE.
- Posting changes receipt, ledger and balances atomically; retries/concurrent requests cannot duplicate a movement. Posted receipts and the ledger remain immutable to business callers.
- Preserve `PasswordHash` / `PasswordSalt` and algorithm-aware verification. Unknown/deprecated permissions deny before superuser evaluation; ordinary direct-user decisions override active-role grants. No permission synchronization, automatic grants, startup seeding or normal-path changes to the protected superuser.
- Keep DbUp and administrator-only migration/maintenance operations. Production SQL scripts do not become test-reset scripts. Local desktop SQL access remains a documented trust limitation.
- P5 contains catalogs, Goods Receipt Draft/Post, Stock Balance and Stock Card. New business modules and HTTP hosting remain deferred.

## Work protocol

1. State the active plan/task, affected behavior and smallest meaningful verification scope.
2. Read the checked-in source and actual project targets before changing them. Use current task evidence, not old totals, to establish progress.
3. Make small, reviewable changes; add tests for meaningful behavior or failure boundaries. Documentation-only changes need static documentation checks, not a product regression run.
4. Follow [TEST_POLICY](docs/TEST_POLICY.md). Broaden tests for shared dependencies or an explicit milestone; do not run full SQL/GUI/repository suites after each edit or by default for every code PR.
5. Use the explicit non-production SQL test connection, preserve local allocation ownership/cleanup checks, and run SQL projects sequentially. The former framework, manifest and wrappers are removed; no approval-file, global lease, external watchdog or orphan recovery service remains. Refuse ambiguous cleanup and report leftovers for manual review.
6. Record commands, revision, scope, actual pass/fail/skip/not-run results and material limitations. Update checkboxes only when their evidence exists.
7. Summarize what changed, why, verification and the next active task. P5/P6 release checks are still open; this roadmap reset grants no release acceptance.

## Editing and resource rules

- Follow `.editorconfig` and `.gitattributes`: Markdown is UTF-8 without BOM with CRLF; C# uses its declared BOM/CRLF policy. Preserve Vietnamese text.
- Compile against the actual target APIs, especially `net48`. Do not upgrade packages broadly, suppress warnings globally or introduce an additional ORM to avoid a compatibility problem.
- Register implementations explicitly with Microsoft DI. Keep ErrorOr, MediatR, RepoDb/LINQ to DB and SQL objects out of transport DTOs.
- Use parameterized SQL. Never commit secrets, production connection strings, local SQL configuration or raw test artifacts.
- SQL tests mutate only positively owned disposable databases on the explicitly designated non-production test instance. Reset/boundary failure poisons reuse; cleanup timeout quarantines the allocation with no later reset/drop until manual review. Verify the boundary again after drained scenario cleanup, including final/focused cases.
- GUI tests use STA, serialized UI resources, deterministic disposal, bounded waits and captured exceptions/DataGridView errors. Headless failures must not open blocking dialogs.
- Missing Windows, targeting packs, SQL configuration or GUI access is `BLOCKED / NOT RUN`, never PASS. Use [tests/README.md](tests/README.md) for current commands and resource constraints.
