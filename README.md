# MyDmsVn

Windows desktop-first SME business software, beginning with **Sales & Inventory Foundation**. Local v1 runs Application handlers in-process; a later HTTP adapter will reuse their contracts.

## Roadmap — v1a, updated 2026-10-19

| Order | Plan | Actual status |
|---:|---|---|
| 1 | [RESP — Respawn fixture reuse](docs/plans/20261018-002-respawn-fixture-reuse.md) | **Complete / accepted** |
| 2 | [L2DB — net48 LINQ to DB migration](docs/plans/20261018-001-linq2db-net48-persistence-migration.md) | **Complete / accepted in plan scope** |
| 3 | [GUI-P5 — full GUI testing and P5 acceptance](docs/plans/20261019-001-full-winforms-gui-p5-acceptance.md) | **New / OPEN** |
| 4 | [REL — P6 production release acceptance](docs/plans/20261019-002-local-v1-p6-release-acceptance.md) | **New / OPEN — NOT READY FOR PRODUCTION** |

The [execution index](docs/plans/README.md) owns task order, dependencies and evidence. REL preparation can overlap GUI-P5; final P6 G4 requires GUI-P5 acceptance.

## Current implementation

- Product, WinForms and all **ten active test projects** target `net48`. The shipped desktop and separate admin migrator publish as Windows x86 directories; AppCore/net8.0-windows remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, **LINQ to DB + typed native SqlClient**, SQL Server and DbUp. RepoDb/Dapper are removed from the active production/test graph following L2DB-T06; no provider fallback.
- Accepted test-only **Respawn 4** reusable, owned SQL fixtures with deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer. The older global SQL gate manifest, standalone harness/runner and wrapper tools are removed.
- WinForms with CommunityToolkit.Mvvm, Bootstrap5WinFormUI and BootstrapSourceGrid. Four catalogs, Goods Receipt Draft/Post, stock ledger, balances and stock card exist. Code-owned permission definitions, deliberate assignments and one configured superuser replace historical permission synchronization.
- [L2DB-T07](docs/L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) proved frozen net48 correctness/package and a **limited** real authenticated Local smoke. The full Desktop GUI suite (139 invocations at that frozen revision), attended P5 checks, clean install, recovery and P6 G1–G5 have **not** been accepted.
- Release schema support metadata is now reconciled to the shipped policy (schema 9, scripts 001–009); [RELEASE-MANIFEST-01](docs/DEVELOPMENT_PLAN.md) is closed **only for metadata**. Older-prefix automatic upgrades are not supported by the current migrator.

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), [active tasks](docs/plans/README.md) and [TEST_POLICY.md](docs/TEST_POLICY.md). Focus affected tests during implementation; GUI-P5 and REL explicitly require broader real-GUI/SQL and installed-package evidence at acceptance. Interactive Windows and an approved disposable SQL test instance are necessary for those runtime facts. Never infer a PASS from blocked/skipped tests or historical runs.

The scope of P7 HTTP, AppCore reactivation and later business modules is [deferred](docs/DEFERRED_WORK.md). Historical removed/superseded material remains indexed in [History](docs/HISTORY.md).

## Documentation

[Docs index](docs/README.md) · [Development roadmap](docs/DEVELOPMENT_PLAN.md) · [Architecture](docs/ARCHITECTURE.md) · [Persistence](docs/PERSISTENCE.md) · [Security](docs/SECURITY.md) · [Test policy](docs/TEST_POLICY.md) · [Release packaging](docs/LOCAL_RELEASE_PACKAGING.md)
