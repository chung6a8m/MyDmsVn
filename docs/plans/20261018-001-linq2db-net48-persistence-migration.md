# L2DB — LINQ to DB persistence migration on active net48

> **For agentic workers:** Use `superpowers:executing-plans` or `superpowers:subagent-driven-development` to implement this plan task by task after review. Checkboxes describe work still required, not evidence of completion.

**Date:** 2026-10-18
**Status:** Owner-approved direction 1A + 2A + 3A; implementation plan proposed; all implementation tasks NOT RUN.
**Goal:** Replace the active RepoDb/Dapper application DAL with LINQ to DB and a documented native-SqlClient escape surface, preserving Local v1, active net48, post-rebuild security, existing data and all correctness gates.
**Architecture:** Existing Clean Architecture modular monolith and explicit UoW. Infrastructure-only mapping/connection adapters; unchanged Application/Contracts/Desktop boundaries; DbUp remains the sole schema owner.
**Tech stack:** WinForms, CommunityToolkit.Mvvm, net48 product/SQL/Desktop, tool-only net8.0 runner, LINQ to DB (initial candidate 6.3.0), current Microsoft.Data.SqlClient, SQL Server, DbUp, MediatR, FluentValidation, ErrorOr, Serilog and xUnit.
**Spec:** [ADR 0009](../adr/0009-linq2db-local-net48-persistence.md).
**Pinned assessment base:** `8cfa25351338666b1a751ee516d5a479cdb37b7f`, `main`, PR #35 merge, 2026-10-18 09:23:30 +07:00.
**Planning branch:** `plan/20261018-linq2db-net48-migration`, created directly from that SHA.

## 1. Reassessment of the actual latest main

| Area | Observed at the pinned base | Consequence for this plan |
|---|---|---|
| Runtime | NET8-PAUSE PR #32 is merged. Product/SQL/Desktop are active net48. Runner and runner tests are tool-only net8.0; AppCore and historical modern cases are parked. Infrastructure csproj explicitly declares net48. | 3A means **keep this profile**, not dual-target restoration and not net10. |
| Permissions | SEC-REBUILD PR #34 is merged; its latest verification records Final 775/775 and packaged GUI D2 138/138 at `0303a91111a514276aa02c6664c25dc0caf58d9e`. | Reuse implemented permissions, procedures, session guards and acceptance contracts; do not implement the rebuild again. |
| Test runner | PR #35 is merged. Its final reviewer-round-3 entry records 11/11 gates and 793/793 at `6d71b711e5cafb38b9f1152efcb520b6c35472f4`, including tool-only runner tests; no GUI manual acceptance is claimed. | Use metadata preflight, workspace-output lease, isolated inventory builds and current lease recovery. Do not treat 793 as an SQL-only count or as a run performed for this plan. |
| Remaining delivery | P5 interactive/P6 operational gates remain open; P7 is future and runtime-reactivation-gated. | This migration does not close, waive or silently absorb those gates. |
| DAL | Catalog and receipt writes use RepoDb; read projections and security administration use Dapper; important inventory SQL already uses native SqlClient. | Consolidate deliberately; retain proven native locking/procedure paths rather than translate every SQL statement to LINQ. |
| UoW | `SqlUnitOfWorkFactory.cs` owns an open IDbConnection; its nested UoW holds the optional current transaction and enforces completion/disposal states. Repository factories receive ISqlExecutionContext. | Borrow the current resources per operation, with non-owning LINQ wrappers and no transaction captured before BeginTransaction. |
| Tests | Modular SQL owners exist. Foundation tests explicitly exercise RepoDb/Dapper sharing, mapping initialization and repository lifetime. | Preserve/replace assertions with a logical-case ledger; deleting library-specific test names is not permission to lose their invariants. |
| Document drift | PRD/ARCHITECTURE still contain older dual-runtime text; ADR 0007/index and TEST_GATE_MATRIX contain stale SEC status; plan index still calls PR #35 under review. | Use AGENTS, actual declarations, merge SHA and latest evidence sections for status. Fix relevant current guidance at cutover; preserve historical evidence. |

Read `AGENTS.md`, the ordered [plan index](README.md), ADR 0009, `docs/PRD.md`, `docs/ARCHITECTURE.md`, `docs/TECH_STACK.md`, `docs/PERSISTENCE.md`, ADRs 0002/0003/0004/0005/0007/0008, `docs/SEC_REBUILD_VERIFICATION.md`, `docs/CODEX_TEST_EXECUTION_POLICY.md` and `docs/TEST_GATE_MATRIX.md` before implementation.

This is a source/document assessment, not a fresh build, dependency restore, benchmark or SQL/GUI acceptance. Earlier SQLPERF numbers and NET8-PAUSE's waived duration comparison cannot establish a LINQ-to-DB speedup.

### Execution order and authority

Prerequisites already merged: NET8-PAUSE -> TESTGATE modular projects -> SEC-REBUILD -> PR #35 stabilization P0/P1. Their historical acceptance is context, not work to redo. STAB-T03/T04 manifest-v2 follow-ups remain separate; they are not a prerequisite for this v1-compatible migration.

The new owner request schedules the reviewed L2DB work before final remaining P5/P6 acceptance, so those later checks exercise the resulting stack. No P5/P6 completion is implied. P7 stays future. T00 must re-resolve any newer implementation base and reconcile drift before code changes; never reset/stash/overwrite the owner's local workspace or claim uncommitted work was assessed.

ADR 0009 overrides only the prior fixed DAL-library choice for this scope. All other current constraints remain. This planning change touches documentation only, not source, packages, migrations, test manifests, evidence or CI.

## 2. Global constraints

- Keep product, SQL and Desktop **net48**; keep existing runner/test tool **net8.0**. Leave AppCore outside solution/release and historical modern cases PARKED / NOT RUN. No net8 reactivation or net10 upgrade.
- Keep Local v1 -> HTTP v2 and identical DTO/result semantics. No API host, new auth stack, UI replacement, new business module, generic repository framework or arbitrary service locator.
- Keep existing UoW state/ownership, explicit transactions, scoped registrations and cancellation. No concurrent use of one UoW, ambient/static/AsyncLocal transaction or hidden extra connection for participating work.
- LINQ to DB types/mappings stay in Infrastructure. Materialize DTO results before disposal; no IQueryable, provider objects or mapping attributes in inner/UI/contract layers.
- Preserve post-SEC-REBUILD authorization, maintenance/runtime separation, legacy password columns, import limits/revision/digest rules and atomic audit. Do not broaden SQL grants to accommodate an ORM.
- Preserve the supported current schema, DbUp scripts/history and data. This plan does not authorize schema changes, reset, seed, destructive migration or replay of the pre-rebuild permission baseline.
- Pin and verify `linq2db` 6.3.0 as an initial compatibility candidate; keep other package pins unless a reviewed compatibility blocker requires a separate decision. Do not claim the candidate is newest or already restored. Track all affected lock files, native dependencies and net48 binding redirects.
- Keep current SQL isolation/serialization and TESTGATE v1. No Respawn, reuse/snapshot reset, parallelism change, container requirement, fingerprint-v2 rollout or CI expansion.
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
**Consumes:** pinned assessment, ADR 0009, actual current declarations and PR #35 tooling.
**Produces:** reviewed source/package/native-SQL inventory, logical-case ledger and executable Task/Final bindings.

- [ ] Record actual implementation HEAD, clean/dirty state, submodule SHAs, active product/test/tool targets and changes since the assessment. Resolve conflicts without discarding local work; verify prerequisite evidence at its recorded revisions.
- [ ] Inventory every RepoDb/Dapper usage in `src`, `tests`, deployment and active csproj/lock graphs. Classify production, test oracle, pilot, parked/history and unused central pin; identify the owning conversion task. Record immutable production SQL script/grant hashes and supported schema/journal baseline.
- [ ] Record affected baseline assertions with durable logical IDs, owner, TFM, classification and theory-row identities. Classify retain/replace with explicit replacement assertions; never delete safety behavior merely because its name mentions an old library.
- [ ] Add the T00 gate selecting existing runner manifest/identity contract tests and define the new T07 Final using all current owners. Add later Task selectors only with their actual test methods in the corresponding task; planned empty selections are NOT RUN, not passing gates. Preserve parked history/exemptions and independent review.
- [ ] Run the T00 commands in section 7, inspect exit/source/discovery/TRX and record results. Baseline metadata preflight is allowed; do not rerun the old unfiltered SEC-REBUILD envelope merely to establish this mapping.
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
- [ ] Write a receipt pilot combining LINQ writes with native SQL in one existing UoW, asserting same-session uncommitted visibility, returned IDs/rowversion, commit and injected mid-operation rollback of header/lines/ledger/balances. Use existing approved fixture ownership, not fixture reuse.
- [ ] Write a security pilot that invokes an existing allowed procedure using the candidate adapter under a real runtime principal, exercises result sets/error translation and outer transaction rollback, and proves denied generic RBAC/binding writes remain denied.
- [ ] Run focused L2 against deterministic disposable fixtures using the old and candidate code separately. Never shadow-write a real operation through both providers. Record SQL shape, commands/round trips, parameter types, dependency footprint and implementation complexity; timings are observations, not a suite-speedup claim.
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
- [ ] Retain native multi-result/window/reconciliation SQL where needed; replace only its execution/mapping layer when justified. Preserve chronological stock-card tie-breakers, starting/running balances, filters and paging.
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
- [ ] Update current AGENTS/TECH_STACK/PERSISTENCE/ARCHITECTURE/PRD/DEVELOPMENT_PLAN and affected configuration/testing/release guidance to describe actual converted code and the active net48 profile. Correct stale SEC/PR #35 status by citing latest evidence, not rewriting past reports.
- [ ] Review all selected fingerprint changes during L2 and reconcile **all** active owner/project/TFM/theory identities after code freeze. Never auto-approve digests or use the old 775/793 count as the required total. Run T06 L2 and metadata-only Final preflight; commit the frozen candidate.
- [ ] **Exit:** no production or active test old-ORM dependency, no unexplained lost assertion, no schema/grant drift; preflight succeeds without claiming SQL acceptance.

### L2DB-T07 — Frozen-revision acceptance, packaging and handoff

**Files:** sanitized `docs/L2DB_VERIFICATION.md`, completed logical-case ledger and task/status checkboxes only after supporting evidence.
**Consumes:** frozen T06 commit, reviewed complete manifest, approved Windows/SQL environment.
**Produces:** strict acceptance tied to an exact commit plus explicit remaining P5/P6 handoff.

- [ ] Build/restore locked dependencies for the exact mandatory project/TFM matrix and run metadata-only preflight. Confirm clean commit, short local artifact paths and approved resources. Preflight is not SQL/GUI execution.
- [ ] Run one planned manifest-owned Final across the complete active matrix in section 6. Preserve strict unfiltered sequential SQL passes, source/discovery/TRX reconciliation, zero mandatory skips, workspace/global leases, path safeguards, watchdog and positive owned-database cleanup.
- [ ] Run guarded net48 GUI D2 and existing release packaging command against that same revision. Exercise the packaged Local host: login, Product CRUD/lookup refresh, receipt draft/post/inquiry, permission deny, explicit assignment import and logout/relogin; run maintenance with separate authority. Verify native SqlClient assets/binding redirects and unchanged schema/history/assignments.
- [ ] If a code/package/manifest fix follows a failed or green gate, repair with focused L2 then rerun the affected complete required final gate. Do not reuse stale acceptance to force a single execution.
- [ ] Publish only sanitized aggregates with exact revisions/commands, pass/fail/skip/not-run counts, ownership/watchdog outcomes and unexecuted/manual gates. Keep raw TRX, journals, fingerprints, connection and approval data local.
- [ ] Record operational rollback to the compatible post-SEC-REBUILD binary, mark only proven tasks complete and hand off to [remaining P5/P6](20261016-004-remaining-p5-acceptance-and-p6-hardening.md). **Exit:** correctness/package gates passed or explicit blockers; no claim of P5/P6 completion or measured performance improvement.

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
| T01 | X `LinqToDbBoundaryTests`; non-SQL mapping/cleanup cases in F; host/migrator builds | F/FastSQL | Borrowed ownership, late transaction, cancellation, bad provider, mapping failure; `Task(T01)` + `Sql(F,FastSQL)`. |
| T02 | X boundary cases; pure parity-fixture/DTO checks only where actually added | F/FastSQL; C/FastSQL; I/FastSQL; S/SecurityProvisioning | Three pilots, independent SQL oracle, injected rollback/denied generic writes; `Task(T02)` if nonempty plus listed Sql calls. |
| T03 | A Catalog handlers/validators and D affected lookup ViewModels, exact reviewed methods | C/FastSQL; I/FastSQL for changed Catalog/reference cross-dependency | Four catalogs, employee linkage, search/field allowlists, mixed-phase transaction; `Task(T03)` + listed Sql calls. |
| T04 | A receipt/posting/inquiry handlers/validators, exact reviewed methods | I/FastSQL; I/SqlConcurrency; C/FastSQL | Duplicate/concurrent posting, locked references, decimals/rowversions and reconciliation; `Task(T04)` + listed Sql calls. |
| T05 | A `SuperuserAuthorizationTests`, `PermissionAuthorizationTests` and import tests; D current expiry/relogin cases; S/M pure config/maintenance tests | S/SecurityProvisioning; S/SqlConcurrency; M/MigrationSchema; O/SecurityProvisioning for current Local login cases | Real principal, stale actor/preview, protected account, procedure mapping/audit; `Task(T05)` + listed Sql calls; the current Local session case explicitly uses SecurityProvisioning; recheck if moved. |
| T06 | X boundary/dependency checks; R repository/manifest contracts; F non-SQL lifetime/cleanup/init checks | F/FastSQL plus categories actually affected by final consumer removal | Package cleanup, test-ledger bijection, no schema/grant drift; `Task(T06)` + applicable Sql calls + Final preflight. |
| T07 | All required non-SQL project owners | **Unfiltered F,C,I,S,M,O**, active net48 | `Final(T07)` across A,X,H,D,R and six SQL owners; separately guarded W GUI D2/package/manual checks. |

T00 must replace any category/selector mismatch with a source-reviewed binding and record the reason before execution. Unsupported/empty selection blocks that gate; do not reclassify a test just to fit a command. SQL wrappers filter one category in one project, not arbitrary module+method combinations. During L1 choose smaller correct non-SQL selectors; SQL L1 remains guarded. Full suites are not task-boundary defaults.

**Budgets/resources for every row:** non-SQL Task gates use `commandTimeoutSeconds=300`, `overallTimeoutSeconds=900` initially; R repository-contract/inventory checks may use the existing reviewed 900/3600 budget. Final SQL gates retain the existing reviewed 1200/7200 limits and runner-enforced timeouts; focused SQL uses its current bounded defaults, with no invented timeout CLI switch. Longer timeouts require a documented reviewed reason, not disabling a watchdog. Use a fresh short local artifact root per run, the existing 240-character path checks, valid approval, matching non-production instance and positive cleanup. No manual deletion of leases/journals or broad pool clearing.

## 7. Runnable command templates and binding rules

Run from a short-path Windows checkout with pinned submodules initialized, .NET Framework 4.8 targeting pack and the existing .NET 8 SDK for tooling. Commands below are **planned**, not executed for this documentation change. Check every native command's exit code; stop on failure. Test artifact roots are examples of short ignored local paths with fresh suffixes, never committed evidence.

### Bootstrap T00 before the new manifest entries exist

```powershell
dotnet restore MyDmsVn.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
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

Use the table's exact task ID for each row after adding its real reviewed selectors. Task mode is not an SQL invocation and accepts no ad-hoc project/TFM/filter override. PR #35 permits unselected fingerprint drift in Task only while complete method ownership/classification/shape stays valid; selected methods still require review. T06/Final reconciles all fingerprints, including transitive changes from new dependencies.

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

### Metadata preflight and frozen Final

T00/T06 must bind the new `L2DB-T07` Final to the current complete matrix. Before that binding exists, do not invoke a nonexistent new gate. A baseline metadata-only check can use the existing SEC-REBUILD-T06 binding, clearly labeled as baseline preflight, never as new-plan acceptance.

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

### Guarded net48 GUI and local package validation

Run after Final completes, not concurrently in the same checkout. The GUI wrapper pins net48 internally and requires the exact clean HEAD; its runner must already be built. Do not add a fictitious Frameworks parameter.

```powershell
[string] $revision = git rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve GUI revision.' }
$root = '.sqlperf/gui-' + [guid]::NewGuid().ToString('N').Substring(0,8)
& ./tests/tools/Invoke-GuiTestAcceptance.ps1 `
  -RepositoryRoot (Get-Location).Path `
  -TestProject 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj' `
  -ArtifactRoot $root -Revision $revision.Trim() -TimeoutSeconds 900
if ($LASTEXITCODE -ne 0) { throw 'GUI D2 acceptance incomplete.' }

# A local validation package, not a production release/version assignment.
$version = '0.0.0-l2db.' + $revision.Trim().Substring(0,8)
$output = Join-Path (Get-Location).Path ('artifacts/release/' + $version)
& ./deployment/release/Publish-LocalRelease.ps1 -Version $version -OutputRoot $output
```

Publish errors are terminating. Do not use SkipPublish for dependency validation or overwrite an existing release output. The current script emits separate net48/x86 Desktop and admin-migrator packages; verify their manifests, dependency/binding redirects and SqlClient native assets, then launch the packaged application on an approved environment. Packaging/GUI D2 alone does not prove the manual login/catalog/posting/security scenarios. Record those separately with artifact hashes, exact revision and outcomes. Do not run bootstrap/migration against an unapproved database as a package smoke check.

## 8. Evidence, success and rollback

L2DB_VERIFICATION must record per task: exact commit/worktree status, source/owner/TFM, selector/category, rationale, actual commands, expected/observed assertions, pass/fail/skip/BLOCKED/NOT RUN, approval result only, timeout/watchdog/lease/owned-cleanup outcome, retained native SQL and package mapping, plus sanitized evidence location. Never copy raw approval, connection strings, TRX, import payloads or ownership journals into Git.

The migration succeeds when application DAL conventions are consolidated, all invariant and packaging gates pass, current data/schema/grants remain intact, active net48 and parked history are preserved, and RepoDb/Dapper are absent from the active execution graph. No fixed percentage test-speed target is imposed and no historical baseline is relabeled as candidate evidence.

If the pilot fails, stop rollout and leave production composition unchanged. If a later conversion fails, revert its scoped code/package/registration change on the unchanged supported schema. No database reset, old-security restoration or permanent dual-provider fallback is allowed. Separately mergeable implementation PRs each require their own final acceptance; the default plan is one staged code-bearing PR with focused task commits and a single planned final envelope.

**Planning verification:** source/docs and actual wrapper signatures were reviewed against the pinned SHA; no implementation package restore, build, SQL, GUI, benchmark or production mutation was performed. All T00–T07 checkboxes remain open. Review the plan before implementation.
