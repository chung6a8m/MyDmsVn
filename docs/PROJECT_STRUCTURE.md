# Current solution and project structure

**Audited source baseline:** `v1a-pro-ui-next@6ee6951c58f6e4ad92f991521ff5c553d2d22526` (merged PR #48), 2026-10-25. The net48 project/source orientation is represented below; this refresh supplies no new runtime acceptance. The [execution index](plans/README.md) records that no execution plan is active, while [UI-NEXT](ui-next/README.md) retains historical implementation evidence. The [archive boundary](archive/20261023-pre-ui-next/README.md) preserves earlier candidate evidence and unfinished acceptance.

## Source projects

| Project under `src/` | Current target | Responsibility and dependencies |
|---|---|---|
| `MyDmsVn.Contracts` | `net48` | Immutable API results, DTOs and shared JSON conventions; independent of SQL/UI implementation. |
| `MyDmsVn.SharedKernel` | `net48` | Shared inner primitives. |
| `MyDmsVn.Server.Domain` | `net48` | Domain types; inner dependencies only. |
| `MyDmsVn.Server.Application` | `net48` | Feature use cases, validation, authorization, persistence interfaces and transport-neutral mapping. |
| `MyDmsVn.Server.Infrastructure` | `net48` | LINQ to DB/native SqlClient implementation of inner interfaces; T07 plan-specific correctness/package/Local smoke acceptance complete; broader release gates deferred. |
| `MyDmsVn.Server.DbMigrator` | `net48` | Controlled DbUp schema deployment and separate maintenance commands. |
| `MyDmsVn.Desktop.Application` | `net48` | Contracts-based client interfaces and ViewModels. |
| `MyDmsVn.Desktop.Infrastructure.Local` | `net48` | In-process Application dispatch, current-account checks and desktop session integration. |
| `MyDmsVn.Desktop.WinForms` | `net48` | Shared views and the BootstrapSourceGrid adapter from the pinned WinFormsKit checkout. |
| `MyDmsVn.Desktop.App` | `net48` | Current WinForms host and Local composition root. |
| `MyDmsVn.Desktop.AppCore` | `net8.0-windows`, parked | Retained source outside active solution/release; not current executable coverage. |

`MyDmsVn.Server.Api` and `MyDmsVn.Desktop.Infrastructure.Http` do not exist as current projects. HTTP v2 is [out of scope](DEFERRED_WORK.md#out-of-scope-directions) and needs a separate runtime decision. Do not create these projects as part of UI-NEXT.

## Active test projects

| Project under `tests/` | Current target | Ownership |
|---|---|---|
| `MyDmsVn.Architecture.Tests` | `net48` | Dependency and composition boundaries. |
| `MyDmsVn.Server.Application.Tests` | `net48` | Application, contract and pure feature behavior. |
| `MyDmsVn.Desktop.Session.Tests` | `net48` | Pure desktop/session behavior without the WinForms GUI owner. |
| `MyDmsVn.Desktop.Tests` | `net48` | Existing WinForms and guarded GUI behavior. |
| `MyDmsVn.Desktop.Ui.Tests` | `net48` | A1 Sales UI Lab scenario and native Sales behavior. |
| `MyDmsVn.Server.Infrastructure.Foundation.Tests` | `net48` | SQL connection/UoW/repository lifetime and transaction guarantees. |
| `MyDmsVn.Server.Infrastructure.Catalog.Tests` | `net48` | Catalog SQL behavior. |
| `MyDmsVn.Server.Infrastructure.Inventory.Tests` | `net48` | Receipt, posting, balance and reconciliation SQL behavior. |
| `MyDmsVn.Server.Infrastructure.Security.Tests` | `net48` | Security administration, authorization and principal behavior. |
| `MyDmsVn.Server.Infrastructure.Migrations.Tests` | `net48` | Schema/migration and maintenance behavior. |
| `MyDmsVn.Server.Infrastructure.IntegrationTests` | `net48` | Local owned-database helpers and remaining Operations/Local SQL scenarios; this name no longer means the entire SQL suite. |

There is no separate current `MyDmsVn.Contracts.Tests` project. Use the actual owning project and source case rather than a historical proposed path. These are eleven current test projects, including the A1 Sales UI Lab owner; no standalone SQL support or runner project remains. Each SQL owner has its own csproj; a shared library or test trait does not by itself prove isolation or parallel safety.

## Implemented Sales locations

Sales uses the existing projects and test owners. Current source includes `Contracts/SalesContracts.cs`, Domain/Application/Infrastructure `Sales/` folders, inventory valuation math/coordinator, `LocalSalesReceiptApiClient.cs`, Sales ViewModels and `SalesReceiptControl.cs` with its app-owned grid/adapter. Schema `010_DirectSalesAndValuation.sql`, preflight/grants and fixture metadata are implemented. The [business](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md) and [archived plan](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) retain their source rationale and historical checkpoints.

UI-NEXT's [historical implementation plan](archive/20261025-pre-next-roadmap/ui-next/20261023-implementation-plan.md) records proposed presentation files, affected owners and subsequent candidate evidence. Proposed paths/types do not exist merely because documentation names them; inspect source before choosing test selectors or using an API. The old SAL native125/attended/Owner gaps do not redefine the implemented project graph.

## Actual feature directories

| Layer | Existing directories and anchors |
|---|---|
| Server.Application | `Catalog/`, `Inventory/`, `Sales/`, `Identity/`, `Security/`, `Persistence/`; pipeline behaviors and result mapping at project root. |
| Server.Infrastructure | `Catalog/`, `Inventory/`, `Sales/`, `Identity/`, `Security/`, `Operations/`, `Persistence/`; `DependencyInjection.cs`. |
| Application persistence contract | `src/MyDmsVn.Server.Application/Persistence/{IUnitOfWork,IUnitOfWorkFactory,UnitOfWorkState}.cs`. |
| Infrastructure persistence | `src/MyDmsVn.Server.Infrastructure/Persistence/{SqlUnitOfWorkFactory,ISqlExecutionContext,SqlConnectionFactory,SqlPersistenceBuilder,LinqToDbSessionFactory,LinqToDbMappingSchemaProvider}.cs`. |
| Schema | `src/MyDmsVn.Server.DbMigrator/Scripts/001_PersistenceFoundation.sql` through `010_DirectSalesAndValuation.sql`; exact names in [PERSISTENCE](PERSISTENCE.md). |
| Implemented WinForms presentation seams | `BusinessCompactAppearance.cs`, `BusinessCompactNavigation.cs`, `GoodsReceiptLinesGrid.cs`, `GoodsReceiptLinesGridAdapter.cs`, `ReceiptNumericInput.cs` and `ReceiptDecimalInputProfiles.cs` in `src/MyDmsVn.Desktop.WinForms/`; `SalesReceiptControl.cs` and its grid/adapter own the Sales workspace; `CatalogControl.cs` remains shared by four catalogs. |
| UI dependency | `vendor/MyDmsVn.WinFormsKit/` at `c232a4c09dc0386a5bd8bb484a5c991acae827d5`; contains UI core, grid adapter and SourceGrid at `f4e457b43582bf01892f50bdc74aa480531e5944`. Existing Bootstrap5WinFormUI/BootstrapSourceGrid assembly names remain valid. |

`Application/Features/...`, `Application/Abstractions/Persistence` and Infrastructure `Persistence/Repositories`/`EntityMaps` were earlier blueprint examples. They are not the current file layout; use source-owned paths when changing code.

## Repository entry points

| Path | Role |
|---|---|
| `MyDmsVn.sln` | Current production/test solution graph; parked AppCore is excluded. |
| `MyDmsVn.UiLab.sln`, `samples/MyDmsVn.Desktop.UiLab*` | Separate A1 WinForms Lab and fixture graph; [runbook](development/UI_LAB.md). |
| `Directory.Build.props`, `Directory.Packages.props`, project lock files | Shared build settings, exact centrally managed packages and project dependency graphs. |
| `AGENTS.md`, `AI_CONTEXT.md`, `README.md` | Current development instructions and orientation. |
| `docs/plans/README.md` | Stable current-work index; records no active plan and requires explicit activation of any successor. |
| `docs/archive/20261025-pre-next-roadmap/plans/20261024-001-a0-a1-sales-ui-lab.md` | Archived A0/A1 authorization, tasks and candidate verification; no current execution authority. |
| `docs/archive/20261025-pre-next-roadmap/ui-next/20261023-active-roadmap.md` | Historical UI-NEXT selection, task records and exact-candidate acceptance. |
| `docs/archive/README.md` | Dated archive boundaries, including the 2026-10-25 reset and retained acceptance limits. |
| `docs/archive/20261023-pre-ui-next/README.md` | Earlier archive boundary; previous queues and unfinished work are excluded without acceptance or waiver. |
| `docs/TEST_POLICY.md` | Authoritative test scope and evidence policy. |
| `docs/TEST_STRATEGY.md` | Behavior coverage reference. |
| `docs/DEVELOPMENT_PLAN.md`, `docs/DEFERRED_WORK.md`, `docs/HISTORY.md` | Execution-index alias, unscheduled release constraints/out-of-scope directions and historical evidence. |
| `tests/README.md`, SQL project-local/linked helpers | Direct `dotnet test`/Test Explorer commands, explicit SQL configuration and positively owned database lifecycle. |
| `deployment/sql/Provision-MyDmsVnPrincipals.sql` | Explicit runtime/maintenance SQL authority separation. |
| `deployment/release/Publish-LocalRelease.ps1` | Existing net48 Local validation/release packaging. |

Keep helper dependencies compatible with net48 and test support inside its existing test owners. Use `-f net48` in test commands; a newer SDK does not change the target runtime. Any future runtime reactivation must review the actual source/project/lock graph under a separately approved plan. UI-NEXT does not reactivate AppCore or upgrade the framework. Sales files are implemented source; consult actual source and the linked UI-NEXT records rather than treating historical proposal wording as current implementation status. Historical evidence remains tied to its exact revision and scope.
