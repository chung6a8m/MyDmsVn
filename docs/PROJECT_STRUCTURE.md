# Proposed solution structure (P0 blueprint)

This is a plan, not a description of current files. P0 creates the directories/projects only after validating references, framework targets and NuGet restore.

```text
/
  MyDmsVn.sln
  Directory.Build.props
  Directory.Packages.props
  README.md
  AGENTS.md
  AI_CONTEXT.md
  project-idea.md
  docs/
    PRD.md
    ARCHITECTURE.md
    TECH_STACK.md
    DEVELOPMENT_PLAN.md
    PROJECT_STRUCTURE.md
    CONTRACTS.md
    PERSISTENCE.md
    SECURITY.md
    DATA_MODEL.md
    TEST_STRATEGY.md
    plans/
    adr/
  src/
    MyDmsVn.Contracts/
    MyDmsVn.SharedKernel/
    MyDmsVn.Server.Domain/
    MyDmsVn.Server.Application/
    MyDmsVn.Server.Infrastructure/
    MyDmsVn.Server.DbMigrator/
    MyDmsVn.Server.Api/                  # P7 only
    MyDmsVn.Desktop.Application/
    MyDmsVn.Desktop.Infrastructure.Local/
    MyDmsVn.Desktop.Infrastructure.Http/  # P7 only
    MyDmsVn.Desktop.WinForms/
    MyDmsVn.Desktop.App/                 # net48
    MyDmsVn.Desktop.AppCore/             # net8.0-windows
  tests/
    MyDmsVn.Architecture.Tests/
    MyDmsVn.Server.Application.Tests/
    MyDmsVn.Server.Infrastructure.IntegrationTests/
    MyDmsVn.Desktop.Tests/
    MyDmsVn.Contracts.Tests/
```

## Targeting and references

| Project | Planned TFM | Depends on |
|---|---|---|
| Contracts | `net48;net8.0` or proven `netstandard2.0` | minimal/no app packages |
| SharedKernel | `net48;net8.0` or proven `netstandard2.0` | minimal |
| Server.Domain | `net48;net8.0` | SharedKernel |
| Server.Application | `net48;net8.0` | Domain, Contracts where appropriate |
| Server.Infrastructure | `net48;net8.0` | Application, Domain, SQL libraries |
| Server.DbMigrator | `net48;net8.0` if required | DbUp, scripts |
| Server.Api (P7) | `net8.0` | Application, Infrastructure, Contracts |
| Desktop.Application | `net48;net8.0` | Contracts |
| Desktop.Infrastructure.Local | `net48;net8.0` | Desktop.Application, Application |
| Desktop.Infrastructure.Http (P7) | `net48;net8.0` | Desktop.Application, Contracts |
| Desktop.WinForms | `net48;net8.0-windows` | Desktop.Application |
| Desktop.App | `net48` | UI, local composition, server infra |
| Desktop.AppCore | `net8.0-windows` | UI, local composition, server infra |

**Important:** An assembly targeting `net8.0` cannot be referenced by a `net48` host. P0 must verify dependencies/TFMs before committing project skeletons. A test project's targets are chosen based on runner/OS support, not necessarily duplicated blindly.

## Feature directory examples

Server.Application:
```text
Features/
  Products/Commands/CreateProduct/
  Products/Commands/UpdateProduct/
  Products/Queries/GetProducts/
  GoodsReceipts/Commands/CreateDraft/
  GoodsReceipts/Commands/PostGoodsReceipt/
  Inventory/Queries/GetStockBalances/
Abstractions/
  Persistence/
  Security/
  Clock/
```

Server.Infrastructure:
```text
Persistence/
  Connection/
  UnitOfWork/
  Repositories/
  Queries/
  EntityMaps/
Identity/
DependencyInjection.cs
```

## P0 minimum viable solution

P0 creates only runnable empty/smoke projects required to prove dual-target CI/build, reference direction, adapter substitution seam, DI startup and automated test scaffolds. P7-specific projects may remain documented placeholders until P7. Avoid creating dummy business logic or early SQL schemas in P0.
