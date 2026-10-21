# Respawn fixture reuse on net48 — implementation plan

> **Historical completed plan:** RESP-T00–T05 are complete in their approved scope. Retain the task record and linked evidence; this file is not an active execution queue. Use the [current plan index](README.md) for completed BCF acceptance and current scheduling; completed BCI remains historical evidence. The implementation instructions below describe the accepted historical work.

**Status:** RESP-T00–T05 complete and accepted, 2026-10-18, for the exact uncommitted net48 working-tree candidate recorded in verification. Both unfiltered six-owner passes: 265 passed / 0 failed / 0 skipped per mode; successful cleanup and observed suite wall-time improvement. Default reuse and explicit fresh verified. At the accepted handoff, the next task was L2DB-T00 to bind the eventual commit and preserve this accepted baseline. See [case map](../RESPAWN_CASE_MAP.md) and [verification](../RESPAWN_VERIFICATION.md).
**Historical order:** RESP preceded the now-completed [LINQ to DB migration](20261018-001-linq2db-net48-persistence-migration.md). This ordering is retained for evidence provenance, not current scheduling.
**Goal:** Amortize database creation and migration across eligible SQL tests while proving each test receives the same clean, deterministic starting state as a fresh database.
**Architecture:** One reusable database belongs to one xUnit v2 collection in one assembly/process/TFM. Small helpers in the existing SQL test projects own allocation, reset, seeding and teardown. Each test owns its application resources; exceptions retain fresh databases.
**Tech stack:** Active SQL/product/Desktop `net48`; xUnit `2.9.3`; DbUp `7.2.0`; central `Microsoft.Data.SqlClient` `7.0.2`; pinned Respawn `4.0.0`.
**Spec:** The owner's 2026-10-18 decisions, [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md), [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md) and [ADR 0010](../adr/0010-respawn-fixture-reuse-and-test-policy.md).

## 1. Current baseline and constraints

The six SQL owners run directly through `dotnet test` or Test Explorer. The local `SqlTestDatabase` creates a fresh database per call and validates its live allocation identity before drop. The standalone SQL framework and its manifests/wrappers/tests have been removed. This plan adds reuse to the existing owners; it does not recreate those projects or a runner/reporting framework.

- Keep active net48, parked AppCore, current RepoDb/Dapper and DbUp. No runtime upgrade, container requirement, CI rollout or SQL parallelism rollout belongs to RESP.
- Keep existing SQL/WinForms assembly serialization; run SQL projects sequentially. Collection coordination cannot establish ownership across testhosts. Generate unique databases and use a distinct allocation for every assembly/process/TFM/collection.
- Allocate through the local helper, using only the explicitly configured non-production instance and administrative connection targeting `master`. Preserve generated-name guards, instance/database ID/creation-time checks, random ownership marker and finite connection/SQL bounds.
- Revalidate live allocation ownership before reset and final drop. Never reset an application, unknown, leftover or merely name-matching database. Preserve `dbo.SchemaVersions` and the `MyDmsVn.TestOwnership` extended property. Ambiguous ownership refuses mutation and needs manual operator review.
- Reset all mutable business and security rows, then seed explicitly. Do not ignore users, assignments, binding, audit, catalogs, receipts, balances or ledger data to hide an incomplete reset.
- Keep schema/history, SQL principal/GRANT/role, bootstrap/maintenance and unresettable fault/lock cases fresh. Responsibility categories remain independent of reset eligibility.
- A reset, seed, ownership/schema validation or teardown failure poisons the fixture. Fail affected tests and reject subsequent bodies; no silent recreation, skip or fresh-mode fallback.
- Preserve existing business/lifecycle assertions and Theory rows. Record explained replacements in the case map; do not import deleted framework tests, fingerprints or historical totals as acceptance requirements.
- There is no external lease, process watchdog or automatic orphan recovery. Drain operations before releasing a fixture; an interrupted process can leave a database for manual review under TEST_POLICY.

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

`TablesToIgnore` is `string[]` matched by table name: `"dbo.SchemaVersions"` and modern `Table`/`Respawner` APIs are incorrect here. Configure no conflicting schema exclusion list. Reject unexpected schemas or ambiguous journal names before reuse. Foundation's separate test migration uses `dbo.TestSchemaVersions`; only a separately reviewed profile actually using that schema may preserve that journal. [Checkpoint](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/Checkpoint.cs) and [SQL Server adapter](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/SqlServerDbAdapter.cs) define these behaviors.

- Create one checkpoint per allocated database after migrations finish. The first reset builds cached metadata; complete it and verify the baseline before publishing the fixture. Never transfer a checkpoint or continue after schema/profile changes.
- `Reset(DbConnection)` uses the caller's open connection, starts its own transaction and has no cancellation-token overload. Use a short-lived reset connection without a caller transaction. `CommandTimeout` bounds delete/reseed commands; metadata queries retain provider defaults. On cancellation/timeout, close the owned reset connection, await actual operation termination and poison the fixture before releasing access. A timed-out wait alone does not stop SQL; no external watchdog is assumed.
- **Full-schema trigger blocker:** `007_Inventory_Stock.sql` installs `dbo.TR_StockLedger_AppendOnly`, an `INSTEAD OF UPDATE, DELETE` trigger that unconditionally throws. This affects the Catalog pilot even with an empty ledger. After positive ownership and exact schema/trigger verification, a test-only profile may temporarily disable only that trigger, reset, restore it in `finally`, then verify its definition and enabled state. Test empty and populated ledgers. Restoration failure poisons the fixture. Do not ignore StockLedger or change the production migration.
- Revalidate constraints, schema, journal contents and ownership after reset. Verify enabled/trusted FK state, including cycles. Unexpected objects, triggers or permissions invalidate the cached boundary.
- `WithReseed` defaults to false and v4 calculates reseeds from seed minus one. Prove actual `IDENTITY(1,1)` behavior before/after first insert, populated reset and repeated reset. Reset `dbo.GoodsReceiptNumberSequence` explicitly to its reviewed test baseline with test-only SQL, or use documented relative-number assertions; Respawn does not reset it. Rowversion, timestamps and database-wide counters do not rewind.
- `SecurityAuditEvents` and `SystemSecuritySettings` reference Users. Clear/rebuild the actual FK graph. Ordinary security data tests may reuse after proof; principal/grant/bootstrap/maintenance and test-injected trigger scenarios remain dedicated.

## 3. Existing owners and proposed helpers

All paths are repository-relative. New paths/APIs below are proposed; implement only what the lifecycle needs.

| Responsibility | Existing anchor / planned treatment |
|---|---|
| Owned allocation/drop | Extend `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs` and retain `SqlTestDatabaseName.cs`. Keep `CreateAsync(...)`, `ConnectionString` and `DisposeAsync()` for fresh tests; add a narrow ownership-verification method usable by reset. |
| Reusable lifecycle/reset | Add `Fixtures/{ReusableSqlFixture,RespawnResetProfile,SqlFixtureTestScope,SqlFixtureRunMode}.cs` inside the existing IntegrationTests project; use the current reference/linked-source pattern for consumers. Add Respawn only to actual test consumers and review affected locks. |
| Migration | Call production `DatabaseMigrationRunner.Migrate` directly. Preserve the small `TestDatabaseMigrationRunner` for Foundation's separate embedded schema. No measured-migration wrapper or invocation-ID registry is required. |
| xUnit v2 adapters | Add explicit collection adapters in Catalog, then proven Inventory/Security owners. Collection `IAsyncLifetime` owns allocation/migration/drop; a per-case async helper resets/seeds before the scenario and drains the scope in `finally`. |
| Correctness tests | Add `ReusableSqlFixtureTests.cs` in IntegrationTests and `ReusableSqlFixtureIntegrationTests.cs` in Foundation, plus focused adapter tests in their actual owners. Keep destructive probe allocations fresh. |
| Reviewable evidence | Create `docs/RESPAWN_CASE_MAP.md` and `docs/RESPAWN_VERIFICATION.md` during T00. Use source review, ordinary xUnit/TRX outcomes and small fixture-operation observations, without a new executable gate manifest. |

Catalog references the IntegrationTests assembly through its existing internals bridge. Foundation/Inventory/Security/Migrations compile linked helper copies. Preserve those boundaries or explain a narrow change; never accidentally compile another owner's tests or reference test infrastructure from production.

IntegrationTests, Foundation, Inventory, Security, Migrations and Desktop have assembly serial guards. Catalog has no assembly guard; its referenced assembly's guard does not apply to it. The first Catalog collection must include every shared-fixture consumer.

Lifecycle implemented in T02, consumed by Catalog in T03 and Inventory/Security in T04:

- `ReusableSqlFixture.InitializeAsync(CancellationToken)` allocates, migrates and verifies one owned database in reuse mode. Configuration contains explicit migration/schema/reset callbacks; there is no static mutable fixture cache or ambient application connection/UoW.
- `BeginTestAsync(caseLabel, seed, cancellationToken)` admits one exclusive test scope after ownership/health verification, reset, deterministic seed and baseline verification. Give each Fact/Theory row a clear non-sensitive method/row label for diagnostics; correctness never depends on parsing an xUnit display name or maintaining a fingerprint registry.
- `SqlFixtureTestScope` exposes the connection string and registers asynchronous drain/dispose actions. Disposal collects cleanup errors without suppressing the original failure, drains workers/readers/transactions/scopes, then releases admission.
- `SqlFixtureRunMode` parses the test-only `MYDMSVN_SQL_TEST_FIXTURE_MODE` values `fresh`/`reuse`; unknown values fail before allocation. T04 freezes accepted eligible profiles with reuse as default; excluded cases remain fresh. Explicit fresh stays available for comparison. The setting was implemented in T02, adopted by Catalog in T03 and Inventory/Security in T04.
- A per-case `RunTestAsync(caseLabel, seed, scenario, cancellationToken)` adapter is the first awaited SQL operation of each consumer. It initializes/reset/seeds before invoking the body and drains the scope in `finally`. Fresh mode uses the same profile, seed and assertions but allocates/migrates for each invocation.

xUnit v2 collection `IAsyncLifetime` supplies asynchronous setup/disposal, but parameterless initialization does not supply the current method or Theory arguments. Pass a stable descriptive row label explicitly when diagnostics need it; do not use reflection into xUnit internals or ambient current-test state. Prove at least a Fact and two rows of the same Theory run their own reset/seed. [xUnit shared-context guidance](https://xunit.net/docs/shared-context) describes the lifecycle; `IAsyncDisposable` alone is insufficient for v2 fixture disposal.

## 4. Initial eligibility and review focus

| Existing group | Decision to prove |
|---|---|
| Catalog `CatalogMappingIntegrationTests`, `CatalogBackendIntegrationTests` | First pilot, including all five methods. Backend CRUD inserts Users for employee linkage; four catalog tables alone are insufficient. |
| Inventory `GoodsReceiptQueryIntegrationTests`, `InventoryQueryIntegrationTests` | Next group; seeded receipt/ledger/balance data needs the full profile. Review data-only mapping/storage methods individually. |
| Security `IdentityStoreIntegrationTests`, data-only `SecurityAdministrationSqlTests` methods | Candidates after users/roles/assignments/revisions/binding/audit reset proof. Separate trigger/bootstrap/principal/maintenance mutations. |
| Migration/compatibility, `SqlPrincipalProvisioningTests`, `SuperuserBootstrapTests`, helper lifecycle/fault and multi-session contention | Fresh ownership, including Foundation schema probes and Inventory command tests installing signal/failure triggers. `FastSQL` does not override exclusions. |
| Pure unit/policy/architecture/ViewModel tests | Remain independent of SQL; a reusable fixture must not turn them into database tests. |

Review empty/populated append-only ledger reset, partial reset/seed failure, leaked workers/transactions, schema/trigger/ownership drift, and truthful results after filtered/failed/aborted initialization. Classify mixed classes per method.

## 5. Tasks and acceptance evidence

### RESP-T00 — Freeze eligibility and compatibility baseline

**Files:** current projects/scripts/helpers; new case map and verification documents. **Produces:** method/Theory row → owner/TFM/category → fresh/reuse/profile/seed mapping and comparison protocol.

- [x] Inventory every case in all six SQL owners. Record seed tables, committed writes, identities/sequence, triggers, schema/principals, scopes and workers. Classify each candidate method and concrete fresh exception.
- [x] Confirm candidate package/API, identity and trigger definitions, locked dependencies, native assets and binding redirects. Keep compatibility expectations separate from executed net48 evidence.
- [x] Define matching fresh/reuse inputs: frozen candidate, cases, project order, machine/SQL configuration and build. Record current fresh allocation/migration behavior; old SQLPERF totals are historical.
- [x] Record direct project/filter commands resolving to actual tests and expected Theory rows. No task registration or bootstrap runner test is required. Exit with complete source mapping and concrete reset exclusions.

### RESP-T01 — Prove the pinned reset profile on net48

**Files:** central pin, consuming SQL projects/locks, proposed reset profile and new Foundation integration checks. **Consumes:** T00 schema/ownership map.

- [x] Add Respawn 4.0.0 only to the test graph. Prove locked restore/build/testhost loading under net48 with SqlClient 7.0.2, including native assets and binding redirects.
- [x] Implement the v4 adapter and exact StockLedger handling. On a fresh owned fully migrated database, test `Empty_ledger_reset_restores_append_only_trigger` before claiming Catalog reuse works.
- [x] Test `Reset_clears_all_mutable_rows_preserves_journal_and_owner`, `Repeated_reset_reseeds_actual_identities_and_sequence` and `Reset_restores_constraints_and_rejects_schema_drift`. Use independent native SQL assertions for journal, owner, constraints, trigger and sentinel rows.
- [x] Inject reset/trigger-restoration failure and timeout; prohibit test-body admission, terminate/drain the operation and attempt guarded cleanup. Ambiguous cleanup remains failed/manual review. Exit requires actual net48 reset proof.

### RESP-T02 — Implement the local collection lifecycle

**Files:** local fixture helpers, allocator ownership verification, xUnit adapters and local helper/lifecycle tests. **Consumes:** T01 profile and T00 map.

- [x] Implement initialization, exclusive test scopes, fresh/reuse parsing, seed, tracked cleanup and terminal poison state. Preserve live ownership before reset/drop and compatibility with existing fresh consumers.
- [x] Prove `Second_scope_cannot_overlap`, `Reset_or_seed_failure_poisons_fixture`, `Ownership_change_prevents_reset_ddl` and `Outstanding_worker_blocks_reset_until_drained`. Cancellation must terminate the actual reset before any next scope.
- [x] Prove a Fact and two Theory rows each reset/seed before their scenario, including focused row execution and reset/seed failure. Collection initialization failure must surface as failed affected tests; a helper label is diagnostic, not an authorization token.
- [x] Record simple per-fixture allocation/migration/drop and per-test reset/seed/cleanup observations through test-owned output/counters. Include mode/profile and non-sensitive case labels. Shared migration occurs once per fixture; do not report it once per member.
- [x] Review selected source versus TRX after normal, filtered, failed initialization and cleanup-only failure. No missing required case or skipped failure may become PASS. Keep all support inside existing test owners; do not add external gate, lease, report or reconciliation machinery.
- [x] Run focused local helper/adapter tests and affected lifecycle SQL. Exit with a usable exclusive collection contract and visible failure/cleanup behavior.

### RESP-T03 — Adopt and challenge the Catalog collection

**Files:** Catalog adapters and both classes, case map and evidence. **Consumes:** T02 lifecycle.

- [x] Move all five Catalog methods into one explicit collection, retaining categories, assertions, employee-user setup, denied-write/table-count checks and per-test DI/UoW disposal.
- [x] Prove one allocation/migration/drop in reuse mode and a complete reset/seed before every body. Run the same methods in explicit fresh mode with identical assertions.
- [x] Prove a failed scenario leaves the next case with a clean baseline; reset/seed/trigger/schema failure rejects subsequent admission. Use controlled sentinel/reordered scenarios without depending on alphabetical test order.
- [x] Exercise tracked multi-connection work in a dedicated lifecycle probe; drain all readers/transactions/workers before reset. Existing business-contention cases stay fresh.
- [x] Compare selected/executed cases and actual outcomes after normal, focused and failed runs. Exit with all five methods safely reusable and truthful failure evidence.

### RESP-T04 — Extend reuse to the proven data-only inventory

**Files:** reviewed Inventory/Security methods/adapters, profiles, case map and evidence. **Consumes:** accepted Catalog lifecycle.

- [x] Adopt eligible Inventory queries/data-only mapping/storage with full receipt/ledger/balance/reference reset; compare rowversions relative to values read in the test.
- [x] Adopt proven Identity/data-only Security methods with deterministic seeds. Keep bootstrap/maintenance, principal/grant/schema and test-injected trigger methods fresh; explain moved/replaced cases.
- [x] Migrate every eligible T00 case or record concrete exclusion/failure evidence. Retain the explicit fresh path; do not stop at a trivial pilot when further reviewed candidates meet the contract.
- [x] Run focused actual project/category or method checks plus contamination/failure cases for added state. Revalidate profiles when schema/seed dependencies expand; cached metadata cannot cross a profile change.
- [x] Review source/Theory coverage and linked consumers; freeze the candidate with reuse as default for accepted profiles and fresh for exceptions. Include that default change before T05. Exit with no unexplained lost case/assertion.

### RESP-T05 — Accept correctness and compare the same cases

**Files:** final case map, sanitized verification, testing guidance and supported status changes. **Consumes:** frozen T04 candidate.

- [x] Locked restore/build all six SQL projects and verify the required source cases and mode selections. Inspect discovery if needed; discovery alone is not SQL acceptance.
- [x] Run one unfiltered sequential net48 pass across all six owners in explicit fresh mode, then one in explicit reuse mode on the same candidate. Unconverted cases remain fresh in both. Require every mandatory Fact/Theory row, zero mandatory skips and successful owned cleanup.
- [x] Compare case outcomes, wall-clock observations and create/migrate/reset/seed/drop counts/durations. State limits of the two observations; require actual improvement for a speedup claim. Investigate regressions; do not auto-waive them or impose an old percentage threshold.
- [x] Require equivalent behavior, no contamination, drained sessions, no unresolved owned allocations and proven explicit fresh mode. Failure or incomplete cleanup blocks acceptance even if timing improves. Repeat only for invalid evidence, relevant repairs or a concrete regression question.
- [x] Verify default reuse for accepted profiles with focused parser/adapter checks and preserve dedicated fresh exceptions. Update current guidance and hand the exact accepted baseline to L2DB. No P5/P6/P7, GUI or product-performance acceptance follows from this test-only work.

## 6. Direct execution scopes

| Task | Focused verification | SQL evidence |
|---|---|---|
| T00 | Source/case/schema/package audit and resolvable command selection | No automatic SQL run merely for planning |
| T01 | Actual net48 package/build and profile checks | Foundation reset probes on dedicated fresh databases |
| T02 | Local lifecycle/parser/adapter tests | Foundation lifecycle/reset safety plus affected shared helper consumers |
| T03 | Catalog adapter and selected lifecycle cases | Catalog `FastSQL`, dedicated failure/worker probes |
| T04 | Changed Inventory/Security profiles and case map | Source-reviewed applicable categories/methods; many Security data cases use `SecurityProvisioning` |
| T05 | All affected lifecycle tests and reviewed source/case inventory | Complete Foundation, Catalog, Inventory, Security, Migrations and original IntegrationTests in each explicit mode |

Use [tests/README](../../tests/README.md) for SQL configuration and the sequential complete-project loop. Current focused Catalog command:

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=catalog.trx' `
  --results-directory ('TestResults/resp-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Catalog SQL tests failed.' }
```

T02 implements the setting for lifecycle adapters. The T05 comparison sets `MYDMSVN_SQL_TEST_FIXTURE_MODE=fresh`, executes the complete six-project loop, then repeats with `reuse` and a separate results directory. Record/restore the caller's prior setting afterward. The setting is supported by all three accepted data collections; T04 changes the default to reuse. The complete T05 comparison is accepted for the exact working-tree candidate in [verification](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up).

Use the designated non-production master connection, short unique local result paths and finite reset/SQL bounds. Keep raw connections, SQL identifiers, Theory values and TRX local; publish sanitized revision/mode/scope/count/cleanup observations. Missing Windows/SQL or failed required cleanup is BLOCKED / NOT RUN or FAIL as appropriate. RESP-T00–T05 checkboxes are complete with evidence in verification; no release acceptance follows.

