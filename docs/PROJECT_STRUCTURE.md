# Current solution and project structure

**Assessment:** `v1a` at `c7ad32a4b0040f87417973eabd6c12757c54f7d5`, refreshed 2026-10-18. This is a map of the checked-in repository. The two [active plans](plans/README.md) describe future fixture and DAL changes; their proposed files are not listed as implemented components.

## Source projects

| Project under `src/` | Current target | Responsibility and dependencies |
|---|---|---|
| `MyDmsVn.Contracts` | `net48` | Immutable API results, DTOs and shared JSON conventions; independent of SQL/UI implementation. |
| `MyDmsVn.SharedKernel` | `net48` | Shared inner primitives. |
| `MyDmsVn.Server.Domain` | `net48` | Domain types; inner dependencies only. |
| `MyDmsVn.Server.Application` | `net48` | Feature use cases, validation, authorization, persistence interfaces and transport-neutral mapping. |
| `MyDmsVn.Server.Infrastructure` | `net48` | RepoDb/Dapper/native SqlClient implementation of inner interfaces; L2DB is the approved future DAL replacement. |
| `MyDmsVn.Server.DbMigrator` | `net48` | Controlled DbUp schema deployment and separate maintenance commands. |
| `MyDmsVn.Desktop.Application` | `net48` | Contracts-based client interfaces and ViewModels. |
| `MyDmsVn.Desktop.Infrastructure.Local` | `net48` | In-process Application dispatch, current-account checks and desktop session integration. |
| `MyDmsVn.Desktop.WinForms` | `net48` | Shared views and pinned BootstrapSourceGrid presentation dependency. |
| `MyDmsVn.Desktop.App` | `net48` | Current WinForms host and Local composition root. |
| `MyDmsVn.Desktop.AppCore` | `net8.0-windows`, parked | Retained source outside active solution/release; not current executable coverage. |

`MyDmsVn.Server.Api` and `MyDmsVn.Desktop.Infrastructure.Http` do not exist as current projects. They belong to deferred HTTP v2 work and need a separate runtime decision. Do not create them as part of RESP or L2DB.

## Test and tooling projects

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
| `MyDmsVn.Server.Infrastructure.IntegrationTests` | `net48` | Remaining Operations/Local SQL scenarios; this name no longer means the entire SQL suite. |
| `MyDmsVn.SqlTestHarness` | `netstandard2.0` | Shared test support library consumed by active net48 tests; not an independent execution TFM. |
| `MyDmsVn.SqlTestHarness.Tests` | `net48` | Harness contracts and safeguards. |
| `tools/MyDmsVn.SqlTestRunner` | tool-only `net8.0` | Guarded SQL execution, inventory/evidence and resource coordination. |
| `tools/MyDmsVn.SqlTestRunner.Tests` | tool-only `net8.0` | Runner tests; never product/SQL net8 coverage. |

There is no separate current `MyDmsVn.Contracts.Tests` project. Use the actual owning project and reviewed case identity from the manifest/source rather than a historical proposed path. Each SQL owner has its own csproj; a shared library or test trait does not by itself prove isolation or parallel safety.

## Actual feature directories

| Layer | Existing directories and anchors |
|---|---|
| Server.Application | `Catalog/`, `Inventory/`, `Identity/`, `Security/`, `Persistence/`; pipeline behaviors and result mapping at project root. |
| Server.Infrastructure | `Catalog/`, `Inventory/`, `Identity/`, `Security/`, `Operations/`, `Persistence/`; `DependencyInjection.cs`. |
| Application persistence contract | `src/MyDmsVn.Server.Application/Persistence/{IUnitOfWork,IUnitOfWorkFactory,UnitOfWorkState}.cs`. |
| Infrastructure persistence | `src/MyDmsVn.Server.Infrastructure/Persistence/{SqlUnitOfWorkFactory,ISqlExecutionContext,SqlConnectionFactory,SqlPersistenceBuilder,RepoDbMappingInitializer,IRepoDbMapping}.cs`. |
| Schema | `src/MyDmsVn.Server.DbMigrator/Scripts/001_PersistenceFoundation.sql` through `009_Security_Permission_Import.sql`; exact names in [PERSISTENCE](PERSISTENCE.md). |
| UI dependency | `vendor/MyDmsVn.BootstrapSourceGrid/`, pinned as checked in. |

`Application/Features/...`, `Application/Abstractions/Persistence` and Infrastructure `Persistence/Repositories`/`EntityMaps` were earlier blueprint examples. They are not the current file layout; use source-owned paths when changing code.

## Repository entry points

| Path | Role |
|---|---|
| `MyDmsVn.sln` | Current declared solution graph; parked AppCore is excluded. |
| `Directory.Build.props`, `Directory.Packages.props`, project lock files | Shared build settings, exact centrally managed packages and project dependency graphs. |
| `AGENTS.md`, `AI_CONTEXT.md`, `README.md` | Current development instructions and orientation. |
| `docs/plans/README.md` | The only active execution order: RESP, then L2DB. |
| `docs/TEST_POLICY.md` | Authoritative test scope and evidence policy. |
| `docs/TEST_STRATEGY.md` | Behavior coverage reference. |
| `docs/DEVELOPMENT_PLAN.md`, `docs/DEFERRED_WORK.md`, `docs/HISTORY.md` | Current status, deferred requirements and historical evidence. |
| `tests/test-gates.v1.json`, `tests/tools/` | Current executable manifest and guarded runner/wrappers; documentation alone does not alter their requirements. |
| `deployment/sql/Provision-MyDmsVnPrincipals.sql` | Explicit runtime/maintenance SQL authority separation. |
| `deployment/release/Publish-LocalRelease.ps1` | Existing net48 Local validation/release packaging. |

An assembly targeting `net8.0` cannot be referenced by the net48 product. Keep shared test library compatibility and explicit runner framework selection. Any future runtime reactivation must review the actual source/project/lock graph; neither active plan performs reactivation or a framework upgrade.
