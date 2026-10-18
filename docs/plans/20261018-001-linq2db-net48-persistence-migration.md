# L2DB — LINQ to DB persistence migration on active net48

> **For agentic workers:** Use `superpowers:executing-plans` or `superpowers:subagent-driven-development` to implement this plan task by task after review. Checkboxes describe work still required, not evidence of completion.

**Date:** 2026-10-18
**Status:** Owner-approved direction 1A + 2A + 3A; implementation proposed; all T00–T07 tasks NOT RUN. Execution is blocked until the preceding RESP plan has an accepted net48 baseline.
**Goal:** Replace the active RepoDb/Dapper application DAL with LINQ to DB and a documented native-SqlClient escape surface, preserving Local v1, active net48, post-rebuild security, existing data and all correctness gates.
**Architecture:** Existing Clean Architecture modular monolith and explicit UoW. Infrastructure-only mapping/connection adapters; unchanged Application/Contracts/Desktop boundaries; DbUp remains the sole schema owner.
**Tech stack:** WinForms, CommunityToolkit.Mvvm, net48 product/SQL/Desktop, tool-only net8.0 runner, LINQ to DB (initial candidate 6.3.0), current Microsoft.Data.SqlClient, SQL Server, DbUp, MediatR, FluentValidation, ErrorOr, Serilog and xUnit.
**Spec:** [ADR 0009](../adr/0009-linq2db-local-net48-persistence.md).
**Original assessment:** `main` at `8cfa25351338666b1a751ee516d5a479cdb37b7f` after PR #35; historical source assessment only.
**Documentation refresh base:** `v1a` at `c7ad32a4b0040f87417973eabd6c12757c54f7d5`, 2026-10-18.
**Predecessor:** [RESP — fixture reuse with Respawn](20261018-002-respawn-fixture-reuse.md), which must be verified on the current RepoDb/Dapper DAL before L2DB-T00.

## 1. Current baseline, execution order and authority

The [active roadmap](README.md) contains exactly two plans: **RESP, then L2DB**. RESP first proves its package and reused-fixture/reset design on active net48 with the current RepoDb/Dapper product. L2DB-T00 consumes that accepted commit and evidence. During DAL migration, freeze the accepted fixture boundaries, reset semantics and runner protocol so persistence changes have a stable comparison base. A harness defect blocks the dependent DAL gate until fixed and accepted explicitly; do not silently combine another fixture redesign with a provider conversion.

| Area | Current source at the refresh base | Treatment |
|---|---|---|
| Runtime | Product/SQL/Desktop actively target net48. Runner and runner tests execute on tool-only net8.0; shared SqlTestHarness is netstandard2.0. AppCore and historical modern product cases are parked. | Preserve declarations, packaging boundaries and PARKED / NOT RUN identities. |
| Security | The permission rebuild is implemented: code-defined catalog, protected configured/database-bound superuser, explicit assignments, current-subject checks and allowlisted procedures. | Reuse current behavior; no new security reset, synchronization or auto-grants. |
| Test infrastructure | Modular SQL owners, guarded runners, metadata preflight, source/discovery/TRX checks, ownership journals and workspace/resource leases exist. Fixture reuse is still proposed at this documentation base. | RESP establishes the accepted harness first. Preserve its supported executable checks during L2DB. |
| DAL | Catalog/receipt RepoDb operations, Dapper queries/security consumers and native SqlClient SQL remain active. | Replace inventoried consumers while preserving native locking/procedure semantics where justified. |
| UoW | SqlUnitOfWorkFactory owns an open connection and guarded current optional transaction; repository factories receive ISqlExecutionContext. | Borrow current resources per operation without taking ownership or caching a pre-begin transaction. |
| Delivery backlog | Remaining P5 interactive/P6 operational acceptance, HTTP v2/P7, modern product runtime and unrelated runner redesign are deferred. | These are not active prerequisites, completed gates or waived obligations. Re-plan them separately when requested. |

Read AGENTS, this plan and [RESP](20261018-002-respawn-fixture-reuse.md), [ADR 0009](../adr/0009-linq2db-local-net48-persistence.md), current PRD/ARCHITECTURE/TECH_STACK/PERSISTENCE/SECURITY/CONTRACTS, [TEST_POLICY](../TEST_POLICY.md) and the accepted ownership/security/runtime ADRs. [TEST_STRATEGY](../TEST_STRATEGY.md) is a coverage reference. [HISTORY](../HISTORY.md) records completed/superseded work; it is not an execution queue.

This is a documentation/source assessment, not fresh restore/build/SQL/GUI/benchmark acceptance. Historical pass counts, SQL timing and a waived duration comparison are not candidate evidence or proof of a LINQ-to-DB speedup. Existing old PlanId records in executable manifests remain transition tooling data; this docs refresh neither executes them nor changes tooling.

ADR 0009 replaces only the fixed DAL-library choice for this scope. Keep Local v1, active net48, UoW ownership, DbUp/data safety, current security and observable business behavior. T00 re-resolves the actual post-RESP implementation HEAD and reconciles drift without discarding local work. The final L2DB milestone hands remaining delivery work back to deferred backlog, without scheduling or waiving it.

## 2. Global constraints

- Keep product, SQL and Desktop **net48**; keep existing runner/test tool **net8.0**. Leave AppCore outside solution/release and historical modern cases PARKED / NOT RUN. No net8 reactivation or net10 upgrade.
- Keep Local v1 -> HTTP v2 and identical DTO/result semantics. No API host, new auth stack, UI replacement, new business module, generic repository framework or arbitrary service locator.
- Keep existing UoW state/ownership, explicit transactions, scoped registrations and cancellation. No concurrent use of one UoW, ambient/static/AsyncLocal transaction or hidden extra connection for participating work.
- LINQ to DB types/mappings stay in Infrastructure. Materialize DTO results before disposal; no IQueryable, provider objects or mapping attributes in inner/UI/contract layers.
- Preserve post-SEC-REBUILD authorization, maintenance/runtime separation, legacy password columns, import limits/revision/digest rules and atomic audit. Do not broaden SQL grants to accommodate an ORM.
- Preserve the supported current schema, DbUp scripts/history and data. This plan does not authorize schema changes, reset, seed, destructive migration or replay of the pre-rebuild permission baseline.
- Pin and verify `linq2db` 6.3.0 as an initial compatibility candidate; keep other package pins unless a reviewed compatibility blocker requires a separate decision. Do not claim the candidate is newest or already restored. Track all affected lock files, native dependencies and net48 binding redirects.
- Require accepted RESP net48 evidence before T00. Freeze its fixture ownership boundaries, reset semantics, fresh-database exceptions, serialization and runner protocol during L2DB. Keep executable approval/manifest/identity/lease/watchdog checks; no additional reuse/reset redesign, runner fingerprint-v2 rollout, container requirement or CI expansion.
- Follow [TEST_POLICY](../TEST_POLICY.md): focused affected tests per task, complete SQL coverage for shared UoW/harness/migration changes and the T07 milestone, and GUI only for changed UI behavior or this plan's packaged smoke. No automatic full repository/GUI gate for each code PR and no mandatory multi-pass benchmark.
- Retain independent SQL test oracles and historical logical-case metadata. Do not bless changed fingerprints automatically or reduce floors/counts to make a gate pass.
- Use UTF-8 Markdown, Git-normalized LF blobs and repository CRLF working-tree policy. Preserve existing C# BOM policy, vendor/submodule pins and secrets redaction.

### Review focus

1. Repository resolved/read before BeginTransaction, then written after begin: use the **new current** transaction; reject use after completion (T01/T02).
2. Borrowed wrapper failure/disposal/cancellation or owned-wrapper construction failure: never prematurely end UoW resources and never leak owned resources (T01).
3. Literal `%`, `_`, `[`, Unicode, blank search, very large offsets, SQL-generated/audit fields and no-match updates: preserve observable results and safe SQL mapping (T02/T03).
4. Concurrent/duplicate posting, mid-ledger failure and reconciliation during posting: preserve status/ledger/balance atomicity, lock ordering and decimals (T02/T04).
5. Least-privileged procedures, stale actor/import preview, protected-account edits and SQL error mapping: no unauthorized writes, stale grants or partial audit (T02/T05).

## 3. Verified source map and proposed files

Paths below are relative to the repository root. Existing anchors were read/listed at the assessment SHA. New names are proposed, not claimed to exist.

| Area | Existing anchors | Planned treatment |
|---|---|---|
| Package/runtime | `Directory.Packages.props`; `src/MyDmsVn.Server.Infrastructure/MyDmsVn.Server.Infrastructure.csproj`; active `packages.lock.json`; solution/NET8-PAUSE profile | Add candidate only to consumers; remove old packages after consumers disappear; no TFM edits. |
| Persistence | `src/MyDmsVn.Server.Infrastructure/Persistence/{IDbConnectionFactory,SqlConnectionFactory,ISqlExecutionContext,SqlUnitOfWorkFactory,SqlPersistenceBuilder,RepoDbMappingInitializer,IRepoDbMapping}.cs`; `src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs` | Keep UoW contracts/state machine; introduce borrowed/owned sessions and explicit LINQ mapping composition; remove RepoDb-only hooks last. |
| Catalog | `src/MyDmsVn.Server.Infrastructure/Catalog/{SqlCatalogWriteRepository,SqlProductQueryService,SqlOtherCatalogQueryServices,CatalogRepoDbMapping,CatalogRepoDbWriteFields}.cs` | Preserve interfaces and field allowlists; use explicit fluent mappings and typed queries or justified native SQL. |
| Inventory | `src/MyDmsVn.Server.Infrastructure/Inventory/{SqlGoodsReceiptWriteRepository,SqlGoodsReceiptQueryService,SqlInventoryQueryService,SqlStockReconciliationQueryService,InventoryRepoDbMapping,InventoryRepoDbWriteFields}.cs` | Replace library calls without changing locking SQL, numbering, posting/reconciliation algorithms or rowversion behavior. |
| Identity/security | `src/MyDmsVn.Server.Infrastructure/Identity/`; `src/MyDmsVn.Server.Infrastructure/Security/SqlSecurityAdministrationStore.cs`; `src/MyDmsVn.Server.DbMigrator/`; `deployment/sql/Provision-MyDmsVnPrincipals.sql` | Inventory all Dapper/RepoDb consumers, preserve procedure allowlist, result sets, errors and maintenance boundary; SQL scripts/grants unchanged. |
| Operations | `src/MyDmsVn.Server.Infrastructure/Operations/`; `deployment/release/Publish-LocalRelease.ps1` | Replace remaining provider calls only where present; retain startup/schema/principal diagnostics and validate packaged net48 dependencies. |
| Test infrastructure | `tests/test-gates.v1.json`; `tests/tools/{Invoke-TestGate,Invoke-FocusedSqlIntegrationTests,Invoke-GuiTestAcceptance}.ps1`; `tests/tools/MyDmsVn.SqlTestRunner/` | Add reviewed gates/case mappings using current capabilities; no runner protocol redesign. |
| Foundation assertions | `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/{ConnectionAndUnitOfWorkTests,PersistenceTransactionTests,RepositoryAndQueryIntegrationTests,RepositoryLifetimeTests,UnitOfWorkExceptionalCleanupTests}.cs` | Preserve transactions, scoped dependencies, resolver rejection, init-failure and cleanup assertions; replace library-specific probes explicitly. |

Create under `src/MyDmsVn.Server.Infrastructure/Persistence/`: `ILinqToDbSessionFactory.cs`, `LinqToDbSessionFactory.cs`, `LinqToDbMappingSchemaFactory.cs`. Create `Catalog/CatalogLinqToDbMapping.cs` and `Inventory/InventoryLinqToDbMapping.cs`; preserve existing entity types unless a narrow projection needs an Infrastructure-only POCO.

Create test files in their existing owners: Foundation `LinqToDbConnectionTests.cs`, `LinqToDbMappingTests.cs`; Catalog `LinqToDbCatalogParityTests.cs`; Inventory `LinqToDbInventoryParityTests.cs`; Security `LinqToDbSecurityParityTests.cs`; Architecture `LinqToDbBoundaryTests.cs`. Pilot-only adapters live under each test project's `Pilot/` folder and are deleted in T06. Do not add a general-purpose test project or another application DAL.

Create `docs/L2DB_VERIFICATION.md` and `docs/L2DB_CASE_MIGRATION.md` during T00; they do not exist and are not PASS evidence in this planning change.

## 4. Internal interfaces and algorithm boundaries

T01 introduces an **Infrastructure-only** `ILinqToDbSessionFactory` with these members:

```csharp
LinqToDB.Data.DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction);
Task<LinqToDB.Data.DataConnection> OpenOwnedAsync(CancellationToken cancellationToken);
```

A borrowed wrapper must use the supplied current Microsoft.Data.SqlClient connection/transaction and explicitly leave ownership with its caller. Validate context/provider/state; `requireTransaction=true` rejects absence before SQL. A standalone wrapper opens through IDbConnectionFactory and owns disposal, including construction-error cleanup. Use per-operation borrowed wrappers, not a cached wrapper that outlives transaction state changes. Reuse successfully built mapping configuration, never mutable per-request global state.

Do not change Application repository signatures solely to pass provider objects. Preserve SqlUnitOfWorkFactory's public factory contract and scoped dependency resolution. Verify candidate API calls on net48 before implementing consumers; official documentation is not disposal proof.

Use native parameterized SqlClient where necessary to preserve existing multi-result batching, long paging offsets, stored-procedure outputs or lock semantics. Record each retained native surface by file/method and reason in L2DB_VERIFICATION. This is not permission to retain Dapper as a permanent hidden fallback. Avoid raw interpolated values, unrestricted entity updates, client-side evaluation and generic MERGE/upsert substitutions.

## 5. Tasks and exit gates

### L2DB-T00 — Bind the current workspace, evidence and executable gates

**Files:** this plan; `tests/test-gates.v1.json`; new `docs/L2DB_VERIFICATION.md`, `docs/L2DB_CASE_MIGRATION.md`; relevant plan/status guidance only.
**Consumes:** accepted RESP net48 fixture baseline on current RepoDb/Dapper, ADR 0009, actual current declarations and supported guarded tooling.
**Produces:** reviewed source/package/native-SQL inventory, logical-case ledger and executable Task/Final bindings.

- [ ] Verify the preceding RESP final milestone is accepted on net48 with the current RepoDb/Dapper product. Record its exact commit, fixture/reset/fresh-database boundaries, case identities and cleanup results. Missing or failed RESP evidence blocks T00; a documentation merge is not acceptance.
- [ ] Record actual post-RESP implementation HEAD, clean/dirty state, submodule SHAs, active product/test/tool targets and changes since the refresh base. Resolve conflicts without discarding local work; freeze the accepted harness/runner behavior for DAL attribution.
- [ ] Inventory every RepoDb/Dapper usage in `src`, `tests`, deployment and active csproj/lock graphs. Classify production, test oracle, pilot, parked/history and unused central pin; identify the owning conversion task. Record immutable production SQL script/grant hashes and supported schema/journal baseline.
- [ ] Record affected baseline assertions with durable logical IDs, owner, TFM, classification and theory-row identities. Classify retain/replace with explicit replacement assertions; never delete safety behavior merely because its name mentions an old library.
- [ ] Add the T00 gate selecting existing runner manifest/identity contract tests and define the new T07 Final using all current owners. Add later Task selectors only with their actual test methods in the corresponding task; planned empty selections are NOT RUN, not passing gates. Preserve parked history/exemptions and independent review.
- [ ] Run the T00 commands in section 7, inspect exit/source/discovery/TRX and record results. Metadata preflight is allowed; do not rerun historical full-suite acceptance merely to establish this mapping.
- [ ] Commit the reviewed binding and inventory. **Exit:** commands/owners resolve and evidence is explicit; missing prerequisites/resources block the dependent gate, not permission to guess.

### L2DB-T01 — Prove net48 package compatibility and resource ownership

**Files:** central packages/affected locks; Infrastructure csproj/DI and new Persistence files; Foundation `LinqToDbConnectionTests.cs`, `LinqToDbMappingTests.cs`; Architecture `LinqToDbBoundaryTests.cs`; manifest entries for the actual new methods.
**Consumes:** T00 inventory and existing ISqlExecutionContext/IDbConnectionFactory.
**Produces:** tested internal session-factory contract and reusable mapping composition. Production composition still uses the old repositories.

- [ ] Write failing tests for borrowed connection identity and same-transaction read visibility; wrapper disposal leaves UoW usable; repository resolution before begin binds after begin; completed/disposed contexts fail before SQL; missing required transaction and wrong provider are rejected.
- [ ] Add failure tests for cancellation/reader disposal, owned-wrapper construction failure, mapping initialization failure followed by a valid retry, concurrent mapping access and independent UoWs. Retain exceptional UoW cleanup and caller-scoped dependency disposal assertions.
- [ ] Add exact candidate package to the Infrastructure consumer, perform deliberate unlocked restore only to generate reviewed lock changes, then locked restore/build on net48. Record actual assets, SqlClient/native binding, licenses/advisories and any incompatibility; no unrelated package upgrades or TFM changes.
- [ ] Observe intended test failures through focused gates, implement the session factory/mappings, then rerun the same gates. No singleton connection, ambient transaction, raw connection string in diagnostics or swallowed SQL error.
- [ ] Build the net48 Desktop host and DbMigrator through their actual project references to expose transitive packaging issues. Record package failures as blockers, not permission to change runtime.
- [ ] Commit passing focused evidence. **Exit:** net48 compatibility and ownership established; rollback remains the existing UoW's responsibility.

### L2DB-T02 — Three-slice pilot and explicit rollout decision

**Files:** new Catalog/Inventory/Security parity tests and temporary test-only `Pilot/` adapters; `docs/L2DB_VERIFICATION.md`; corresponding reviewed case/gate entries.
**Consumes:** T01 sessions/mappings; existing production repositories/queries and supported SQL schema.
**Produces:** a GO / NO-GO pilot record, not a silent production provider switch.

- [ ] Write Product parity tests for CRUD/identity, audit allowlists, active-only lookups, literal `%`, `_`, `[`, Vietnamese text, null/blank search, stable ordering, not-found behavior and duplicate-code errors. Compare results against independent native-SQL assertions; capture current count/page round trips and long-offset semantics.
- [ ] Write a receipt pilot combining LINQ writes with native SQL in one existing UoW, asserting same-session uncommitted visibility, returned IDs/rowversion, commit and injected mid-operation rollback of header/lines/ledger/balances. Use the accepted RESP fixture contract and preserve its fresh-database exceptions. Reset eligible fixtures between old/candidate executions; never compare against data left by the preceding provider run.
- [ ] Write a security pilot that invokes an existing allowed procedure using the candidate adapter under a real runtime principal, exercises result sets/error translation and outer transaction rollback, and proves denied generic RBAC/binding writes remain denied.
- [ ] Run focused L2 against deterministic, explicitly owned disposable fixtures using the old and candidate code separately under the frozen RESP harness. Never shadow-write a real operation through both providers. Record SQL shape, commands/round trips, parameter types, dependency footprint and implementation complexity; timings are observations, not a suite-speedup claim.
- [ ] Record GO only if all three slices preserve behavior/ownership/security, package startup works and there is no unexplained round-trip or materialization regression. Resolve any query batching/offset gap using an explicitly justified native path before GO. A blocker yields NO-GO and stops T03–T06; amend the design rather than silently changing package/runtime/security scope.
- [ ] Commit the pilot result and temporary-code removal obligation. **Exit:** documented, reviewable rollout decision with no new production provider flag.

### L2DB-T03 — Convert all Catalog writes and reads

**Files:** Catalog source anchors in section 3; new `CatalogLinqToDbMapping.cs`; DI; Catalog parity tests and existing Catalog/Application/Desktop lookup tests selected by actual source; manifest/ledger.
**Consumes:** T01 sessions and T02 GO. **Produces:** all four Catalog services on the new DAL without changing interfaces.

- [ ] Extend failing parity tests to Warehouses, Employees and Customers, including employee-user uniqueness/FK error mapping, nullable fields, literal search, active-state transitions and created/updated field preservation. Assert no catalog concurrency-token feature is invented.
- [ ] Convert repository operations with explicit field allowlists and existing affected-row/not-found contracts. Replace Dapper projection internals; keep SQL collation semantics, COUNT_BIG/long offsets, deterministic ordering and lookup limits. Do not materialize before filtering/paging or parallelize count/page on one connection.
- [ ] Exercise new Catalog writes and still-existing receipt/reference/native reads in one UoW, preserving interoperability during the phased conversion. Verify ordinary cancellation and Local DTO/error behavior.
- [ ] Run T03 L2, update the retained-native-SQL ledger and scoped case evidence, then commit. **Exit:** no RepoDb/Dapper calls remain in production Catalog; Inventory/Security still function through unchanged contracts.

### L2DB-T04 — Convert Inventory without changing its SQL algorithms

**Files:** Inventory source anchors in section 3; new `InventoryLinqToDbMapping.cs`; inventory parity/regression tests; DI/manifest/ledger.
**Consumes:** T01 sessions, T03 Catalog mappings and current posting/reconciliation contracts.
**Produces:** receipt/ledger/query persistence on LINQ or justified native SqlClient; proven existing SQL algorithms retained.

- [ ] Write/retain failures for draft insert/edit with returned identity and fresh rowversion, stale-version rejection, duplicate products, decimal boundaries and sequence behavior. Check SQL-generated/audit fields are never overwritten by broad updates.
- [ ] Replace RepoDb inserts/loads and Dapper projections only. Keep receipt header -> Warehouse -> Employee -> sorted Products -> sorted balances lock order, conditional Draft/rowversion update, ledger source uniqueness, atomic delta SQL and SERIALIZABLE reconciliation semantics. Do not introduce MERGE or C# read-modify-write balances.
- [ ] Retain native multi-result/window/reconciliation SQL where needed; replace only its execution/mapping layer when justified. Preserve stock-card movement rows, chronological `PostedAtUtc, LedgerId` ordering, quantity changes, UTC timestamps, filters and paging. Current `StockCardRow` has no starting/running balance contract; adding one is outside this migration.
- [ ] Run duplicate/concurrent posting, concurrent catalog deactivation, missing-balance races, injected ledger/audit failure, cancellation and reconciliation-during-posting cases using separate sessions inside deliberately isolated tests. Do not confuse these with runner-level parallelism.
- [ ] If T00 discovers a genuinely used bulk consumer, bind it to the same UoW and test constraint/identity/error/rollback semantics before conversion; otherwise keep row-wise inserts and add no speculative bulk feature.
- [ ] Run T04 L2 and commit. **Exit:** identical receipt/status/ledger/balance outcomes and no remaining production Inventory RepoDb/Dapper calls.

### L2DB-T05 — Convert Identity, Security and operational consumers

**Files:** inventoried Identity/Security/Operations/DbMigrator consumers; Security parity tests; existing Security/Migrations/IntegrationTests and pure session/application tests; manifest/ledger.
**Consumes:** T01 sessions, ADR 0007 and actual maintenance/runtime SQL permissions.
**Produces:** no active product Dapper/RepoDb consumers, with existing security procedures and schema intact.

- [ ] Pin tests for current account/session validation, configured binding mismatch, disabled/protected account, direct Deny/Allow/active-role precedence and unknown/deprecated key denial. Preserve next-request revocation and offline permission export requiring no DB.
- [ ] Pin additive import identity/no-op/conflict behavior, stale revision/catalog digest/actor, payload limits, transactional audit failure and safe credential/error redaction. Exercise real runtime-principal denials, not only owner connections.
- [ ] Replace query/procedure execution and result materialization without replacing the evaluator or broadening SQL privileges. Preserve multiple result sets, binary/string/null values, output parameters and mapped SQL errors. Use explicit native SqlClient where safer; do not introduce generic RBAC DML.
- [ ] Convert remaining operational/migrator library usages found by T00 while preserving separate connection ownership, schema compatibility, controlled maintenance authority and supported-baseline refusal. DbUp scripts and grant hashes must stay unchanged.
- [ ] Run T05 gates, including actual Local login/expiry and maintenance regressions; record the retained native surface and commit. **Exit:** production uses LINQ/native SqlClient only; no authentication/provisioning behavior changed.

### L2DB-T06 — Remove transitional providers and reconcile the complete dependency/test graph

**Files:** old mapping/write-field helpers and DI hooks; active csproj/lock files/central pins; temporary Pilot folders; affected Foundation/library-specific tests; current guidance listed in ADR 0009; manifest/ledger.
**Consumes:** T03–T05 completed conversions. **Produces:** one production DAL, reviewed dependency graph, no orphaned tests or misleading current guidance.

- [ ] Remove RepoDbMappingInitializer/IRepoDbMapping/AddRepoDbMapping plumbing and old Catalog/Inventory RepoDb helpers only after their final consumers are converted. Remove every pilot-only adapter and provider-comparison switch.
- [ ] Replace library-specific test probes with LINQ plus independent native-SqlClient assertions, retaining all transaction/resolver/lifetime/failure guarantees. Map renamed/replaced logical cases and theory rows explicitly; parked history remains historical, not deleted coverage.
- [ ] Remove obsolete RepoDb/Dapper package references from active code/test graphs and unused central pins, regenerate affected locks deliberately and verify locked restore. Audit parked AppCore/reactivation references without reactivating or falsely testing them; document required future lock refreshes.
- [ ] Add boundary/dependency checks for provider leakage, zero active RepoDb/Dapper references, explicit registration, no IQueryable exposure and no newly introduced SQL grants/schema edits. Preserve native independent test setup rather than routing assertions through the new DAL.
- [ ] Update current AGENTS/TECH_STACK/PERSISTENCE/ARCHITECTURE/PRD/DEVELOPMENT_PLAN and affected configuration/testing/release guidance only where code is now converted. Distinguish delivered LINQ/native persistence from proposed/unfinished work, preserve active net48 and keep historical evidence attached to the provider/revision that produced it.
- [ ] Review all selected fingerprint changes during L2 and reconcile **all** active owner/project/TFM/theory identities after code freeze. Never auto-approve digests or use a historical count as the required total. Run T06 L2 and metadata-only Final preflight; commit the frozen candidate.
- [ ] **Exit:** no production or active test old-ORM dependency, no unexplained lost assertion, no schema/grant drift; preflight succeeds without claiming SQL acceptance.

### L2DB-T07 — Frozen-revision acceptance, packaging and handoff

**Files:** sanitized `docs/L2DB_VERIFICATION.md`, completed logical-case ledger and task/status checkboxes only after supporting evidence. This is a planned L2DB milestone, not an automatic final gate at every code-PR boundary.
**Consumes:** frozen T06 commit, reviewed complete manifest, approved Windows/SQL environment.
**Produces:** plan-specific final acceptance tied to an exact commit, packaged Local smoke and an accurate deferred-backlog handoff.

- [ ] Build/restore locked dependencies for the exact mandatory project/TFM matrix and run metadata-only preflight. Confirm clean commit, short local artifact paths and approved resources. Preflight is not SQL/GUI execution.
- [ ] Run one planned manifest-owned Final across the complete active matrix in section 6. Preserve strict unfiltered sequential SQL passes, source/discovery/TRX reconciliation, zero mandatory skips, workspace/global leases, path safeguards, watchdog and positive owned-database cleanup.
- [ ] Run the existing net48 release packaging command against that same revision and perform this plan's packaged Local smoke: login, Product CRUD/lookup refresh, receipt draft/post/inquiry, permission deny, explicit assignment import and logout/relogin; run any maintenance smoke with separate authority. Verify native SqlClient assets/binding redirects and unchanged schema/history/assignments. Record manual observations independently of automated test totals.
- [ ] Evaluate whether changed UI behavior or a packaged-smoke regression requires automated GUI D2. If applicable, use the existing guarded net48 wrapper and record the reason/result. Otherwise record D2 as NOT RUN with its scope rationale; it is not a default full GUI requirement for DAL-only task commits.
- [ ] If a code/package/manifest fix follows a failed or green gate, repair with focused L2 then rerun the affected complete required final gate. Do not reuse stale acceptance to force a single execution.
- [ ] Publish only sanitized aggregates with exact revisions/commands, pass/fail/skip/not-run counts, ownership/watchdog outcomes and unexecuted/manual gates. Keep raw TRX, journals, fingerprints, connection and approval data local.
- [ ] Record operational rollback to the compatible post-security-rebuild binary and mark only evidence-supported tasks complete. Record remaining P5/P6/HTTP/runtime work in [deferred backlog](../DEFERRED_WORK.md), without activating or waiving it. **Exit:** required correctness/package/smoke gates passed or explicit blockers; no claim of deferred delivery completion or measured performance improvement.

## 6. Per-task L2 gate matrix and final owners

Exact aliases below refer to current existing projects. All are `net48` except R, which is tool-only `net8.0`. F/C/I/S/M/O are six distinct SQL owners; the legacy-named O project is not the pre-modular full SQL suite.

| Alias | Project path |
|---|---|
| A | `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj` |
| X | `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj` |
| F | `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj` |
| C | `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj` |
| I | `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj` |
| S | `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` |
| M | `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj` |
| O | `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj` |
| H | `tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj` |
| D | `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` |
| W | `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` |
| R | `tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj` |

`Task(Txx)` means the exact new manifest PlanId/TaskId command in section 7, **after** the corresponding reviewed entries/tests exist. It selects only non-SQL methods. `Sql(owner,category)` means the guarded existing category wrapper in section 7, never plain `dotnet test`. New named classes below are created by their tasks; T00 binds existing supplemental types/methods from actual source rather than inventing them.

| Task | Affected module/dependencies and non-SQL selector | Guarded SQL L2 | Adversarial obligations / command |
|---|---|---|---|
| T00 | R `TestGateManifestTests`, `LogicalCaseIdentityTests`, `TestGateRepositoryContractTests` | None; optional metadata-only SQL preflight | Source/ownership/TFM/exemption audit; `Task(T00)` once bound; bootstrap command below. |
| T01 | X `LinqToDbBoundaryTests`; non-SQL mapping/cleanup cases in F; host/migrator builds | F/FastSQL; complete active SQL checkpoint if shared UoW/connection/mapping behavior changes | Borrowed ownership, late transaction, cancellation, bad provider, mapping failure; `Task(T01)` + focused SQL, then the applicable shared-change checkpoint. |
| T02 | X boundary cases; pure parity-fixture/DTO checks only where actually added | F/FastSQL; C/FastSQL; I/FastSQL; S/SecurityProvisioning | Three pilots, independent SQL oracle, injected rollback/denied generic writes; `Task(T02)` if nonempty plus listed Sql calls. |
| T03 | A Catalog handlers/validators and D affected lookup ViewModels, exact reviewed methods | C/FastSQL; I/FastSQL for changed Catalog/reference cross-dependency | Four catalogs, employee linkage, search/field allowlists, mixed-phase transaction; `Task(T03)` + listed Sql calls. |
| T04 | A receipt/posting/inquiry handlers/validators, exact reviewed methods | I/FastSQL; I/SqlConcurrency; C/FastSQL | Duplicate/concurrent posting, locked references, decimals/rowversions and reconciliation; `Task(T04)` + listed Sql calls. |
| T05 | A `SuperuserAuthorizationTests`, `PermissionAuthorizationTests` and import tests; D current expiry/relogin cases; S/M pure config/maintenance tests | S/SecurityProvisioning; S/SqlConcurrency; M/MigrationSchema; O/SecurityProvisioning for current Local login cases | Real principal, stale actor/preview, protected account, procedure mapping/audit; `Task(T05)` + listed Sql calls; the current Local session case explicitly uses SecurityProvisioning; recheck if moved. |
| T06 | X boundary/dependency checks; R repository/manifest contracts; F non-SQL lifetime/cleanup/init checks | F/FastSQL plus affected categories; complete active SQL for shared ownership/mapping changes | Package cleanup, test-ledger bijection, no schema/grant drift; `Task(T06)` + applicable SQL/shared-change checkpoint + Final preflight. |
| T07 | All required non-SQL project owners | **Unfiltered F,C,I,S,M,O**, active net48 | `Final(T07)` across A,X,H,D,R and six SQL owners; required package/manual smoke; W guarded GUI D2 only when its documented applicability trigger is met. |

T00 must replace any category/selector mismatch with a source-reviewed binding and record the reason before execution. Unsupported/empty selection blocks that gate; do not reclassify a test just to fit a command. SQL wrappers filter one category in one project, not arbitrary module+method combinations. During L1 choose smaller correct non-SQL selectors; SQL L1 remains guarded. Full suites are not task-boundary or code-PR defaults. Shared UoW/connection/mapping/harness/migration changes require the broader affected SQL matrix before dependent rollout, including all six owners when the shared contract affects them. T07 remains the complete planned final milestone; focused runs or an earlier checkpoint do not replace it.

**Budgets/resources for every row:** non-SQL Task gates use `commandTimeoutSeconds=300`, `overallTimeoutSeconds=900` initially; R repository-contract/inventory checks may use the existing reviewed 900/3600 budget. At this source baseline, all six SEC-REBUILD Final SQL entries use `commandTimeoutSeconds=7200` and `overallTimeoutSeconds=7200` in `tests/test-gates.v1.json`. L2DB-T00 must inherit the accepted RESP budgets, or explicitly review a narrower budget against its measured evidence; do not present a reduction as an already accepted limit. Preserve runner-enforced timeouts; focused SQL uses its current bounded defaults, with no invented timeout CLI switch. Longer timeouts require a documented reviewed reason, not disabling a watchdog. Use a fresh short local artifact root per run, the existing 240-character path checks, valid approval, matching non-production instance and positive cleanup. No manual deletion of leases/journals or broad pool clearing.

## 7. Runnable command templates and binding rules

Run from a short-path Windows checkout with pinned submodules initialized, .NET Framework 4.8 targeting pack and the existing .NET 8 SDK for tooling. Commands below are **planned**, not executed for this documentation change. Check every native command's exit code; stop on failure. Test artifact roots are examples of short ignored local paths with fresh suffixes, never committed evidence.

### Bootstrap T00 before the new manifest entries exist

```powershell
dotnet restore tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Runner-test locked restore failed.' }
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj `
  -c Release -f net8.0 --no-restore `
  --filter 'FullyQualifiedName~TestGateManifestTests|FullyQualifiedName~LogicalCaseIdentityTests'
if ($LASTEXITCODE -ne 0) { throw 'T00 runner contracts failed.' }
```

This command does not run the SQL suite. The heavier repository-inventory fixture target is opt-in; use the existing wrapper when required rather than restoring the removed unconditional before-build hooks. T00 must review any selected stale fingerprints before manifest-owned execution; never generate an automatic approval patch.

### Manifest-owned non-SQL L2 (example T01)

```powershell
$plan = '20261018-001-linq2db-net48-persistence-migration'
$root = '.sqlperf/l2-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-TestGate.ps1 -Mode Task -PlanId $plan `
  -TaskId 'L2DB-T01' -ArtifactRoot $root
if ($LASTEXITCODE -ne 0) { throw 'L2DB Task gate incomplete.' }
```

Use the table's exact task ID for each row after adding its real reviewed selectors. Task mode is not an SQL invocation and accepts no ad-hoc project/TFM/filter override. Current Task tooling permits unselected fingerprint drift only while complete method ownership/classification/shape stays valid; selected methods still require review. Recheck this contract against the accepted RESP tooling; documentation does not bypass enforced checks. T06/Final reconciles all fingerprints, including transitive changes from new dependencies.

### Guarded SQL L2 (example Foundation/FastSQL)

Precondition: the process environment contains the operator-approved non-production SQL connection with effective database `master`; `$env:MYDMSVN_SQL_TEST_APPROVAL_FILE` identifies a valid local approval for that **same instance**. Do not print, commit, auto-create or bypass either. A failed approval is BLOCKED, not a reason to alter the lease or discover another database.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release -f net8.0 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Runner build failed.' }
dotnet build $project -c Release -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'SQL owner build failed.' }
$root = '.sqlperf/sql-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location).Path -TestProject $project -ArtifactRoot $root `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category FastSQL -Frameworks @('net48')
if ($LASTEXITCODE -ne 0) { throw 'Focused SQL gate incomplete.' }
```

For each `Sql(owner,category)` row, substitute the exact project from section 6 and the listed category, rebuild that owner and use a new artifact root. These are real existing wrapper parameters. Always pass `-Frameworks @('net48')`: the lower-level focused wrapper still defaults to both historical SQL TFMs.

### Complete SQL checkpoint for a shared persistence change

After focused repairs are green and shared-change inputs are committed/reviewed, use the existing strict SQL wrapper sequentially for every affected SQL owner. When the shared UoW/connection/mapping contract affects the entire matrix, run F, C, I, S, M and O from section 6. For each owner, build the current net48 project and use a fresh artifact root; build the tool-only runner first as in the focused example.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
dotnet build $project -c Release -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'SQL owner build failed.' }
$root = '.sqlperf/shared-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-SqlPrAcceptance.ps1 `
  -RepositoryRoot (Get-Location).Path -TestProject $project -ArtifactRoot $root `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE -Frameworks @('net48')
if ($LASTEXITCODE -ne 0) { throw 'Shared-change SQL checkpoint incomplete.' }
```

Repeat this exact owner substitution for the reviewed matrix. The wrapper requires a clean committed checkout and an already built runner. Its name does not make it mandatory for every PR; invoke it for the scope triggers in TEST_POLICY and this plan. It performs one unfiltered `pr-acceptance` pass per selected SQL owner/TFM, not the separate multi-pass benchmark. Preserve its full evidence/approval/ownership checks. A runner incompatibility with accepted RESP behavior is a blocker to resolve explicitly, not permission to use bare `dotnet test` as equivalent evidence.

### Metadata preflight and frozen Final

T00/T06 bind the new `L2DB-T07` Final to the current complete matrix. Before that binding exists, do not invoke a nonexistent new gate. A baseline metadata-only check may use the accepted RESP Final binding exactly as recorded in its evidence; label it baseline preflight, never new-plan acceptance. Existing old manifest records are transition tooling, not additional active plans.

```powershell
$plan = '20261018-001-linq2db-net48-persistence-migration'
$root = '.sqlperf/pf-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-TestGate.ps1 -Mode Final -PlanId $plan `
  -TaskId 'L2DB-T07' -ArtifactRoot $root -PreflightOnly
if ($LASTEXITCODE -ne 0) { throw 'Manifest preflight failed; no SQL acceptance.' }

# Only after all gate-affecting edits/locks/manifest review are committed and clean.
$root = '.sqlperf/fin-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-TestGate.ps1 -Mode Final -PlanId $plan `
  -TaskId 'L2DB-T07' -ArtifactRoot $root `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
if ($LASTEXITCODE -ne 0) { throw 'Final acceptance incomplete.' }
```

Preflight runs bounded source/fingerprint/discovery checks without SQL test execution or DDL and needs no SQL approval; it cannot produce SQL/GUI/cleanup acceptance. Final requires clean revision and SQL approval, runs the current isolated fixture build path and preserves workspace/global leases. Exit 0 is accepted only with complete reconciled evidence; denied/incomplete/usage outcomes remain non-PASS.

### Required local package smoke and conditional guarded net48 GUI

Run package/smoke validation after Final, not concurrently in the same checkout. Packaging and the listed manual Local smoke are required by T07. Run the GUI D2 block only when the T07 applicability assessment requires it. The current wrapper runs its guarded GUI owner, pins net48 internally and requires exact clean HEAD plus an already built runner; it has no focused filter or Frameworks parameter.

Required package command:

```powershell
[string] $revision = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve package revision.' }
$version = '0.0.0-l2db.' + $revision.Trim().Substring(0,8)
$output = Join-Path (Get-Location).Path ('artifacts/release/' + $version)
& ./deployment/release/Publish-LocalRelease.ps1 -Version $version -OutputRoot $output
```

This is a local validation package, not a production release/version assignment. Conditional automated GUI D2 command, when applicable to the reviewed scope:

```powershell
[string] $revision = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve GUI revision.' }
$root = '.sqlperf/gui-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-GuiTestAcceptance.ps1 `
  -RepositoryRoot (Get-Location).Path `
  -TestProject 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj' `
  -ArtifactRoot $root -Revision $revision.Trim() -TimeoutSeconds 900
if ($LASTEXITCODE -ne 0) { throw 'GUI D2 acceptance incomplete.' }
```

Publish errors are terminating. Do not use SkipPublish for dependency validation or overwrite an existing release output. The current script emits separate net48/x86 Desktop and admin-migrator packages; verify their manifests, dependency/binding redirects and SqlClient native assets, then launch the packaged application on an approved environment. Packaging or automated GUI D2 alone does not prove the manual login/catalog/posting/security scenarios. Record those separately with artifact hashes, exact revision and outcomes. Do not run bootstrap/migration against an unapproved database as a package smoke check.

## 8. Evidence, success and rollback

L2DB_VERIFICATION must record per task: exact commit/worktree status, source/owner/TFM, selector/category, rationale, actual commands, expected/observed assertions, pass/fail/skip/BLOCKED/NOT RUN, approval result only, timeout/watchdog/lease/owned-cleanup outcome, retained native SQL and package mapping, plus sanitized evidence location. Never copy raw approval, connection strings, TRX, import payloads or ownership journals into Git.

The migration succeeds when application DAL conventions are consolidated, all invariant and packaging gates pass, current data/schema/grants remain intact, active net48 and parked history are preserved, and RepoDb/Dapper are absent from the active execution graph. No fixed percentage test-speed target is imposed and no historical baseline is relabeled as candidate evidence.

If the pilot fails, stop rollout and leave production composition unchanged. If a later conversion fails, revert its scoped code/package/registration change on the unchanged supported schema. No database reset, old-security restoration or permanent dual-provider fallback is allowed. Each implementation change records scope-appropriate evidence under TEST_POLICY. Splitting work into code PRs does not automatically add full repository or GUI runs; shared-change checkpoints and the planned T07 SQL/package/smoke milestone still apply. A later relevant change invalidates its affected prior evidence and requires rerunning that applicable gate after focused repair.

**Planning verification:** source/docs and existing wrapper signatures were reviewed at the refresh base. No package restore, build, SQL, GUI, benchmark, dependency/tooling change or production mutation was performed. All T00–T07 checkboxes remain open and require accepted RESP evidence before execution.

**Candidate reference:** [official linq2db 6.3.0 package](https://www.nuget.org/packages/linq2db/6.3.0); [official ownership API](https://linq2db.github.io/api/linq2db/linq2db--LinqToDB.Data.ConnectionOptions.html). Published API/assets guide the pilot; they do not prove runtime compatibility or disposal correctness.
