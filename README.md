# MyDmsVn

Windows desktop-first SME business management, beginning with **Sales & Inventory Foundation** and designed to evolve from a local Application/SQL adapter to a remotely hosted HTTP API without duplicating business handlers.

## Current status — 2026-10-16

P0/P1/P2/P3/P3.1/P4 implementation is complete. P4.1 Login/session is **closed by explicit product-owner manual-UI acceptance**, with a documented incomplete packaged idle-expiry observation; the owner exception does not close remaining P5/P6 release gates. P5 business features and automated tests exist, but P5.1/P5.4 interactive acceptance remains open. P6 operational/release acceptance remains open. TESTGATE T01–T03 are verified; modularization, permission-system replacement and P7 are future work.

**Current candidate:** [NET8-PAUSE](docs/plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) T01–T05 runtime/build/package/GUI/restoration gates pass at `6b0ce337`; the same-protocol duration comparison remains open and merge is pending. Application, SQL and Desktop projects actively target `net48`; historical .NET 8 case metadata and the parked AppCore source remain for [reviewed reactivation](docs/REACTIVATE_NET8.md). The runner's tool-only `net8.0` target is not product coverage. Follow the [ordered active roadmap](docs/plans/README.md) and [Development Plan](docs/DEVELOPMENT_PLAN.md).

## Approved technology and boundaries

- C#, active product/runtime target `.NET Framework 4.8`; `.NET 8` product targets are temporarily parked while the test runner remains tool-only `net8.0`. No unapproved move to .NET 10. See [Tech Stack](docs/TECH_STACK.md).
- Clean Architecture/modular monolith, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork.
- RepoDb for write-side storage, Dapper for read models, SQL Server and versioned DbUp migrations.
- Shared WinForms/CommunityToolkit.Mvvm views, Bootstrap5WinFormUI and BootstrapSourceGrid; LocalApiClient for v1, HttpApiClient planned for v2.
- Security enforcement is mandatory in Application, not just desktop UI. The current permission implementation exists but [SEC-REBUILD](docs/plans/20261016-003-permission-system-rebuild.md) is a **proposed replacement**, not current product behavior.
- Historical legacy password columns remain for safe migration evaluation.

## How to work in this repository

1. Read [AGENTS.md](AGENTS.md), [PRD](docs/PRD.md), [Architecture](docs/ARCHITECTURE.md) and [Tech Stack](docs/TECH_STACK.md).
2. Follow the **exact order** of the [six active roadmap entries](docs/plans/README.md). Completed TESTGATE-T01–T03 stays active as context only.
3. Run focused **Task** tests during implementation; use the guarded manifest-owned strict **Final** acceptance only after gate-affecting inputs are frozen. Never substitute unapproved SQL/GUI tests or run the full SQL suite after every edit.
4. Publish reviewed sanitized results; do not commit SQL credentials, instance approval files, TRX, ownership journals or other raw diagnostics. Missing external environment means BLOCKED/NOT RUN, not PASSED.

## Documentation

[Development Plan](docs/DEVELOPMENT_PLAN.md) · [Active plans](docs/plans/README.md) · [Archive](docs/plans/archive/README.md) · [Contracts](docs/CONTRACTS.md) · [Security](docs/SECURITY.md) · [Persistence](docs/PERSISTENCE.md) · [Test Strategy](docs/TEST_STRATEGY.md) · [Test Gate Matrix](docs/TEST_GATE_MATRIX.md) · [Codex policy](docs/CODEX_TEST_EXECUTION_POLICY.md) · [ADR index](docs/adr/README.md)

Previous completed dual-framework build/test evidence is historical and is **not** proof that any future TFM reduction, restored net8 target or P7 API has already passed.
