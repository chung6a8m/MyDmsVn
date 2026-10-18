# L2DB verification — T00 baseline binding

**Date:** 2026-10-18. **Scope:** L2DB-T00 only, documentation/static source audit. **Implementation base:** `1e24df4c4ad5243963da82d808da19f12278242b` (merge PR #36). **Branch:** `codex/l2db-t00`. **Worktree:** `C:\Users\pdchung\.codex\worktrees\l2db-t00\CleanArchitecture`. Original `v1a` checkout was clean and is preserved. This record is committed with the T00 inventory; use `git log -- docs/L2DB_VERIFICATION.md` to resolve the evidence commit without a self-referential SHA. No L2DB package/provider code is installed.

## RESP prerequisite and exact binding

Accepted evidence: [PR 36 repaired lifecycle](RESPAWN_VERIFICATION.md#pr-36-review-follow-up), follow-up commit `a96eb7b425e76fc9d31b2a9bbc55ed63397b0b9a`, merged unchanged into `1e24df4c4ad5243963da82d808da19f12278242b`. `git diff a96eb7b425e76fc9d31b2a9bbc55ed63397b0b9a 1e24df4c4ad5243963da82d808da19f12278242b -- src tests deployment Directory.Packages.props` is empty. Thus this binds the accepted implementation bytes, rather than treating a documentation merge as runtime acceptance.

Recomputed code/config SHA256 **`75f282bc99c0c5d8f5bc04bcd2fe4c75f9f0c650d2ecd2a53203e1c46a79165c`** matches the repaired acceptance exactly. Reproduction: take sorted `git diff --name-only 6383289a2e43e2021abd65048a1e2f3f3838324e 1e24df4c4ad5243963da82d808da19f12278242b` paths ending in `.cs`, `.csproj`, `packages.lock.json`, `.props` or `xunit.runner.json`; for each of the **37** paths hash UTF-8 repository-relative path, NUL, raw CRLF working-tree bytes, NUL. Documentation is excluded. The accepted original worktree had uncommitted edits during testing; they are now bound to the follow-up and merge commits, with no missing local edit to transfer. No new SQL acceptance is claimed.

Historical accepted pair on these bytes: F 44, C 10, I 36, S 53, M 65, O 57 = **265 passed / 0 failed / 0 skipped in each mode**, including 169 SQL/96 non-SQL. Fresh/reuse exact identities and rows match. All participating 75 scenario admissions per mode retain reset/seed/pre-verify/body/cleanup/post-verify; fresh 75 allocations/drops versus reuse 3. Read-only before/after censuses found zero leftovers/attached sessions; the controlled quarantine probe explicitly drained its own known worker and used guarded cleanup. These are predecessor observations, not T00 reruns.

Freeze APP's 15 mutable tables, verified DbUp journal/ownership metadata, deterministic seeds, append-only StockLedger trigger restoration, pre/post-drain boundary checks, two-minute cooperative cleanup budget, terminal poison/quarantine and positive ownership/drop checks. Preserve default/blank reuse, explicit fresh, original dedicated lifecycle/schema/principal/trigger/contention allocations, lazy mixed-class fixtures and serial assembly/collection guards. Timeout quarantine forbids later reset/verify/drop and requires manual review. [RESP case/seed boundaries](RESPAWN_CASE_MAP.md) remain authoritative; no test fixture, assertion, direct command, package or schema is changed by T00.

## Workspace, tooling and drift

Starting source and new worktree were clean at the bound merge HEAD. Native Codex worktree creation used that explicit ref; branch was created locally. New worktree submodules initially were uninitialized; `git submodule update --init --recursive` succeeded at the pinned SHAs:

```text
09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f vendor/MyDmsVn.BootstrapSourceGrid (heads/main)
 aba102e33c48937fd92468791c287afda0a59e77 vendor/MyDmsVn.BootstrapSourceGrid/vendor/Bootstrap5WinFormUI (heads/main)
 f4e457b43582bf01892f50bdc74aa480531e5944 vendor/MyDmsVn.BootstrapSourceGrid/vendor/sourcegrid (heads/main)
```

`global.json` selects SDK 8.0.400, latestFeature roll-forward, prerelease false; `dotnet --version` returned **8.0.425** on Windows. Build/targeting-pack/runtime readiness is not inferred from SDK availability: restore/build, SQL and GUI were NOT RUN because T00 changes Markdown only. Later tasks must establish those resources for their gates. No connection configuration or secrets were printed. SQL readiness is not tested in T00.

Changes since refresh `a5ee124bed72f2994c4ee712e660b91d0dc2f953`: `6383289` removed the old external SQL framework (baseline simplification); `6bb77f2` implemented RESP; `a96eb7b` repaired final-boundary verification and cleanup quarantine; `1e24df4` merged PR #36. No product DAL/runtime migration occurred. Older documents saying neither plan is implemented describe their earlier refresh snapshot; the active roadmap and repaired RESP evidence supersede that historical wording. No conflict warrants changing delivered product behavior.

| Project | Declared TFM | State |
|---|---|---|
| `src/MyDmsVn.Contracts/MyDmsVn.Contracts.csproj` | `net48` | active |
| `src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj` | `net48` | active |
| `src/MyDmsVn.Desktop.AppCore/MyDmsVn.Desktop.AppCore.csproj` | `net8.0-windows` | PARKED |
| `src/MyDmsVn.Desktop.Application/MyDmsVn.Desktop.Application.csproj` | `net48` | active |
| `src/MyDmsVn.Desktop.Infrastructure.Local/MyDmsVn.Desktop.Infrastructure.Local.csproj` | `net48` | active |
| `src/MyDmsVn.Desktop.WinForms/MyDmsVn.Desktop.WinForms.csproj` | `net48` | active |
| `src/MyDmsVn.Server.Application/MyDmsVn.Server.Application.csproj` | `net48` | active |
| `src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj` | `net48` | active |
| `src/MyDmsVn.Server.Domain/MyDmsVn.Server.Domain.csproj` | `net48` | active |
| `src/MyDmsVn.Server.Infrastructure/MyDmsVn.Server.Infrastructure.csproj` | `net48` | active |
| `src/MyDmsVn.SharedKernel/MyDmsVn.SharedKernel.csproj` | `net48` | active |
| `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj` | `net48` | active |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` | `net48` | active |

All ten active test projects are net48. AppCore is the only parked modern product declaration and stays outside active solution/release. No historical deleted test framework is part of this inventory.

## Complete RepoDb/Dapper source and direct-reference inventory

Tracked source/test/deployment matches at the bound HEAD, including comments, reflection names and fixture helpers. Line numbers are navigation aids at this revision. No pilot exists yet; old documents and parked sources are context, not conversion work. Central versions: Dapper 2.1.79, RepoDb 1.16.0, RepoDb.SqlServer 1.16.1. RepoDb.SqlServer.BulkOperations 1.16.2 is an unused central pin (no active direct reference/lock asset). Candidate LINQ to DB 6.3.0 remains proposed and absent from current graph; T01 proves compatibility without upgrading unrelated packages. SqlClient 7.0.2 / Respawn 4.0.0 stay pinned.

| File | Matching lines | Classification | Owning task | Treatment |
|---|---|---|---|---|
| `Directory.Packages.props` | 9,26,27,28 | central pins | T06 | remove unused BulkOperations pin; remove other pins only after last consumer |
| `src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj` | 7 | direct package graph | T06 | remove old reference after all consumers converted |
| `src/MyDmsVn.Server.DbMigrator/SecurityProvisioning/SuperuserBootstrap.cs` | 5 | production | T05 | convert adapter; preserve procedure authority/error/transaction semantics |
| `src/MyDmsVn.Server.DbMigrator/SecurityProvisioning/SuperuserMaintenanceEngine.cs` | 5 | production | T05 | convert adapter; preserve procedure authority/error/transaction semantics |
| `src/MyDmsVn.Server.Infrastructure/Catalog/CatalogRepoDbMapping.cs` | 4,8 | production | T03 | convert queries/writes/mapping with explicit allowlists |
| `src/MyDmsVn.Server.Infrastructure/Catalog/CatalogRepoDbWriteFields.cs` | 3,7 | production | T03 | convert queries/writes/mapping with explicit allowlists |
| `src/MyDmsVn.Server.Infrastructure/Catalog/SqlCatalogWriteRepository.cs` | 10,27,35,55,62,66,69,80,85,89,92,134,153 | production | T03 | convert queries/writes/mapping with explicit allowlists |
| `src/MyDmsVn.Server.Infrastructure/Catalog/SqlOtherCatalogQueryServices.cs` | 6 | production | T03 | convert queries/writes/mapping with explicit allowlists |
| `src/MyDmsVn.Server.Infrastructure/Catalog/SqlProductQueryService.cs` | 6 | production | T03 | convert queries/writes/mapping with explicit allowlists |
| `src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs` | 52,54,64,74 | production shared composition | T01 + T06 | introduce sessions in T01; retire RepoDb hooks only at T06 |
| `src/MyDmsVn.Server.Infrastructure/Identity/SqlIdentityStore.cs` | 5 | production | T05 | convert adapter; preserve procedure authority/error/transaction semantics |
| `src/MyDmsVn.Server.Infrastructure/Inventory/InventoryRepoDbMapping.cs` | 4,8 | production | T04 | convert adapters; preserve locking/decimal/rowversion algorithms |
| `src/MyDmsVn.Server.Infrastructure/Inventory/InventoryRepoDbWriteFields.cs` | 3,7,9 | production | T04 | convert adapters; preserve locking/decimal/rowversion algorithms |
| `src/MyDmsVn.Server.Infrastructure/Inventory/SqlGoodsReceiptQueryService.cs` | 7 | production | T04 | convert adapters; preserve locking/decimal/rowversion algorithms |
| `src/MyDmsVn.Server.Infrastructure/Inventory/SqlGoodsReceiptWriteRepository.cs` | 14,136,144,242,287,309 | production | T04 | convert adapters; preserve locking/decimal/rowversion algorithms |
| `src/MyDmsVn.Server.Infrastructure/Inventory/SqlInventoryQueryService.cs` | 6 | production | T04 | convert adapters; preserve locking/decimal/rowversion algorithms |
| `src/MyDmsVn.Server.Infrastructure/Inventory/SqlStockReconciliationQueryService.cs` | 6 | production | T04 | convert adapters; preserve locking/decimal/rowversion algorithms |
| `src/MyDmsVn.Server.Infrastructure/MyDmsVn.Server.Infrastructure.csproj` | 7,10,11 | direct package graph | T06 | remove old reference after all consumers converted |
| `src/MyDmsVn.Server.Infrastructure/Operations/SqlRuntimePrincipalValidator.cs` | 4 | production | T05 | convert startup diagnostics without changing validation |
| `src/MyDmsVn.Server.Infrastructure/Operations/SqlSchemaCompatibilityChecker.cs` | 6 | production | T05 | convert startup diagnostics without changing validation |
| `src/MyDmsVn.Server.Infrastructure/Operations/SqlSuperuserBindingValidator.cs` | 4 | production | T05 | convert startup diagnostics without changing validation |
| `src/MyDmsVn.Server.Infrastructure/Persistence/IRepoDbMapping.cs` | 3 | production shared composition | T01 + T06 | introduce sessions in T01; retire RepoDb hooks only at T06 |
| `src/MyDmsVn.Server.Infrastructure/Persistence/RepoDbMappingInitializer.cs` | 4,8,12,14 | production shared composition | T01 + T06 | introduce sessions in T01; retire RepoDb hooks only at T06 |
| `src/MyDmsVn.Server.Infrastructure/Persistence/SqlPersistenceBuilder.cs` | 28,29,31 | production shared composition | T01 + T06 | introduce sessions in T01; retire RepoDb hooks only at T06 |
| `src/MyDmsVn.Server.Infrastructure/Persistence/SqlUnitOfWorkFactory.cs` | 16,30 | production shared composition | T01 + T06 | introduce sessions in T01; retire RepoDb hooks only at T06 |
| `src/MyDmsVn.Server.Infrastructure/Security/SqlSecurityAdministrationStore.cs` | 6,236,244,249,272,278,283,303,336,371 | production | T05 | convert adapter; preserve procedure authority/error/transaction semantics |
| `tests/MyDmsVn.Architecture.Tests/ArchitectureBoundaryTests.cs` | 54,55 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Architecture.Tests/ProjectReferencePolicy.cs` | 40,41,42,43,44,45 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/CatalogBackendIntegrationTests.cs` | 5 | test oracle/probe | T03 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/CatalogFixtureReuseIntegrationTests.cs` | 4,53 | test oracle/probe | T03 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/CatalogMappingIntegrationTests.cs` | 6,12,100,101 | test oracle/probe | T03 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/CatalogSqlCollectionFixture.cs` | 5 | frozen RESP helper/oracle | T06 review | freeze semantics; independent test-only dependency decision before cleanup |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj` | 12 | direct package graph | T06 | remove old reference after all consumers converted |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/SqlFixtureCollectionAdapterTests.cs` | 3 | test oracle/probe | T03 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/ConnectionAndUnitOfWorkTests.cs` | 5 | test oracle/probe | T01 + T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj` | 8 | direct package graph | T06 | remove old reference after all consumers converted |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/PersistenceTransactionTests.cs` | 5,11,106,171,284 | test oracle/probe | T01 + T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/RepositoryAndQueryIntegrationTests.cs` | 6,10,18,28,76,79,128,140 | test oracle/probe | T01 + T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/ReusableSqlFixtureIntegrationTests.cs` | 6,284 | test oracle/probe | T01 + T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/ReusableSqlFixtureLifecycleIntegrationTests.cs` | 8 | test oracle/probe | T01 + T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/DesktopLoginSessionIntegrationTests.cs` | 4 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/Fixtures/ApplicationSqlCollectionFixture.cs` | 5 | frozen RESP helper/oracle | T06 review | freeze semantics; independent test-only dependency decision before cleanup |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj` | 8 | direct package graph | T06 | remove old reference after all consumers converted |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlConcurrencyGate.cs` | 4 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlConcurrencyGateTests.cs` | 5 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabaseLifecycleTests.cs` | 3 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabaseMutationTests.cs` | 4 | test oracle/probe | T06 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/ApplicationDataFixtureReuseIntegrationTests.cs` | 34 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/GoodsReceiptCommandIntegrationTests.cs` | 9 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/GoodsReceiptMappingIntegrationTests.cs` | 7,12,31,32,149 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/GoodsReceiptModelRegistrationTests.cs` | 22,28 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/GoodsReceiptQueryIntegrationTests.cs` | 5 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/InventoryQueryIntegrationTests.cs` | 6 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/InventoryStorageIntegrationTests.cs` | 7 | test oracle/probe | T04 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj` | 11 | direct package graph | T06 | remove old reference after all consumers converted |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/DatabaseMigrationTests.cs` | 4 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/DatabaseMigratorCommandTests.cs` | 6 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj` | 8 | direct package graph | T06 | remove old reference after all consumers converted |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/SchemaCompatibilityIntegrationTests.cs` | 6 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/SecurityBaselineMigrationTests.cs` | 3 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/IdentityStoreIntegrationTests.cs` | 5 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` | 11 | direct package graph | T06 | remove old reference after all consumers converted |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/SecurityAdministrationSqlTests.cs` | 6 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/SqlPrincipalProvisioningTests.cs` | 4 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/SuperuserBootstrapTests.cs` | 5 | test oracle/probe | T05 | preserve all cases; replace provider-specific probes per R1 and retain independent oracle |

## Resolved package graph

Every tracked src/tests lock containing an old DAL package is listed, including transitive Desktop/DbMigrator/test consumers and parked AppCore. T06 removes active old assets only after source/test-oracle decisions; regenerate affected locks deliberately, then locked restore. A transitive test-only oracle dependency must not be mistaken for a remaining product dependency, and must not be silently deleted.

| Lock | Resolved assets and dependency type / target | State |
|---|---|---|
| `src/MyDmsVn.Desktop.App/packages.lock.json` | Dapper 2.1.79 CentralTransitive (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `src/MyDmsVn.Desktop.AppCore/packages.lock.json` | Dapper 2.1.79 CentralTransitive (net8.0-windows7.0); RepoDb 1.16.0 CentralTransitive (net8.0-windows7.0); RepoDb.SqlServer 1.16.1 CentralTransitive (net8.0-windows7.0) | parked |
| `src/MyDmsVn.Server.DbMigrator/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `src/MyDmsVn.Server.Infrastructure/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 Direct (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 Direct (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Architecture.Tests/packages.lock.json` | Dapper 2.1.79 CentralTransitive (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Desktop.Tests/packages.lock.json` | Dapper 2.1.79 CentralTransitive (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/packages.lock.json` | Dapper 2.1.79 Direct (.NETFramework,Version=v4.8); RepoDb 1.16.0 CentralTransitive (.NETFramework,Version=v4.8); RepoDb.SqlServer 1.16.1 CentralTransitive (.NETFramework,Version=v4.8) | active; T06 regenerate only after owning conversions |

## Immutable SQL/schema/grant baseline

SHA256 below uses exact CRLF working-tree bytes; compare on the same checkout EOL policy. It freezes all tracked production migration SQL and deployment grants. No script may become a test reset script. DbUp `DatabaseMigrationRunner` journals to `dbo.SchemaVersions`. `SchemaCompatibilityPolicy.CurrentScripts` in `Infrastructure/Operations/SchemaCompatibility.cs` requires the ordered scripts 001–009 below; preserve journal content/order/known-artifact checks. This is the supported source contract, not a live database schema census. No product database was opened in T00.

| Production SQL / grants | SHA256 |
|---|---|
| `deployment/sql/Provision-MyDmsVnPrincipals.sql` | `3ade280457d6344a87dbf4f7ed068fe221d60b97c7fd6f5201d5539ba782027d` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/001_PersistenceFoundation.sql` | `a25ae4925c08ff689fc6e949482f4f51c20093a30757139d493df1148f59f166` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/002_Identity.sql` | `a363f792b16231735e0c857f0fd6815de45f682258b9a8dd9b012f3aaaae8b09` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/003_Catalog.sql` | `b0e63dc655aa3b6358985915e9d5e9f205e3cf7955622b8b99bc8d6ab18b0feb` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/004_Catalog_Nul_Constraints.sql` | `4db1485dd5718140da51119351b7a1a4848009be523339f66c9d82b209a9696c` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/005_GoodsReceipt.sql` | `f26ff1d5489850e629f4a3a02f31d94d2073f48a516fbc571c24cbaa8663295a` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/006_GoodsReceipt_Number_Sequence.sql` | `4f5e422c70f9d2f9971ee6542c7a4e27d0c7978843982c949943b5302ad7d1e3` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/007_Inventory_Stock.sql` | `efab420deb7b29c9be88c9610cd7d84b5f251efc1baf266d557c39f5d34126ea` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/008_Security_Administration.sql` | `3df74bbb2d4ee8c3af15ed99fc2338a80287c6c44a1cee39b63229355c677da1` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/009_Security_Permission_Import.sql` | `e4dad6096ae13deda331fbd313af9c54f7dbbb314d8abb4a0d2a1f5dca4652c6` |

Known **OPEN RELEASE-MANIFEST-01**: packaging reports 8..8 while runtime supports complete 001–009 history. Track under [deferred work](DEFERRED_WORK.md#release-manifest-01--observed-schema-metadata-discrepancy); T07 owns a narrow repair if required for package acceptance. T00 does not change release metadata or waive the blocker.

## Native SQL retention review surface

These are explicit candidate retention decisions for conversion, not claims that a Dapper method already uses native SqlClient. T03–T06 must record final file/method, adapter and exact reason, with relevant test evidence. Provider consolidation must not rewrite SQL algorithms just to remove a library.

| File / methods | Task | Preserved SQL contract / reason |
|---|---|---|
| `Inventory/SqlGoodsReceiptWriteRepository.cs`: GetReferenceSnapshotAsync, LockHeaderAsync, LockPostSnapshotAsync, TryMarkPostedAsync, ApplyStockBalanceDeltasAsync | T04 | Same-UoW session/current transaction; lock hints/order, conditional Draft+rowversion transition, sequence/atomic sorted balance increments. Keep native lock SQL. |
| `Inventory/SqlGoodsReceiptWriteRepository.cs`: InsertDraftAsync, AppendStockLedgerAsync, ReplaceDraftAsync | T04 | Convert RepoDb writes but keep generated identity, audit allowlists, decimal precision, rowversion exclusion and rollback. |
| `Inventory/SqlStockReconciliationQueryService.cs`: FindDifferencesAsync | T04 | Explicit SERIALIZABLE transaction, ledger then balance table locks, independent mismatch projection. |
| `Catalog/SqlProductQueryService.cs`, `SqlOtherCatalogQueryServices.cs`: query/search/lookup methods | T03 | Materialized DTOs, literal escaping/Unicode, COUNT_BIG, long offset and multi-result round trips; justify a native path if translation loses batching/offset semantics. |
| `Identity/SqlIdentityStore.cs`: identity/permission reads and credential update | T05 | Legacy hash/salt verification, current account/binding, explicit-user precedence; no new grants. |
| `Security/SqlSecurityAdministrationStore.cs`: ListUsersAsync, ListRolesAsync, ApplyAsync, ReadTargetAsync (repository methods) | T05 | Allowlisted procedures, multi-results, outer UoW rollback, stale preview/revision/digest, SQL error mapping and atomic audit. |
| `DbMigrator/SecurityProvisioning/SuperuserMaintenanceEngine.cs`: maintenance operations | T05 | Separate administrator authority, bounded app locks and protected binding; no normal-path provisioning. |
| `Operations/SqlSchemaCompatibilityChecker.cs`, `SqlRuntimePrincipalValidator.cs`, `SqlSuperuserBindingValidator.cs`: validation methods | T05 | Read-only startup diagnostics/history/grant/binding validation, no automatic migration/seeding. |
| `Persistence/SqlUnitOfWorkFactory.cs`, `SqlConnectionFactory.cs` | T01/T06 | Native resource ownership/state machine; do not introduce another participating connection or cache pre-begin transaction. |

## Verification commands and source-resolved task scopes

All paths below are existing projects, all filters in the current-scope column resolve to source cases in [case ledger](L2DB_CASE_MIGRATION.md). For one method use the ledger's namespace plus Class.Method as an exact `FullyQualifiedName=...`; modular SQL owners retain namespace `MyDmsVn.Server.Infrastructure.IntegrationTests`. The class filters below are deliberately broader than one method when behavior is shared. New LinqToDbBoundaryTests, connection/mapping and parity selectors are **NOT AVAILABLE / NOT RUN** until T01/T02 add actual methods. No invented selector is considered passing.

| Alias | Existing project |
|---|---|
| F | `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj` |
| C | `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj` |
| I | `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj` |
| S | `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` |
| M | `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj` |
| O | `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj` |
| A | `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj` |
| X | `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj` |
| D | `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` |
| W | `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` |

Run from this worktree; substitute the exact existing project and filter from the following table. Commands stop on every native failure. These are reviewed templates, NOT executed tests in T00.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
$filter = 'Category=FastSQL'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore --filter $filter `
  --logger 'trx;LogFileName=cases.trx' `
  --results-directory ('TestResults/l2db-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
```

SQL commands additionally require explicit `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` for the designated non-production master. Keep the accepted default reuse or explicit reviewed mode; fault-probe settings unset. Invoke SQL owners sequentially. Missing configuration/mandatory skips, empty selection, aborted runs or cleanup errors are BLOCKED / NOT RUN or FAIL, never PASS. Inspect actual TRX rows and positive owned cleanup; preserve raw artifacts locally.

| Task | Current SQL scope | Existing non-SQL/build scope | New tests / escalation |
|---|---|---|---|
| T00 | None | Static inventory, paths/attributes/rows, hashes, links/encoding/diff | No runtime execution required |
| T01 | F `Category=FastSQL`; F `Category=SqlConcurrency` retains same-session contention boundary | F `FullyQualifiedName~UnitOfWorkExceptionalCleanupTests` (pure); RepositoryLifetimeTests is SQL in F/FastSQL; build Desktop.App and Server.DbMigrator net48 | New X boundaries/F sessions/mappings pending. Full every affected SQL owner for shared UoW/connection/mapping change; all six if shared contract affects all. |
| T02 | F/C/I `Category=FastSQL`; S `Category=SecurityProvisioning` | Existing X unfiltered; actual new pure parity cases pending | Three new SQL pilot selectors pending; do not proceed to T03–T06 without GO. |
| T03 | C `Category=FastSQL`; I `Category=FastSQL` for mixed catalog/reference paths | A/D class scopes listed below | New Catalog parity selectors pending |
| T04 | I `Category=FastSQL` and `Category=SqlConcurrency`; C `Category=FastSQL` | A receipt/post/inquiry class scopes below | New rollback/parity selectors pending |
| T05 | S `Category=SecurityProvisioning` and `Category=SqlConcurrency`; M `Category=MigrationSchema`; O `Category=SecurityProvisioning` | A authorization/import and D session scopes below; pure M/S cases identified in ledger | New security parity selectors pending |
| T06 | F `Category=FastSQL` plus F `Category=SqlConcurrency`; all six when shared contracts affected | X unfiltered; pure F cleanup/init source cases; F lifetime remains SQL | Remove last active old package assets only after consumer/fixture-oracle review |
| T07 | Unfiltered F, C, I, S, M, O, sequential, frozen candidate | Unfiltered A, X, D; Desktop.App/DbMigrator builds; local package/manual smoke | W unfiltered only for documented UI/packaged regression trigger; no empty selector acceptance |

- T03 / A: `FullyQualifiedName~CatalogApplicationTests|FullyQualifiedName~PermissionCatalogTests`
- T03 / D: `FullyQualifiedName~CatalogLookupViewModelTests|FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~GoodsReceiptProductOptionsViewModelTests|FullyQualifiedName~PermissionCatalogClientTests`
- T04 / A: `FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~InventoryQueryApplicationTests`
- T05 / A: `FullyQualifiedName~AuthenticationTests|FullyQualifiedName~PermissionAuthorizationTests|FullyQualifiedName~PermissionCatalogTests|FullyQualifiedName~PermissionImportValidationTests|FullyQualifiedName~PermissionRegistryTests|FullyQualifiedName~PermissionReleaseRegressionTests|FullyQualifiedName~SecurityAdministrationTests|FullyQualifiedName~SuperuserAuthorizationTests`
- T05 / D: `FullyQualifiedName~DesktopSessionExpiryTests|FullyQualifiedName~DesktopSessionProjectContractTests|FullyQualifiedName~LoginViewModelTests`


For T07 use the unfiltered six-owner loop in [tests/README](../tests/README.md#complete-sql-milestone), followed by unfiltered A/X/D in plan section 7. Restore each locked project before test/build; omit the `--filter` argument entirely for unfiltered checks. Build `src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj` and `src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj` with `dotnet build <project> -c Release -f net48 --no-restore` after locked restore, including initialized vendor graph. Package with `deployment/release/Publish-LocalRelease.ps1` exactly as plan section 7, never SkipPublish/overwrite. Required manual Local workflows: login, Product CRUD/lookup refresh, receipt draft/post/inquiry, permission deny, explicit assignment import, logout/relogin; maintenance uses separate authority. Record package hashes/native SqlClient assets/binding redirects and unchanged SQL/history/grants independently of automated totals. These remain T07 obligations.

## Source/category corrections

`RepositoryLifetimeTests.Repository_uses_caller_scoped_context_without_taking_ownership_of_it` is a SqlServerFact, not pure code: it creates an owned disposable database. It remains in F/FastSQL and must run with SQL resources. The original section-6 phrase "pure F lifetime" is corrected in the plan; exceptional-cleanup and mapping-initialization cases remain pure. This changes only scope documentation, not test categories or behavior.

Operations runtime consumers belong to **T05**, whose exit requires no active production RepoDb/Dapper consumers. T06 owns removal of obsolete hooks/packages and reconciliation, not delayed conversion of startup validators. The inventory/native retention task ownership follows that existing T05 contract.

## Independent review

A fresh-context read-only reviewer independently checked source/lock inventory, 612 method IDs, InlineData identities, SQL/pure categories, SQL hashes, the 37-file RESP digest and the repair-to-merge comparison. One P2 finding assigned Operations conversion to T06 instead of the existing T05 exit; all three inventory rows and the native-surface row now assign it to T05, verified statically. No other actionable findings or deferred review items were reported.

## T00 actual outcomes and limitations

Executed: `git status --porcelain=v1`, `git rev-parse HEAD`, `git log -5 --oneline`, native explicit-ref worktree creation, `git switch -c codex/l2db-t00`, `git submodule update --init --recursive`, `git submodule status --recursive`, `dotnet --version`, `rg -n 'RepoDb|Dapper' src tests deployment Directory.Packages.props` with source/project/lock checks, test-attribute/InlineData source enumeration, RESP digest recomputation, production SQL SHA256 and repair-to-merge byte comparison. All bindings and counts above were derived from current checked-in source; not historical totals alone.

Static final checks: diff whitespace, Markdown UTF-8 without BOM/CRLF, relative links/files, project targets, every declared category/class selector nonempty against source, six-owner counts/rows, inventory completeness and unchanged executable/SQL/fixture bytes. Actual static results: **PASS** for all 6 changed Markdown files (UTF-8 without BOM, CRLF, local file links); 612 unique case IDs; 234/265 six-owner methods/rows with 169 SQL; all 10 production SQL/grant hashes; 19 existing A/D class filters and 9 SQL category scopes; all 10 net48 owner paths; complete 77-file source/reference/lock inventory; unchanged executable/fixture/package/SQL bytes. `python "$env:TEMP/l2db_t00_check.py"` performed these local source checks; `git diff --check` and staged whitespace checks completed with exit 0. The temporary audit script is local tooling only, not a repository runner or acceptance registry. Test execution **NOT RUN: 0 executed, no new pass/fail/skip totals**. Discovery/build/restore/SQL/GUI/package smoke **NOT RUN**, intentionally outside this documentation task. No database allocated, reset or dropped; cleanup not applicable. Accepted predecessor evidence stays attributed to PR #36. Next active task is **L2DB-T01**; its first action is compatibility/ownership tests, not production composition cutover.
