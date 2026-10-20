# Current solution and project structure

**Assessment:** `v1a-pro` at `f37038b250821a538b9cdc10a6d6d8b4bc1f386f`, refreshed 2026-10-20. All ten active net48 test projects and the current LINQ/native sources are represented here. RESP, L2DB and GUI-P5 are complete in their approved scopes. [BCI](plans/20261020-001-business-compact-inline-goods-receipt.md) is the sole active roadmap and is not implemented; it does not add a new product host.

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
| `MyDmsVn.Desktop.WinForms` | `net48` | Shared views and pinned BootstrapSourceGrid presentation dependency. |
| `MyDmsVn.Desktop.App` | `net48` | Current WinForms host and Local composition root. |
| `MyDmsVn.Desktop.AppCore` | `net8.0-windows`, parked | Retained source outside active solution/release; not current executable coverage. |

`MyDmsVn.Server.Api` and `MyDmsVn.Desktop.Infrastructure.Http` do not exist as current projects. HTTP v2 is [out of scope](DEFERRED_WORK.md#out-of-scope-directions) and needs a separate runtime decision. Do not create these projects as part of BCI.

## Active test projects

| Project under `tests/` | Current target | Ownership |
|---|---|---|
| `MyDmsVn.Architecture.Tests` | `net48` | Dependency and composition boundaries. |
| `MyDmsVn.Server.Application.Tests` | `net48` | Application, contract and pure feature behavior. |
| `MyDmsVn.Desktop.Session.Tests` | `net48` | Pure desktop/session behavior without the WinForms GUI owner. |
| `MyDmsVn.Desktop.Tests` | `net48` | WinForms and guarded GUI behavior. |
| `MyDmsVn.Server.Infrastructure.Foundation.Tests` | `net48` | SQL connection/UoW/repository lifetime and transaction guarantees. |
| `MyDmsVn.Server.Infrastructure.Catalog.Tests` | `net48` | Catalog SQL behavior. |
| `MyDmsVn.Server.Infrastructure.Inventory.Tests` | `net48` | Receipt, posting, balance and reconciliation SQL behavior. |
| `MyDmsVn.Server.Infrastructure.Security.Tests` | `net48` | Security administration, authorization and principal behavior. |
| `MyDmsVn.Server.Infrastructure.Migrations.Tests` | `net48` | Schema/migration and maintenance behavior. |
| `MyDmsVn.Server.Infrastructure.IntegrationTests` | `net48` | Local owned-database helpers and remaining Operations/Local SQL scenarios; this name no longer means the entire SQL suite. |

There is no separate current `MyDmsVn.Contracts.Tests` project. Use the actual owning project and source case rather than a historical proposed path. These are ten current test projects; no standalone SQL support or runner project remains. Each SQL owner has its own csproj; a shared library or test trait does not by itself prove isolation or parallel safety.

## Actual feature directories

| Layer | Existing directories and anchors |
|---|---|
| Server.Application | `Catalog/`, `Inventory/`, `Identity/`, `Security/`, `Persistence/`; pipeline behaviors and result mapping at project root. |
| Server.Infrastructure | `Catalog/`, `Inventory/`, `Identity/`, `Security/`, `Operations/`, `Persistence/`; `DependencyInjection.cs`. |
| Application persistence contract | `src/MyDmsVn.Server.Application/Persistence/{IUnitOfWork,IUnitOfWorkFactory,UnitOfWorkState}.cs`. |
| Infrastructure persistence | `src/MyDmsVn.Server.Infrastructure/Persistence/{SqlUnitOfWorkFactory,ISqlExecutionContext,SqlConnectionFactory,SqlPersistenceBuilder,LinqToDbSessionFactory,LinqToDbMappingSchemaProvider}.cs`. |
| Schema | `src/MyDmsVn.Server.DbMigrator/Scripts/001_PersistenceFoundation.sql` through `009_Security_Permission_Import.sql`; exact names in [PERSISTENCE](PERSISTENCE.md). |
| UI dependency | `vendor/MyDmsVn.BootstrapSourceGrid/`, pinned as checked in. |

`Application/Features/...`, `Application/Abstractions/Persistence` and Infrastructure `Persistence/Repositories`/`EntityMaps` were earlier blueprint examples. They are not the current file layout; use source-owned paths when changing code.

## Repository entry points

| Path | Role |
|---|---|
| `MyDmsVn.sln` | Current declared solution graph; parked AppCore is excluded. |
| `Directory.Build.props`, `Directory.Packages.props`, project lock files | Shared build settings, exact centrally managed packages and project dependency graphs. |
| `AGENTS.md`, `AI_CONTEXT.md`, `README.md` | Current development instructions and orientation. |
| `docs/plans/README.md` | Sole active BCI roadmap; completed RESP/L2DB/GUI-P5 plans retained as history. |
| `docs/TEST_POLICY.md` | Authoritative test scope and evidence policy. |
| `docs/TEST_STRATEGY.md` | Behavior coverage reference. |
| `docs/DEVELOPMENT_PLAN.md`, `docs/DEFERRED_WORK.md`, `docs/HISTORY.md` | Current roadmap pointer, unscheduled release constraints/out-of-scope directions and historical evidence. |
| `tests/README.md`, SQL project-local/linked helpers | Direct `dotnet test`/Test Explorer commands, explicit SQL configuration and positively owned database lifecycle. |
| `deployment/sql/Provision-MyDmsVnPrincipals.sql` | Explicit runtime/maintenance SQL authority separation. |
| `deployment/release/Publish-LocalRelease.ps1` | Existing net48 Local validation/release packaging. |

Keep helper dependencies compatible with net48 and test support inside its existing test owners. Use `-f net48` in test commands; a newer SDK does not change the target runtime. Any future runtime reactivation must review the actual source/project/lock graph under a separately approved plan. BCI does not reactivate AppCore or upgrade the framework.

