# MyDmsVn

Windows desktop-first SME business software, beginning with **Sales & Inventory Foundation**. Local v1 runs shared Application handlers in process; a future HTTP adapter will reuse their contracts and semantics.

## Active roadmap — v1a, 2026-10-18

There are exactly two active implementation plans:

| Order | Plan | Status |
|---:|---|---|
| 1 | [Respawn fixture reuse](docs/plans/20261018-002-respawn-fixture-reuse.md) | Owner-selected direction; implementation pending |
| 2 | [LINQ to DB persistence migration](docs/plans/20261018-001-linq2db-net48-persistence-migration.md) | Owner-selected direction; starts after RESP acceptance |

Respawn first establishes reusable, isolated test fixtures on the current DAL. LINQ to DB then replaces production persistence behind the existing contracts while that fixture baseline stays stable. [The active plan index](docs/plans/README.md) owns execution order.

## Current implementation

- Product, SQL tests and WinForms run on **.NET Framework 4.8**. All ten active test projects also target net48; AppCore and modern product targets remain parked.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, SQL Server and DbUp.
- **RepoDb/Dapper remain in production code** until the LINQ to DB migration is completed. Respawn is also planned, not yet installed.
- WinForms with CommunityToolkit.Mvvm, Bootstrap5WinFormUI and BootstrapSourceGrid.
- Code-defined permissions, deliberate assignments and one configured superuser are implemented; the previous permission synchronization workflow is retired.
- Catalogs, Goods Receipt Draft/Post, stock ledger, balances and stock card exist. Remaining P5 interactive and P6 operational/release acceptance is [deferred work](docs/DEFERRED_WORK.md), still open.

## Working with Codex

Start with [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the active task, and [TEST_POLICY.md](docs/TEST_POLICY.md). The policy has been rewritten for this roadmap: focused affected tests during a task, broader SQL validation for shared infrastructure and the two acceptance milestones, and GUI checks when the change or milestone requires them.

Completed and superseded plans, duplicate test policies and old verification narratives were removed from the working documentation. Their immutable Git sources are indexed in [History](docs/HISTORY.md). The owner also removed the gate manifest, standalone SQL framework, runner, related tests and wrappers. [tests/README.md](tests/README.md) documents direct `dotnet test`/Test Explorer execution and the local owned-database helper.

## Documentation

[Documentation index](docs/README.md) · [Development plan](docs/DEVELOPMENT_PLAN.md) · [Architecture](docs/ARCHITECTURE.md) · [Persistence](docs/PERSISTENCE.md) · [Contracts](docs/CONTRACTS.md) · [Security](docs/SECURITY.md) · [Test policy](docs/TEST_POLICY.md) · [ADR index](docs/adr/README.md)

This branch is not yet production-release accepted. In particular, release packaging still records schema `8..8` while runtime expects migrations `001–009`; the outstanding release checklist tracks that discrepancy.
