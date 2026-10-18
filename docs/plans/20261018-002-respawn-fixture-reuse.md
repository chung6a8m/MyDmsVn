# Respawn fixture reuse on net48 — implementation plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. This document authorizes no implementation or SQL execution in the documentation refresh.

**Status:** Owner-selected direction; implementation pending / NOT IMPLEMENTED, 2026-10-18. Source assessment: `c7ad32a4b0040f87417973eabd6c12757c54f7d5`. No package restore, runtime compatibility, reset correctness or performance result is claimed.
**Order:** First active plan; complete its accepted fixture/test contract before the [LINQ to DB migration](20261018-001-linq2db-net48-persistence-migration.md).
**Goal:** Amortize database creation and migration across eligible SQL tests while proving that each test receives the same clean, deterministic starting state as a fresh database.
**Architecture:** A reusable database belongs to one xUnit v2 collection in one assembly, process, run and TFM. A test-only harness owns allocation, reset, seeding and teardown; each test gets an exclusive scope and its own application/connection resources. Tests outside the reviewed reset boundary continue using fresh owned databases.
**Tech stack:** Active SQL/product/Desktop `net48`; shared harness `netstandard2.0`; runner and runner tests tool-only `net8.0`; xUnit `2.9.3`; DbUp `7.2.0`; central `Microsoft.Data.SqlClient` `7.0.2`; proposed Respawn `4.0.0`.
**Spec:** The owner's 2026-10-18 fixture-reuse decision, [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md), and existing ownership/isolation contracts in [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md).

## 1. Global constraints

- Keep all current application/SQL/Desktop TFMs, parked AppCore and historical net8 inventory unchanged. No runtime upgrade, container requirement, CI rollout, production DAL change or SQL parallelism rollout belongs to this plan.
- Keep SQL and WinForms assembly serial guards and cross-process SQL leases. A collection name or static lock cannot establish ownership across testhosts; preserve unique generated database names and bind fixture metadata to the actual owner.
- Allocate only through the guarded test database lifecycle. Require the existing operator-maintained, instance-bound non-production approval and an administrative connection explicitly targeting `master`; revalidate live database ownership before destructive reset and final drop.
- Preserve DbUp scripts and `dbo.SchemaVersions`, the `MyDmsVn.TestOwnership` database extended property, durable ownership records, quarantine/recovery and positive cleanup proof. Never reset an application, unknown or merely name-matching database.
- Reset mutable business and security rows, then seed explicitly. Do not exclude `Users`, role/permission assignments, `SystemSecuritySettings`, `SecurityAuditEvents`, catalogs, receipts, balances or ledger data to conceal an incomplete reset.
- Keep schema/history changes, SQL principal/GRANT/role mutation, bootstrap/maintenance mutation and deliberately destructive/fault or lock-contention cases on fresh dedicated resources. Responsibility categories remain independent of reset eligibility.
- A reset, seed, ownership/schema validation or teardown failure poisons the fixture. Fail affected tests and reject subsequent users; never silently skip, recreate or switch to fresh mode to report a pass. Fresh mode is an explicit reviewed execution choice.
- Keep every existing assertion and logical case unless a separately explained replacement preserves its behavior. Do not weaken inventory checks, refresh fingerprints without review or use historical aggregate counts as present acceptance thresholds.

## 2. Version and reset contract

Respawn 4.0.0 publishes `net461` and `netstandard2.0` assets; later checked releases 5.0.1/6.2.1/7.0.0 target `netstandard2.1`, which cannot serve the active .NET Framework tests. Pin 4.0.0 in the test dependency graph and verify it with the current provider under Windows `net48`; its minimum SqlClient dependency does not prove compatibility with 7.0.2. Use the [published package](https://www.nuget.org/packages/Respawn/4.0.0) and [pinned v4 source](https://github.com/jbogard/Respawn/tree/4788e2c51535919221de03edc90d354f4fd7d02f), not current-version examples.

The application-schema profile uses the v4 API:

```csharp
var checkpoint = new Respawn.Checkpoint {
    DbAdapter = Respawn.DbAdapter.SqlServer,
    SchemasToInclude = new[] { "dbo" },
    TablesToIgnore = new[] { "SchemaVersions" },
    WithReseed = true,
    CommandTimeout = 60
};
await checkpoint.Reset(openDbConnection);
```

`TablesToIgnore` is `string[]` matched by table name: `"dbo.SchemaVersions"` and modern `Table`/`Respawner` APIs are incorrect here. Configure no conflicting schema exclusion list. Reject unexpected schemas or ambiguous journal names before reuse. The separate Foundation test migration uses `dbo.TestSchemaVersions`; only a separately reviewed profile that actually uses that schema may preserve that journal. [Checkpoint](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/Checkpoint.cs) and [SQL Server adapter](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/SqlServerDbAdapter.cs) define these behaviors.

- Create one checkpoint after migrations finish for each allocated database; first reset builds cached metadata. Never transfer it to another database or continue after schema/profile changes. Complete the initial reset and baseline verification before publishing the fixture as ready.
- `Reset(DbConnection)` uses the caller's open connection, starts its own transaction and has no cancellation-token overload. Use a dedicated short-lived reset connection, without a caller transaction. `CommandTimeout` bounds delete/reseed commands; metadata queries retain provider defaults. Keep an outer watchdog, close the owned reset connection on cancellation/timeout, await operation termination and poison the fixture before releasing access. A timed-out wait is not proof that SQL stopped.
- **Full-schema trigger blocker:** `007_Inventory_Stock.sql` installs `dbo.TR_StockLedger_AppendOnly`, an `INSTEAD OF UPDATE, DELETE` trigger that unconditionally throws. This affects the Catalog pilot too. Implement a test-only profile that, after positive ownership and exact schema/trigger verification, temporarily disables only this named trigger, resets, restores it in `finally`, and verifies its definition and enabled state. Verify empty-ledger and populated-ledger resets. Any restoration failure poisons the fixture; without this proven profile, use fresh databases. Never ignore `StockLedger` or change the production migration.
- Revalidate constraints, schema, journal contents and ownership after reset. The adapter may handle FK cycles internally; verify that all constraints retain their expected enabled/trusted state. Unexpected objects, triggers or permissions invalidate the cached reset boundary.
- `WithReseed` defaults to false and v4 calculates reseeds from seed minus one. Prove actual `IDENTITY(1,1)` behavior before/after first insert, populated reset and repeated reset. Reset `dbo.GoodsReceiptNumberSequence` explicitly to its reviewed test baseline with test-only SQL, or document relative-number assertions in a profile; Respawn does not reset it. Never expect rowversion, timestamps or database-wide counters to rewind.
- `SecurityAuditEvents` has an FK to `Users`; `SystemSecuritySettings` also references `Users`. Clear and rebuild their state in the real FK graph. Ordinary security data tests may reuse after proof; SQL principals, grants, bootstrap/maintenance and test-injected audit triggers remain dedicated.

## 3. Source map and interfaces

All paths are repository-relative. New paths and APIs below are proposed, not implemented.

| Responsibility | Existing anchors and planned files |
|---|---|
| Owned allocation/drop | Extend `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs`; retain `SqlTestDatabaseName.cs` and `tests/MyDmsVn.SqlTestHarness/{Approval,Preflight,Ownership}/`. Existing `CreateAsync(...)`, `ConnectionString`, `MeasurementContext`, `DisposeAsync()` remain usable for fresh tests. |
| Reuse engine without xUnit dependency | Create `tests/MyDmsVn.SqlTestHarness/Fixtures/{ISqlFixtureDatabase,ReusableSqlFixture,RespawnResetProfile,SqlFixtureCaseExecution,SqlFixtureTestScope,SqlFixtureRunMode}.cs`; add the Respawn package only to test consumers and update affected locks. |
| Migration and measurement | Preserve `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/{MeasuredDatabaseMigrationRunner,TestDatabaseMigrationRunner}.cs`; adapt `tests/MyDmsVn.SqlTestHarness/Timing/` for separately identified fixture and test operations. |
| xUnit v2 adapters | Create `Fixtures/` adapters in the Catalog, Inventory and selected Security test owners. Collection fixture implements `IAsyncLifetime.InitializeAsync()` / `DisposeAsync()` for allocation/migration/drop. An explicit per-case async helper binds method/row/execution identity before reset/seed and drains the scope in `finally`; keep xUnit out of the shared harness. |
| Runner/inventory | Adapt `tests/tools/MyDmsVn.SqlTestRunner/{Gates,Inventory,Reconciliation,Reporting}/`, `Program.cs`, `tests/test-gates.v1.json`, `tests/tools/sqlperf-t01-migration-invocations.json` and `sql-test-nonsql-exemptions.json`, with their existing runner tests. |
| New correctness checks | Create `tests/MyDmsVn.SqlTestHarness.Tests/Fixtures/ReusableSqlFixtureTests.cs` and `tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/ReusableSqlFixtureIntegrationTests.cs`; keep destructive probe allocations fresh. |

The existing helper is internal. Catalog references the IntegrationTests assembly and uses its `InternalsVisibleTo` bridge; Foundation/Inventory/Security/Migrations compile linked copies of allocator/migration helpers. Preserve those actual project boundaries or change them explicitly with owner/discovery reconciliation; do not accidentally compile another owner's tests or introduce a production reference to test infrastructure.

Current source has assembly serial guards in IntegrationTests, Foundation, Inventory, Security, Migrations and Desktop.Tests. Catalog.Tests has no assembly-wide guard; the guard in its referenced assembly does not apply to it. There are no existing xUnit collection-fixture adapters. The Catalog pilot must introduce one explicit collection covering all users of its reusable database; it cannot rely on an inherited serial policy.

The shared lifecycle contract is:

- `ISqlFixtureDatabase`: `string ConnectionString`, `SqlTestMeasurementContext MeasurementContext`, `Task VerifyOwnershipAsync(CancellationToken)`, `Task DisposeAsync()`. `SqlTestDatabase` implements it without exposing ownership tokens to tests or weakening create/drop checks.
- `ReusableSqlFixture`: configured with explicit allocation, migration, schema-verification and reset-profile callbacks; `Task InitializeAsync(CancellationToken)`, `Task<SqlFixtureTestScope> BeginTestAsync(SqlFixtureCaseExecution execution, Func<DbConnection, CancellationToken, Task> seed, CancellationToken)`, `Task DisposeAsync()`. No ambient connection/UoW or static mutable fixture cache.
- `SqlFixtureCaseExecution`: carries the reviewed method-level `LogicalCaseId`, stable `RowKey` (`fact` for a Fact), and a new unique `ExecutionId` per attempted invocation, bound to the fixture's run/owner/TFM. Reconcile them to the existing v1 logical key from normalized signature, TFM, reviewed row and classification; do not replace `LogicalCaseIdentity` with a class name or a new display-name hash. The harness must remain netstandard2.0-compatible and must not reference the net8 runner assembly.
- `SqlFixtureTestScope`: exposes the allocated connection string, registers `void RegisterCleanup(Func<Task> drainAndDispose)` actions and provides `Task DisposeAsync()`. Cleanup cancels/drains workers and disposes test resources before releasing the single active scope; collect cleanup failures without suppressing the original failure.
- `SqlFixtureRunMode`: parses test-only `MYDMSVN_SQL_TEST_FIXTURE_MODE` values `fresh` / `reuse`; unknown values fail before allocation. Use `fresh` during the initial rollout. Before freezing the T04 candidate, select `reuse` as the default for profiles that passed their focused correctness gates; T05 verifies and accepts this default. `fresh` remains an explicit control/fallback mode and the required mode for excluded cases. Record requested and effective mode per case in evidence. No product setting or automatic fallback.

In reuse mode collection initialization allocates/migrates once. Each Fact or Theory invocation makes its first awaited test operation an explicit adapter call, `RunTestAsync(execution, seed, scenario, cancellationToken)`. It passes a reviewed method/row binding from the case map and a newly allocated execution ID; the adapter calls `BeginTestAsync`, verifies health/ownership, resets/seeds/verifies the baseline, and only then invokes the scenario delegate. Its `finally` drains/disposes the test scope; collection teardown drops only its allocation. Tests must not perform scenario SQL before this helper. In fresh mode the same helper/profile/seed/assertions allocate and migrate for each invocation.

xUnit v2 collection `IAsyncLifetime` supports asynchronous allocation and disposal, but its parameterless initialization does not supply the current method or Theory arguments. Do not reset/seed from a parameterless per-test initializer that assumes an ambient current-test identity. Bind explicit stable row keys to the runner's reviewed source/discovery/TRX mapping; reject a missing, unknown or duplicate execution binding before scenario admission. Preserve the existing physical-row locators for reconciliation, without scraping them inside the fixture adapter or reflecting into xUnit internals. [xUnit shared-context guidance](https://xunit.net/docs/shared-context) describes the lifecycle; `IAsyncDisposable` alone is insufficient for v2 fixture disposal.

## 4. Eligibility and review focus

| Initial audit group | Decision to prove, with exact owner |
|---|---|
| `CatalogMappingIntegrationTests` and `CatalogBackendIntegrationTests` | First reusable pilot in `MyDmsVn.Server.Infrastructure.Catalog.Tests`; include all five existing methods. Backend CRUD also inserts `Users` for employee linkage, so four catalog tables alone are not its reset boundary. |
| `GoodsReceiptQueryIntegrationTests`, `InventoryQueryIntegrationTests` | Next reusable group in `MyDmsVn.Server.Infrastructure.Inventory.Tests`; their seeded receipt/ledger/balance data requires the complete profile. Review data-only mapping/storage methods individually. |
| `IdentityStoreIntegrationTests`, data-only `SecurityAdministrationSqlTests` methods | Candidates in `MyDmsVn.Server.Infrastructure.Security.Tests`; reset users, assignments, revisions, bindings and audit. Separate methods injecting triggers or exercising bootstrap/principal/maintenance changes before adoption. |
| Migration/compatibility, `SqlPrincipalProvisioningTests`, `SuperuserBootstrapTests`, helper lifecycle/fault and multi-session contention tests | Retain fresh ownership. This includes schema-mutating Foundation probes and Inventory command tests installing signal/failure triggers. A `FastSQL` label does not override these exclusions. |
| Pure unit/policy/architecture/ViewModel tests | Remain independent of SQL fixtures; adding a fixture must not turn them into SQL tests or conceal SQL behind a NonSql exemption. |

Five review obligations are assigned below: empty/populated append-only ledger reset; partial reset or seed failure; leaked workers/transactions at scope exit; unexpected schema/trigger or ownership changes; and lost fixture/case attribution in filtered, failed or aborted runs. Security mutation eligibility is a per-method resource decision, not a blanket judgment about its category.

## 5. Tasks and acceptance evidence

### RESP-T00 — Freeze the eligibility and compatibility baseline

**Files:** current projects/scripts/helpers/manifests above; create `docs/RESPAWN_CASE_MAP.md` and `docs/RESPAWN_VERIFICATION.md` during implementation.
**Produces:** reviewed method/theory-row → owner/TFM/category → fresh/reuse/profile/seed mapping and a comparable execution contract.

- [ ] Inventory all six SQL owners and every active logical case/theory row. Record seed tables, committed writes, identities/sequence, triggers, schema/principals, scopes and workers; classify every candidate method explicitly, including mixed classes.
- [ ] Confirm package assets/API, actual identities and trigger definitions against this source revision; record locked dependency, native asset and binding-redirect requirements. Keep proposed compatibility separate from executed proof.
- [ ] Specify identical fresh/reuse comparison inputs: frozen revision, complete SQL case inventory, machine/approval/configuration, build and runner protocol. Record current per-test create/migrate/drop behavior; do not treat historical 775/793 totals as a baseline for this work.
- [ ] Bind future RESP selectors/gates only when their referenced tests exist. All `RESP-Txx` IDs are absent from the current v1 manifest; a command using them is not currently runnable. Exit when the map covers every case and reset exception has a concrete reason.

### RESP-T01 — Prove the pinned reset profile on the actual runtime

**Files:** central pins, harness project/locks, `RespawnResetProfile.cs`, new Foundation SQL integration checks. **Consumes:** T00 schema/ownership map.

- [ ] Add Respawn 4.0.0 to the test graph and prove locked restore/build/testhost loading with netstandard2.0 harness, net48 SQL tests and SqlClient 7.0.2; verify binding redirects/native dependencies and tool-only net8 runner loading.
- [ ] Implement the v4 reset adapter and the exact StockLedger trigger treatment in section 2. Use an owned, fully migrated test database, a separate open reset connection and finite timeouts; test `Empty_ledger_reset_restores_append_only_trigger` before claiming the Catalog pilot can work.
- [ ] Test `Reset_clears_all_mutable_rows_preserves_journal_and_owner`, `Repeated_reset_reseeds_actual_identities_and_sequence`, and `Reset_restores_constraints_and_rejects_schema_drift`. Use independent native SQL assertions for unchanged journal contents, state, constraints, trigger definition/enabled status and absence of cross-test sentinel rows.
- [ ] Inject reset/trigger-restoration timeout/failure; require no test body admission and guarded cleanup/quarantine. Exit only with measured net48 runtime/reset evidence; package metadata alone cannot close this task.

### RESP-T02 — Add owned fixture lifecycle and preserve complete runner accounting

**Files:** proposed harness lifecycle APIs, allocator, timing types and runner/manifests/tests in section 3. **Consumes:** T01 profile and T00 identities.

- [ ] Implement initialization, exclusive test scopes, fresh/reuse parser, per-test seeding, tracked cleanup and terminal poison state. Preserve run-unique ownership checks before reset/drop; new resource APIs must remain compatible with the existing fresh helper and linked consumers.
- [ ] Add `Second_scope_cannot_overlap`, `Reset_or_seed_failure_poisons_fixture`, `Ownership_change_prevents_reset_ddl`, and `Outstanding_worker_blocks_reset_until_drained` unit/SQL checks. Cancellation must terminate the actual reset operation before another scope can proceed.
- [ ] Implement the explicit xUnit v2 `RunTestAsync` identity bridge before T03. Map a Fact and at least two rows of the same Theory to their reviewed stable row keys, assign separate execution IDs, and prove a filtered row selection plus reset/seed failure before the scenario runs. Missing/unknown identity must fail before reset or scenario SQL; collection initialization failure must be reported against the selected member inventory even though no per-case helper ran.
- [ ] Give fixture create/migration/drop events explicit fixture/owner/TFM/run identities and test reset/seed/cleanup events explicit method/row/execution identities. Record allocated fixture ID and profile/mode without publishing database names or tokens. A shared migration must not masquerade as one migration per test.
- [ ] Update `MigrationInvocationManifest`, its auditor and `MigrationTimingArtifactReconciler`: v1 currently accepts only `once-per-test-execution` / `once-per-caller-execution`, recognizes source literal patterns and scopes entries by source directory. Add a reviewed `once-per-fixture-lifetime` rule and fixture/member identity fields; derive expected allocations/migrations from reviewed collection membership plus run mode, not observed event counts alone. Preserve old rules for unchanged fresh tests and reconcile every selected member. Do not remove the manifest or timing gate.
- [ ] Update logical-case binding/classification/fingerprint dependencies to include fixture setup, adapters, reset profiles and theory membership. Verify missing/extra/duplicate fixture migrations, unexecuted members, filtered collections, constructor/helper SQL, failed initialization, aborted run and cleanup-only failure; no NonSql exemption may hide fixture SQL.
- [ ] Register reviewed RESP Task/Final entries and test the actual v1 migration/selection behavior. Keep SQL approval, source/discovery/TRX completeness, zero required skips, process-tree watchdog, workspace/SQL leases and owned cleanup. Exit with focused harness/runner and lifecycle SQL checks; this is not a fingerprint-v2 or inventory-file redesign.

### RESP-T03 — Adopt and challenge the Catalog collection

**Files:** Catalog collection/base adapters, both Catalog test classes and case map/manifests. **Consumes:** T02 scopes/accounting.

- [ ] Move the five Catalog methods to the reviewed collection lifecycle, retaining categories, assertions and per-test DI/UoW disposal. Preserve employee-user linkage setup and exact denied-write/table-count expectations; do not share application scopes or test data between methods.
- [ ] In explicit reuse mode prove one allocation/migration for that collection, a completed reset/seed before each body and one owned drop. Run the same methods in explicit fresh mode to establish behavioral equivalence.
- [ ] Test a failed assertion followed by a fresh baseline, and injected reset/seed/trigger/schema failures followed by rejected fixture admission. Run controlled reordered/alternating sentinel cases to expose contamination; do not rely on alphabetical xUnit order.
- [ ] Exercise tracked multi-connection work within a dedicated lifecycle probe, then prove all readers/transactions/workers finish before reset. Retain existing deliberate business-contention tests on fresh databases and keep assembly serialization.
- [ ] Reconcile source/discovery/TRX/theory/fixture evidence after normal, filtered and failed runs. Exit with all five Catalog methods safely reusable and every failure producing truthful failed/incomplete evidence.

### RESP-T04 — Extend reuse to the proven data-only inventory

**Files:** reviewed Inventory/Security adapters and methods, case map, reset/seed profiles and affected manifests. **Consumes:** accepted Catalog lifecycle.

- [ ] Adopt the reviewed Inventory query group and eligible data-only mapping/storage cases; reset seeded receipts, ledger, balances, references and generated values. Preserve rowversion comparisons as relative to values read during that test.
- [ ] Adopt proven Identity/data-only Security methods with deterministic user/role/assignment/binding/audit seeds. Keep bootstrap/maintenance, SQL principal/grant/schema and test-injected trigger methods fresh; split mixed classes only with explicit logical-case mappings.
- [ ] Migrate every eligible case in the T00 map or record its concrete exclusion/failure evidence; completion cannot stop at a trivial pilot when further reviewed candidates meet the contract. Keep the original fresh path available through explicit mode selection.
- [ ] Run each affected owner's smallest supported guarded categories, plus reset contamination/failure checks for its added state. Revalidate profiles when schema/seed dependencies expand; never reuse cached metadata after a profile change.
- [ ] Review new/moved fingerprints and theory mappings, check linked helper consumers, and freeze the complete candidate with `reuse` selected by default for accepted eligible profiles and `fresh` for dedicated exceptions. Include the default-selection change before T05 runs so its evidence belongs to that candidate. Exit with no unaccounted case or assertion loss and a documented final reuse boundary.

### RESP-T05 — Accept correctness and measure the same inventory

**Files:** final case map, sanitized verification, task/status checkboxes and current testing guidance. **Consumes:** frozen T04 revision and reviewed RESP gates.

- [ ] Build the affected harness/runner/all-six-SQL project matrix and run complete affected harness/runner regressions plus metadata-only SQL preflight. Keep clean revision and approved short artifact roots; metadata PASS is not SQL execution.
- [ ] Run one complete guarded, unfiltered sequential net48 SQL pass in explicit `fresh` mode, then one in explicit `reuse` mode on the same frozen candidate and logical-case inventory. All six owners participate; unconverted cases stay fresh in both. Preserve original discovery/TRX, zero required skips, watchdog and ownership evidence.
- [ ] Compare wall time and create/migrate/reset/seed/drop counts/durations, with fixture and case identity mappings. Report the exact two observations and their limits; require measured improvement for a performance claim, without a percentage threshold or mandatory multi-pass benchmark. Record a regression as a failed optimization outcome requiring investigation, not an automatic waiver.
- [ ] Require identical behavioral results and complete test/fixture accounting, no contamination, no outstanding sessions/owned databases and a proven fresh-mode fallback. Correctness or cleanup failures block acceptance even if timing improves. Repeat a pass only to resolve an invalid run, relevant fix or concrete regression question.
- [ ] Verify that the frozen candidate defaults to `reuse` for accepted eligible profiles and retains explicit `fresh` mode plus dedicated fresh exceptions; update current guidance and hand off the accepted fixture baseline to L2DB. Verify default-mode selection with focused parser/adapter checks; document that the complete same-inventory comparison above used explicit modes. Do not claim P5/P6/P7, GUI acceptance or product performance from this test-only change. SQL/harness changes require the broad SQL gate here; unrelated full Desktop/repository suites are not automatic requirements.

## 6. Execution scopes and current tooling boundary

| Task | Focused checks during implementation | Required SQL evidence |
|---|---|---|
| T00 | Static case/dependency/schema audit; existing runner manifest-contract selectors if modified | No automatic SQL run for planning; define the comparison protocol. |
| T01 | Package/build and reset-profile tests on affected actual TFMs | Foundation `FastSQL` reset probes through the guarded wrapper; destructive probes own fresh databases. |
| T02 | Harness lifecycle/parser/timing tests; affected runner inventory/reconciliation/lease tests | Foundation lifecycle/reset safety cases and categories actually changed in shared helper consumers. |
| T03 | Catalog adapter/setup and selected accounting cases | Catalog `FastSQL`; controlled dedicated fixture lifecycle/failure probes. |
| T04 | Affected Inventory/Security profiles and case mappings | Inventory `FastSQL`; Security's source-reviewed applicable category (currently many data methods are `SecurityProvisioning`); fresh mutation cases remain covered. |
| T05 | Complete affected harness/runner checks; full source/discovery preflight | One fresh + one reuse full pass across Foundation, Catalog, Inventory, Security, Migrations and original IntegrationTests, all `net48`. |

Follow [tests/README](../../tests/README.md) for operator preparation and full parameter reference. Currently the focused wrapper accepts one project plus one category, not arbitrary SQL method/class selectors; its default includes parked net8, so explicitly pass `-Frameworks @('net48')`. Build the selected project and Release net8 runner first. With the existing SQL/approval environment prepared, this Catalog command already exists:

```powershell
$repo = (Get-Location).Path
$project = Join-Path $repo 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
$artifacts = Join-Path $repo ('.sqlperf\resp-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 -RepositoryRoot $repo `
  -TestProject $project -ArtifactRoot $artifacts `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category FastSQL -Frameworks @('net48')
```

`Invoke-TestGate.ps1` still selects exact manifest PlanId/TaskId identities with no caller project/filter/TFM override. Its current `Final` requires clean committed revision, approved SQL and complete unfiltered SQL ownership; `Final -PreflightOnly` allows dirty/no-SQL-approval metadata checks only. Existing SEC-REBUILD-T06 Final is the legacy six-owner envelope and includes other gates. It is not a registered RESP milestone. T02 must deliberately register/adapt the new scope; until then use current guarded capabilities and describe their broader enforcement honestly. Never bypass their checks to approximate the new policy.

Existing focused runs have 30-minute command / one-hour overall limits; lower strict PR runs have 30-minute command / four-hour overall limits; manifest runs use reviewed gate budgets. Preserve bounded cleanup (currently 30 seconds for post-run ownership reconciliation), path budgets and leases while adding reset-specific timeouts. Keep raw approval, ownership, TRX and timing evidence local and publish reviewed sanitized aggregate results with the exact revision, mode, scope and pass/fail/skip/blocked status. This documentation change itself runs no tests and closes none of the implementation checkboxes above.
