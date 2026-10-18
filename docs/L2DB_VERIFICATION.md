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


## L2DB-T01 — net48 package and resource ownership

**Date:** 2026-10-18. **Implementation base:** `a32a48835274089ead5812cab9fc5bd7558d9265`, branch/worktree unchanged from T00. **Candidate code/config SHA256:** `c6751edbc223a67608b3a42b68be743d3507c552d5e90f46a171628c4462756d` (23 changed/new C#/csproj/central-props/lock files relative to the base, sorted path/NUL/raw CRLF bytes/NUL). The eventual T01 commit binds this candidate; evidence-only Markdown changes do not change its digest. The completed checkpoint below accepts this exact code/config candidate.

### Scope and ownership contract

Added internal `ILinqToDbSessionFactory`, `LinqToDbSessionFactory`, `ILinqToDbMapping` and `LinqToDbMappingSchemaProvider`. Explicit Microsoft DI registration is additive: the session factory and mapping composition are singleton services holding configuration/factory references only; every Borrow/OpenOwned operation creates a separate wrapper. No singleton connection/transaction, ambient storage, new production provider flag or hidden fallback connection exists. Existing production repositories, UoW state machine, RepoDb registrations and mappings stay unchanged. T02 may consume this seam in test-only pilots; production cutover remains gated by GO.

Borrow first reads the guarded UoW connection, then its **current** transaction on each operation. It requires an already-open Microsoft.Data.SqlClient connection and rejects a missing required transaction or a transaction from another connection before SQL. A repository can be resolved/read before BeginTransaction and borrow again after begin; completed/disposed UoWs fail before wrapper creation. Wrappers are operation-scoped: callers must materialize results and dispose them inside each operation, rather than retain a wrapper across UoW transitions or run concurrent commands on one UoW. Borrow does not own/close/commit/rollback the UoW resources. OpenOwned uses the existing connection factory; cancellation after open or construction failure disposes the acquired connection and preserves construction plus cleanup errors. Successful owned-wrapper disposal closes its connection.

The explicit SQL Server **v2012 dialect** with MicrosoftDataSqlClient avoids provider auto-detection (and its potential second connection). This conservative LINQ translation dialect does not change supported schema/server requirements; T02 must review emitted SQL/round trips before rollout. Default command timeout is finite (30 seconds), and query APIs receive caller cancellation. Mappings are composed on a private candidate under an instance lock and published only after every module and FluentMappingBuilder.Build succeeds. A failed composition is discarded; a later call retries. The published internal MappingSchema is used read-only by convention (the package type itself is mutable); no global mutable mapping registry or domain mapping attribute is introduced. No product entity mappings are registered until the owning pilot/module tasks.

### RED, diagnosis and focused outcomes

Tests were written first; nonworking signature-only contract shells throw NotImplementedException so the test assembly can execute its intended missing-feature assertions. Initial F `FullyQualifiedName~LinqToDb` result: **0 passed / 14 failed / 0 skipped**, all expected missing session/mapping/DI behavior; normal owned cleanup completed. These failures are not acceptance passes.

First implementation attempt: **8 passed / 6 failed / 0 skipped**. All six failures were the same transaction-binding defect: chaining UseConnection then UseTransaction retained DbConnection, which takes precedence over DbTransaction in package 6.3.0. Inspection of the package's pinned [DataOptionsExtensions source](https://github.com/linq2db/linq2db/blob/b8263fd60c12804ae874c555b2347defccec09fa/Source/LinqToDB/DataOptionsExtensions.cs) confirmed UseTransaction only sets the transaction option; the adapter now chooses mutually exclusive connection/transaction branches. [ConnectionOptions ownership reference](https://linq2db.github.io/api/linq2db/linq2db--LinqToDB.Data.ConnectionOptions.html) guided the disposal checks; runtime tests, not documentation alone, establish behavior. Same focused rerun: **14/0/0**. No package/runtime upgrade or weakened transaction assertion was needed.

Added a typed fluent-mapping write/query test with an independent native-SqlClient oracle and rollback assertion. Its first build failed because asynchronous LINQ methods require the `LinqToDB.Async` namespace; no tests executed in that compile attempt. Added the required import; final focused F result **15 passed / 0 failed / 0 skipped**. Final unfiltered X result **10/0/0**, including both new LinqToDbBoundaryTests. Cancellation, incomplete-reader disposal, borrowed query/construction failure, owned construction cleanup, independent-UoW session isolation, late transaction, terminal state rejection, provider validation, retry/concurrent mapping publication and actual typed mapping all pass. Existing exceptional-cleanup/caller-scoped-lifetime cases are retained and included in the unfiltered F checkpoint.

### Package, tooling, host assets and advisories

Pinned [linq2db 6.3.0](https://www.nuget.org/packages/linq2db/6.3.0) in central props, referenced only by Infrastructure. Ran deliberate `dotnet restore MyDmsVn.sln --force-evaluate`, reviewed the generated graph, then `dotnet restore MyDmsVn.sln --locked-mode`. Both succeeded on SDK 8.0.425/Windows; no TFM or vendor SHA changed. Eleven affected active locks add only linq2db; all existing package version/content hashes/dependency metadata are unchanged. Formatting-only lock edits were reverted; parked AppCore and unrelated locks remain unchanged.

Actual Infrastructure assets for `.NETFramework,Version=v4.8`: compile/runtime `lib/net462/linq2db.dll`. Package license: **MIT** from the installed nuspec. Net462 dependencies require Microsoft.Bcl.AsyncInterfaces >=8.0.0 and System.ValueTuple >=4.6.2; the existing resolved graph satisfies these without another added/updated package. SqlClient **7.0.2**, Respawn **4.0.0** and all previous pins remain intact. Inner/Application/Contracts/UI projects gain no direct package reference; architecture guards forbid linq2db there.

Locked restore and `dotnet build <project> -c Release -f net48 --no-restore` succeeded for both Desktop.App and Server.DbMigrator, **0 warnings / 0 errors** each, through actual initialized project/vendor references. Both outputs contain linq2db.dll, Microsoft.Data.SqlClient.dll and SNI native arm64/x64/x86 DLLs. Generated configs retain the Microsoft.Bcl.AsyncInterfaces redirect to 10.0.0.11; DbMigrator also has SqlClient redirect to assembly 7.0.0.0, while Desktop uses its exact SqlClient assembly reference without an added redirect. No new redirect was required by linq2db. These are build-output checks, not release packaging/startup/manual Local smoke; T07 still owns package validation.

`dotnet list src/MyDmsVn.Server.Infrastructure/MyDmsVn.Server.Infrastructure.csproj package --vulnerable --include-transitive --format json` completed. The queried advisory feed reports no linq2db advisory, but reports **existing transitive System.Security.Cryptography.Pkcs 8.0.1**: High [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx), Moderate [GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7). Its version/hash was already in the T00 graph and is unchanged. This is an outstanding dependency risk, not a clean security/release acceptance claim or authorization for unrelated upgrades. Raw audit JSON stays local.

### Frozen fixture and verification scope

All accepted pre-existing RESP C# files compare byte-equivalent to `a32a488`; no reset/seed/drain/ownership/exception/serial guard, production SQL/grant or original Fact/Theory identity changes. T01 necessarily extends the dependency graph as planned; the historical 37-file RESP digest includes locks/central pins and is therefore not represented as the digest of this new package candidate. The T00 binding remains the predecessor, with new results explicitly attributed to T01.

Focused F runs use dedicated positively owned fresh databases through the existing SqlTestDatabase helper; they do not change the reusable APP schema/profile. Cleanup errors aggregate with scenario errors; no uncontrolled hang, killed host, ambiguous deletion or quarantine recovery was introduced. One six-owner unfiltered sequential checkpoint is required here because the new common package/configuration is transitive across the SQL owners. This is a shared dependency checkpoint, not an automatic per-edit full suite or new fresh/reuse benchmark. Default mode is unset/blank reuse and both application/adapter fault-probe variables are absent. No caller environment setting is changed.

A combined PowerShell checkpoint command was rejected before execution by automatic approval review with the generic reason `blocked by policy`. Direct per-project dotnet commands with no environment mutation were used instead. No denied operation was executed and no SQL concurrency was introduced.

### Actual commands and final checkpoint

Each test command below ran with `-c Release -f net48 --no-restore`, after the successful locked solution restore. Commands are direct existing project invocations; source cases and TRX rows are reconciled below. Raw logs/TRX/allocation identifiers remain ignored/local.

```powershell
# RED: --filter 'FullyQualifiedName~LinqToDb', results TestResults/l2db-t01-red
# Intermediate retries: TestResults/l2db-t01-green1, green2, green3 (compile failure), green4
# Final focused:
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~LinqToDb' --logger 'trx;LogFileName=green.trx' --results-directory TestResults/l2db-t01-green4
dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=architecture.trx' --results-directory TestResults/l2db-t01-architecture-final
# Sequential unfiltered SQL checkpoint: substitute each owner below in this exact command.
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=cases.trx' --results-directory TestResults/l2db-t01-checkpoint-Foundation.Tests
```

One sequential unfiltered checkpoint completed on the frozen candidate, using the designated non-production connection and default reuse with probe variables absent. Result folders are `TestResults/l2db-t01-checkpoint-<owner>`; commands use the exact project corresponding to each owner and no filter, as above. TRX elapsed time is host start-to-finish (not a performance comparison or a dotnet process wall-time benchmark).

| Owner | Passed / failed / skipped | TRX elapsed seconds |
|---|---:|---:|
| F / Foundation.Tests | 59/0/0 | 301.81 |
| C / Catalog.Tests | 10/0/0 | 27.26 |
| I / Inventory.Tests | 36/0/0 | 136.89 |
| S / Security.Tests | 53/0/0 | 119.88 |
| M / Migrations.Tests | 65/0/0 | 179.11 |
| O / IntegrationTests | 57/0/0 | 29.99 |
| **Total six owners** | **280/0/0** | - |

Reconciled all **249 methods / 280 invocations**, with exact per-method Theory-row cardinalities and unique display names; the original **234 methods / 265 rows** remain, plus 15 single-row Foundation cases. No mandatory omissions, skips, aborts or duplicate results. X unfiltered independently passed **10/0/0**. The final 15-case focused F run is an overlapping diagnostic scope, not 15 additional unique acceptance cases. All 17 new F/X case IDs in the ledger executed successfully; no original provider-specific probe was removed.

All commands completed normally. Existing per-case dedicated ownership/drop assertions and awaited `SqlTestDatabase.DisposeAsync` cleanup succeeded; Catalog/Inventory/Security reuse collection diagnostics report successful guarded drops. No killed host or unresolved cleanup/quarantine allocation was reported. No global leftover census was performed for T01, and no prefix-based/orphan deletion or recovery was attempted. The intentionally quarantined Foundation probe retains its own known-worker drain and guarded cleanup contract. Full-owner passing results also retain post-drain verification/trigger/schema/permissions/ownership failures as real assertions, rather than replacing those safety cases.

A read-only independent reviewer found no actionable issue in ownership/disposal/current-transaction handling, mutually exclusive provider options, mapping publication/retry/concurrency, tests/boundaries or the lock diff. Existing package versions/content hashes are unchanged; only linq2db is added. Root independently reconciled the runtime evidence and unchanged fixture/UoW/SQL bytes. Final static checks cover diff whitespace, BOM/CRLF, links, source/project/TFM/row inventory and the exact frozen code digest.

**L2DB-T01 complete:** net48 package compatibility and borrowed/owned resource ownership are established for this scope. Production composition still uses RepoDb/Dapper; rollback belongs to the existing UoW. **Next task: L2DB-T02 three-slice pilot**, with no rollout before its explicit GO. Application/Session/GUI suites, release packaging and manual Local smoke were NOT RUN because T01 changed no application/UI behavior and requires host builds instead. Those remain scoped/final obligations; no T07, GUI, release or product-performance acceptance is implied. The existing Pkcs advisory risk above remains open.


## L2DB-T02 — three-slice pilot NO-GO

**Date:** 2026-10-18. **Base:** `e8ee6112b3bed98229a3e9b1d2f9b65e8403b77d`; same isolated `codex/l2db-t00` worktree. **Code/config candidate SHA256:** `46cec9a0d26fc20426fcaeb7e88e512ffd1cf12f831a8521ffbd5037f74c5d56` over eleven changed/new C#/csproj files, sorted path/NUL/raw CRLF bytes/NUL. The T02 commit binds this candidate and the decision below. **Decision: NO-GO.** Production RepoDb/Dapper composition remains unchanged; T03–T06 may not roll out until the observable error gap is explicitly resolved and the dependent pilot is rerun.

### Decisive behavior gap (L2DB-PILOT-01)

Current legacy `SqlSecurityAdministrationRepository.ReadTargetAsync` calls Dapper `ReadSingleAsync<ImportTargetRow>` for the first result. `009_Security_Permission_Import.sql` emits a target SELECT first and only then throws **51031** when no row exists (User and Role branches). On the real SQL Server/runtime-principal pilot, Dapper throws **InvalidOperationException: Sequence contains no elements** before consuming the subsequent SQL error. `SqlSecurityAdministrationStore.ReadTargetAsync` catches SqlException for the existing `Security.TargetNotFound` mapping, so that mapping is not reached. This is a **baseline reader/error-propagation defect**, not evidence of LINQ/net48 incompatibility.

The native candidate drains an empty first result set and receives SqlException 51031; the unchanged production store maps that to `Security.TargetNotFound`. This preserves the intended error mapping but changes the legacy observable behavior. The dedicated gap detector executes both on separately migrated/seeded owned databases and asserts their distinct outcomes. **All pilot tests passing does not establish GO**: the gap test passes precisely when the unresolved difference is observed. No safety assertion was deleted, weakened to equality or skipped. Independent review confirmed the source/error chain and the NO-GO decision under T02 and plan section 8.

**Ruling:** keep production baseline bytes and record NO-GO rather than silently fixing normal-path error semantics during a test-only pilot. The next action is an explicit decision on the intended missing-target result and a narrowly scoped baseline-reader repair (including deferred SQL-error propagation), followed by strict old/candidate parity and the dependent gate. Do not change SQL scripts/grants, package/runtime pins or swallow the error to obtain GO. This finding is recorded in the active L2DB work, not a third implementation plan or an authorized T05 rollout.

### Product slice

Temporary `ProductPilot` uses LINQ fluent mappings and explicit Value/Set allowlists for identity inserts and updates; created/audit/generated fields and IsActive cannot leak through generic updates. SetActive, missing-row behavior, duplicate code conflict, materialized Get/Lookup and inactive historical reads are tested against the existing repository/query service and independent parameterized native SQL. Inputs include literal `%`, `_`, `[`, Vietnamese text, null/empty/blank/trimmed search, stable order, page boundaries and PageNumber=int.MaxValue/PageSize=100 (**offset 214748364600**, Int64).

The list escape is an explicit **single native COUNT_BIG + page batch** through an owned T01 session, with unchanged CHARINDEX filtering and Code/ProductId order. It avoids separate count/page requests and the Int32 Skip limit. Both old and candidate actually report **one ServerRoundtrips** per list call; SqlClient DiagnosticListener observes exactly one command containing COUNT_BIG and bigint offset. Observed parameters: old Search NVarChar(0) for null and NVarChar(4000) for non-null, candidate explicit NVarChar(4000); both Bit, BigInt, Int. The harmless null-string size difference is explicit, not hidden. No search/offset/paging or materialization regression was observed in the tested slice. LINQ Get/Lookup filter/order/Take before materializing; Product write string mappings are NVarChar(32/256/32), audit timestamps DateTime2 and actor IDs Int32. Only SQL/parameter metadata is observed; values/identifiers and raw diagnostics stay local.

Old/candidate execute under separate unchanged Catalog reset/seed/pre/post-verification scopes. No fixture reset implementation or production SQL changes were needed. The pilot is deliberately a narrow Product adapter, not the T03 conversion of all catalog implementations.

### Receipt slice

LINQ writes receipt header/lines/ledger; the existing native lock-header/reference/post/atomic sorted-balance SQL remains in the production repository and is reused explicitly. Both providers run independently on dedicated fresh owned migrated databases (including the injected fault case). Assertions cover returned header/line IDs, generated eight-byte rowversion and server audit time, Decimal(18,4) quantity and Decimal(19,4) unit cost, sequence-generated receipt number, same SPID/current transaction, native visibility of uncommitted LINQ writes, committed Posted state/two ledger movements/two balances and exact quantities.

The injected native THROW occurs after the first ledger append and first balance delta, with those partial rows proven present; explicit UoW rollback leaves **zero header, line, ledger and balance rows** while reference seeds remain. No shared/ambient transaction, second participating connection, trigger rewrite or new posting algorithm is introduced. Server-generated/audit/rowversion fields are excluded by typed insert Value lists. Receipt insert and ledger/balance ServerRoundtrips observations are retained in local TRX output and summarized with final results below; they are not a product/suite-speed claim.

### Security slice

Each old/candidate execution provisions **database users WITHOUT LOGIN** inside its dedicated owned database, using the unchanged deployment principal script. Connections execute as the real restricted database runtime user; USER_NAME and SqlRuntimePrincipalValidator verify the identity/authority. Candidate procedure calls borrow the current T01 connection/transaction and execute native typed SqlCommands for the existing allowlist. No new permission evaluator, generic RBAC DML or expanded grant is added. The existing production store supplies error translation for both adapters.

ReadSecurityPermissionImportTarget materializes two result sets; ApplySecurityPermissionImport preserves TargetRevision/AddedCount/Changed, JSON and outer transaction ownership. Valid read/apply each require one observed ServerRoundtrips. Read parameters are NVarChar(8)/Int/Int; apply additionally uses BigInt expected revision, NVarChar(max) entries and UniqueIdentifier correlation. Before rollback, native administrative READUNCOMMITTED observation proves the audit row exists; after rollback there is no audit row, no added permission and revision returns to zero. Runtime generic Role/RolePermission writes and protected-binding UPDATE are rejected with **SQL 229**. Invalid request and revoked/missing actor translate to the same existing `Security.ImportInvalid` / `Auth.Forbidden`; missing-target differs as recorded above.

### RED/GREEN, resources and scope

Product first build exposed a linked-helper name conflict with IntegrationTests' friend-visible assembly; relocated only the new helper to `tests/Pilot`, without touching the accepted helper/fixtures. Executed Product RED **0/2/0** with nonworking pilot contract shells, then fixed the candidate identity API's nullable Int32 return at compile time; Product focused GREEN **2/0/0**. A later focused run adds actual command/parameter observations and again passes 2/0/0.

Receipt initial attempts were harness failures, **not valid feature RED**: unquoted reserved LineNo and a native @@SPID SmallInt-to-Int32 cast. Repaired native oracle quoting/type, discarded the premature implementation and reran the nonworking shell: intended Receipt RED **0/2/0**, then GREEN **2/0/0** with real old/candidate commit and rollback assertions. No original production test or schema was edited to fix the harness.

Security's first run **0/2/0** exposed the legacy missing-target gap as well as the unimplemented candidate. Split the gap into its explicit detector and retained ordinary invalid/actor translation assertions. Intended shell RED **0/3/0**, then focused GREEN **3/0/0**, with NO-GO still in force. Final scopes also include the strengthened pre-rollback audit observation, per-provider fresh allocations and command observations. Intermediate failures/build errors remain distinct from final outcomes.

Explicit designated non-production master configuration is present; default fixture mode unset/blank reuse, adapter/application probes absent. Project SQL commands execute sequentially; no caller environment mutation or external runner/manifest/lease/watchdog is introduced. All old fixture/C# safety tests, UoW/provider seam and production schema/grants remain byte-equivalent. No package reference, pin, lock asset, TFM or vendor revision changes; the footprint is the existing T01 linq2db 6.3.0 graph. Production diff consists only of test-assembly InternalsVisibleTo grants. The temporary prototypes/oracles/observers are seven C# files plus three Compile links and are subject to the T06 removal/retention obligations in the case ledger.

T02 final scopes are **F/FastSQL, C/FastSQL, I/FastSQL, S/SecurityProvisioning**, followed by unfiltered X. Expected current source rows: **49, 12, 24, 33** respectively = **118 SQL cases**, plus X **10**. The all-six/unfiltered T07 milestone and GUI/Application/Session suites are not inferred from this filtered matrix. No shared product provider/connection implementation changed, so M/O and unrelated GUI/repository suites are outside this task scope.

Package publication/startup/manual Local smoke is **NOT RUN** in T02 because the demonstrated semantic blocker already forbids GO/rollout. Prior T01 host builds are not relabeled as T02 package-startup evidence. Package startup must still be proven before any subsequent GO; T07 packaged/manual workflows and OPEN RELEASE-MANIFEST-01 remain open. Existing Pkcs advisory risk from T01 is unchanged. No package, performance or release acceptance is claimed.

### Direct commands and final outcomes

Executed `dotnet restore MyDmsVn.sln --locked-mode`; focused RED/GREEN commands use each actual owner project with `--filter FullyQualifiedName~<Product|Receipt|Security>PilotParityTests`, Release/net48/no-restore and distinct `TestResults/l2db-t02-...` TRX folders. Final commands use the exact four project/category pairs above, sequentially:

```powershell
# Substitute each existing owner and category from the scope table; no empty/new proposed selector.
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj -c Release -f net48 --no-restore --filter 'Category=FastSQL' --logger 'trx;LogFileName=cases.trx' --results-directory TestResults/l2db-t02-final-Foundation.Tests
# C: Catalog.Tests / Category=FastSQL
# I: Inventory.Tests / Category=FastSQL
# S: Security.Tests / Category=SecurityProvisioning
dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=cases.trx' --results-directory TestResults/l2db-t02-final-Architecture.Tests
```

The final sequential scoped SQL run completed with **118/0/0**; unfiltered Architecture then completed with **10/0/0**. Final TRX elapsed seconds are host start-to-finish observations, not comparative performance evidence.

| Owner / selected scope | Passed / failed / skipped | TRX elapsed seconds |
|---|---:|---:|
| F / Foundation.Tests | 49/0/0 | 298.33 |
| C / Catalog.Tests | 12/0/0 | 27.99 |
| I / Inventory.Tests | 24/0/0 | 106.93 |
| S / Security.Tests | 33/0/0 | 140.24 |
| X / Architecture.Tests | 10/0/0 | 4.42 |

Reconciled all **116 SQL methods / 118 invocations** and **10 Architecture methods / 10 invocations** against the ledger, including exact Theory-row cardinalities and unique result names. All seven new pilot Facts ran; no mandatory omission, skip, abort or duplicate result. These scoped results do not claim the unexecuted remainder of the 287-case six-owner source inventory.

Receipt observations: insert header/two lines/reload used **6 round trips old / 5 candidate**; each ledger append plus native balance delta used **2 old / 2 candidate**. Product list remained **1 / 1** and Security read/apply each **1 / 1**. The measurements cover only these calls, not application latency or suite speed.

All final commands completed normally. Dedicated ownership/cleanup assertions and awaited disposal succeeded; Catalog/Inventory/Security reusable collection diagnostics reported successful guarded drops. No unresolved cleanup/quarantine allocation or killed host was reported. No global leftover census, orphan recovery or prefix deletion was performed. Accepted fixtures and all pre-existing safety cases remain unchanged.

Independent source review found no new P1/P2 issue; its pre-rollback audit-observation improvement was implemented and passed. Review explicitly agreed NO-GO reflects a legacy observable error gap, not a LINQ compatibility rejection. Final static checks confirmed 636 unique ledger method IDs/source links, BOM/CRLF policy, unchanged pre-existing fixture/safety bytes, unchanged packages/locks/TFMs/production SQL and the frozen eleven-file code digest.

**L2DB-T02 complete as a documented NO-GO decision.** No production provider switch or flag was added. **Next action:** explicitly resolve L2DB-PILOT-01, repair the baseline reader narrowly, then prove strict old/candidate parity and package startup before reconsidering GO. T03–T06 remain blocked. Package startup, publication and manual Local smoke remain NOT RUN; no release acceptance is implied.


## L2DB-T02 blocker repair — L2DB-PILOT-01

**Date:** 2026-10-18. **Base:** `b1a8834e0e9645220da45c07046a0107abd532cc`, same isolated worktree. The owner explicitly requested resolving the blocker. **Semantic blocker RESOLVED; T02 GO is still withheld pending successful package startup.** The preceding NO-GO record describes its frozen pre-repair revision; its missing-target diagnosis is not the current behavior.

**Decision:** the existing `Security.TargetNotFound` / NotFound mapping is the intended missing-User/Role contract. Do not preserve an accidental Dapper `Sequence contains no elements` failure. `ReadSingleOrDefaultAsync<ImportTargetRow>` consumes the empty first grid and advances to the deferred SQL 51031; the existing store catches/maps that SqlException. A null guard still rejects an unexpected empty response with no SQL error. Single-row cardinality enforcement, cancellation, the second decisions result set and current connection/transaction ownership remain unchanged. No SQL script, grant, permission evaluator, provider registration, fixture/reset or package/TFM changes.

Two-file repair candidate SHA256 (same sorted path/NUL/raw bytes/NUL convention): `1a1a4732bdf3ee91d25c3779bc972cf9b159b0bcad2c6df8731c1d7163f41c1e`. The commit containing this record binds the reader and successor test bytes. The original gap-detector logical ID is retained with its explicit successor mapping in the case ledger; no coverage disappears or adds to totals.

### Repair verification

The successor test now requires strict old/candidate equality for missing User and Role: independent native absence checks, explicit actor permissions, Security.TargetNotFound and ErrorType.NotFound, then a successful read with both result sets. Each provider uses its own positively owned migrated/seeded disposable database; invalid/revoked actor, real restricted runtime-principal validation, SQL229 generic-write denials and outer import/audit rollback remain in the other pilot Facts.

After locked restore of the affected projects, direct `dotnet test` commands used Release/net48/no-restore and the existing projects/filters below. Raw logs/TRX remain local under `TestResults/l2db-t02-repair-*`.

| Project / selector | Passed / failed / skipped | Evidence |
|---|---:|---|
| S / successor missing-target method, pre-fix | 0/1/0 | Expected RED: legacy InvalidOperationException at ReadSingleAsync before mapped SQL51031 |
| S / FullyQualifiedName~SecurityPilotParityTests, repaired | 3/0/0 | Strict User/Role parity and all three pilots |
| S / Category=SecurityProvisioning | 33/0/0 | Complete affected category, including principal/import/audit/error boundaries |
| A / FullyQualifiedName~PermissionImportValidationTests | 8/0/0 | Existing Application parsing/reauthorization/import contract |
| X / unfiltered | 10/0/0 | Provider/architecture boundaries |

No skips, aborts or duplicate results. The focused three cases overlap the final 33 and are not additional unique acceptance cases. The 33 selected S rows retain exact ledger method/Theory cardinalities with the one successor mapping. Cleanup completed normally, reusable guarded drop succeeded and no unresolved allocation was reported. No global leftover census or orphan deletion was performed. C/I/F are unaffected by this local Security reader repair; their T02 evidence is retained as scoped predecessor evidence, not relabeled as a new run. No full SQL/GUI gate was run.

An independent read-only reviewer found no P1/P2 issue: the optional single-row read preserves cardinality, surfaces deferred SQL errors, and the null guard preserves the unexpected-empty invariant. Startup/package proof remains separate; source review and green SQL tests do not waive it. Existing Pkcs advisory risk and OPEN RELEASE-MANIFEST-01 are unchanged.

### Remaining startup gate

The process environment currently provides only the designated SQL-test administrative connection, not a least-privileged runtime connection. The package and bounded entry-point checks below are bound to clean repair commit `56830332ee6a69e122835920734497180723870b`. Do not grant GO using the test administrator as a runtime principal, bypass validation, create a server login or change server security to force startup. T03–T06 remain blocked until successful startup on an explicitly designated non-production database with a suitable runtime identity is evidenced. T07 attended business workflows and release acceptance remain separate obligations.


### Clean-commit validation package and bounded startup checks

Executed the existing publication script after all test commands finished, with no SkipPublish and a new empty output directory:

```powershell
$version = '0.0.0-l2db.56830332'
& ./deployment/release/Publish-LocalRelease.ps1 -Version $version -OutputRoot (Join-Path (Get-Location).Path ('artifacts/release/' + $version))
```

**PASS — publication/dependency validation:** script completed locked solution restore and net48/x86 Desktop/admin publish. Manifest sourceCommit is `56830332ee6a69e122835920734497180723870b`, sourceTreeDirty=false; all **224 file hashes/lengths** match (Desktop 142, admin 82). Both packages contain linq2db/SqlClient/native SNI assets and parseable executable configs. Manifest SHA256: `3f13c9d91245f9d6d547c36ec1c3efe22def55fb17d22bfd71009a88ca6d9b47`. These are local validation artifacts, not a published production release.

A temporary local .NET Framework probe used reflection only to invoke the unchanged test SqlTestDatabase helper, allocate one positively owned database on the designated non-production instance, and await its normal guarded disposal. It launched the actual package executables with **child-only** environment settings; no parent environment/configuration, production database, server login or grants were changed. Subprocesses have a 30-second wait, termination/drain on timeout and refusal to drop over an undrained process. No timeout occurred. Probe source/executable/config/logs remain local/ignored; this does not add a test runner or restore the retired framework.

| Actual package entry point | Observed outcome | Meaning |
|---|---|---|
| Admin DbMigrator, no arguments, child migration connection to the owned fresh database | Exit 0 / Migration.Completed | Packaged migration/native SQL dependencies work on the owned disposable database |
| Desktop App --smoke-test, child runtime connection to that same database using the only available test administrator | Exit 2 / Security.RuntimePrincipalOverprivileged | Entry point and native SQL loaded; existing least-privilege guard correctly refuses admin runtime. This is **not successful Desktop startup** |
| Existing SqlTestDatabase guarded disposal after both children exited | Completed normally | No unresolved allocation reported; no global leftover census/deletion performed |

Initial local probe setup had a source-path compile failure before allocation; corrected the compiler path and ran the probe above. The static artifact checker initially expected architecture `x86`; the actual documented manifest value is `win-x86`, and the corrected check passed. Neither setup issue changed product code or acceptance assertions.

**Remaining state:** L2DB-PILOT-01 is resolved by the committed baseline repair and strict parity. **T02 GO remains withheld / T03–T06 blocked** until Desktop startup succeeds on an explicitly designated disposable non-production database using a suitable least-privileged runtime identity. No such runtime configuration is currently provided. Do not count the expected privilege rejection as startup PASS. Attended login/catalog/receipt/import workflows are NOT RUN and remain T07 obligations. The package still emits schemaSupport 8 while runtime expects scripts 001–009; OPEN RELEASE-MANIFEST-01 remains an acknowledged release metadata blocker, not a waived or repaired result. Existing Pkcs advisory risk remains open.


## L2DB-T02 startup follow-up — runtime identity and schema manifest

**Base:** `a38f6f8bd19907e27bdc64880fed1b5210287b5e`, same isolated worktree, 2026-10-18. The owner requested continuing the startup blocker. No DAL conversion or accepted fixture change is included.

### Runtime identity boundary

A read-only query against the explicitly configured test master verified **contained database authentication disabled** and **SQL authentication available**. No server setting was changed. No least-privileged runtime connection is provided. A prepared local plan requests explicit owner authorization for exactly one temporary random-name/SID SQL login, with no server role or extra server grant, database-local runtime provisioning on a positively owned disposable database, bounded packaged startup, then exact server/name/SID/create_date verification before login removal. It leaves server authentication configuration and unrelated identities unchanged. The login has **not been created**; server-level mutation is outside AGENTS.md's database-only SQL-test rule without that explicit exception. Merely preparing/compiling a local probe does not authorize execution. T02 GO remains withheld pending successful real runtime startup.

### RELEASE-MANIFEST-01 source repair

The packaging script now obtains minimum/maximum schema rank and the ordered `requiredScripts` from **SchemaCompatibilityPolicy.CurrentScripts in the shipped admin Infrastructure assembly**. It no longer owns another hard-coded version8/P3.1 contract. Missing/empty policy rejects manifest generation. Package targets remain net48/x86; SQL scripts, runtime policy, providers, locks and grants are unchanged.

Independent review identified an x86 boundary in the initial implementation: the shipped assembly is 32BITREQUIRED whereas test dependencies build AnyCPU. The fixture now sets 32BITREQUIRED on its own copied policy DLL, matching the real publish shape. The final implementation reads the assembly in a hidden x86 Windows PowerShell subprocess, including when the outer publisher is 64-bit; a 30-second bound and termination/drain wait prevent an unbounded metadata helper. The helper emits only policy JSON, verifies 32-bit execution and suppresses progress records; errors still reject the manifest. No product startup/security validation is bypassed.

### Focused verification and review

After locked restore, executed only `MyDmsVn.Desktop.Tests` Release/net48 with `--filter FullyQualifiedName~ReleasePackagingTests` (non-GUI cases; no SQL). Existing STA/GUI suite is NOT RUN for this packaging-only change.

| Attempt / local results folder suffix | Passed / failed / skipped | Meaning |
|---|---:|---|
| metadata-red | 2/2/0 | Expected RED: manifest8 differs from runtime9; missing policy was accepted |
| metadata-green | 4/0/0 | Intermediate AnyCPU fixture only; insufficient x86 artifact evidence |
| x86-red | 3/1/0 | Review regression RED: BadImageFormatException when PS64 loads the x86 fixture |
| x86-green | 3/1/0 | Helper setup failed on encoded-command progress CLIXML emitted to stderr despite exit0; diagnosed via read-only isolated helper output |
| x86-green2 | 4/0/0 | Final x86 fixture: runtime-list equality, missing-policy rejection and both secret/admin rejection Theory rows |

Actual commands use `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~ReleasePackagingTests' --logger 'trx;LogFileName=<red|green>.trx' --results-directory TestResults/l2db-startup-<suffix>`. All four final rows passed without skip/abort/duplicates. Test fixture temp directories were disposed normally. No SQL was mutated by these cases. The new single-row rejection case is added to the stable case ledger; the original three invocations remain. Final source total is **637 logical method IDs**; no broader SQL-owner/runtime results are inferred.

Review's x86 finding was fixed through the observed RED/GREEN boundary above. Runtime identity permission remains pending; no login, role, server grant or global authentication setting was modified. Clean-commit real package publication/manifest reconciliation will be recorded below before closing RELEASE-MANIFEST-01. T07 attended workflows, operational/release acceptance and existing Pkcs advisory risk remain open.


Frozen packaging code/test SHA256 (sorted path/NUL/raw CRLF bytes/NUL over the two changed .ps1/.cs files): `76aef3bdf093fe94ae9222c66941a333fe00ab0ce05b9dd064506540b1845b60`. The containing source commit binds this package candidate; runtime SQL/application code, accepted fixtures, scripts/grants and package locks are unchanged.


### Clean-commit package reconciliation — RELEASE-MANIFEST-01 closed

Executed the actual release script, without SkipPublish, from clean source commit **`d80130300f39ccbf716a1c5df627d3550e2bd0a5`**, version `0.0.0-l2db.d8013030`, output `artifacts/release/0.0.0-l2db.d8013030`. The exact command follows the existing publish template above with this version/output. Locked solution restore and both net48/x86 publishes completed normally. The outer publisher was 64-bit and its x86 policy subprocess read the real 32BITREQUIRED Infrastructure.dll successfully.

**PASS:** manifest sourceCommit matches d8013030, sourceTreeDirty=false, minimum=maximum=**9**, ordered requiredScripts exactly match runtime's **001–009**, and stale P3.1 wording is absent. Recomputed all **224 SHA256/length pairs** (Desktop142/admin82), and independently checked the policy DLL in both packages has 32BITREQUIRED. Manifest SHA256: `46aecdeaf0f09a0dc7dc997472cc7a1066d54456b7946dfed2943c8f06f7d3ce`. This closes the narrowly scoped **RELEASE-MANIFEST-01 metadata discrepancy**; it does not grant release approval, certify operational SQL versions, signing, attended workflows or a successful runtime startup.

Only documentation changes follow this clean candidate, so the built code/script bytes remain identical. Historical package records with schema8 remain accurate for their predecessor commits. Existing SQL/application/architecture repair evidence remains scoped to its unchanged source; no SQL/GUI regression run was necessary for the metadata-only source change.

**Current next action:** explicit owner decision on the prepared one-login test-instance exception (or provision of an existing suitable runtime identity), then bounded Desktop startup on a positively owned disposable database, drain and identity-checked cleanup. Contained authentication remains disabled; no server authentication setting, login, server role or server grant has been changed. T02 remains NO-GO pending that startup proof; T03–T06 are still blocked. Temporary source/compiled probe and credential-free resource plan are local/ignored, not an additional framework or committed secret.


## L2DB-T02 final GO — approved runtime startup

**Date:** 2026-10-18. **Evidence/documentation base:** `3243f267ba64315c5e9fc54a4055f86dddeef8ce`. **Package/code revision:** `d80130300f39ccbf716a1c5df627d3550e2bd0a5`; subsequent commits change documentation only. **Final decision: GO for the T02 pilot gate.** This supersedes the earlier NO-GO/pending-startup status, not its historical findings. Production still uses RepoDb/Dapper; no T03 conversion or release acceptance is claimed.

### Explicit authorization and resource boundary

The owner explicitly replied **Cho phép** to the concrete one-login test-instance exception prepared in the previous turn. That authorizes exactly the prepared random-name/SID SQL login and its guarded cleanup, not general server security changes. The probe rechecked the prepared identity/SID input before execution, refused pre-existing identity collisions, generated the password only in memory and granted no server role or additional server permission. No credential was printed, persisted or placed on a command line.

The unchanged SqlTestDatabase helper allocated one positively owned fresh database on the explicitly designated non-production instance. Packaged DbMigrator migrated it. After ownership revalidation, setup created the runtime database user for the temporary login and a separate migration user WITHOUT LOGIN, then applied the unchanged deployment principal script. The existing administrator-only bootstrap engine enrolled/bound a test superuser only in this empty owned database. This is explicit test setup, not startup seeding or a product security change.

Runtime used SQL authentication as that restricted login, Pooling=false and the helper-owned database. The probe independently verified USER_NAME / ORIGINAL_LOGIN and ran the unchanged SqlRuntimePrincipalValidator before starting Desktop. Child-only environment configuration supplied the runtime connection and configured superuser username. The actual package entry point retained all production principal/schema/binding validation and rendered Login through its existing --smoke-test path.

### Actual outcomes

Before SQL mutation, recomputed all224 package hashes/lengths and rechecked clean source revision, schema9/ordered001–009, x86 policy DLLs and manifest SHA256 `46aecdeaf0f09a0dc7dc997472cc7a1066d54456b7946dfed2943c8f06f7d3ce`. The package was unchanged from the accepted metadata candidate. Recompiled the prepared local .NET Framework probe and executed it once with its explicit --execute-approved switch, the exact package directory, repository directory and approved identity. No competing SQL/GUI project command ran.

| Operation | Actual result |
|---|---|
| Packaged admin DbMigrator, no arguments, owned fresh database | **Exit0 / Migration.Completed** |
| Restricted runtime identity + unchanged SqlRuntimePrincipalValidator | **Passed**, no impersonation or validator bypass |
| Packaged Desktop.App.exe --smoke-test | **Exit0**, no stderr; principal/schema/binding checks and Login render completed |
| Child/process/connection drain | Normal exit; no timeout/kill; finite 30-second subprocess waits were not exhausted |
| Database cleanup | Unchanged helper guarded disposal completed normally |
| Login cleanup after database disposal/drain | Exact server/name/SID/create_date and no-active-session checks passed; DROP LOGIN completed and absence was rechecked |

The local sanitized probe log contains each success result; raw artifacts and allocation/login identifiers remain local/ignored. There is no unresolved owned allocation/login reported. No global leftover census, prefix cleanup, orphan adoption, server authentication toggle, unrelated identity change or caller-environment mutation was performed. The one-login exception is finished; the general database-only SQL-test rule remains intact.

### Gate reconciliation and limits

The accepted T02 evidence now consists of the original F49/C12/I24 filtered scopes (their code/fixtures are unchanged), repaired S33 strict parity scope, A8 import contract scope, X10 boundaries, W4 packaging rows, clean net48/x86 publication with matching schema metadata, and this successful real restricted-runtime package startup. Focused RED/GREEN runs overlap those scopes; startup is a separate subprocess scenario, not another xUnit Fact or an invented new pass total. No mandatory selected case was skipped. Source/ledger remains **637 logical method IDs**, with the original gap-detector ID explicitly mapped to its strict User/Role parity successor.

L2DB-PILOT-01 is resolved; RELEASE-MANIFEST-01 is closed narrowly; Product batching/long offsets/native escape, receipt shared-session/commit/rollback and Security procedure/result/error/permission/outer-rollback evidence have no remaining T02 blocker. Existing Pkcs advisories remain recorded dependency risks, not a new clean-security claim. T07's unfiltered final matrix, attended login/business/maintenance workflows, signing and P5/P6 operational release acceptance remain **NOT RUN / OPEN** as applicable. A rendered Login form does not prove those attended workflows.

**Next active task: L2DB-T03.** T03–T06 are no longer blocked by T02; each retains its own dependencies/tests. No T03 code was implemented in this approval turn. Keep the T06 pilot removal/native-oracle retention obligation; there is no production provider flag or permanent dual-provider fallback.

Local probe source SHA256 (audit binding only; source contains no credentials): `b31e8557b4204c05a99b753a0a87d6067bb0c05123b877b25575358166cbf02b`.


## L2DB-T03 — Catalog conversion

**Base:** `b6c38db6415b438320b8867cd4f8792c1d663862`, same isolated `codex/l2db-t00` worktree. **T03 complete** with the final scoped gate below. T02 final GO is the entry evidence; T03 does not redo the completed one-login startup exception or authorize another server login.

### Implementation and rulings

All four production Catalog write paths now use the internal T01 session factory, a per-operation borrowed wrapper and explicit typed Value/Set allowlists. Inserts return identities and exclude server creation time, identity and update audit; ordinary updates exclude creation audit and active state; SetActive touches only active/update audit. Missing updates remain null/false. Employee duplicate-code, unique UserId and SQL547 mappings retain the existing conflict categories. No catalog concurrency token or new business contract is introduced.

CatalogLinqToDbMapping is explicitly registered with Microsoft DI and maps dbo table/key aliases, identity, Unicode lengths, nullable columns and DateTime2/Int32/Boolean metadata. Standalone typed GetById/Lookup own their T01 wrapper, filter/order/Take before materializing and return only DTO arrays/results. Four native paged-list calls use the documented T02 escape: one COUNT_BIG + page batch, typed Search/Bit/BigInt/Int parameters, CHARINDEX and unchanged Code/key ordering with long offsets. Request values are parameterized; identifiers/projections are private constants. SQL shape/round trips are asserted independently for all four catalogs.

**Ruling:** retain the original RepoDb mapping Configure implementation under Persistence as a temporary bridge for unchanged Inventory reference reads, not as a Catalog command/query fallback. Inventory still loads Warehouse/Employee via RepoDb in the same UoW; removing those mappings before T04 would break uncommitted reference visibility. The bridge remains explicit in DI and is removed with its final consumers in T04/T06. Cost if wrong: reference loading during mixed-provider transactions would fail; the new interoperability/rollback case and I/FastSQL gate cover that risk. No UoW, session factory, Inventory algorithm, SQL/grant, schema, package pin/lock or TFM change is included.

The historical Catalog mapping-case method ID/name is retained because the accepted reuse adapter invokes that method directly. Its provider-specific body now uses registered LINQ mapping writes and independent native SQL identity/field/DateTime2/audit assertions; original behavior/rows are retained, with audit-user checks strengthened. Fixture reset/seed/ownership/drain/poison/admission/adapter bytes remain unchanged. The first creation-default case still has its pre-existing independent Dapper oracle; fixture/test oracle dependency cleanup remains T06, not a product fallback.

ProductPilot's constructor mechanically uses the new production signatures. After T03, its old/candidate labels mean production LINQ versus the temporary candidate prototype; they no longer represent a live RepoDb Product path. Its independently asserted native results, generated/audit/active/error/search/batch boundaries and logical IDs remain. T02's old-RepoDb comparison evidence stays historical. No permanent dual-provider production flag was added.

### Verification trajectory and selected scope

New migration cases cover typed mapping/native audit reads, Warehouse/Employee/Customer identity/nullables/create-update-active allowlists/errors, employee linkage insert/update/unlink, four-catalog literal Unicode/null/blank search/stable pages/lookup limits/Int64 offsets, late-Begin shared UoW with still-native/RepoDb receipt references and rollback, cancellation without write/transaction loss, and SQL rejection of overlong fields without silent truncation.

The first attempt failed compilation because the test lacked the LinqToDB.Data namespace for ExecuteAsync; it is not feature RED. After that harness import fix, the six initial new cases produced **5 passed / 1 failed / 0 skipped** on the old DAL: the registered LINQ mapping was absent. The existing behavior/oracle cases passed on the baseline. Conversion then produced **6/0/0**; the additional SQL-overlength boundary separately passed **1/0/0**. An exploratory 4001-character search failed inside the native CHARINDEX oracle before any candidate call; it was removed from the supported-input parity matrix. Application's existing MaximumSearchLength is256 and validation is covered by A/CatalogApplicationTests. This harness failure is not a provider regression or a hidden passing test.

An initial C/FastSQL checkpoint was18/1/0 solely because of that unsupported oracle input; after correcting the matrix, C passed19/0/0 and I/FastSQL passed24/0/0. These are intermediate results before the final mapping-metadata clarification. Independent read-only review found no P1/P2 behavior issue; extra EOF whitespace was removed and explicit nullability was checked against sys.columns before finalization. The nullability concern produced a real metadata RED (0/1/0 against sys.columns); explicit IsNullable(true)/IsNotNull annotations for every Catalog column resolved it. The final full required scopes below ran after that repair. No review finding remains deferred.

Required T03 scopes are C/FastSQL, I/FastSQL, A/CatalogApplicationTests and D's CatalogViewModelTests/CatalogLookupViewModelTests/GoodsReceiptProductOptionsViewModelTests. Focused F LINQ-session/mapping cases and unfiltered X also cover the explicit mapping-registration/provider seam without changing common ownership implementation. M/O/S/GUI/full-repository and final package/manual release workflows are outside this local Catalog change; no historical run is relabeled as current. Projects execute sequentially under the explicit designated SQL master configuration, default fixture reuse and absent fault probes; no environment mutation or retired framework is introduced.

Final source includes seven new single-row SQL Facts: all-owner IDs644; six SQL owners263 methods /294 invocations (194 SQL /100 non-SQL). These are source inventory totals, not this task's actual pass counts. Required methods/rows and final code are reconciled below before closing T03 checkboxes.


### Final T03 gate and retained native SQL

All commands used the actual net48 projects, Release, locked restore and no-restore test invocation. Each project completed before the next began; no SQL/GUI commands overlapped. Source-verified filters were C/I Category=FastSQL, F FullyQualifiedName~LinqToDb, unfiltered X, A FullyQualifiedName~CatalogApplicationTests and D the three ViewModel classes listed above. Results are in ignored/local `TestResults/l2db-t03-gate-<owner>/cases.trx` and corresponding local logs.

| Owner / selected scope | Passed / failed / skipped | TRX elapsed seconds |
|---|---:|---:|
| C / Catalog.Tests | 19/0/0 | 34.76 |
| I / Inventory.Tests | 24/0/0 | 105.83 |
| F / Foundation.Tests | 15/0/0 | 11.04 |
| X / Architecture.Tests | 10/0/0 | 4.30 |
| A / Application.Tests | 14/0/0 | 4.94 |
| D / Session.Tests | 43/0/0 | 3.77 |
| **Total selected scopes** | **125/0/0** | - |

Reconciled **123 methods /125 invocations** against the case ledger, including exact Theory-row cardinalities, unique test display names, no aborts and zero skipped mandatory cases. This is **54 SQL /71 non-SQL invocations**; F's15 includes four pure cases. Overlapping intermediate/RED/GREEN runs and nested reusable-fixture scenarios are not added to125. The original cases/rows remain, plus seven new Catalog Facts; the retained historical mapping Fact now proves LINQ/native boundaries rather than RepoDb adapter behavior.

The final mapping/native oracle asserts sys.columns nullability matches every mapped Catalog column, exact server-generated audit timestamps and no invented version property. Its RED showed implicit metadata differed from schema; explicit metadata then passed as part of C19. The overlong Unit/Address/Phone/TaxCode SQL case proves no silent truncation. Product's existing native oracle and full Catalog CRUD/Local error tests still pass. Actual Product/Warehouse/Employee/Customer list calls each retain exactly one observed COUNT_BIG/BigInt-offset batch; typed Get/Lookup filter/order/limit before materialization. Reference reads see uncommitted new Catalog rows through the same UoW/SPID and rollback leaves no participating rows.

| Retained native surface | Justification / ownership |
|---|---|
| SqlCatalogPageReader.ReadAsync, four Catalog ListAsync calls | T02-approved one COUNT_BIG+page batch; CHARINDEX literal search, long offsets and stable ordering. Owned standalone T01 wrapper; typed request parameters; no new grant/schema |
| Existing Inventory lock/reference/post/balance SQL | Unchanged algorithm and explicit current UoW transaction. RepoDb Warehouse/Employee reference mapping bridge remains temporarily under Persistence; T04/T06 own final conversion/removal |
| Temporary ProductPilot native list/oracles | Test-only prototype; T06 removal/independent-oracle retention obligations remain |

All final commands and guarded cleanup completed normally. Catalog/Inventory collection diagnostics report successful owned drops; dedicated fresh exception disposal and Foundation ownership/session assertions passed. No killed testhost, unresolved quarantine/cleanup allocation or external recovery was reported. No global leftover census or prefix/orphan deletion was attempted. Fixtures/SQL helpers/accepted adapter bytes and all reset/seed/drain boundaries are unchanged; only the explicitly migrated Catalog mapping-test body replaces a provider-specific probe. M/O/S, I/SqlConcurrency, full GUI, release packaging and attended workflows were NOT RUN: no shared UoW/session/SQL/security/fixture implementation or dependency graph changed, and those wider task/milestone obligations remain T04/T05/T06/T07. T02 startup evidence remains tied to its older package revision, not relabeled as T03 package acceptance.

The independent source reviewer found no P1/P2 business behavior issue. Both metadata/format notes were resolved; nullability is now independently schema-checked and passed final C19. The transitional bridge ruling is explicit above and its failure boundary is covered by the mixed-provider test plus I24. No unresolved review finding or unrecorded plan deviation remains.

**Frozen code manifest SHA256:** `10d1760537505f1a7bd600471214dff91844e3d76a7f9ce9c38f2fa7b4718c74`. Convention: twelve changed/new/deleted .cs paths, sorted repository-relative path/NUL/raw CRLF bytes/NUL; deleted paths contribute literal `<deleted>` instead of bytes, using git diff --no-renames. Ten files exist and two old Catalog files are deleted (the mapping is relocated under Persistence with its Configure body unchanged). The containing commit binds these bytes, final tests and evidence. No package/pin/lock/TFM, Domain/Application/Contracts, Inventory/Security algorithm, production SQL/grant, UoW/session implementation or fixture/helper changes. Static checks verify644 unique logical IDs, links, BOM/CRLF, diff whitespace, explicit DI and zero RepoDb/Dapper references in production Catalog.

**L2DB-T03 complete. Next active task: L2DB-T04.** Production Catalog commands and queries are now LINQ/native; Inventory, Identity/Security and operational consumers retain their previous implementations until their planned tasks. No permanent fallback/provider switch was added. T07's final unfiltered matrix, packaged/manual workflows and release acceptance remain open; existing Pkcs advisory risk is unchanged.


## L2DB-T04 — Inventory conversion

**Base:** `0a2e30e520471e56ec7aff7cc2f6d33e745d0ced`, isolated `codex/l2db-t00` worktree. The containing commit binds the code digest, tests and this evidence. **T04 complete** in the scoped gate below; the original v1a checkout is preserved. T05 is next. No push, merge or release acceptance is implied.

### Implementation and retained native SQL

Goods receipt reference loads (Warehouse/Employee), header/line/ledger inserts, draft allowlist updates and generated-token reloads now use registered Inventory/Catalog LINQ mappings through an operation-scoped borrowed T01 session. Each operation obtains the current UoW transaction; no transactional connection is opened independently. Identity/date/alias/nullability/string length/decimal metadata is explicit, including ANSI Status/DocumentType and server-generated RowVersion skipped on insert/update. Explicit Value/Set allowlists preserve server-created identity/audit/version, receipt number, status and posting audit during draft replacement. Draft editing still uses the existing Application header-lock/state/version check; TryMarkPosted retains its exact SQL Draft/RowVersion predicate. SQL duplicate errors and rollback remain intact.

InventoryRepoDbMapping and InventoryRepoDbWriteFields are removed. The T03 Catalog mapping bridge and its DI registration are removed because the final Inventory reference consumers now use LINQ; Foundation's independent custom probe mappings and generic legacy initializer/hooks remain for T06. No shared UoW/session/provider or fixture implementation changes. T00 found no genuine bulk consumer; inserts remain row-wise. No MERGE, C# balance read-modify-write or new stock-card running balance is introduced.

| Retained native surface | Reason and resource ownership |
|---|---|
| SqlGoodsReceiptWriteRepository.GetProductReferenceStatesAsync | Existing typed parameterized product batch on the current UoW transaction |
| LockHeaderAsync, LockPostSnapshotAsync, LockCatalogStateAsync, LockProductStatesAsync, LoadLinesAsync | Existing header -> Warehouse -> Employee -> sorted Products lock order and line ordering; current UoW |
| TryMarkPostedAsync, ApplyStockBalanceDeltasAsync, NextReceiptNumberValueAsync, ReplaceDraftAsync line delete | Exact conditional Draft/version transition, sorted atomic balance delta/unique-key retry, sequence and line replacement SQL; current UoW |
| SqlGoodsReceiptQueryService.ListAsync/GetByIdAsync | Existing multi-result count/header and complete aggregate reads; single factory-owned connection and SERIALIZABLE transaction per call. Page line IDs become explicit BigInt parameters in the same set-based query; readers close before commit |
| SqlInventoryQueryService.GetStockBalancesAsync/GetStockCardAsync | Preserve COUNT_BIG/page batches, CHARINDEX, long offset, movement rows and PostedAtUtc/LedgerId order, UTC DateTime2 boundaries and fallback document numbers; single standalone owned factory connection per call, same transaction behavior as baseline |
| SqlStockReconciliationQueryService.FindDifferencesAsync | Exact shared table-lock batches and FULL OUTER JOIN totals under one explicit SERIALIZABLE transaction, StockLedger -> StockBalances lock order |
| InventorySql command/count/row helpers | Small typed native reader layer for those batches, timeout30 and cancellation propagated; creates commands from the existing connection, never creates or owns another connection/transaction |

Static comparison against the base confirmed all receipt/inquiry/reconciliation SQL constants and native write lock/post/delta/sequence/load method sections are unchanged after EOL normalization. Request values remain parameterized; identifiers/projections are private constants. The native query services retain the existing IDbConnectionFactory ownership path, including the recording connection's isolation observer, rather than adding an unused LINQ wrapper around native-only batches.

### Tests and rulings

The registration test first failed **0 passed /1 failed /0 skipped** because InventoryLinqToDbMapping was missing. The new real-SQL mapping/audit/schema case separately failed **0/1/0** at the same missing-registration assertion. After conversion both passed **2/0/0**. Eight retained receipt/inquiry/reconciliation cases passed before and after query execution replacement (**8/0/0** each). The initial Inventory category checkpoint passed **34/0/0**, before the last two command cases and inquiry case were added.

Four new SQL cases are recorded in the [case ledger](L2DB_CASE_MIGRATION.md#t04-inventory-migration-and-retained-boundaries): mapping/sys.columns metadata, decimal maxima and generated/audit allowlists; duplicate-product SQL rejection with aggregate rollback and a consumed sequence value; controller-observed blocked line insert cancellation with drained rollback and a successful new scope; and native inquiry literal Unicode search, tied timestamps, signed minimum movements, document fallbacks, UTC ticks, long offsets, empty/missing results and pre-cancel for every native query service. All four passed in the final gate. Original mapping/registration IDs and the frozen fixture adapter's historical method name remain. ReceiptPilot now labels production LINQ versus its temporary T02 prototype; its native commit/rollback/SPID/decimal/token assertions remain until T06 cleanup.

**Ruling:** preserve the existing SqlClient/net48 cancellation contract rather than require a new public exception type. The first cancellation assertion wrongly required OperationCanceledException and failed; a diagnostic captured SqlException reporting user cancellation from InsertLineAsync, safely translated by the unchanged Application pipeline to InternalError. Existing Foundation cancellation already allows SqlException or OperationCanceledException. The corrected test pins observed blocking before cancellation, cancellation-specific SQL error0 (or propagated OperationCanceledException), no partial rows and successful fresh scope. Cost if wrong: an unrelated cancellation failure could be mistaken for rollback safety; native zero-row checks and the subsequent committed receipt cover that boundary. No production cancellation/error translation change was needed. The extra diagnostic assertion failed as expected during investigation; both command boundary cases then passed **2/0/0**. A first inquiry-test build used the wrong GoodsReceiptListRequest constructor argument order; correcting it yielded **1/0/0**. That compilation error is not behavioral RED or SQL acceptance.

Existing final cases retain stale update/post rejection, duplicate/concurrent posting, all reference deactivation lock orders, receipt update/post interleavings, missing-balance upsert races, partial-ledger failure and reconciliation during posting. Application cases retain decimal/duplicate validators and post-commit audit failure semantics: audit-sink failure does not falsely claim business rollback. Concurrency uses separate owned sessions inside dedicated fresh scenarios; test projects run sequentially.

### Final gate

Windows SDK8.0.425 / Release / net48; explicit designated non-production master configuration present, default fixture mode unset (reuse), unchanged accepted collection/exception boundaries. Each project was restored with locked mode, built and tested sequentially. Actual local command template:

```powershell
dotnet restore <project.csproj> --locked-mode
dotnet test <project.csproj> -c Release -f net48 --no-restore `
  --filter '<selector>' --logger 'trx;LogFileName=cases.trx' `
  --results-directory TestResults/l2db-t04-gate-<owner>
```

The script checked every restore/test exit and stopped on nonzero. I/X omitted --filter. Project paths and exact selectors:

| Owner | Project | Selector / reason |
|---|---|---|
| I | tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj | unfiltered; all FastSQL + SqlConcurrency and five pure invocations including explicit replacement mapping registration |
| C | tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj | Category=FastSQL; reference mappings/current-transaction interoperability |
| F | tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj | FullyQualifiedName~LinqToDb\|FullyQualifiedName~RepositoryAndQueryIntegrationTests\|FullyQualifiedName~PersistenceTransactionTests; sessions/mappings, explicit legacy test probes/hooks and rollback/cancellation after bridge removal |
| A | tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj | FullyQualifiedName~GoodsReceiptApplicationTests\|FullyQualifiedName~InventoryQueryApplicationTests; receipt/posting/inquiry handlers, validators and audit failure |
| X | tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj | unfiltered; provider boundaries |

| Owner | Pass/fail/skip invocations | Distinct methods | TRX wall seconds |
|---|---:|---:|---:|
| I | 42/0/0 | 40 | 125.28 |
| C | 19/0/0 | 17 | 36.59 |
| F | 23/0/0 | 23 | 16.88 |
| A | 40/0/0 | 30 | 4.65 |
| X | 10/0/0 | 10 | 4.53 |

Total **134 passed /0 failed /0 skipped**,120 distinct methods:74 SQL and60 pure invocations. Expected case IDs and every Theory cardinality match the source ledger and actual TRX TestMethods exactly; no duplicate result names, aborts, omitted or unexecuted cases. I has40 methods/42 invocations, including37 SQL; its FastSQL27 + SqlConcurrency10 cover the T04 SQL matrix. C has17 methods/19 SQL invocations. Source-wide totals648 logical method IDs and267 methods/298 invocations across the six SQL owners (198 SQL/100 non-SQL) are inventories, not this task's pass count.

All gates and guarded cleanup completed normally; Catalog/Inventory collection diagnostics report successful owned drops and dedicated fresh scenarios drained/disposed through their existing helpers. No failed cleanup, unresolved quarantine/allocation, killed testhost or leftover recovery was reported. No global census, prefix deletion or new SQL principal was attempted. Accepted fixture/helper/adapter/reset/seed/ownership/drain bytes, SQL scripts/grants, schema, packages/locks, TFMs and Domain/Application/Contracts are unchanged.

Fresh-context read-only review of the whole T04 change set found no Critical/Important/Minor issue and declined no behavior for judgment. The reviewer did not run competing tests/SQL. Static verification covers native-section equivalence,648 unique ledger IDs, relative links, BOM/CRLF, whitespace, explicit DI and zero RepoDb/Dapper in production Inventory/Catalog.

**Frozen code manifest SHA256:** `bdb258bf512cc1d9a9e35a5d038cb427fe3f4fbe58a7a3ca9b6f4a8b11035fc9`. Convention:15 changed/new/deleted .cs paths sorted repository-relative path/NUL/raw CRLF bytes/NUL; deleted paths contribute literal `<deleted>` (git diff --no-renames). Twelve files exist and three old mapping/write-field paths are deleted. The containing commit binds these bytes and the final evidence.

M/O/S full suites, full GUI, release packaging and attended workflows were **NOT RUN**: no shared UoW/session/provider/fixture/security/schema/package contract changed. Their T05/T06/T07 milestone obligations remain. Prior T02 package/startup evidence is not relabeled as T04 host acceptance. Existing transitive Pkcs advisory risk remains unchanged. **L2DB-T04 complete; next L2DB-T05.**


## L2DB-T05 — Identity Security and operations conversion

**Base:** `c8a1fb0f670e52c53a6bc63c082ed31d531642db`; isolated `codex/l2db-t00` worktree. The containing commit binds the frozen code digest, final gate and documentation. **T05 complete** for the scope below. Original v1a is preserved; next T06 removes transitional hooks/packages/pilots and reconciles the graph. T07 final release/package/manual acceptance remains open.

### Implementation and native surface

Seven remaining business/operational Dapper consumers now use explicit SqlClient parameters and typed readers. Every concatenated SQL string in those files compares unchanged against the base, including lock batches, credential CAS, schema/principal diagnostics and maintenance binding predicates. Security store/evaluator and SQL-error mapper source is unchanged. No new generic RBAC DML, permission grant, automatic provisioning, schema edit, credential flow or connection path is introduced.

| Native surface | Retention reason / ownership |
|---|---|
| Identity/SqlIdentityStore.FindByNormalizedUsernameAsync, TryReplacePasswordAsync, IsActiveAsync, GetSnapshotAsync | Existing account/credential/permission SQL, Unicode/hash/salt/algorithm/nullables and active-account CAS. One existing factory-owned connection per call; snapshot retains its SERIALIZABLE transaction and explicit binding/role/direct-decision SQL |
| Security/SqlSecurityAdministrationRepository.ListUsersAsync/ListRolesAsync | Existing ordered user/role/membership/decision/grant projections, materialized on current UoW connection/optional transaction; no extra connection or arbitrary discovery |
| Administration ApplyAsync | Same dbo.ApplySecurityAdministration allowlist, explicit nullable Bit/BigInt/Guid/text parameters, AssignmentRevision/Changed/nullable RelatedAssignmentRevision; current UoW |
| Import ReadTargetAsync/ApplyAsync | Same two procedures, TargetName/IsActive/AssignmentRevision/IsProtected plus ordinal decision dictionary, JSON nvarchar(max), one round trip each. Current UoW and unchanged mapped SQL errors, including deferred missing-target errors |
| Operations/SqlRuntimePrincipalValidator.ValidateConnectionAsync | Unchanged elevated-permission query and refusal; existing owned or borrowed startup connection |
| Operations/SqlSchemaCompatibilityChecker.CheckConnectionAsync | Same journal, artifact/column queries and policy evaluation, including unsupported/future/forked/partial refusal. Existing owned or caller-owned open connection; no automatic migration |
| Operations/SqlSuperuserBindingValidator.ValidateAsync | Exact configured/database-bound identity check; owns its existing startup connection |
| DbMigrator/SuperuserBootstrapEngine.BootstrapAsync | Unchanged administrator principal validation, SERIALIZABLE transaction, bounded application lock, empty-store refusal, account/binding atomicity and no grants |
| DbMigrator/SuperuserMaintenanceEngine.RecoverAsync/RenameAsync and lock/binding helpers | Same separate administrator authority, account/binding predicates and atomic update batches. Engines still own connections/transactions and validate principal before mutations |
| Persistence/SqlRead and DbMigrator/SecurityProvisioning/MaintenanceSql | Small internal command/result adapters, timeout30, typed DBNull/text/scalar parameters and cancellation. They open no connection, own no transaction and drain trailing result sets before reporting success. No reflection mapper or new repository framework |

Text bindings retain the prior Unicode4000/max behavior rather than silently truncating to a client-side schema length; import JSON is explicitly max. Nullable bits/revisions remain typed when DBNull. The existing sp_getapplock output variable is still consumed by the unchanged SELECT batch; no external output-parameter contract or binary-valued T05 result has been replaced. Existing Inventory rowversion/binary handling is untouched. Native-only consumers retain their existing factory/UoW connection paths; adding an unused LINQ wrapper would add no mapping guarantee.

**Ruling:** audit precision — bind all four credential/bootstrap/recovery/rename DateTime values explicitly as DateTime2, matching the existing datetime2(7) columns. The new identity oracle exposed baseline Dapper DateTime rounding from .1234567 to .1233333; preserve full ticks rather than reproduce that mapping defect. Cost if wrong: audit timestamps would differ from legacy rounded values. The independent SQL oracle now verifies exact ticks and maintenance identity/atomicity; no authentication/provisioning decision or SQL predicate changes.

**Ruling:** task boundary — T05's no-legacy-consumer exit applies to business/operational SQL execution. The empty RepoDb bootstrap/DI hooks, old package references, test-only independent oracles and T02 prototypes remain explicitly owned by T06. Removing them now would combine the separately planned dependency/test migration. Cost if wrong: claiming complete dependency cutover prematurely would hide old runtime assets; current guidance and the source boundary test make no such claim, and T06/T07 remain incomplete. No permanent fallback/provider switch exists.

### Verification trajectory and review

The new X source-consumer boundary first failed **0 passed/1 failed/0 skipped** on the seven legacy files, then passed **1/0/0** after conversion. The initial focused Security baseline (two new SQL cases plus three retained pilots) was **4/1/0**: only the exact DateTime2 audit assertion failed. The converted set passed **5/0/0**. Three initial new boundary cases then passed **3/0/0**, including bootstrap/recovery/rename precision and stable identity. Existing CLI maintenance cases were found to cover argument rejection, so a fourth SQL case adds wrong-bound-identity refusals, injected Users recovery failure and SystemSecuritySettings rename failure after the Users update, independent unchanged-state checks, and a successful subsequent rename. It uses a dedicated owned fresh trigger-fault database.

The first unfiltered Security checkpoint was **59/0/0** before that last test addition; it is not the final acceptance scope. The complete Security project was rebuilt/rerun with that case in the final60 run below. No production change followed the initial gate; M/O/A/D/X/host evidence remains current. All original IDs/Theory rows remain, including the prior explicit missing-target successor mapping. SecurityPilot labels now identify production native versus the temporary T02 adapter, retaining all three native-oracle/runtime-denial/rollback cases until T06.

Fresh-context read-only review found no Critical/Important/Minor finding; it checked the added maintenance-failure case separately as part of the same review. The reviewer ran no competing build/test/SQL. It left package/hook/pilot cleanup to the explicit T06 ruling above and execution/evidence acceptance to this executor; those are not silently accepted final-cutover or test claims.

### Final gate and commands

Windows / SDK8.0.425 / Release / net48; explicit designated non-production master configuration, default fixture mode unset (reuse). SQL projects ran sequentially using existing owned helpers/collections. No server login was created and the prior one-login T02 exception was not reused. Real restricted database principals in the existing Security cases verify runtime denials/allowlisted execution, not just administrative success.

Each project was restored with locked mode and every restore/test exit checked. Actual template:

```powershell
dotnet restore <project.csproj> --locked-mode
dotnet test <project.csproj> -c Release -f net48 --no-restore `
  --filter '<selector>' --logger 'trx;LogFileName=cases.trx' `
  --results-directory TestResults/l2db-t05-gate-<owner>
```

S/M/X omit --filter. Final S directory is `l2db-t05-gate-s-final`; the prior `l2db-t05-gate-s` is the59-case checkpoint. Other owner suffixes are m/o/a/d/x. Projects/selectors:

| Owner | Project | Selector / scope |
|---|---|---|
| S | tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj | unfiltered; SecurityProvisioning + SqlConcurrency and pure startup/configuration/credential cases, all new native boundaries |
| M | tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj | unfiltered; MigrationSchema plus pure CLI/maintenance configuration, supported-baseline and privilege regressions |
| O | tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj | Category=SecurityProvisioning; actual Local login/wrong password/logout/relogin/live authorization/account-disable expiry |
| A | tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj | FullyQualifiedName~AuthenticationTests\|FullyQualifiedName~PermissionAuthorizationTests\|FullyQualifiedName~PermissionCatalogTests\|FullyQualifiedName~PermissionImportValidationTests\|FullyQualifiedName~PermissionRegistryTests\|FullyQualifiedName~PermissionReleaseRegressionTests\|FullyQualifiedName~SecurityAdministrationTests\|FullyQualifiedName~SuperuserAuthorizationTests |
| D | tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj | FullyQualifiedName~DesktopSessionExpiryTests\|FullyQualifiedName~DesktopSessionProjectContractTests\|FullyQualifiedName~LoginViewModelTests; deterministic idle expiry/relogin/session contract |
| X | tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj | unfiltered; new consumer boundary plus inner/provider boundaries |

| Owner | Pass/fail/skip invocations | Distinct methods | TRX wall seconds |
|---|---:|---:|---:|
| S | 60/0/0 | 56 | 158.56 |
| M | 65/0/0 | 65 | 177.26 |
| O | 1/0/0 | 1 | 11.89 |
| A | 49/0/0 | 45 | 4.44 |
| D | 14/0/0 | 14 | 4.53 |
| X | 11/0/0 | 11 | 4.06 |

Total **200 passed/0 failed/0 skipped**,192 distinct methods:85 SQL and115 pure invocations. Source ledger/actual TRX TestMethods match every selected logical successor and Theory cardinality; no duplicate result names, omissions, aborts or unexecuted cases. S60 includes38 SQL/22 pure; M65 includes46 SQL/19 pure; O1 is SQL. Source-wide653 logical IDs and271 methods/302 invocations across the six SQL owners (202 SQL/100 non-SQL) are inventories, not these task results.

The required behavior cases retain current-account/binding validation; disabled/protected accounts; direct Deny/Allow/active-role precedence and unknown/deprecated key denial; next-request revocation; offline no-DB permission export; additive import identity/no-op/conflict, stale revision/digest/actor, limits and audit-failure rollback; credential/error redaction; real runtime-principal denial; migration/schema refusal; and current Local login/expiry. New native readers drain both empty and populated result sets before mapped success/commit. Maintenance fault probes verify no partial account/binding update.

Locked restore and `dotnet build src/MyDmsVn.Desktop.App -c Release -f net48 --no-restore` passed; the SQL projects also built the changed DbMigrator. This is source-host build evidence, not a packaged startup acceptance. Existing transitive Pkcs advisory risk is unchanged.

All final commands and guarded database disposal completed normally. Security collection diagnostics report successful owned drop; dedicated procedure/trigger/principal/schema/concurrency scenarios retain existing fresh ownership/drain boundaries. No cleanup failure, quarantine, killed testhost or unresolved allocation was reported. No prefix/orphan deletion or global census was attempted. Scripts/grants, RESP fixture/helpers/adapters, UoW/session/provider setup, package/lock/TFM and Domain/Application/Contracts source remain unchanged.

**Frozen code manifest SHA256:** `ec075a71c862ccbb65b8192ca4e6a587787fc6ce6cac4cbbb140df18583f8b55`. Twelve changed/new .cs paths, sorted repository-relative path/NUL/raw CRLF bytes/NUL; no deletion. The containing commit binds these tested bytes. Static checks confirm exact SQL literal/store/error-map equality,653 unique ledger IDs, links/encoding/whitespace and no legacy ORM consumer in converted production folders. Remaining RepoDb references are the explicit T06 bootstrap/plumbing, not a second business implementation.

Full F/C/I/O repository suites, GUI, package smoke and attended workflows were **NOT RUN** for T05: no Catalog/Inventory algorithms, shared connection/UoW/provider/fixture/schema/package contract changed. T06 dependency cleanup and T07 unfiltered six-owner/package/manual gates remain open. Prior package/login evidence remains bound to its older revision. **L2DB-T05 complete; next L2DB-T06.**
