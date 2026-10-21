# Respawn verification — RESP-T00 through RESP-T04

> Historical evidence record. Original checkpoints, failures, exceptions and task handoffs retain their dated scope. The [current execution index](plans/README.md) now schedules BCF lookup and visual refinement; this record does not satisfy its new obligations or schedule retired plans. Completed BCI remains historical evidence for its own accepted scope.

**Date:** 2026-10-18. **Reviewed source revision:** `6383289a2e43e2021abd65048a1e2f3f3838324e` (v1a). **Working branch:** `codex/resp-t00-baseline`. This document and the [case map](RESPAWN_CASE_MAP.md) freeze the source baseline for [RESP-T00](plans/20261018-002-respawn-fixture-reuse.md). This opening inventory describes the T00 audit snapshot. T01 execution evidence below now verifies the pinned reset profile; T02 lifecycle, T03 Catalog adoption and T04 Inventory/Security/default-reuse evidence are appended below. The frozen T04 candidate awaits complete RESP acceptance and speedup comparison (T05).

## T00 scope and outcome

The change is documentation only: enumerate all six actual SQL-owner projects, classify every Fact/Theory invocation, review schema/package/resource boundaries, and define direct checks and the later matched comparison. Per [TEST_POLICY](TEST_POLICY.md), source/links/commands/status/encoding checks are the meaningful verification; SQL/build/regression runs are not required for this planning task.

| Owner, in acceptance order | TFM | Methods | Source-expanded cases | SQL / non-SQL cases | Proposed reusable / dedicated SQL |
|---|---|---:|---:|---|---|
| Foundation.Tests | net48 | 16 | 16 | 12 / 4 | 0 / 12 |
| Catalog.Tests | net48 | 5 | 5 | 5 / 0 | 5 / 0 |
| Inventory.Tests | net48 | 33 | 35 | 30 / 5 | 7 / 23 |
| Security.Tests | net48 | 48 | 52 | 30 / 22 | 8 / 22 |
| Migrations.Tests | net48 | 65 | 65 | 46 / 19 | 0 / 46 |
| IntegrationTests | net48 | 26 | 38 | 12 / 26 | 0 / 12 |
| **Total** | | **193** | **211** | **135 / 76** | **20 / 115** |

These totals come from current attributes and InlineData rows, not discovery, historical SQLPERF or a run. No MemberData/ClassData was found. Existing categories can overlap. Preserve original assertions, inner-loop scenarios and all rows; later profile/lifecycle tests increase totals with explained additions.

## Source audit anchors

- [SqlTestDatabase](../tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs): every successful CreateAsync makes a unique owned allocation. Explicit non-production master/instance validation, generated-name validation, server/database ID/creation time and random ownership marker protect drop. Pooling=false, retry=0, connect timeout capped at 30 seconds, command timeout 30 seconds, linked two-minute create/dispose budget. Unconfirmed allocation refuses automatic cleanup. Drop retries retain ownership after failure. No reuse/reset method exists.
- [TestDatabaseMigrationRunner](../tests/MyDmsVn.Server.Infrastructure.IntegrationTests/TestDatabaseMigrationRunner.cs) and [test script](../tests/MyDmsVn.Server.Infrastructure.IntegrationTests/TestScripts/001_TestFoundation.sql): Foundation's separate `P1TestProbe` identity table and `dbo.TestSchemaVersions` journal. Other Foundation probes create `UnitOfWorkProbe` or use a bare database. Never apply APP's SchemaVersions exclusion to this boundary.
- [Production migrations](../src/MyDmsVn.Server.DbMigrator/Scripts/001_PersistenceFoundation.sql), scripts 002–009: 15 mutable application tables, 13 mutable identities, one receipt number sequence, append-only ledger trigger, security procedures and audit/binding FK state. Full details in the case map. Migration runner journals to `dbo.SchemaVersions`; production migration 001 has no business table or seed. No production reset scripts are introduced.
- Catalog references IntegrationTests through its existing InternalsVisibleTo bridge. Foundation/Inventory/Security/Migrations compile linked SqlTestDatabase, SqlTestDatabaseName and TestDatabaseMigrationRunner copies; Inventory also links SqlConcurrencyGate/SqlConcurrencyCleanup. Linked helpers do not link another owner's test cases. Catalog consumes the SqlServerFact/category discoverer compiled in IntegrationTests through its reference. Foundation/Inventory/Security/Migrations each compile their own SqlServerFactAttribute.cs and discoverer naming the owner assembly; they do not reference IntegrationTests. Read actual csproj Compile/EmbeddedResource/ProjectReference entries when adding fixture files in T01/T02.
- Foundation/Inventory/Security/Migrations/IntegrationTests have `CollectionBehavior(DisableTestParallelization = true)`. Catalog has no assembly guard; its referenced assembly guard does not serialize Catalog. All five future pilot consumers must join an explicit xUnit2 collection; no project parallelism is introduced.

Fresh allocation and migration counts are **per helper invocation**, not per test name. Most application tests create and migrate once, but looped scenarios and history/lock probes can allocate or migrate more than once. Bare/helper tests can migrate zero times; invalid-configuration and pure tests allocate zero. T00 does not invent measured operation counts. Later output must count actual calls and report failed/incomplete operations and owned cleanup, including setup failure.

## Package and host compatibility boundary

| Item | Source/package finding | Evidence still required in T01 |
|---|---|---|
| Candidate | Published [Respawn 4.0.0](https://www.nuget.org/packages/Respawn/4.0.0) ships net461 and netstandard2.0 assets, with Microsoft.Data.SqlClient >=2.0.0. It is absent from central pins, all six csproj references and lock graphs. | Pin 4.0.0 only in test consumers; restore updated locks and build/load the actual net48 testhosts with the central 7.0.2 provider. Asset compatibility is not execution proof. |
| API | Pinned [Checkpoint source](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/Checkpoint.cs): Checkpoint.Reset(DbConnection), DbAdapter.SqlServer, string[] TablesToIgnore, SchemasToInclude, WithReseed and nullable CommandTimeout. No CancellationToken overload. | Open owned short-lived connection without caller transaction; finite timeout, close and await actual reset termination on failure/cancellation before releasing admission. |
| SQL adapter | Pinned [SQL Server adapter](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/SqlServerDbAdapter.cs) filters ignored tables by unqualified name. Reseed uses seed minus one only when identity last_value is non-null; sequence is outside that operation. | dbo-only APP, ignore exactly SchemaVersions; reject ambiguous journal/unexpected schemas, cache per allocated/schema-verified database. Independently prove first/populated/repeated identity and sequence behavior, FK trust and trigger restoration. |
| Locked graph | All six `.NETFramework,Version=v4.8` lock sections resolve SqlClient 7.0.2, dbup-sqlserver 7.2.0, xunit 2.9.3; SqlClient.SNI 6.0.2, System.Buffers 4.6.1, System.Memory 4.6.3, Unsafe 6.1.2. Central transitive pinning and RestorePackagesWithLockFile are enabled. | Locked restore after the narrow package change; examine provider dependency selection rather than silently upgrading or restoring the removed SQL framework. |
| Native assets | Local cached SNI 6.0.2 package contains x86/x64/arm64 DLLs; its build/net462 targets copy SNI outputs for the selected host. This confirms package payload/targets only. | Inspect new worktree build output and matching testhost architecture, load native SqlClient using real SQL. No old checkout bin/obj artifact counts as current evidence. |
| Binding redirects | No checked-in app.config or explicit AutoGenerateBindingRedirects/GenerateBindingRedirectsOutputType override in these six test projects or repository build props/targets. Do not infer the generated redirects from the lock file. | Inspect effective MSBuild settings and generated `<test assembly>.dll.config` after build; verify SqlClient and transitive assembly binding by executing net48, preserving warning-as-error policy. |
| Runtime | All six csproj target net48; global.json asks SDK 8.0.400 with latestFeature roll-forward, C# 12. | Windows Framework 4.8 targeting/runtime and SDK availability; no modern product target activation. |

The candidate was checked against published/pinned primary sources on 2026-10-18. Current-version Respawner/Table examples must not replace the v4 contract. Actual Respawn/provider loading, reset timeout/drain, empty/populated ledger reset, reseeding and cleanup remain **NOT RUN (T01)**. No Respawn package is installed by T00.

## Direct selection protocol

Run from the worktree root. The owner project path is `tests/MyDmsVn.Server.Infrastructure.<suffix>/MyDmsVn.Server.Infrastructure.<suffix>.csproj`. Every listed class uses namespace `MyDmsVn.Server.Infrastructure.IntegrationTests`. Restore before tests; stop on failure. These are **source-resolved commands, not commands executed by T00**:

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=catalog.trx' `
  --results-directory ('TestResults/resp-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Catalog tests failed.' }
```

| Owner suffix | Filter (literal --filter argument) | Expected baseline cases |
|---|---|---:|
| Catalog.Tests | `Category=FastSQL` | 5 SQL |
| Catalog.Tests | `FullyQualifiedName~CatalogMappingIntegrationTests` | 2 SQL |
| Inventory.Tests | `FullyQualifiedName~GoodsReceiptQueryIntegrationTests` | 3 SQL; one lock exception remains fresh |
| Inventory.Tests | `FullyQualifiedName~InventoryQueryIntegrationTests` | 2 SQL |
| Inventory.Tests | `FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.GoodsReceiptCommandIntegrationTests.Cross_session_DMV_permission_changes_at_SQL_Server_2022` | 2 pure Theory rows |
| Security.Tests | `FullyQualifiedName~IdentityStoreIntegrationTests` | 4 SQL; one contention exception remains fresh |
| Security.Tests | `FullyQualifiedName~SecurityAdministrationSqlTests` | 10 SQL; five candidates/five fresh exceptions |
| Security.Tests | `FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.LoginTimingMitigationTests.Wrong_password_for_supported_hash_has_same_work_budget_as_missing_user` | 3 pure Theory rows |
| IntegrationTests | `FullyQualifiedName~SqlTestDatabaseMutationTests` | 11 cases: 2 SQL, 9 pure |
| IntegrationTests | `FullyQualifiedName~SqlTestDatabaseNameTests` | 6 pure cases |
| Foundation.Tests | `FullyQualifiedName~PersistenceTransactionTests` | 5 SQL |
| Migrations.Tests | `FullyQualifiedName~SchemaCompatibilityPolicyTests` | 8 pure cases |

For another selector substitute a method/class from the case map and its actual project. Discovery via `--list-tests` may help resolve a row display name; it does not run SQL or establish acceptance. Review TRX actual cases/outcomes, not just process exit. Missing mandatory cases/skips/aborts or failed cleanup block SQL acceptance.

## Matched T05 comparison (future execution)

Freeze the T04 implementation revision before either run; T00's source revision is an inventory anchor, not the future benchmark candidate. Retain the same case/assertion inventory plus explained new tests, reset profile/seed version, build outputs and package locks in both modes. Record exact commit and any working diff; relevant edits invalidate affected evidence.

Use the same Windows machine, SDK/Framework/testhost architecture, SQL Server version/instance, designated non-production master configuration, permissions, project order and test options. Record safe configuration labels only; keep raw connection data, allocation IDs, Theory values and TRX local. Avoid competing SQL/GUI workloads. Do not restore old framework manifests, leases, wrappers or watchdogs.

Build/restore that candidate once using the six-project sequential loop in [tests/README](../tests/README.md#complete-sql-milestone), with `dotnet build <project> -c Release -f net48 --no-restore` before reusing outputs. Then run **unfiltered Foundation, Catalog, Inventory, Security, Migrations, IntegrationTests**, once with explicit fresh and once with explicit reuse. New lifecycle/profile cases belong in the correct owners; dedicated exceptions remain fresh in both modes. The fixture-mode variable below does not exist before T02; T04 must first make accepted profiles default to reuse.

```powershell
# Future T05 only, after the setting and frozen build exist.
$priorMode = [Environment]::GetEnvironmentVariable('MYDMSVN_SQL_TEST_FIXTURE_MODE', 'Process')
$owners = @('Foundation.Tests', 'Catalog.Tests', 'Inventory.Tests',
  'Security.Tests', 'Migrations.Tests', 'IntegrationTests')
try {
  foreach ($mode in @('fresh', 'reuse')) {
    $env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $mode
    $results = 'TestResults/resp-' + $mode + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
    foreach ($owner in $owners) {
      $name = 'MyDmsVn.Server.Infrastructure.' + $owner
      $project = 'tests/' + $name + '/' + $name + '.csproj'
      dotnet test $project -c Release -f net48 --no-restore --no-build `
        --logger ('trx;LogFileName=' + $owner + '.trx') --results-directory $results
      if ($LASTEXITCODE -ne 0) { throw ('Failed: ' + $mode + '/' + $owner) }
    }
  }
}
finally {
  [Environment]::SetEnvironmentVariable('MYDMSVN_SQL_TEST_FIXTURE_MODE', $priorMode, 'Process')
}
```

Before this loop, load only the explicit operator-designated non-production SQL test connection as described in tests/README; validate required privileges and fail on missing configuration. No command prints the connection. No automatic orphan cleanup or fresh fallback is permitted.

For each mode/project, record selected versus executed cases/rows, pass/fail/skip/not-run, wall time, allocation/migration/reset/seed/drain/drop counts and durations from actual operations. Count shared migration once per collection, not once per member. Correlate setup/reset/seed/final-cleanup failures to affected outcomes; never report missing tests as PASS. Compare identical cases, explain any difference and require successful guarded cleanup/no unresolved allocation. Inspect normal/filtered, initialization-failure and cleanup-only-failure outcomes during T02–T04 before the final comparison.

Require equivalent behavior, no contamination, restored trigger/constraints/journal/ownership, drained sessions and proven fresh mode. One pair is a local observation, not a statistical benchmark; claim improvement only if measured. Repeat only for invalid evidence, relevant repairs or a concrete regression question. A failed reset poisons the fixture; do not continue on uncertain state or classify a skipped body as success.

## T00 execution record

Scope: documentation/static audit only. Product/test/schema/package code unchanged. Source baseline remains the revision above; documentation changes are the reviewable working diff on this branch.

| Check actually performed | Outcome |
|---|---|
| Source attributes/signatures/InlineData enumeration against all six project owners; compare map completeness, totals, SQL traits and Theory row counts | PASS: 193 methods / 211 expanded cases, 135 SQL and 76 non-SQL; no missing/duplicate method rows. |
| Read csproj targets, linked Compile/resources/references, central pins and six lock sections; production/test migrations, trigger, identity and sequence definitions | PASS: net48 source/package boundary recorded; runtime expectations remain separate. |
| Resolve documented project/class/method/category selectors against source; local Markdown links and relative destinations | PASS after static validation. No discovery/testhost execution implied. |
| UTF-8 without BOM, CRLF/final newline, diff whitespace and roadmap/status consistency | PASS after static validation. |
| Locked restore/build, discovery, product/SQL/GUI regression, Respawn reset/runtime/performance comparison | NOT RUN: not required for documentation-only T00; T01 runtime proof remains pending. |
| SQL allocation/reset/seed/drop and leftover cleanup | NOT RUN: no SQL mutation or allocation was attempted by T00. |

Commands actually used for the static audit (worktree root; scratch helpers are ignored local audit aids, not a checked-in runner):

```powershell
python .superpowers/sdd/20261018-002-respawn-fixture-reuse/inventory.py
python .superpowers/sdd/20261018-002-respawn-fixture-reuse/verify-t00.py
git diff --check
git diff -- AI_CONTEXT.md docs/plans/README.md docs/plans/20261018-002-respawn-fixture-reuse.md
```

The inventory uses current top-level cs files in the six owners, excluding generated bin/obj artifacts, and expands each InlineData attribute. The static verifier compares the method multiset, row counts and categories to the map, checks the six TFM/lock sections, counts migration tables/identities, resolves selectors/local links and validates encoding. All listed final checks exited 0. Initial scratch-script decoding used the Windows default encoding and failed; it was corrected to explicit UTF-8, affected documentation was regenerated from original UTF-8 source, and the final checks passed. No product failure or test result was inferred from that tooling repair.

Independent documentation review confirmed source totals and candidate classifications. Two source-description findings were corrected: owner-local category discoverers versus Catalog's reference, and the bare allocation used by the Foundation unregistered-repository test. Static checks were repeated after those corrections.

At the T00 handoff, the next task was **RESP-T01 — Prove the pinned reset profile on net48**. The 20 reusable candidates are conditional, not accepted fixtures. RESP acceptance and all L2DB/P5/P6/P7 gates remain open.

## RESP-T01 execution record — pinned profile accepted

Date: **2026-10-18**. Source base: `6383289a2e43e2021abd65048a1e2f3f3838324e`; uncommitted candidate on `codex/resp-t00-baseline`. Candidate content SHA256 (ordered central pin, two csproj, Foundation/Integration/Catalog locks, allocator, profile and probe source): `2fd833d4cb14f61f90a0e96be9c673dc157f901e3c10f72779645939e7fbdfd0`. T00 documents remain in this same worktree; no commit, merge or push is implied.

Implemented [RespawnResetProfile](../tests/MyDmsVn.Server.Infrastructure.IntegrationTests/Fixtures/RespawnResetProfile.cs) and the narrow live-connection ownership verifier in [SqlTestDatabase](../tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs). The verifier was pulled forward from T02 because T01 must prove positive ownership before reset DDL. Existing fresh create/drop behavior is unchanged; linked helper owners were rebuilt. The cost of this sequencing is one additive helper API consumed by the profile; T02 must reuse that ownership check.

The profile owns one v4 Checkpoint for one allocation, validates the current production schema and exact initial objects/constraints/indexes/default security metadata before caching, and compares native metadata/journal snapshots before and after reset. It clears all 15 mutable tables, reseeds all 13 mutable identities and explicitly restarts the receipt sequence. Only the exact append-only trigger is disabled; finally uses a separately bounded, ownership-verified connection to restore and verify it. Reset/validation/restoration/cancellation failure terminally poisons the profile; later reset is rejected. It closes the owned reset connection on cancellation and awaits the real reset task before restoring/releasing. No Task.WhenAny timeout is used by the reset implementation. Test-only worker handshakes remain bounded and drained.

All production projects/scripts, UoW/permissions/business assertions, net48 targets and assembly serial guards are unchanged. Respawn is referenced directly only by IntegrationTests and Foundation; Catalog receives its existing project-reference dependency transitively. Only those three lock files change. Product lock graphs contain no Respawn. Collection fixtures, seed/admission lifecycle and fresh/reuse parsing remain **T02**, and business adoption remains **T03/T04**.

| Final verification scope | Actual outcome |
|---|---|
| Six SQL owners, sequential locked restore + Release/net48 build | PASS: 6/6, zero build warnings/errors. |
| Foundation, unfiltered Release/net48 | PASS: **27 passed / 0 failed / 0 skipped** (16 original + 11 new SQL reset probes). |
| IntegrationTests, `FullyQualifiedName~SqlTestDatabase` | PASS: **22 passed / 0 failed / 0 skipped**, including all selected Theory rows and owned allocation/cleanup/pooling guards. |
| Focused cancellation probe after keeping observer session open | PASS: 1/1; real reset blocking observed before cancellation, no reset request afterward. Included again in final Foundation pass. |
| Initial RED with unimplemented profile | 10/10 failed with NotImplementedException; no reset implementation existed. |
| Schema-boundary regression RED | 2 failed / 1 passed: initial extra-object admission and cached database-DDL-trigger drift reproduced, while the empty-ledger control passed. Both findings were corrected and covered by final probes. |
| Allocation cleanup | Every new probe's scenario harness attempted positively owned cleanup and independently asserted its allocation absent; final selected cases reported no unresolved owned cleanup. No orphan adoption or prefix-only deletion. |
| Runtime/native/binding | SDK **8.0.425**, Windows net48; Respawn asset **lib/net461**, SqlClient **ref/lib/net462 7.0.2**, SNI **6.0.2**. Live testhost loaded **Microsoft.Data.SqlClient.SNI.x64.dll**. Generated Foundation/Integration/Catalog configs redirect SqlClient to assembly **7.0.0.0**. Real SQL/reset execution proves this tested combination loads. |

Commands actually run from this worktree (each nonzero restore/build/test stops its sequence):

```powershell
# Refresh only the new dependency graph before locked verification.
foreach ($owner in @('Foundation.Tests','IntegrationTests','Catalog.Tests')) {
  $name = 'MyDmsVn.Server.Infrastructure.' + $owner
  dotnet restore ('tests/' + $name + '/' + $name + '.csproj')
  if ($LASTEXITCODE -ne 0) { throw ('Restore failed: ' + $owner) }
}
foreach ($owner in @('Foundation.Tests','Catalog.Tests','Inventory.Tests',
  'Security.Tests','Migrations.Tests','IntegrationTests')) {
  $name = 'MyDmsVn.Server.Infrastructure.' + $owner
  $project = 'tests/' + $name + '/' + $name + '.csproj'
  dotnet restore $project --locked-mode
  if ($LASTEXITCODE -ne 0) { throw ('Locked restore failed: ' + $owner) }
  dotnet build $project -c Release -f net48 --no-restore
  if ($LASTEXITCODE -ne 0) { throw ('Build failed: ' + $owner) }
}
$foundation = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
dotnet test $foundation -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~Cancellation_closes_and_drains' `
  --logger 'trx;LogFileName=cancel.trx' --results-directory TestResults/resp-t01-cancel
if ($LASTEXITCODE -ne 0) { throw 'Observed cancellation probe failed.' }
dotnet test $foundation -c Release -f net48 --no-restore --no-build `
  --logger 'trx;LogFileName=foundation.trx' --results-directory TestResults/resp-t01-accepted
if ($LASTEXITCODE -ne 0) { throw 'Foundation acceptance failed.' }
dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -c Release -f net48 --no-restore --no-build --filter 'FullyQualifiedName~SqlTestDatabase' `
  --logger 'trx;LogFileName=helper.trx' --results-directory TestResults/resp-t01-helper
if ($LASTEXITCODE -ne 0) { throw 'Owned database helper tests failed.' }
git diff --check
```

The restore refreshes above were performed as individual Foundation, then Integration/Catalog commands; the loop is their equivalent direct form, not an external wrapper. Short named results directories belong to this local session and are ignored; use fresh names for a new run. The focused cancellation command rebuilds Foundation after the last probe-only fix. No relevant code/package changes occurred after its final build; final Foundation and helper runs were sequential on that candidate. Review of actual TRX names/outcomes found all 27/22 selected cases with no skips.

Intermediate development runs exposed and repaired test-seed SQL quoting, system-table identity enumeration, initial permission-inventory grouping and empty XML snapshot handling. A strengthened cancellation probe initially counted its own re-opened observer after SQL reused the closed reset SPID; retaining the observer connection fixed that measurement, and the final real-block cancellation probe passed. These intermediate failures are not claimed as acceptance. No testhost was killed and no cleanup was waived.

Fresh independent source review identified initial extra-object admission, missing database DDL-trigger metadata and pending dependency-lock synchronization. The fix pass added an exact initial inventory, independent trigger/event snapshots and synchronized Integration/Catalog locks; regression probes demonstrated RED before the final GREEN verification. No second review or unrelated suite was substituted for those checks.

At the T01 handoff, no collection lifecycle or fixture-mode switch existed; the next task was RESP-T02. The T02 execution record below supersedes that lifecycle status. Business adoption, all-six-owner SQL acceptance, performance comparison and GUI/P5/P6/L2DB acceptance remain pending.

## RESP-T02 execution record — exclusive lifecycle

This section records the T02 snapshot before business adoption; current Catalog status is in the T03 record below.

Completed 2026-10-18 on the same uncommitted `codex/resp-t00-baseline` worktree, base revision `6383289a2e43e2021abd65048a1e2f3f3838324e`. No commit/merge/push was performed. Code/config candidate SHA256: `2099a01dbadea96a2ab7aa369c70f3abde7661356c1e38e94cf9bd843b7b37cb`. This digest covers sorted repository-relative paths plus NUL-delimited bytes of the changed C#/project/lock/central-package/xUnit-config files, including T01 inputs; documentation and raw artifacts are excluded. All normal and final fault runs below used this source candidate. The full Foundation run rebuilt it before the subsequent six-owner build checks; no code changed between those checks and final runs.

`ReusableSqlFixture` owns one migrated APP checkpoint per positively owned allocation, validates checkpoint identity, serializes initialization/reset/seed/body/drain/drop, and exposes no shared product connection/UoW. `SqlFixtureTestScope` stops and awaits all tracked workers, attempts every cleanup in reverse order, preserves concurrent worker and resource failures, and releases admission only afterward. Failed reset/seed/schema/ownership/cleanup is terminal; an ordinary scenario assertion with successful resource drain allows the next reset. Post-seed verification validates cached schema/ownership; the caller's deterministic seed must assert its own row baseline. Explicit teardown can retry only the same guarded failed drop; it cannot clear poison or allocate a replacement.

`MYDMSVN_SQL_TEST_FIXTURE_MODE` supports fresh/reuse; absent/blank defaults to fresh. Only the new Catalog adapter probes use the collection, with one Fact and two independently reset/seeded Theory rows. The five original Catalog business methods remain fresh. Fixture initialization/final drop diagnostics use the xUnit v2 collection fixture `IMessageSink`; member observations use `ITestOutputHelper`. This is supported by [xUnit output guidance](https://xunit.net/docs/capturing-output). Shared phases are reported once per allocation, with mode= fresh/reuse, profile=APP and bounded non-sensitive method/row labels; no database name or connection is emitted by the observation formatter.

| Final scope | Actual outcome |
|---|---|
| Six SQL owners, sequential locked restore + Release/net48 build | PASS: 6/6, zero build warnings/errors; no dependency refresh required. |
| Foundation, unfiltered | **41 passed / 0 failed / 0 skipped**: all original and T01 probes, 13 new lifecycle SQL probes and one local probe-cleanup regression. |
| IntegrationTests, scope/parser plus existing allocator helper selection | **35 passed / 0 failed / 0 skipped**: 13 new local cases and all 22 existing helper cases. |
| Catalog, Category=FastSQL, mode unset | **8 passed / 0 failed / 0 skipped**: five unchanged business cases and three default-fresh adapter cases. |
| Catalog, adapter class, explicit reuse | **3 passed / 0 failed / 0 skipped**: exact Fact + Theory rows 1/2; three body entries and reset/seed/verify/cleanup observations each. |
| Catalog, explicit reuse, focused display-name row 1 | **1 passed / 0 failed / 0 skipped**; actual TRX name ends `(row: 1)`. |
| Adapter initialize-failure, explicit reuse | **3 expected FAILED / 0 passed / 0 skipped**, every affected source row present; zero body entries; owned failed initialization cleanup observed. |
| Adapter reset-failure / seed-failure, separate reuse runs | **3 expected FAILED each / 0 passed / 0 skipped**, zero body entries; later admission remains poisoned. |
| Adapter cleanup-failure, explicit reuse | **3 expected FAILED / 0 passed / 0 skipped**, exactly one body entry; cleanup failure is visible and both later bodies rejected. |

The fault-run command exits were nonzero as required; these are failure-propagation evidence, never passing SQL acceptance. Actual `UnitTestResult` names/outcomes were reconciled with current source, including every selected parameter row and original helper case. No missing result or skipped fault is counted as PASS. Current six-owner source: **226 methods / 252 cases (162 SQL, 90 non-SQL)**. The frozen original 193 methods and every original Theory row remain present, with no business-source assertion changes. See [T02 additions](RESPAWN_CASE_MAP.md#resp-t02-additions).

Final reuse adapter observations below combine shared phases from fixture diagnostics and per-case phases from the three TRX test outputs exactly once. Per-phase milliseconds are local elapsed totals; phases overlap (`initialize-reset` includes profile preparation/first reset), so summing them is not a wall-clock metric.

| Phase | Observations | Elapsed total ms |
|---|---|---|
| allocate | 1 | 869.0 |
| migrate | 1 | 3235.5 |
| initialize-reset | 1 | 2541.8 |
| reset | 3 | 868.2 |
| seed | 3 | 500.8 |
| verify | 3 | 380.2 |
| cleanup | 3 | 0.4 |
| drop | 1 | 54.3 |

In default-fresh adapter output, allocation/migration/initialize-reset/reset/seed/verify/cleanup/drop each occurs three times; reuse has allocation/migration/initialize-reset/drop once. The focused reuse row has each operation once. Foundation assertions additionally prove shared initialization counts, fresh per-case allocation counts, worker rollback/disposal before admission, actual blocked reset cancellation drain, marker preservation, profile mismatch rejection, retained guarded drop retry, and independent absence checks for fixture-owned probe allocations (the separate mismatched-profile database also completes guarded disposal). No testhost was killed, no allocation was adopted by prefix and no unresolved owned cleanup was reported.

Direct commands actually sequenced in this session (SQL projects never run concurrently):

```powershell
# Prerequisite: approved non-production master connection in the existing test environment.
$foundation = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
$integration = 'tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj'
$catalog = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
dotnet test $foundation -c Release -f net48 --no-restore `
  --logger 'trx;LogFileName=foundation.trx' --results-directory TestResults/resp-t02-final-foundation
# Check exit, then for each Foundation/Catalog/Inventory/Security/Migrations/Integration owner:
dotnet restore $project --locked-mode
dotnet build $project -c Release -f net48 --no-restore
# Check each exit before continuing.
dotnet test $integration -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~ReusableSqlFixtureTests|FullyQualifiedName~SqlTestDatabase' `
  --logger 'trx;LogFileName=helper.trx' --results-directory TestResults/resp-t02-final-helper
# Save caller's mode/probe values, then unset both for default-fresh evidence.
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $null
$env:MYDMSVN_SQL_TEST_ADAPTER_PROBE = $null
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=catalog.trx' `
  --results-directory TestResults/resp-t02-final-catalog-fresh
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = 'reuse'
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~SqlFixtureCollectionAdapterTests' `
  --logger 'trx;LogFileName=adapter.trx' --results-directory TestResults/resp-t02-final-adapter-reuse
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~Every_theory_row_receives_reset_and_seed_before_body&DisplayName~row: 1' `
  --logger 'trx;LogFileName=row.trx' --results-directory TestResults/resp-t02-final-filtered-row
# Separately set each fault value, require nonzero exit AND all three actual failed results:
# initialize-failure, reset-failure, seed-failure, cleanup-failure
$env:MYDMSVN_SQL_TEST_ADAPTER_PROBE = $fault
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~SqlFixtureCollectionAdapterTests' `
  --logger 'trx;LogFileName=adapter.trx' --results-directory ('TestResults/resp-t02-final-' + $fault)
# Restore the caller's prior environment values in finally.
git diff --check
```

The six-owner restore/build portion uses the direct loop already shown in the T01 record. No external runner, report machinery or invocation manifest was introduced. Local TRX/logs are ignored development evidence, not committed artifacts; use new result names on rerun. Catalog adapter diagnostics are enabled through its test-owned xunit.runner.json. Per-case runner error summaries may repeat captured text; audit the TRX test output rather than adding those duplicate summaries.

Development evidence: initial local scope/parser and lifecycle scaffolds were RED before implementation. The first lifecycle run had 11 passing and one failing cancellation case: a rejected waiting admission incorrectly entered setup cleanup and dropped the active lifetime's database. Moving unavailable-admission rejection outside that cleanup path fixed the actual failure; final Foundation includes that real blocking/cancellation probe. Fresh independent review found a pending-scope leak in two probe failure paths; a local cleanup regression reproduced it RED, and the corrected harness always drains/disposes pending scopes while preserving the original error. Further RED regressions proved lost aggregate worker failures and cross-allocation checkpoint admission; both are corrected and included in final runs. No review findings remain deferred.

Verification limits: no full six-owner SQL pass, business collection adoption, default-reuse rollout, speedup comparison or GUI/P5/P6/L2DB acceptance. T02 establishes a usable lifecycle contract; it does not complete RESP. Next active task: **RESP-T03 — Adopt and challenge the Catalog collection**.

## RESP-T03 execution record — Catalog adoption

This section records the T03 snapshot before the T04 rollout/default change; current status is in the T04 record below.

Completed 2026-10-18 on uncommitted `codex/resp-t00-baseline`, base revision `6383289a2e43e2021abd65048a1e2f3f3838324e`, in the existing managed worktree. Code/config candidate SHA256: `72e0072a2c3a35ebd549f1cc130f65366ced88a46e3859848a97c5a174369be7` (same path/byte digest algorithm as T02). No commit, merge or push. Only Catalog test code changed after the T02 candidate; fixture/profile core, production code/scripts, packages and targets are unchanged. All final normal/fault runs below use that same code candidate; later documentation edits do not change its digest.

All five original Catalog methods now participate in one explicit `Catalog SQL` collection alongside the three T02 adapter cases and two new contamination rows. `CatalogSqlCollectionFixture.RunAsync` awaits APP reset, explicit catalog/User empty-baseline seed and cached-boundary verification before each body. The adapter cases optionally add/assert their one User. The original employee-link User setup remains inside CRUD, using its returned ID. Original business bodies are byte-identical after normalizing only connection references and resource-ownership syntax; all assertions, commands, queries, constants, permissions, denied-write counts and rollback calls remain intact. Migration/allocation move to the shared fixture, and its migration error replaces per-method migration-success checks.

Each provider, DI scope, UoW and direct scenario connection registers cleanup with the exclusive scope. LIFO keeps UoW/scopes before provider disposal; dispose exceptions participate in poison. Nothing shares a business connection/transaction/UoW across cases. All current Catalog SQL consumers explicitly join the collection; no assembly-wide parallelism change was introduced.

The new contamination Theory deliberately commits a sentinel User/Product/Warehouse/linked Employee/Customer and fails an assertion. Successful tracked cleanup leaves the fixture reusable. Each row then calls the five original business scenarios in a controlled forward/reverse order and asserts that each used another collection reset, followed by an empty-baseline scope. These nested calls are additional executions of the original assertions, not additional xUnit results. The test first failed RED twice against the old independent fresh business helpers (missing expected collection-reset observation), then passed after adoption. An initial analyzer error in the draft probe was corrected before that valid RED run; it is not failure-boundary evidence.

| Final selected scope / configuration | Actual results |
|---|---|
| Catalog and Foundation, locked restore + Release/net48 build | PASS: 2/2 affected owners, zero warnings/errors. |
| Catalog complete project, explicit fresh | **10 passed / 0 failed / 0 skipped**, **22 scenario scopes**, identical original business assertions. |
| Catalog complete project, explicit reuse | **10 passed / 0 failed / 0 skipped**, same named cases/rows and **22 scenario scopes**. |
| Focused original denied-write method, reuse | **1 passed / 0 failed / 0 skipped**, one scope. |
| Focused new Theory row `reverse: True`, reuse | **1 passed / 0 failed / 0 skipped**, seven scopes; other row is not reported as executed. |
| Foundation dedicated worker and blocked-reset cancellation methods | **2 passed / 0 failed / 0 skipped**; separate SQL sessions/transaction, explicit worker stop/rollback/disposal before admission, real blocked reset drained before rejection of waiting scope. |
| Initialization fault, complete Catalog project, reuse | **10 expected FAILED / 0 passed / 0 skipped**; intended setup error visible in every source case/row, zero bodies. |
| Reset / seed faults, separate reuse runs | **8 expected FAILED each / 0 passed / 0 skipped**: five original methods + three adapter rows; intended first error, seven poisoned admissions, zero bodies. |
| Schema / trigger faults, separate reuse runs | **8 expected FAILED each / 0 passed / 0 skipped** on the same eight members; mutation succeeds in seed, post-seed verify fails, seven later admissions poisoned, zero bodies. |
| Cleanup-only fault, reuse | **8 expected FAILED / 0 passed / 0 skipped**, exactly one body then seven poisoned admissions. Tracked failing IDisposable is attempted after the actual body resources drain. |

The five non-initialization fault selections intentionally exclude the two rows that themselves catch a deliberate scenario failure. Initialization runs the full project unfiltered. Every expected fault exits nonzero and retains actual failed test results; none is counted as a passing SQL run. An earlier initialization selector matched seven cases because it omitted the three adapter rows; it was corrected to an unfiltered run and all ten actual failed results were checked. The earlier seven-result run is not the final initialization evidence.

Source versus TRX review checked every original business method and both rows of each Catalog Theory, exact selected counts, distinct result names, actual outcomes and captured phase/body observations. For normal runs, each of the eight non-challenge cases has one reset/seed/verify/cleanup/body; each new challenge row has seven. In faults, the first root message matches the intended reset/seed/schema/trigger/cleanup boundary, and each later member reports terminal poison with no body. Schema and trigger faults both have successful seed followed by failed verify, so a SQL mutation error cannot masquerade as boundary rejection. Initialization exposes its intended migration failure in all ten results.

| Phase | Fresh observations | Reuse observations | Fresh elapsed total ms | Reuse elapsed total ms |
|---|---|---|---|---|
| allocate | 22 | 1 | 7444.8 | 1012.7 |
| migrate | 22 | 1 | 54086.6 | 3372.2 |
| initialize-reset | 22 | 1 | 53754.2 | 2569.0 |
| reset | 22 | 22 | 4057.8 | 4502.1 |
| seed | 22 | 22 | 688.5 | 627.7 |
| verify | 22 | 22 | 1546.0 | 1420.5 |
| cleanup | 22 | 22 | 5.4 | 4.1 |
| drop | 22 | 1 | 1386.9 | 95.4 |

TRX run elapsed observations: fresh **140.3 s**, reuse **21.2 s** on this machine/configuration. Counts/durations combine per-test captured output and shared fixture diagnostics once, without duplicated runner summaries. `initialize-reset` includes first profile preparation/reset per allocation; elapsed phases overlap and must not be summed as run wall time. These are local Catalog observations, not the six-owner T05 comparison or product-performance acceptance.

Fresh has 22 owned successful drops; reuse has one. Each fault run has one allocation and one successful guarded drop, including cleanup-only failure and schema/trigger drift. Foundation's harness separately asserts absence of its owned probe allocations after teardown. No testhost was killed, no cleanup error was waived and no unresolved owned allocation was reported. There was no adoption or prefix-only deletion of a leftover database.

Direct commands executed in this worktree, with the explicit pre-existing designated non-production master test connection and saved/restored process mode/fault settings:

```powershell
$catalog = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
$foundation = 'tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj'
# For each affected project, check each exit before proceeding:
dotnet restore $project --locked-mode
dotnet build $project -c Release -f net48 --no-restore
dotnet test $foundation -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~Outstanding_worker_blocks_reset_until_drained|FullyQualifiedName~Reset_cancellation_drains_before_rejecting_the_waiting_scope' `
  --logger 'trx;LogFileName=lifecycle.trx' --results-directory TestResults/resp-t03-lifecycle
$env:MYDMSVN_SQL_TEST_ADAPTER_PROBE = $null
# Execute separately with $mode = 'fresh', then 'reuse'; stop on an unexpected failure.
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $mode
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --logger 'trx;LogFileName=catalog.trx' --results-directory ('TestResults/resp-t03-' + $mode)
# Mode remains reuse for focused runs and fault probes.
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~Explicitly_denied_catalog_write_leaves_business_tables_unchanged' `
  --logger 'trx;LogFileName=business.trx' --results-directory TestResults/resp-t03-focused-business
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~Failed_scenario_and_reordered_business_bodies_receive_clean_collection_scopes&DisplayName~reverse: True' `
  --logger 'trx;LogFileName=row.trx' --results-directory TestResults/resp-t03-focused-row
# Final initialization fault evidence is unfiltered: require nonzero + ten named failed results.
$env:MYDMSVN_SQL_TEST_ADAPTER_PROBE = 'initialize-failure'
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --logger 'trx;LogFileName=fault.trx' --results-directory TestResults/resp-t03-initialize-all
# Separately use each $fault: reset-failure, seed-failure, schema-failure, trigger-failure, cleanup-failure.
$env:MYDMSVN_SQL_TEST_ADAPTER_PROBE = $fault
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --filter 'FullyQualifiedName~CatalogBackendIntegrationTests|FullyQualifiedName~CatalogMappingIntegrationTests|FullyQualifiedName~SqlFixtureCollectionAdapterTests' `
  --logger 'trx;LogFileName=fault.trx' --results-directory ('TestResults/resp-t03-' + $fault)
# Require nonzero + eight named failed results per fault, then restore prior settings in finally.
git diff --check
```

RED/targeted GREEN development runs used the new challenge class filter in explicit reuse; final complete-project fresh/reuse passes supersede those focused runs. Raw TRX/logs/source snapshots remain ignored local evidence. No external support project, runner/report framework, global lease or watchdog was introduced. Source inventory preserves all original **193 methods / 211 cases** and every original row; current six-owner source is **227 methods / 254 cases (164 SQL / 90 non-SQL)**. See [T03 map](RESPAWN_CASE_MAP.md#resp-t03-adoption-and-additions).

Fresh independent review found no actionable T03 defects after comparing original bodies, collection/admission, resource disposal, fault paths and probe/session semantics. The reviewer did not execute SQL or assess runtime results while these sequential runs were active; the final source/TRX/operation audit above supplies that evidence. No aborted-run recovery is claimed. Profile-specific empty/populated ledger, ownership and drift evidence remains the T01–T02 record; unchanged business contention/principal/schema exceptions remain dedicated fresh.

Remaining limits: default remains fresh; Inventory/Security adoption awaits T04; full all-six-owner fresh/reuse correctness/performance acceptance awaits T05. No GUI/P5/P6/P7, product runtime, HTTP or L2DB acceptance follows. Next active task: **RESP-T04 — Extend reuse to the proven data-only inventory**.

## RESP-T04 execution record — frozen data fixtures

Completed 2026-10-18 in the existing uncommitted `codex/resp-t00-baseline` worktree, base revision `6383289a2e43e2021abd65048a1e2f3f3838324e`. Frozen code/config SHA256: `0838ff87bcc0c1d8d815f5e09d94f3c9c951fd48b00105d9fd78f170f580f4fb`, using the same sorted path/NUL/byte digest defined at T02. No commit/merge/push. All matched Inventory/Security runs below used this source after the final review fix. Subsequent documentation edits do not change the digest. Default reuse is part of this candidate, before T05.

All **20 original T00 candidates** are adopted: Catalog 5, Inventory 7, Identity/Security 8. No original candidate was omitted or reclassified; all original 193 methods/211 cases and rows remain. Current source has **230 methods / 257 cases (166 SQL / 91 non-SQL)**, with owner totals Foundation 41, Catalog 10, Inventory 36, Security 53, Migrations 65, IntegrationTests 52. T04 adds two SQL contamination Facts and one local failure-probe regression. The parser test's name/expected blank-value behavior changes to the approved reuse default; its seven input rows, invalid-mode rows and explicit fresh path remain.

Inventory/Security link the existing local core/profile and one small ApplicationSqlCollectionFixture adapter into their own assemblies, with pinned test-only Respawn 4.0.0 references/locks. SDK **8.0.425**, Windows **net48**, SqlClient **7.0.2**, SNI **6.0.2** remain; generated owner bindings target SqlClient assembly **7.0.0.0**. Live reset/data runs below verify the new owner graphs load. No production code/package/script, runtime target, HTTP or persistence-provider change was made.

The core is lazy per xUnit collection. Only a participating data case evaluates mode/fault configuration and allocates/migrates its owned APP database; a fresh-only selection in a mixed class evaluates none of them. Existing fresh methods and all unconverted files retain their prior code. Per-case reset checks the entire APP graph, then an explicit 15-table empty baseline. Query SeedAsync runs before body in a typed seed callback and returns the actual captured IDs/version; profile verification follows it. Other cases retain their deterministic data-only arrangements after the empty baseline. Top-level providers/scopes/UoWs/direct connections register LIFO cleanup; no business connection/transaction/UoW is shared.

The unchanged APP schema/profile already includes all new seed dependencies: receipt/header/line/reference, ledger/balance, users/roles/decisions/revisions/audit and singleton binding. Repeated native reset/boundary checks and contamination proof below validate that graph; no additional ignored table or narrower profile is introduced. All 15 adopted scenario bodies match pre-T04 source after explicit connection/resource/seed ownership normalization. Relative rowversion comparisons and all original authorization/revision/preview/audit/rollback assertions remain. Eight mixed-class fresh exception method bodies and every other unconverted Inventory/Security file are byte-unchanged.

| Selected scope / mode | Actual outcome |
|---|---|
| Final six SQL owners, sequential locked restore + Release/net48 build | **6/6 PASS**, zero build warnings/errors. |
| Foundation complete project after default change | **41 passed / 0 failed / 0 skipped**; full reset/profile/lifecycle, real cancellation/timeout/worker drain, ownership/schema/trigger and guarded cleanup probes. |
| IntegrationTests helper/parser/local regression after review fix | **36 passed / 0 failed / 0 skipped**: 22 original helper cases and 14 local lifecycle/parser/probe cases. |
| Catalog complete project, mode unset | **10 passed / 0 failed / 0 skipped**, default reuse; 22 scopes, one allocation/migration/drop. |
| Inventory seven original eligible methods + new contamination Fact, explicit fresh | **8 passed / 0 failed / 0 skipped**, 25 scopes; 25 allocations/migrations/drops. |
| Same Inventory selection, explicit reuse | **8 passed / 0 failed / 0 skipped**, same assertions/25 scopes; one allocation/migration/drop. |
| Identity/Security eight original eligible methods + new contamination Fact, explicit fresh | **9 passed / 0 failed / 0 skipped**, 28 scopes; 28 allocations/migrations/drops. |
| Same Identity/Security selection, explicit reuse | **9 passed / 0 failed / 0 skipped**, same assertions/28 scopes; one allocation/migration/drop. |
| Inventory two selected query methods, mode unset | **2 passed / 0 failed / 0 skipped**, default reuse, one allocation/drop and two reset/seed/verify/cleanup/body observations. |
| Security selected identity + protected-target methods, mode unset | **2 passed / 0 failed / 0 skipped**, same default-reuse counts; actual binding/user/assignment cleanup exercised. |
| Inventory fresh-only lock + schema methods, mode=reuse and application initialize-failure fault set | **2 passed / 0 failed / 0 skipped**; no application fixture observation/evaluation/allocation. Each uses its original owned fresh helper. |
| Security fresh-only contention + audit-trigger + repeated-migration methods, same settings | **3 passed / 0 failed / 0 skipped**; no application fixture observation/evaluation/allocation. |

Foundation/Catalog shared-input runs preceded the final contamination-probe-only review fix; their own sources, parser/profile/core behavior and runtime dependencies were unchanged by that fix. The final six-owner builds, 36-case helper run and matched owner selections include it. The earlier 35-case helper run is superseded by the final 36-case run. No unrelated full-project SQL suite was substituted for the actual T04 selections; Migrations and remaining unselected cases await T05.

Each new SQL Fact loops through two controlled orders. Every order fills at least one row in each of the 15 mutable tables, changes User/Role revisions to nonzero values, advances the receipt sequence, and requires successful seed verification before the exact deliberate scenario assertion. Successful drain allows the next reset; the Fact then calls every original eligible body, checks a new reset observation per call and verifies a final empty baseline. Inventory has **18 scopes inside one result**, Security **20 scopes inside one result**. Together with the separately discovered original methods, the matched selections have 25/28 scopes; nested calls never become extra TRX results.

| Fault, separate explicit-reuse selections excluding contamination Facts | Inventory actual results | Security actual results | Bodies / boundary |
|---|---|---|---|
| initialize-failure | 7 expected FAILED / 0 passed / 0 skipped | 8 expected FAILED / 0 passed / 0 skipped | First participating member exposes intended migration failure; remaining 6/7 admissions reject poison; zero bodies. |
| reset-failure | 7 expected FAILED / 0 passed / 0 skipped | 8 expected FAILED / 0 passed / 0 skipped | Intended post-disable reset fault restored through profile finally; zero bodies, remaining admissions poisoned. |
| seed-failure | 7 expected FAILED / 0 passed / 0 skipped | 8 expected FAILED / 0 passed / 0 skipped | Seed fills/verifies all 15 tables before throwing; no uncertain partial state reaches a body; later admissions poisoned. |
| schema-failure | 7 expected FAILED / 0 passed / 0 skipped | 8 expected FAILED / 0 passed / 0 skipped | Seed succeeds after adding a nullable User column; cached-boundary verify fails; zero bodies. |
| trigger-failure | 7 expected FAILED / 0 passed / 0 skipped | 8 expected FAILED / 0 passed / 0 skipped | Seed succeeds after disabling the append-only trigger; verify rejects it; zero bodies. |
| cleanup-failure | 7 expected FAILED / 0 passed / 0 skipped | 8 expected FAILED / 0 passed / 0 skipped | Exactly one body, actual resource cleanup then tracked intentional Dispose failure; all later members poisoned. |

Every expected fault run exits nonzero and contains every selected original method as an actual failed result, with zero skips. These are failure-propagation evidence, never passing SQL acceptance. Audit verified the intended first root error/failed phase, remaining poison errors, and absence of later bodies; a setup SQL syntax error cannot masquerade as a schema/trigger proof. Each fault selection has exactly one owned allocation and one successful guarded drop; no silent recreation or fresh fallback. Lazy initialization differs from Catalog fixture initialization only to preserve mixed-class fresh exceptions; failed initialization remains visible and terminal for participating cases.

Native per-fixture/per-scope observations (TRX member output plus final fixture diagnostics, counted once):

| Owner/mode | Case results | Scopes/reset/seed/verify/cleanup | Allocation/migration/initialize-reset/drop | TRX run elapsed s |
|---|---|---|---|---|
| Inventory/fresh | 8 PASS | 25 each | 25 each | 150.1 |
| Inventory/reuse | 8 PASS | 25 each | 1 each | 24.4 |
| Security/fresh | 9 PASS | 28 each | 28 each | 163.6 |
| Security/reuse | 9 PASS | 28 each | 1 each | 34.0 |

Durations are local observations on these selected scopes. Each phase observation also records its elapsed milliseconds; initialization-reset includes first profile preparation/reset and overlaps other phase accounting. Do not sum phases as wall time or call this the complete six-owner T05 speedup comparison. Same build/source/seeds/assertions are used within each matched owner pair.

Direct commands actually executed sequentially (existing approved non-production master connection, both fault settings unset for normal/default runs, caller settings restored in finally):

```powershell
# First restore Inventory/Security to add their pinned test-only dependency; then locked checks.
foreach ($owner in @('Foundation.Tests','Catalog.Tests','Inventory.Tests',
  'Security.Tests','Migrations.Tests','IntegrationTests')) {
  $name = 'MyDmsVn.Server.Infrastructure.' + $owner
  $project = 'tests/' + $name + '/' + $name + '.csproj'
  dotnet restore $project --locked-mode
  if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
  dotnet build $project -c Release -f net48 --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
# Shared setting/profile consumers; exact project paths use tests/<name>/<name>.csproj.
dotnet test $foundation -c Release -f net48 --no-restore --no-build `
  --logger 'trx;LogFileName=foundation.trx' --results-directory TestResults/resp-t04-foundation
dotnet test $integration -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~ReusableSqlFixtureTests|FullyQualifiedName~SqlTestDatabase' `
  --logger 'trx;LogFileName=helper.trx' --results-directory TestResults/resp-t04-final-helper
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $null
dotnet test $catalog -c Release -f net48 --no-restore --no-build `
  --logger 'trx;LogFileName=catalog.trx' --results-directory TestResults/resp-t04-catalog-default
# For each owner separately, eligibleFilter is the OR of exact FQNs of its seven/eight
# T04 adopted methods in the case map. Add the contamination class for withProbeFilter.
# Mode order was fresh then reuse, using the same source/build and selected methods.
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $mode
dotnet test $project -c Release -f net48 --no-restore --no-build --filter $withProbeFilter `
  --logger 'trx;LogFileName=cases.trx' --results-directory ('TestResults/resp-t04-' + $owner + '-' + $mode)
# Default selections: two exact original query methods (Inventory), or identity/protected-target (Security).
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $null
dotnet test $project -c Release -f net48 --no-restore --no-build --filter $defaultFilter `
  --logger 'trx;LogFileName=default.trx' --results-directory ('TestResults/resp-t04-' + $owner + '-default')
# Fresh-only exceptions, with fault enabled to prove no reusable fixture evaluation.
$env:MYDMSVN_SQL_TEST_FIXTURE_MODE = 'reuse'
$env:MYDMSVN_SQL_TEST_APPLICATION_PROBE = 'initialize-failure'
dotnet test $project -c Release -f net48 --no-restore --no-build --filter $exceptionsFilter `
  --logger 'trx;LogFileName=exceptions.trx' --results-directory ('TestResults/resp-t04-' + $owner + '-exceptions')
# For each of six fault values, select only the seven/eight original participating methods.
$env:MYDMSVN_SQL_TEST_APPLICATION_PROBE = $fault
dotnet test $project -c Release -f net48 --no-restore --no-build --filter $eligibleFilter `
  --logger 'trx;LogFileName=fault.trx' --results-directory ('TestResults/resp-t04-' + $owner + '-' + $fault)
# Require nonzero + all actual named failures, then restore the caller's settings.
git diff --check
```

Exact focused defaults were `Get_returns_complete_aggregate_version_and_inactive_historical_labels` and `Balance_and_stock_card_queries_filter_page_sort_and_keep_inactive_history` (Inventory), `User_and_permission_stores_read_current_sql_state_and_replace_hash_atomically` and `Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected` (Security). Fresh-only exception selectors were `Get_holds_one_revision_while_header_and_lines_are_read`, `Inventory_migration_enforces_ledger_and_balance_storage_invariants`; and `Permission_snapshot_never_exposes_role_grant_without_concurrent_direct_deny`, `Audit_failure_rolls_back_the_security_mutation_and_revision`, `Catalog_only_release_adds_a_registered_key_without_rewriting_assignments`. Class/owner/exact FQN formation is defined by the [case map](RESPAWN_CASE_MAP.md#resp-t04-adoption-and-frozen-candidate); no new manifest/runner or invocation registry is introduced. The local ignored scripts only sequence these direct commands and inspect ordinary TRX.

TDD evidence: parser default RED **3 failed / 4 passed**, then local GREEN **13/13**. Each owner adoption challenge was RED **1 failed** against original private fresh helpers (missing collection reset), then GREEN **1/1** after adoption, including both controlled orders. Fresh independent review found one P2 probe defect: broad expected-exception capture could accept an incomplete seed assertion. A new local regression reproduced RED **1 failed**; the common helper now requires the exact deliberate failure message, so a seed assertion escapes as a failed probe. Final helper **36/36** and all matched owner runs include that fix. No review findings remain deferred; no second review was substituted for the regression.

The reviewer read source and declined runtime/timing/aborted-host assessment; the execution audit above independently reconciles actual names/rows/outcomes, counters and failure messages. No aborted-run recovery is claimed. Every participating allocation in these runs reports successful guarded teardown, including fault runs; fresh exceptions retain their deterministic owned helper cleanup, and Foundation additionally asserts owned allocation absence. No testhost was killed, no cleanup error was waived and no unresolved owned cleanup was reported. Raw TRX, source snapshots and detailed logs remain ignored local evidence.

Static checks cover original case/row retention, exact preserved adopted and mixed-exception bodies, unchanged unconverted source/assembly guards, all 20 candidates mapped, locked test-only graph/net48, UTF-8/BOM/CRLF, local links, correct task checkbox boundaries and git diff --check. The default-reuse candidate is frozen for T05; further relevant changes require affected re-verification.

Remaining limits: complete six-owner SQL runs in both explicit modes, all required 257 case outcomes and matched performance acceptance are **NOT RUN / pending T05**. No GUI/P5/P6/P7, product-runtime or L2DB acceptance follows. Next active task: **RESP-T05 — Accept correctness and compare the same cases**.


## RESP-T05 acceptance — matched complete owners

**Executed:** 2026-10-18, Windows, SDK 8.0.425, net48 Release; explicit non-production master connection. Accepted **working-tree candidate**, branch `codex/resp-t00-baseline`, base/HEAD `6383289a2e43e2021abd65048a1e2f3f3838324e` plus uncommitted RESP implementation. Code/config SHA256 **`0838ff87bcc0c1d8d815f5e09d94f3c9c951fd48b00105d9fd78f170f580f4fb`**, using the sorted path/NUL/byte digest defined above. This is not an accepted commit SHA; retain the changes and bind their eventual commit at L2DB-T00. Documentation edits do not change this digest.

Locked restore and net48 Release build passed for all six owners, zero warnings/errors. The same builds/source, project order, seeds and assertions were used for one complete explicit **fresh** pass followed by one complete explicit **reuse** pass. No filter, no rebuild between modes, both fault settings unset, caller environment restored. No rerun or waived failure substitutes for this pair.

| Owner, sequential order | Fresh passed / failed / skipped | Reuse passed / failed / skipped | Fresh wall seconds | Reuse wall seconds |
|---|---:|---:|---:|---:|
| Foundation.Tests | 41 / 0 / 0 | 41 / 0 / 0 | 222.54 | 228.71 |
| Catalog.Tests | 10 / 0 / 0 | 10 / 0 / 0 | 139.34 | 22.63 |
| Inventory.Tests | 36 / 0 / 0 | 36 / 0 / 0 | 253.52 | 122.57 |
| Security.Tests | 53 / 0 / 0 | 53 / 0 / 0 | 262.36 | 116.53 |
| Migrations.Tests | 65 / 0 / 0 | 65 / 0 / 0 | 178.85 | 168.40 |
| IntegrationTests | 52 / 0 / 0 | 52 / 0 / 0 | 30.23 | 30.28 |
| **Total** | **257 / 0 / 0** | **257 / 0 / 0** | **1086.85** | **689.12** |

Source/TRX reconciliation checks all **230 methods / 257 actual invocations**, including every required Fact and Theory row, unique display names and identical exact case-name/row sets between modes. All original **193 methods / 211 cases** remain. Current split: **166 SQL / 91 non-SQL**. No missing/mandatory skipped rows. The 27 participating collection invocations contain 75 scenario scopes (including nested contamination scenarios); these are not 75 xUnit results. Other cases retain their dedicated helper/probe lifecycles; a mode setting does not convert schema, principal, bootstrap, trigger or contention exceptions.

### Allocation and phase observations

Telemetry below covers participating Catalog/Inventory/Security collection fixtures only. Dedicated exceptions and Foundation helper/probe allocations do not emit this adapter telemetry; no global allocation total or timing is fabricated for them. Counts represent successful observations from per-case TRX output plus xUnit fixture diagnostic lines, counted once. Shared final drop appears in diagnostics.

| Participating owner | Scenario scopes, each mode | Fresh allocate / migrate / initialize-reset / drop | Reuse allocate / migrate / initialize-reset / drop | Reset / seed / verify / cleanup, each mode |
|---|---:|---:|---:|---:|
| Catalog | 22 | 22 each | 1 each | 22 each |
| Inventory | 25 | 25 each | 1 each | 25 each |
| Security | 28 | 28 each | 1 each | 28 each |
| **Total** | **75** | **75 each** | **3 each** | **75 each** |

| Successful participating phase | Fresh count | Reuse count | Fresh cumulative phase seconds | Reuse cumulative phase seconds |
|---|---:|---:|---:|---:|
| allocate | 75 | 3 | 24.204 | 1.626 |
| migrate | 75 | 3 | 183.648 | 8.098 |
| initialize-reset | 75 | 3 | 182.706 | 8.565 |
| reset | 75 | 75 | 20.123 | 15.269 |
| seed | 75 | 75 | 6.905 | 5.538 |
| verify | 75 | 75 | 6.724 | 5.643 |
| cleanup | 75 | 75 | 0.012 | 0.008 |
| drop | 75 | 3 | 4.601 | 0.198 |

`initialize-reset` includes first checkpoint preparation/reset. Nested operation timings overlap: phase totals must not be added to infer wall time. `allocate` measures owned database allocation; `drop` includes guarded teardown. Each of 75 admissions completed reset/seed/verify/body/resource cleanup, and each actual participating allocation reports a successful guarded drop; no failed observation or silent fallback/recreation.

The complete reuse observation was **397.73s shorter (36.6% lower wall time; 1.58x fresh/reuse ratio)** for the same 257 cases. Wall time is the sum of Stopwatches around each `dotnet test` process, including host startup/teardown, excluding restore/build and read-only snapshots. This is one ordered local pair, not repeated sampling: cache warmth, SQL background activity and host noise can affect the second pass. It establishes observed suite improvement only, not statistical, product-performance or release acceptance. Per-owner/per-case differences are examined below; no percentage threshold is used.

### Timing differences reviewed

Foundation was **6.16s slower** (228.71s versus 222.54s). Its largest case increases were cancellation/drain **3.72s**, ownership refusal **3.50s** and worker-drain **3.36s**, offset by decreases elsewhere. Source inspection confirms direct dedicated profile allocation or an explicitly chosen probe mode; these paths do not read the collection-mode environment. IntegrationTests was **0.05s slower** overall; its largest increase, live desktop identity/session, was 0.88s on its unchanged provisioned fresh helper. The local parser/scope tests also passed, and no timeout, assertion, drain or cleanup failure appeared.

For owners whose total improved, the largest individual increases were the dedicated Inventory eight-UoW concurrent-create case **5.38s**, Security repeated migration/catalog-release **2.86s**, and Migrations unknown-history case **1.46s**. Their source paths still allocate independent databases and do not use the participating collection mode; the mixed-class Security body is unchanged. Catalog's participating cases all became shorter in this pair. These observations were investigated through case-level TRX durations and actual allocation/mode paths, not discarded behind a threshold. No mode-driven behavioral/performance defect was identified; the exact cause of dedicated-case timing variation cannot be determined from one pair. No owner-specific improvement is claimed for Foundation/IntegrationTests, and no extra full pair was run to select a favorable result.

### Correctness, defaults and cleanup

Both full passes include actual reset/profile/lifecycle safety probes, cancellation and worker-drain boundaries, ownership/schema drift and poison handling, business rollback/concurrency, and forward/reverse contamination scenarios. They preserve deterministic seeds, original permissions/assertions and relative rowversion reads. Failure challenges from T01–T04 remain evidence on this unchanged candidate; those deliberate failed fault runs are never counted as passing acceptance cases.

Read-only SQL snapshots before the pair, after fresh and after reuse each found **0 matching test databases and 0 sessions attached to them**. Prefix matching is only a diagnostic census, never proof of ownership or authorization to delete. Foundation independently asserts owned allocation absence; all full runs finished normally, with no unresolved cleanup failure, killed testhost or leftover cleanup. No bulk/orphan recovery was attempted.

A fourth read-only census after the focused default selections also found 0 matching databases/0 attached sessions.

Fresh mode is proven by the unfiltered first pass and 75 independent participating allocations. Focused checks after the pair, with mode unset and faults unset: IntegrationTests local parser/scope **14/14** (includes all seven default/explicit input rows and two invalid rows); Catalog adapter **3/3**, Inventory original query **2/2**, Security identity/admin **2/2**. Each SQL default selection emitted reuse, one allocate/migrate/drop and one reset/seed/verify/cleanup per selected scenario. The unchanged lazy-exception T04 challenge additionally proves no reusable fixture evaluation when only fresh exceptions are selected; all such original exceptions pass in both full modes here.

### Reproduction and handoff

Run these direct commands sequentially, using the designated connection and saving/restoring caller mode/fault settings. Use separate results paths for each mode/owner; never run SQL owners concurrently.

```powershell
$owners = 'Foundation.Tests','Catalog.Tests','Inventory.Tests','Security.Tests','Migrations.Tests','IntegrationTests'
foreach ($owner in $owners) {
  $project = "tests/MyDmsVn.Server.Infrastructure.$owner/MyDmsVn.Server.Infrastructure.$owner.csproj"
  dotnet restore $project --locked-mode
  if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
  dotnet build $project -c Release -f net48 --no-restore
  if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
# Both MYDMSVN_SQL_TEST_ADAPTER_PROBE and MYDMSVN_SQL_TEST_APPLICATION_PROBE unset.
foreach ($mode in 'fresh','reuse') {
  $env:MYDMSVN_SQL_TEST_FIXTURE_MODE = $mode
  foreach ($owner in $owners) {
    $project = "tests/MyDmsVn.Server.Infrastructure.$owner/MyDmsVn.Server.Infrastructure.$owner.csproj"
    dotnet test $project -c Release -f net48 --no-restore --no-build `
      --logger 'trx;LogFileName=cases.trx' --results-directory "TestResults/resp-t05-$mode-$owner"
    if ($LASTEXITCODE -ne 0) { throw 'Unfiltered acceptance failed.' }
  }
}
# Finally restore the caller environment. Actual execution did so even on failure.
```

Default selections use the exact T04 defaults named above, plus `FullyQualifiedName~ReusableSqlFixtureTests` and `FullyQualifiedName~SqlFixtureCollectionAdapterTests`. Ordinary TRX and detailed local observation/snapshot logs remain ignored; this checked-in account is sanitized, without connection details or raw identifiers. No new runner, manifest, approval fingerprint, global lease or recovery service was added.

The fresh-context whole-branch read-only review found no Critical/Important/Minor actionable findings. Root independently audited runtime evidence. Rulings on the review's declined scope: (1) runtime/timing requires the executed audit above, not source review; (2) final current guidance includes AGENTS.md; (3) killed/crashed or permanently noncooperative hosts still require manual leftover review, without recovery claims; (4) future schema/provider/SQL-version changes require new boundary verification, not an assumed general reset capability; (5) L2DB/GUI/P5/P6/P7/product performance/release remain outside RESP acceptance. No deferred minors or implementation repairs arose from this final review.

**RESP-T00–T05 accepted for this exact local candidate. Next active task: L2DB-T00**, which must bind the accepted bytes to the actual commit/worktree without discarding these uncommitted changes. Freeze the APP table/seed/reset/schema boundary, ownership/drop guards, worker drain/poison semantics, fresh exceptions, serial guards, net48 package graph, all case identities/assertions and direct commands during DAL migration. Production remains RepoDb/Dapper/DbUp; L2DB is NOT IMPLEMENTED. No GUI/P5/P6/P7 or product release acceptance. Worktree and local evidence retained; no commit/merge/push.


### Publication candidate

Preparation for the owner's requested PR to `v1a` removes exactly one trailing empty line from `CatalogSqlCollectionFixture.cs`; no C# content or executable configuration changes. The runtime-tested digest above remains the historical T05 observation. The published code/config digest is **`fdcce9a0015eb449e5837b49dc4e20b7995f5210b2d36f747a104ec696760055`**, using the same path/NUL/byte algorithm. An exact byte comparison against the accepted file proves that this final CRLF removal is the only code/config difference. Staged whitespace/static checks cover all new files; the completed SQL pair remains applicable to this formatting-only change. The PR records the commit binding these bytes; preserve the same fixture boundaries during L2DB.


## PR 36 review follow-up

**Revision:** repairs on top of PR commit `6bb77f2e55567af603a228f2dace234b6d771c4f`, 2026-10-18, Windows / SDK 8.0.425 / net48 Release. Current code/config digest **`75f282bc99c0c5d8f5bc04bcd2fe4c75f9f0c650d2ecd2a53203e1c46a79165c`**, using the same sorted path/NUL/byte algorithm against RESP base 6383289. The subsequent PR commit binds these exact bytes. This section supersedes the earlier fixture acceptance for the repaired lifecycle; earlier 257-case observations remain historical. Product RepoDb/Dapper/DbUp, packages and dedicated exception boundaries are unchanged.

Both owner review findings are fixed:

- [Final-case boundary verification](https://github.com/chung6a8m/MyDmsVn/pull/36#discussion_r4306810826): after registered workers and resources drain, `verify-after` checks schema/journal/permissions/trigger/ownership while admission is held. Drift fails and poisons that same case, including the last/focused case. Ordinary body failures still permit a later reset only after successful drain and unchanged boundary. Body plus verification errors and verification plus guarded-drop errors are preserved. Failed admission skips the post-body check, preserving its original error.
- [Finite cleanup and quarantine](https://github.com/chung6a8m/MyDmsVn/pull/36#discussion_r4306810832): one **two-minute cooperative budget** covers stop callbacks, actual worker Tasks and LIFO resource cleanup. Callback invocation runs off-thread so synchronous blocking is bounded too. Timeout fails the case, observes late faults and stops further cleanup. The fixture poisons and quarantines before waking waiters; they and teardown fail promptly without verification/reset/drop. The owned allocation remains identified for manual review even if work later completes. This is not cancellation, forced termination, an orphan-recovery service or automatic cleanup.

The controlled SQL quarantine probe holds a real transaction in both modes, proves pending admission and teardown finish with quarantine errors while worker/database remain active, with no additional reset/drop, then explicitly releases/awaits its own worker and uses the original guarded allocator to clean up. This test-only known drain is not a general recovery path. Normal complete runs finish with no unresolved allocations.

### Regression evidence

Boundary RED 2 actual SQL failures → GREEN 2: both modes and four final-case mutations (table, append-only trigger, journal, ownership), including mutation by resource cleanup; body+boundary aggregation also checked. Cleanup RED 5 local failures (no-op worker stop, throwing stop, blocking stop, never-completing async cleanup and blocking synchronous cleanup) → GREEN 19 local checks. Live-SQL quarantine RED 1 → GREEN in the combined 3-case focused run.

The first expanded full attempt stopped at Foundation 43 passed / 1 failed / 0 skipped: an existing worker probe asserted the stop callback ran synchronously. Off-thread invocation made that assertion race. The probe now awaits its explicit stop notification, retaining resource/disposal/nonoverlap assertions; focused GREEN 1. That failed attempt is not acceptance evidence. The final pair below uses new result paths and the frozen final digest; no failed row or cleanup requirement was waived.

Current source: **234 methods / 265 cases (169 SQL / 96 non-SQL)**. All previous 257 case identities/rows and original 193 methods / 211 cases remain. Added regression methods: Foundation 3 SQL Facts, IntegrationTests 1 Theory with 5 rows. No business case/assertion or dedicated exception was removed.

### Final complete pair

Locked restore and Release net48 build passed for all six owners, zero warnings/errors. One unfiltered sequential fresh pass then one reuse pass used identical final source/builds/order and explicit non-production master configuration, with fault settings unset and caller environment restored.

| Owner | Fresh passed / failed / skipped | Reuse passed / failed / skipped | Fresh wall seconds | Reuse wall seconds |
|---|---:|---:|---:|---:|
| Foundation.Tests | 44/0/0 | 44/0/0 | 310.56 | 298.68 |
| Catalog.Tests | 10/0/0 | 10/0/0 | 155.28 | 26.28 |
| Inventory.Tests | 36/0/0 | 36/0/0 | 282.15 | 136.70 |
| Security.Tests | 53/0/0 | 53/0/0 | 282.87 | 118.95 |
| Migrations.Tests | 65/0/0 | 65/0/0 | 181.31 | 183.47 |
| IntegrationTests | 57/0/0 | 57/0/0 | 29.44 | 30.58 |
| **Total** | **265/0/0** | **265/0/0** | **1241.61** | **794.65** |

All exact full namespace/method/Theory-row sets match current source and each other, with unique display names and no mandatory skips. Both modes retain 75 participating scenario scopes (Catalog 22, Inventory 25, Security 28); fresh 75 vs reuse 3 allocate/migrate/initialize-reset/drop, each 75 reset/seed/verify/**verify-after**/cleanup and body entries, all successful. Other allocations belong to dedicated probes/exceptions and are not included in this adapter telemetry.

| Participating phase | Fresh count | Reuse count | Fresh cumulative seconds | Reuse cumulative seconds |
|---|---:|---:|---:|---:|
| allocate | 75 | 3 | 27.759 | 2.023 |
| migrate | 75 | 3 | 182.100 | 9.201 |
| initialize-reset | 75 | 3 | 187.525 | 8.508 |
| reset | 75 | 75 | 21.469 | 18.576 |
| seed | 75 | 75 | 11.203 | 6.187 |
| verify | 75 | 75 | 7.977 | 6.904 |
| verify-after | 75 | 75 | 8.270 | 6.646 |
| cleanup | 75 | 75 | 0.056 | 0.022 |
| drop | 75 | 3 | 5.442 | 0.185 |

Wall seconds sum the process Stopwatches, excluding restore/build/census. Reuse observed 446.96s lower wall time (36.0% reduction) in this single ordered local pair. Cache/SQL-host noise limits inference; no statistical/product/release performance claim. Phase totals overlap and do not sum to wall time. Case-level timing review found Migrations **2.16s slower** overall (largest dedicated interrupted-baseline case +6.48s) and IntegrationTests **1.14s slower** (largest missing-marker guard case +0.90s). Their unchanged source paths use the dedicated allocator/migrator, not the collection mode or repaired scope. No failure, timeout or cleanup difference appeared; the cause of latency variation cannot be inferred from this single pair, and neither owner is claimed faster.

Inventory's largest individual increase was `Reconciliation_executes_in_a_serializable_transaction` (+1.34s), which is the first lazy reuse member and includes its collection allocation/migration/checkpoint preparation. Both modes' per-case telemetry contains those initialization phases; the added final verification itself measured 84.0ms fresh / 83.8ms reuse. Its query/Serializable assertions pass unchanged. Other dedicated-case variation was inspected through the exact TRX timings and allocation/mode paths; no percentage threshold or selected favorable rerun was used.

Focused unset-mode checks on this final build: parser/scope 19/19, Catalog 3/3, Inventory 2/2, Security 2/2, all passed, zero skipped. Each SQL default selection uses reuse 1 allocation/drop and one reset/seed/verify/verify-after/cleanup per scenario. Read-only censuses before/after both full modes and after defaults find 0 matching test databases / 0 attached sessions. No host killed, unresolved cleanup or prefix-based deletion.

Direct commands are the T05 reproduction commands above with separate `resp-review-final-<mode>-<owner>` TRX folders. Focused selectors are `FullyQualifiedName~Last_scope_rejects`, `FullyQualifiedName~Scenario_failure_preserves_post_body`, `FullyQualifiedName~Cleanup_timeout_quarantines`, `FullyQualifiedName~Cleanup_budget_bounds` and the existing worker probe; defaults use the earlier exact selections. Raw results and SQL allocation identifiers stay ignored/local.

**Accepted repaired RESP baseline:** digest above plus the PR follow-up commit. Preserve pre/post-boundary checks, two-minute cleanup/quarantine behavior, local ownership guards, all case identities/assertions, fresh exceptions and serial net48 commands during L2DB. No L2DB/GUI/P5/P6/P7 implementation or release acceptance follows.

