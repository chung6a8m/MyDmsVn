# MyDmsVn

MyDmsVn is a Windows desktop-first business-management platform for Vietnamese SMEs, initially implementing **Sales & Inventory Foundation**. The design evolves from a locally executed application stack (v1) into an ASP.NET Core Web API client/server solution (v2), without rewriting WinForms views or business handlers.

> Status: documentation / implementation planning. The repository does **not** yet contain a compilable solution. Do not describe planned capabilities as implemented.

## Decisions already approved

- C#, **.NET Framework 4.8 + .NET 8**; use `net8.0-windows` for modern WinForms targets. Do **not** upgrade to .NET 10 without explicit approval.
- Clean Architecture, modular monolith, MediatR + FluentValidation + ErrorOr.
- Explicit Unit of Work, RepoDb for writes and Dapper for read models, SQL Server and DbUp.
- RepoDb `1.16.0`, RepoDb.SqlServer `1.16.1`, RepoDb.SqlServer.BulkOperations `1.16.2`.
- Preserve both `PasswordHash` and `PasswordSalt` for legacy-system migration; do not assume legacy hash algorithms.
- Two interchangeable desktop adapters: `LocalApiClient` in v1 and `HttpApiClient` when v2 becomes available.
- WinForms, CommunityToolkit.Mvvm, [Bootstrap5WinFormUI](https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI) and [BootstrapSourceGrid](https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid).

## Start here (human and Codex)

1. Read [AGENTS.md](AGENTS.md) and [AI_CONTEXT.md](AI_CONTEXT.md).
2. Read [Product requirements](docs/PRD.md) and [Architecture](docs/ARCHITECTURE.md).
3. Work sequentially through the [Development plan](docs/DEVELOPMENT_PLAN.md).
4. Start implementation at [P0 plan](docs/plans/20261008-002-phase-0-foundation.md); implement P5 only after P0–P4 gates pass.
5. Use the [P5 Sales & Inventory plan](docs/plans/20261008-001-sales-inventory-foundation.md) to guide the first vertical slice.

## Documentation index

| Subject | Document |
|---|---|
| Product scope and acceptance | [PRD](docs/PRD.md) |
| Clean Architecture and adapters | [Architecture](docs/ARCHITECTURE.md) |
| Project and solution layout | [Project structure](docs/PROJECT_STRUCTURE.md) |
| Runtime, package pins, coding constraints | [Tech stack](docs/TECH_STACK.md) |
| Local/HTTP contracts and error conventions | [Contracts](docs/CONTRACTS.md) |
| Explicit UoW / RepoDb / Dapper / DB migrations | [Persistence](docs/PERSISTENCE.md) |
| Users, legacy hashing, authorization | [Security](docs/SECURITY.md) |
| P5 data model and inventory invariants | [Data model](docs/DATA_MODEL.md) |
| Testing and quality gates | [Test strategy](docs/TEST_STRATEGY.md) |
| Phase plan and gates | [Development plan](docs/DEVELOPMENT_PLAN.md) |
| Decisions log | [ADR index](docs/adr/README.md) |

The original [project-idea.md](project-idea.md) is preserved as historical source material. Where it conflicts with approved design choices, **this documentation and accepted ADRs take precedence**.

## Implementation status

- [x] Architecture and roadmap documentation created
- [ ] P0 — Solution foundation
- [ ] P1 — Persistence foundation
- [ ] P2 — Application pipeline and API response
- [ ] P3 — Security foundation
- [ ] P4 — Desktop shell and client adapters
- [ ] P5 — Sales & Inventory Foundation
- [ ] P6 — Local v1 hardening / deployment
- [ ] P7 — Remote API v2 and contract parity

## Local developer environment (once P0 code exists)

Windows, Visual Studio 2022, .NET 8 SDK, .NET Framework 4.8 targeting pack, SQL Server instance for integration tests. See the phase plans for actual commands and environment variables; **do not assume build commands succeed before the solution exists**.
