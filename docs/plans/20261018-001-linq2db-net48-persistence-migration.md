# L2DB — LINQ to DB persistence migration on active net48

> **For agentic workers:** Use `superpowers:executing-plans` or `superpowers:subagent-driven-development` to implement this plan task by task after review. Checkboxes describe work still required, not evidence of completion.

**Date:** 2026-10-18
**Status:** Owner-approved direction 1A + 2A + 3A; L2DB-T00–T06 complete; T02 final **GO**; T07 in progress:581 frozen-revision correctness invocations passed and clean package validated; authenticated interactive smoke awaits a new restricted-runtime identity approval. RESP-T05 has accepted the exact local net48 working-tree candidate in [verification](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up). T00 binds those bytes to PR #36 merge `1e24df4c4ad5243963da82d808da19f12278242b`; see [verification](../L2DB_VERIFICATION.md) and [case ledger](../L2DB_CASE_MIGRATION.md). [T02 final GO](../L2DB_VERIFICATION.md#l2db-t02-final-go--approved-runtime-startup): strict User/Role parity, schema9/x86 package and least-privileged startup passed; approved temporary identity/database cleaned up. The internal session seam is verified; T03 has converted production Catalog to LINQ/native ([evidence](../L2DB_VERIFICATION.md#l2db-t03--catalog-conversion)), T04 converts Inventory to LINQ/native ([evidence](../L2DB_VERIFICATION.md#l2db-t04--inventory-conversion)); T05 converts Identity/Security/operational consumers to typed native SqlClient ([evidence](../L2DB_VERIFICATION.md#l2db-t05--identity-security-and-operations-conversion)); T06 removes transitional hooks/packages/pilots and reconciles active dependencies/tests ([evidence](../L2DB_VERIFICATION.md#l2db-t06--transitional-provider-and-test-graph-cleanup)).
**Goal:** Replace the active RepoDb/Dapper application DAL with LINQ to DB and a documented native-SqlClient escape surface, preserving Local v1, active net48, post-rebuild security, existing data and all correctness gates.
**Architecture:** Existing Clean Architecture modular monolith and explicit UoW. Infrastructure-only mapping/connection adapters; unchanged Application/Contracts/Desktop boundaries; DbUp remains the sole schema owner.
**Tech stack:** WinForms, CommunityToolkit.Mvvm, net48 product/SQL/Desktop and test projects, LINQ to DB (initial candidate 6.3.0), current Microsoft.Data.SqlClient, SQL Server, DbUp, MediatR, FluentValidation, ErrorOr, Serilog and xUnit.
**Spec:** [ADR 0009](../adr/0009-linq2db-local-net48-persistence.md).
**Original assessment:** `main` at `8cfa25351338666b1a751ee516d5a479cdb37b7f` after PR #35; historical source assessment only.
**Context base:** `v1a` documentation refresh at `a5ee124bed72f2994c4ee712e660b91d0dc2f953`, updated for the owner's subsequent complete SQL test framework removal on 2026-10-18.
**Predecessor:** [RESP — fixture reuse with Respawn](20261018-002-respawn-fixture-reuse.md), which must be verified on the current RepoDb/Dapper DAL before L2DB-T00.

## 1. Current baseline, execution order and authority

The baseline table and assessment paragraphs in this section describe the pre-migration roadmap snapshot. Current T03–T06 implementation and verification supersede those delivered-provider claims; the task/evidence sections retain the exact progression.

The [active roadmap](README.md) contains exactly two plans: **RESP, then L2DB**. RESP first proves its package and reused-fixture/reset design on active net48 with the current RepoDb/Dapper product. L2DB-T00 consumes that accepted commit and evidence. During DAL migration, freeze the accepted fixture boundaries, reset semantics and direct test commands so persistence changes have a stable comparison base. A fixture defect blocks the dependent DAL gate until fixed and accepted explicitly; do not silently combine another fixture redesign with a provider conversion.

| Area | Current v1a source after framework removal | Treatment |
|---|---|---|
| Runtime | Product/SQL/Desktop and all ten current test projects target net48. AppCore and historical modern product cases are parked; the .NET 8 SDK remains the build tool. | Preserve declarations, packaging boundaries and PARKED / NOT RUN identities. |
| Security | The permission rebuild is implemented: code-defined catalog, protected configured/database-bound superuser, explicit assignments, current-subject checks and allowlisted procedures. | Reuse current behavior; no new security reset, synchronization or auto-grants. |
| Test infrastructure | Six modular SQL owners run directly with dotnet test/Test Explorer; RESP-T05 accepts default reusable data collections, explicit fresh and dedicated exceptions. The former framework and manifests are removed. | RESP establishes the accepted fixture baseline first. Preserve local ownership/cleanup, serialization and required case coverage during L2DB. |
| DAL | Catalog/receipt RepoDb operations, Dapper queries/security consumers and native SqlClient SQL remain active. | Replace inventoried consumers while preserving native locking/procedure semantics where justified. |
| UoW | SqlUnitOfWorkFactory owns an open connection and guarded current optional transaction; repository factories receive ISqlExecutionContext. | Borrow current resources per operation without taking ownership or caching a pre-begin transaction. |
| Delivery backlog | Remaining P5 interactive/P6 operational acceptance, HTTP v2/P7, and modern product runtime are deferred. | These are not active prerequisites, completed gates or waived obligations. Re-plan them separately when requested. |

Read AGENTS, this plan and [RESP](20261018-002-respawn-fixture-reuse.md), [ADR 0009](../adr/0009-linq2db-local-net48-persistence.md), current PRD/ARCHITECTURE/TECH_STACK/PERSISTENCE/SECURITY/CONTRACTS, [TEST_POLICY](../TEST_POLICY.md) and the accepted ownership/security/runtime ADRs. [TEST_STRATEGY](../TEST_STRATEGY.md) is a coverage reference. [HISTORY](../HISTORY.md) records completed/superseded work; it is not an execution queue.

This is a documentation/source assessment, not fresh restore/build/SQL/GUI/benchmark acceptance. Historical pass counts, SQL timing and a waived duration comparison are not candidate evidence or proof of a LINQ-to-DB speedup. Old Task/Final commands and their manifests are removed. Plan IDs identify work and evidence; they are not executable registrations.

ADR 0009 replaces only the fixed DAL-library choice for this scope. Keep Local v1, active net48, UoW ownership, DbUp/data safety, current security and observable business behavior. T00 re-resolves the actual post-RESP implementation HEAD and reconciles drift without discarding local work. The final L2DB milestone hands remaining delivery work back to deferred backlog, without scheduling or waiving it.

## 2. Global constraints

- Keep product, SQL, Desktop and all active test projects **net48**. Leave AppCore outside solution/release and historical modern cases PARKED / NOT RUN. No net8 reactivation or net10 upgrade.
- Keep Local v1 -> HTTP v2 and identical DTO/result semantics. No API host, new auth stack, UI replacement, new business module, generic repository framework or arbitrary service locator.
- Keep existing UoW state/ownership, explicit transactions, scoped registrations and cancellation. No concurrent use of one UoW, ambient/static/AsyncLocal transaction or hidden extra connection for participating work.
- LINQ to DB types/mappings stay in Infrastructure. Materialize DTO results before disposal; no IQueryable, provider objects or mapping attributes in inner/UI/contract layers.
- Preserve post-SEC-REBUILD authorization, maintenance/runtime separation, legacy password columns, import limits/revision/digest rules and atomic audit. Do not broaden SQL grants to accommodate an ORM.
- Preserve the supported current schema, DbUp scripts/history and data. This plan does not authorize schema changes, reset, seed, destructive migration or replay of the pre-rebuild permission baseline.
- Pin and verify `linq2db` 6.3.0 as an initial compatibility candidate; keep other package pins unless a reviewed compatibility blocker requires a separate decision. Do not claim the candidate is newest or already restored. Track all affected lock files, native dependencies and net48 binding redirects.
- Require accepted RESP net48 evidence before T00. Freeze its ownership boundaries, reset semantics, fresh-database exceptions, serialization and direct test commands during L2DB. Preserve local guarded lifecycle and case outcomes; no additional fixture redesign, external test framework, container requirement or CI expansion.
- Follow [TEST_POLICY](../TEST_POLICY.md): focused affected tests per task, complete SQL coverage for shared UoW/fixture/migration changes and the T07 milestone, and GUI only for changed UI behavior or this plan's packaged smoke. No automatic full repository/GUI gate for each code PR and no mandatory multi-pass benchmark.
- Retain independent SQL test oracles and explain retained/replaced cases and Theory rows in the case ledger. Compare current source with actual TRX outcomes; do not reduce required behavior to make a run pass.
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
| Catalog | `src/MyDmsVn.Server.Infrastructure/Catalog/{SqlCatalogWriteRepository,SqlProductQueryService,SqlOtherCatalogQueryServices,CatalogLinqToDbMapping,SqlCatalogPageReader}.cs` | Preserve interfaces and field allowlists; use explicit fluent mappings and typed queries or justified native SQL. |
| Inventory | `src/MyDmsVn.Server.Infrastructure/Inventory/{SqlGoodsReceiptWriteRepository,SqlGoodsReceiptQueryService,SqlInventoryQueryService,SqlStockReconciliationQueryService,InventoryLinqToDbMapping,InventorySql}.cs` | Replace library calls without changing locking SQL, numbering, posting/reconciliation algorithms or rowversion behavior. |
| Identity/security | `src/MyDmsVn.Server.Infrastructure/Identity/`; `src/MyDmsVn.Server.Infrastructure/Security/SqlSecurityAdministrationStore.cs`; `src/MyDmsVn.Server.DbMigrator/`; `deployment/sql/Provision-MyDmsVnPrincipals.sql` | Inventory all Dapper/RepoDb consumers, preserve procedure allowlist, result sets, errors and maintenance boundary; SQL scripts/grants unchanged. |
| Operations | `src/MyDmsVn.Server.Infrastructure/Operations/`; `deployment/release/Publish-LocalRelease.ps1` | Replace remaining provider calls only where present; retain startup/schema/principal diagnostics and validate packaged net48 dependencies. |
| Test infrastructure | Existing six SQL projects; `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs`; accepted RESP adapters/profiles | Use direct project/filter commands, ordinary TRX and a reviewed case ledger; keep the accepted fixture behavior fixed. |
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

### L2DB-T00 — Bind the current workspace, cases and direct commands

**Files:** this plan; new `docs/L2DB_VERIFICATION.md`, `docs/L2DB_CASE_MIGRATION.md`; relevant plan/status guidance only.
**Consumes:** accepted RESP net48 fixture baseline on current RepoDb/Dapper, ADR 0009, actual current declarations and direct test commands.
**Produces:** reviewed source/package/native-SQL inventory, case ledger and concrete project/filter acceptance commands.

- [x] Verify the preceding RESP final milestone is accepted on net48 with the current RepoDb/Dapper product. Record its exact commit, fixture/reset/fresh-database boundaries, case identities and cleanup results. Missing or failed RESP evidence blocks T00; a documentation merge is not acceptance.
- [x] Record actual post-RESP implementation HEAD, clean/dirty state, submodule SHAs, active product/test targets and build tooling and changes since the refresh base. Resolve conflicts without discarding local work; freeze accepted fixture behavior and test commands for DAL attribution.
- [x] Inventory every RepoDb/Dapper usage in `src`, `tests`, deployment and active csproj/lock graphs. Classify production, test oracle, pilot, parked/history and unused central pin; identify the owning conversion task. Record immutable production SQL script/grant hashes and supported schema/journal baseline.
- [x] Record affected baseline assertions with durable logical IDs, owner, TFM, classification and theory-row identities. Classify retain/replace with explicit replacement assertions; never delete safety behavior merely because its name mentions an old library.
- [x] Map each task and T07 to the actual projects and source-verified filters in section 6. Add selectors for new tests only after those methods exist. An empty selection is NOT RUN; no external gate registration or framework restoration is required.
- [x] Review source/project/TFM mappings and inspect discovery where useful; record what was actually checked. Discovery is not test execution. T00's inventory work alone does not require rerunning historical full-suite acceptance.
- [x] Commit the reviewed binding and inventory. **Exit:** commands/owners resolve and evidence is explicit; missing prerequisites/resources block the dependent gate, not permission to guess.

### L2DB-T01 — Prove net48 package compatibility and resource ownership

**Files:** central packages/affected locks; Infrastructure csproj/DI and new Persistence files; Foundation `LinqToDbConnectionTests.cs`, `LinqToDbMappingTests.cs`; Architecture `LinqToDbBoundaryTests.cs`; case-ledger entries and direct filters for actual new methods.
**Consumes:** T00 inventory and existing ISqlExecutionContext/IDbConnectionFactory.
**Produces:** tested internal session-factory contract and reusable mapping composition. Production composition still uses the old repositories.

- [x] Write failing tests for borrowed connection identity and same-transaction read visibility; wrapper disposal leaves UoW usable; repository resolution before begin binds after begin; completed/disposed contexts fail before SQL; missing required transaction and wrong provider are rejected.
- [x] Add failure tests for cancellation/reader disposal, owned-wrapper construction failure, mapping initialization failure followed by a valid retry, concurrent mapping access and independent UoWs. Retain exceptional UoW cleanup and caller-scoped dependency disposal assertions.
- [x] Add exact candidate package to the Infrastructure consumer, perform deliberate unlocked restore only to generate reviewed lock changes, then locked restore/build on net48. Record actual assets, SqlClient/native binding, licenses/advisories and any incompatibility; no unrelated package upgrades or TFM changes.
- [x] Observe intended test failures through focused gates, implement the session factory/mappings, then rerun the same gates. No singleton connection, ambient transaction, raw connection string in diagnostics or swallowed SQL error.
- [x] Build the net48 Desktop host and DbMigrator through their actual project references to expose transitive packaging issues. Record package failures as blockers, not permission to change runtime.
- [x] Commit passing focused evidence. **Exit:** net48 compatibility and ownership established; rollback remains the existing UoW's responsibility.

### L2DB-T02 — Three-slice pilot and explicit rollout decision

**Files:** new Catalog/Inventory/Security parity tests and temporary test-only `Pilot/` adapters; `docs/L2DB_VERIFICATION.md`; corresponding case-ledger entries and focused commands.
**Consumes:** T01 sessions/mappings; existing production repositories/queries and supported SQL schema.
**Produces:** a GO / NO-GO pilot record, not a silent production provider switch.

- [x] Write Product parity tests for CRUD/identity, audit allowlists, active-only lookups, literal `%`, `_`, `[`, Vietnamese text, null/blank search, stable ordering, not-found behavior and duplicate-code errors. Compare results against independent native-SQL assertions; capture current count/page round trips and long-offset semantics.
- [x] Write a receipt pilot combining LINQ writes with native SQL in one existing UoW, asserting same-session uncommitted visibility, returned IDs/rowversion, commit and injected mid-operation rollback of header/lines/ledger/balances. Use the accepted RESP fixture contract and preserve its fresh-database exceptions. Reset eligible fixtures between old/candidate executions; never compare against data left by the preceding provider run.
- [x] Write a security pilot that invokes an existing allowed procedure using the candidate adapter under a real runtime principal, exercises result sets/error translation and outer transaction rollback, and proves denied generic RBAC/binding writes remain denied.
- [x] Run the focused SQL tests in section 6 against deterministic, explicitly owned disposable fixtures using the old and candidate code separately under the frozen RESP fixture baseline. Never shadow-write a real operation through both providers. Record SQL shape, commands/round trips, parameter types, dependency footprint and implementation complexity; timings are observations, not a suite-speedup claim.
- [x] Record GO only if all three slices preserve behavior/ownership/security, package startup works and there is no unexplained round-trip or materialization regression. Resolve any query batching/offset gap using an explicitly justified native path before GO. A blocker yields NO-GO and stops T03–T06; amend the design rather than silently changing package/runtime/security scope.
- [x] Commit the pilot result and temporary-code removal obligation. **Exit:** documented, reviewable rollout decision with no new production provider flag.

**Final executed result: GO.** Initial NO-GO was resolved through the authorized baseline-reader repair and strict User/Role parity, schema metadata repair and successful packaged startup with a real restricted runtime login. Database/login cleanup passed ownership/identity/drain checks. [Final evidence](../L2DB_VERIFICATION.md#l2db-t02-final-go--approved-runtime-startup) distinguishes unchanged predecessor scopes from rerun S33/A8/X10/W4 and actual package proof. T03 consumes this accepted GO; temporary-code removal remains recorded in the case ledger. T07 attended workflows/release acceptance remain open.

### L2DB-T03 — Convert all Catalog writes and reads

**Files:** Catalog source anchors in section 3; new `CatalogLinqToDbMapping.cs`; DI; Catalog parity tests and existing Catalog/Application/Desktop lookup tests selected by actual source; case ledger and evidence.
**Consumes:** T01 sessions and T02 GO. **Produces:** all four Catalog services on the new DAL without changing interfaces.

- [x] Extend failing parity tests to Warehouses, Employees and Customers, including employee-user uniqueness/FK error mapping, nullable fields, literal search, active-state transitions and created/updated field preservation. Assert no catalog concurrency-token feature is invented.
- [x] Convert repository operations with explicit field allowlists and existing affected-row/not-found contracts. Replace Dapper projection internals; keep SQL collation semantics, COUNT_BIG/long offsets, deterministic ordering and lookup limits. Do not materialize before filtering/paging or parallelize count/page on one connection.
- [x] Exercise new Catalog writes and still-existing receipt/reference/native reads in one UoW, preserving interoperability during the phased conversion. Verify ordinary cancellation and Local DTO/error behavior.
- [x] Run T03's focused checks in section 6, update the retained-native-SQL ledger and scoped case evidence, then commit. **Exit:** no RepoDb/Dapper calls remain in production Catalog; Inventory/Security still function through unchanged contracts.

**Verified result:** Catalog commands/reads contain no RepoDb/Dapper calls;125 scoped invocations passed without fail/skip, including current-transaction interoperability with unchanged Inventory. The temporary legacy reference mapping is explicit under Persistence; no fixture/schema/UoW/package change. [Evidence](../L2DB_VERIFICATION.md#l2db-t03--catalog-conversion).

### L2DB-T04 — Convert Inventory without changing its SQL algorithms

**Files:** Inventory source anchors in section 3; new `InventoryLinqToDbMapping.cs`; inventory parity/regression tests; DI, case ledger and evidence.
**Consumes:** T01 sessions, T03 Catalog mappings and current posting/reconciliation contracts.
**Produces:** receipt/ledger/query persistence on LINQ or justified native SqlClient; proven existing SQL algorithms retained.

- [x] Write/retain failures for draft insert/edit with returned identity and fresh rowversion, stale-version rejection, duplicate products, decimal boundaries and sequence behavior. Check SQL-generated/audit fields are never overwritten by broad updates.
- [x] Replace RepoDb inserts/loads and Dapper projections only. Keep receipt header -> Warehouse -> Employee -> sorted Products -> sorted balances lock order, conditional Draft/rowversion update, ledger source uniqueness, atomic delta SQL and SERIALIZABLE reconciliation semantics. Do not introduce MERGE or C# read-modify-write balances.
- [x] Retain native multi-result/window/reconciliation SQL where needed; replace only its execution/mapping layer when justified. Preserve stock-card movement rows, chronological `PostedAtUtc, LedgerId` ordering, quantity changes, UTC timestamps, filters and paging. Current `StockCardRow` has no starting/running balance contract; adding one is outside this migration.
- [x] Run duplicate/concurrent posting, concurrent catalog deactivation, missing-balance races, injected ledger/audit failure, cancellation and reconciliation-during-posting cases using separate sessions inside deliberately isolated tests. Do not confuse these with concurrent test-project execution.
- [x] If T00 discovers a genuinely used bulk consumer, bind it to the same UoW and test constraint/identity/error/rollback semantics before conversion; otherwise keep row-wise inserts and add no speculative bulk feature.
- [x] Run T04's focused checks in section 6 and commit. **Exit:** identical receipt/status/ledger/balance outcomes and no remaining production Inventory RepoDb/Dapper calls.

**Verified result:** Inventory has no production RepoDb/Dapper consumer;134 scoped invocations passed with unchanged locking/posting/balance/reconciliation algorithms, generated/audit tokens and fixture boundaries. [Evidence](../L2DB_VERIFICATION.md#l2db-t04--inventory-conversion).

### L2DB-T05 — Convert Identity, Security and operational consumers

**Files:** inventoried Identity/Security/Operations/DbMigrator consumers; Security parity tests; existing Security/Migrations/IntegrationTests and pure session/application tests; case ledger and evidence.
**Consumes:** T01 sessions, ADR 0007 and actual maintenance/runtime SQL permissions.
**Produces:** no active product Dapper/RepoDb consumers, with existing security procedures and schema intact.

- [x] Pin tests for current account/session validation, configured binding mismatch, disabled/protected account, direct Deny/Allow/active-role precedence and unknown/deprecated key denial. Preserve next-request revocation and offline permission export requiring no DB.
- [x] Pin additive import identity/no-op/conflict behavior, stale revision/catalog digest/actor, payload limits, transactional audit failure and safe credential/error redaction. Exercise real runtime-principal denials, not only owner connections.
- [x] Replace query/procedure execution and result materialization without replacing the evaluator or broadening SQL privileges. Preserve multiple result sets, binary/string/null values, output parameters and mapped SQL errors. Use explicit native SqlClient where safer; do not introduce generic RBAC DML.
- [x] Convert remaining operational/migrator library usages found by T00 while preserving separate connection ownership, schema compatibility, controlled maintenance authority and supported-baseline refusal. DbUp scripts and grant hashes must stay unchanged.
- [x] Run T05 gates, including actual Local login/expiry and maintenance regressions; record the retained native surface and commit. **Exit:** production uses LINQ/native SqlClient only; no authentication/provisioning behavior changed.

**Verified result:** All business/operational DAL consumers use LINQ/native;200 scoped invocations passed, Desktop.App net48 build passed, and SQL/procedures/grants/fixtures remain unchanged. Audit DateTime2 precision is explicitly preserved. Transitional hook/package cleanup is T06. [Evidence](../L2DB_VERIFICATION.md#l2db-t05--identity-security-and-operations-conversion).

### L2DB-T06 — Remove transitional providers and reconcile the complete dependency/test graph

**Files:** old mapping/write-field helpers and DI hooks; active csproj/lock files/central pins; temporary Pilot folders; affected Foundation/library-specific tests; current guidance listed in ADR 0009; case ledger and evidence.
**Consumes:** T03–T05 completed conversions. **Produces:** one production DAL, reviewed dependency graph, no orphaned tests or misleading current guidance.

- [x] Remove RepoDbMappingInitializer/IRepoDbMapping/AddRepoDbMapping plumbing and old Catalog/Inventory RepoDb helpers only after their final consumers are converted. Remove every pilot-only adapter and provider-comparison switch.
- [x] Replace library-specific test probes with LINQ plus independent native-SqlClient assertions, retaining all transaction/resolver/lifetime/failure guarantees. Map renamed/replaced logical cases and theory rows explicitly; parked history remains historical, not deleted coverage.
- [x] Remove obsolete RepoDb/Dapper package references from active code/test graphs and unused central pins, regenerate affected locks deliberately and verify locked restore. Audit parked AppCore/reactivation references without reactivating or falsely testing them; document required future lock refreshes.
- [x] Add boundary/dependency checks for provider leakage, zero active RepoDb/Dapper references, explicit registration, no IQueryable exposure and no newly introduced SQL grants/schema edits. Preserve native independent test setup rather than routing assertions through the new DAL.
- [x] Update current AGENTS/TECH_STACK/PERSISTENCE/ARCHITECTURE/PRD/DEVELOPMENT_PLAN and affected configuration/testing/release guidance only where code is now converted. Distinguish delivered LINQ/native persistence from proposed/unfinished work, preserve active net48 and keep historical evidence attached to the provider/revision that produced it.
- [x] Reconcile all active source cases, owners, TFMs and Theory rows after code freeze. Explain additions/replacements/removals without using a historical aggregate as the required total. Run T06's focused checks, verify the final project/command list and commit the frozen candidate.
- [x] **Exit:** no production or active test old-ORM dependency, no unexplained lost assertion, no schema/grant drift; source/build/selection checks do not claim SQL acceptance.

**Executed result:** 585 final selected gate invocations passed plus27 overlapping focused fresh fixture invocations, zero failed/skipped; all ten active locked restores, host/migrator builds, independent review and exact case/row reconciliation passed. [Evidence](../L2DB_VERIFICATION.md#l2db-t06--transitional-provider-and-test-graph-cleanup). Source provider cutover is delivered; T07 release/package/manual acceptance is pending.

### L2DB-T07 — Frozen-revision acceptance, packaging and handoff

**Files:** sanitized `docs/L2DB_VERIFICATION.md`, completed logical-case ledger and task/status checkboxes only after supporting evidence. This is a planned L2DB milestone, not an automatic final gate at every code-PR boundary.
**Consumes:** frozen T06 commit, reviewed complete case/project list, designated Windows/SQL environment.
**Produces:** plan-specific final acceptance tied to an exact commit, packaged Local smoke and an accurate deferred-backlog handoff.

- [x] Restore/build locked dependencies for the exact mandatory project/TFM matrix. Confirm the frozen commit, selected cases, short local result paths and designated resources. Build/discovery checks are not SQL/GUI execution.
- [x] Run the planned final matrix in section 6 directly with dotnet test: unfiltered sequential passes across all six SQL owners plus required Application/Architecture/Session projects. Review source cases and TRX outcomes including Theory rows; require zero mandatory skips and positive owned cleanup.
- [ ] Run the existing net48 release packaging command against that same revision and perform this plan's packaged Local smoke: login, Product CRUD/lookup refresh, receipt draft/post/inquiry, permission deny, explicit assignment import and logout/relogin; run any maintenance smoke with separate authority. Verify native SqlClient assets/binding redirects and unchanged schema/history/assignments. Record manual observations independently of automated test totals.
- [x] Evaluate whether changed UI behavior or a packaged-smoke regression requires automated Desktop GUI tests. If applicable, run the existing Desktop project directly on interactive Windows, retaining its STA/serialization/cleanup guards, and record the reason/result. Otherwise record automated GUI tests as NOT RUN with its scope rationale; it is not a default full GUI requirement for DAL-only task commits.
- [x] If a code/package/test-selection fix follows a failed or green gate, repair with focused affected tests, then rerun the affected complete required final gate. Do not reuse stale acceptance to force a single execution.
- [x] Publish only sanitized aggregates with exact revisions/commands, pass/fail/skip/not-run counts, ownership/cleanup outcomes and unexecuted/manual gates. Keep raw TRX, Theory values, SQL identifiers and connection data local.
- [x] Record operational rollback to the compatible post-security-rebuild binary and mark only evidence-supported tasks complete. Record remaining P5/P6/HTTP/runtime work in [deferred backlog](../DEFERRED_WORK.md), without activating or waiving it. **Exit:** required correctness/package/smoke gates passed or explicit blockers; no claim of deferred delivery completion or measured performance improvement.

**Checkpoint:** frozen de3df037 F/C/I/S/M/O/A/X/D581/0/0 and clean package205-file reconciliation passed. W is conditionally NOT RUN; no candidate fix was needed. Authenticated package workflows remain BLOCKED / NOT RUN pending a new exact test-runtime login authorization or suitable existing runtime identity; the package/smoke checkbox stays open. [Evidence](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke). T07 is not complete and no overall L2DB/release acceptance is claimed.

## 6. Per-task verification and final owners

All aliases below refer to current existing **net48** test projects. F/C/I/S/M/O are six distinct SQL owners; the legacy-named O project is not the pre-modular full SQL suite.

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
| D | `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` |
| W | `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` |

A focused command is `dotnet test <project> -c Release -f net48 --filter <actual selector>`. Categories use `Category=...`; classes/methods use `FullyQualifiedName~...` or an exact match. New named tests below are created by the owning tasks; selectors become usable when the actual methods exist. The case ledger records concrete commands and expected cases, without a separate executable task registry.

| Task | Non-SQL/build scope | SQL scope | Required behavior |
|---|---|---|---|
| T00 | Static source/package/case audit; discovery if useful | No automatic SQL for planning | Accepted RESP baseline, owners/TFMs and real selectors |
| T01 | X `LinqToDbBoundaryTests`; pure F mapping/cleanup cases; host/migrator build | F/FastSQL; full affected SQL checkpoint if shared connection/UoW/mapping changes | Borrowed ownership, late transaction, cancellation, provider rejection and retryable mapping failure |
| T02 | X boundaries and actual pure parity/DTO checks | F/FastSQL, C/FastSQL, I/FastSQL, S/SecurityProvisioning | Three pilots, independent SQL oracle, rollback and denied generic writes |
| T03 | A Catalog behavior and D affected lookup ViewModels | C/FastSQL; I/FastSQL for changed Catalog/reference dependencies | Four catalogs, employee linkage, search/field allowlists and mixed-provider transaction |
| T04 | A receipt/posting/inquiry handlers and validators | I/FastSQL, I/SqlConcurrency, C/FastSQL | Concurrent/duplicate posting, locked references, decimal/rowversion and reconciliation |
| T05 | A authorization/import; D expiry/relogin; pure S/M configuration/maintenance | S/SecurityProvisioning, S/SqlConcurrency, M/MigrationSchema, O/SecurityProvisioning | Real principal, stale actor/preview, protected account, procedure/audit; current Local login uses SecurityProvisioning |
| T06 | X boundaries/dependencies and pure F cleanup/init (F lifetime is SQL/FastSQL) | F/FastSQL plus affected categories; all six for shared ownership/mapping changes | Package cleanup, retained/replaced case review and unchanged schema/grants |
| T07 | Unfiltered A, X, D; required host/package build | Unfiltered F, C, I, S, M, O sequentially | Complete SQL correctness, package/manual Local smoke; W only when its documented GUI trigger applies |

T00 corrects any source/category mismatch before execution. Never reclassify SQL as pure code to obtain a convenient selector. A filtered pass proves only its actual scope. Shared UoW, connection, mapping, fixture or migration changes require every affected SQL owner before dependent rollout; all six participate when the shared contract affects them. T07 remains the final milestone, while full suites are not defaults after each edit/task/PR.

Run one SQL project at a time on the designated non-production instance. Keep accepted RESP reset/SQL timeouts, local ownership checks and drained workers. There is no global lease, external watchdog or automatic orphan cleanup. Record aborted/timeout/cleanup failures honestly and review leftover databases manually; never drop them by prefix. Use short fresh local results directories and preserve connection secrecy.

## 7. Direct command templates

Run from a Windows checkout with pinned submodules initialized, .NET Framework 4.8 targeting/runtime support and the SDK selected by `global.json`. These examples are command templates, not executed acceptance. Stop on every nonzero native-command exit. T00 establishes source/dependency/case mappings; it has no runner bootstrap step.

### Focused non-SQL example: implemented T01 boundary tests

```powershell
$project = 'tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~LinqToDbBoundaryTests'
if ($LASTEXITCODE -ne 0) { throw 'L2DB boundary tests failed.' }
```

Use the actual project/filter for each task. This class exists; both cases passed in T01's unfiltered 10-case Architecture run. An empty selection is still NOT RUN. `--list-tests` may help inspect a filter but is not execution evidence.

### Focused SQL example: Foundation / FastSQL

Precondition: `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` explicitly selects the operator-designated non-production SQL Server and effective database `master`, as documented in [tests/README](../../tests/README.md). No approval file or external preflight is required. Missing configuration skips SQL facts and blocks mandatory SQL acceptance.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
$results = 'TestResults/l2db-f-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'SQL restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=foundation.trx' `
  --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'Foundation SQL tests failed.' }
```

Substitute the exact project/category from section 6, or a source-verified method filter for narrower work. Review selected and executed cases/rows; successful exit alone cannot establish mandatory coverage when tests skip.

### Shared-change checkpoint and frozen final matrix

Omit `--filter` for a complete owner pass. For shared changes, run every affected SQL owner sequentially using the loop in [tests/README](../../tests/README.md#complete-sql-milestone). T07 requires that complete six-owner loop on the frozen candidate and the accepted RESP fixture mode, plus unfiltered A/X/D:

```powershell
$owners = @('MyDmsVn.Server.Application.Tests',
  'MyDmsVn.Architecture.Tests', 'MyDmsVn.Desktop.Session.Tests')
$results = 'TestResults/l2db-pure-' + [guid]::NewGuid().ToString('N').Substring(0,8)
foreach ($name in $owners) {
  $project = 'tests/' + $name + '/' + $name + '.csproj'
  dotnet restore $project --locked-mode
  if ($LASTEXITCODE -ne 0) { throw ('Restore failed: ' + $name) }
  dotnet test $project -c Release -f net48 --no-restore `
    --logger ('trx;LogFileName=' + $name + '.trx') `
    --results-directory $results
  if ($LASTEXITCODE -ne 0) { throw ('Test project failed: ' + $name) }
}
```

Record exact commands/revision, actual TRX cases including Theory rows, outcomes and cleanup. Explain each added/replaced case against the reviewed source ledger. No current manifest, fingerprint update, Task/Final command or custom reconciliation service is required. Build/discovery alone is not final acceptance.

### Required package smoke and conditional GUI

Run packaging and smoke after test commands finish in the checkout. T07 requires a Local validation package and the manual scenarios listed in its task:

```powershell
[string] $revision = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve package revision.' }
$version = '0.0.0-l2db.' + $revision.Trim().Substring(0,8)
$output = Join-Path (Get-Location).Path ('artifacts/release/' + $version)
& ./deployment/release/Publish-LocalRelease.ps1 -Version $version -OutputRoot $output
```

This creates a local validation package, not a production release/version assignment. Publish errors are terminating. Do not use SkipPublish for dependency validation or overwrite an existing release output. Verify Desktop/admin package metadata, dependencies, native SqlClient assets and binding redirects. [RELEASE-MANIFEST-01](../DEFERRED_WORK.md#release-manifest-01--observed-schema-metadata-discrepancy) is closed narrowly by T02's source fix and clean-commit d8013030 artifact reconciliation ([evidence](../L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed)); each later package must still match its shipped runtime policy.

If changed UI behavior or a packaged-smoke regression makes automated Desktop GUI tests applicable, run W directly on an interactive Windows session:

```powershell
$project = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --logger 'trx;LogFileName=desktop.trx' `
  --results-directory ('TestResults/l2db-gui-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Desktop GUI tests failed.' }
```

Retain existing STA, serialization, local bounded waits and deterministic cleanup. Any new fatal/non-cooperative probe must provide its own bounded subprocess; current packaging subprocess tests do not have an outer timeout. Run one GUI command at a time; no external launcher/lease remains. Automated GUI or packaging alone does not prove manual login/catalog/posting/security workflows. Record those observations separately with artifact hashes and revision. Never run bootstrap/migration on an unreviewed database as a package check.

## 8. Evidence, success and rollback

L2DB_VERIFICATION records each task's revision/worktree state, project/TFM, source cases/Theory rows, selector/category, rationale, exact commands, expected/observed assertions, pass/fail/skip/BLOCKED/NOT RUN, cleanup and interruption outcomes, retained native SQL and package mapping. Keep raw TRX, import/connection data and SQL identifiers local; publish sanitized summaries.

The migration succeeds when DAL conventions are consolidated, all required invariant/package/smoke checks pass, current data/schema/grants remain intact, active net48 is preserved, and RepoDb/Dapper are absent from the active execution graph. No fixed percentage speed target is imposed and no historical result becomes candidate evidence.

If the pilot fails, stop rollout and leave production composition unchanged. A later conversion failure is reverted through its scoped code/package/registration changes on the unchanged supported schema. No database reset, old-security restoration or permanent dual-provider fallback is allowed. Scope-appropriate verification follows TEST_POLICY; T07's SQL/package/smoke breadth remains mandatory. Relevant later changes invalidate their affected evidence and require the applicable rerun after focused repair.

T00 is complete with the accepted RESP commit binding, source inventory and static checks in [L2DB_VERIFICATION](../L2DB_VERIFICATION.md). T01 is complete with net48 ownership/package evidence in the same record; T02 is complete with final GO after repair/package/startup evidence; T03 is complete with Catalog conversion evidence; T04/T05 module conversions and T06 transitional dependency/test cleanup are complete with scoped evidence. T07 frozen-revision automated correctness/package checks now pass; authenticated interactive smoke remains blocked pending a new restricted-runtime identity approval. Its package/smoke checkbox remains open. Source cutover does not imply final package/manual or release acceptance. The framework removal does not implement either migration or establish net48/SQL/package acceptance.

**Candidate reference:** [official linq2db 6.3.0 package](https://www.nuget.org/packages/linq2db/6.3.0); [official ownership API](https://linq2db.github.io/api/linq2db/linq2db--LinqToDB.Data.ConnectionOptions.html). Published API/assets guide the pilot; they do not prove runtime compatibility or disposal correctness.
