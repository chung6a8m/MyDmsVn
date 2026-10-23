# Test projects and commands — v1a-pro-ui-next

**Updated:** 2026-10-23. [TEST_POLICY](../docs/TEST_POLICY.md) alone owns execution scope and [TEST_STRATEGY](../docs/TEST_STRATEGY.md) owns coverage. The [UI-NEXT roadmap](../docs/ui-next/20261023-active-roadmap.md) is the sole active queue from merged PR #43 baseline `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`. All implementation tasks are NOT STARTED and new runtime validation is NOT RUN. No external SQL test framework is restored.

## Active UI-NEXT commands and evidence

Use the current ten net48 projects and the [scoped validation and handoff](../docs/ui-next/20261023-validation-and-handoff.md). Safe field metadata/mapping belongs to Server.Application.Tests; workspace state/lifetime belongs to Desktop.Session.Tests where controls are unnecessary; native control composition, focus and disposal belong to Desktop.Tests. Shared controls also require their upstream owner's affected tests/targets. Select real source cases, verify nonempty discovery and inspect actual TRX; proposed class names in a plan are not runnable tests and a successful zero-test filter is not PASS.

The [archive boundary](../docs/archive/20261023-pre-ui-next/README.md) excludes prior unfinished acceptance and full SAL integration repeats. [T08 evidence](../docs/evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS. R3/R4 source fixes are approved; native125, attended observations and dated Owner acceptance remain OPEN. Original failed trials and old accepted BCF/BCI/GUI-P5/L2DB/RESP scopes remain historical, not new expected counts or active gates.

For this documentation-only activation, run diff/link/status/encoding checks only. No full six-owner fresh/reuse repeat, archived SAL-T08 completion or production-release work is inherited. If implementation actually changes SQL/fixture/schema contracts, choose its affected owners under TEST_POLICY and preserve the resource rules below. [Production release prerequisites](../docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.

## Current implementation

Run test projects directly with `dotnet test` or Test Explorer. The standalone SQL framework, its projects/tests, gate and invocation manifests, and command wrappers have been removed. The local [SqlTestDatabase](MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs) helper creates a fresh uniquely owned database per call. RESP-T01 pins Respawn 4.0.0 and verifies the test-only application reset profile on net48. RESP-T02 implements exclusive collection lifecycle and the fixture-mode setting; RESP-T03 adopts all five Catalog business methods in one collection with its adapter/contamination probes. RESP-T04 also adopts seven Inventory and eight Identity/Security data-only cases; reviewed lifecycle/schema/principal/trigger/contention exceptions remain fresh.

All ten active test projects target **net48**:

| Responsibility | Project under `tests/` |
|---|---|
| Application/contracts | `MyDmsVn.Server.Application.Tests` |
| Architecture | `MyDmsVn.Architecture.Tests` |
| Pure Desktop/session | `MyDmsVn.Desktop.Session.Tests` |
| Desktop/WinForms | `MyDmsVn.Desktop.Tests` |
| SQL foundation | `MyDmsVn.Server.Infrastructure.Foundation.Tests` |
| SQL catalogs | `MyDmsVn.Server.Infrastructure.Catalog.Tests` |
| SQL inventory | `MyDmsVn.Server.Infrastructure.Inventory.Tests` |
| SQL security | `MyDmsVn.Server.Infrastructure.Security.Tests` |
| SQL migrations/schema | `MyDmsVn.Server.Infrastructure.Migrations.Tests` |
| Shared SQL helper and remaining Local scenarios | `MyDmsVn.Server.Infrastructure.IntegrationTests` |

Each directory contains the matching `.csproj`. The last six are separate SQL owners; the original IntegrationTests project no longer means the entire SQL suite. Some classes in those projects are pure code. Inspect source before selecting them; an assembly name alone does not classify a case.

Use Windows with the SDK selected by `global.json` and .NET Framework 4.8 targeting/runtime support. The SDK request is `8.0.400` with `rollForward: latestFeature`; it is a build tool, not a modern product/test target. AppCore and historical modern product targets are PARKED / NOT RUN. Run commands from the repository root. Stop on failed restore/build/test; restore/build the current revision before using `--no-restore` or `--no-build`.

## Focused non-SQL example

For a Catalog Application change, the current source contains `CatalogApplicationTests`:

```powershell
$project = 'tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'FullyQualifiedName~CatalogApplicationTests'
if ($LASTEXITCODE -ne 0) { throw 'Application tests failed.' }
```

Select a real source/discovered class or method, then review the executed cases; an empty selection is not a successful check. Pure ViewModel/session tests use their actual project. Controls/focus tests use the Desktop section below.

## SQL configuration

Set only `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` for an operator-designated **non-production** SQL Server, with explicit effective database `master`. The account needs database create/drop rights and any privileges required by the selected principal/maintenance/DMV scenarios. Do not use a production connection or the application's runtime/migration configuration.

If the reviewed value is already stored at Windows User scope, load it without printing:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING = [Environment]::GetEnvironmentVariable('MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING', 'User')
if ([string]::IsNullOrWhiteSpace($env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING)) {
  throw 'Configure the designated SQL test instance before running required SQL coverage.'
}
```

The helper rejects blank instance/catalog input, non-master catalogs, `AttachDBFilename` and `UserInstance` before opening SQL. No approval JSON or external preflight command is used. `SqlServerFact` skips when the connection is absent; those cases remain NOT RUN for mandatory SQL evidence. Invalid configured input or failed live ownership checks fail the test. The helper never silently chooses an instance.

## Focused SQL project/category

The retained `SqlServerFact` trait uses `Category`: `FastSQL`, `MigrationSchema`, `SecurityProvisioning` and `SqlConcurrency`. Standard VSTest `--filter` syntax can select one category or a source-verified class/method. A case can carry several categories, so category totals are not necessarily disjoint.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
$results = 'TestResults/cat-' + [guid]::NewGuid().ToString('N').Substring(0,8)
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=catalog.trx' `
  --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'Catalog SQL tests failed.' }
```

Omit `--filter` for a complete project pass. Substitute the exact owner and category for another affected scope. A focused pass proves only the selected cases. `dotnet test <project> -c Release -f net48 --list-tests --filter <filter>` can inspect discovery after restore; it does not prove SQL execution or cleanup.

## Complete SQL milestone

Only use all six owners when required by TEST_POLICY or a plan milestone. Execute them sequentially, with one unfiltered pass per project and separate results:

```powershell
$owners = @('Foundation.Tests', 'Catalog.Tests', 'Inventory.Tests',
  'Security.Tests', 'Migrations.Tests', 'IntegrationTests')
$results = 'TestResults/sql-' + [guid]::NewGuid().ToString('N').Substring(0,8)
foreach ($owner in $owners) {
  $name = 'MyDmsVn.Server.Infrastructure.' + $owner
  $project = 'tests/' + $name + '/' + $name + '.csproj'
  dotnet restore $project --locked-mode
  if ($LASTEXITCODE -ne 0) { throw ('Locked restore failed: ' + $name) }
  dotnet test $project -c Release -f net48 --no-restore `
    --logger ('trx;LogFileName=' + $owner + '.trx') `
    --results-directory $results
  if ($LASTEXITCODE -ne 0) { throw ('SQL project failed: ' + $name) }
}
```

Review each TRX's required cases, Theory rows, pass/fail/skip counts and cleanup failures. A zero exit with SQL skipped is not SQL acceptance. Use the current source and plan case map to explain additions/replacements/omissions; there is no separate gate registry, fingerprint approval or automated case-reconciliation service.

This loop does not automatically run Application, Architecture, Desktop.Session or Desktop. Add those project commands only when their affected scope or milestone requires them. Use a new short local results directory for each run; raw artifacts are not committed evidence.

## SQL helper lifecycle and interruption

The shared source stays inside the existing IntegrationTests project. Catalog references that assembly; Foundation, Inventory, Security and Migrations compile linked helper copies. Keep that project ownership explicit when editing helpers.

The helper requires `master`, generates a `MyDmsVn_Test_` GUID name, and records the live instance, database ID/creation time and random `MyDmsVn.TestOwnership` marker. It verifies those values before destructive cleanup. Helper connections disable pooling; disposing them does not clear unrelated connection pools. A missing/changed marker or changed allocation identity refuses deletion. It never adopts an arbitrary existing database.

The helper disables connection retry, caps connection timeout at 30 seconds, uses 30-second SQL command timeouts and applies a two-minute cancellation budget to each create/dispose lifecycle. These are local operation bounds, not an external test-process timeout. A cleanup failure remains visible and can be retried where ownership still matches. Failure before ownership is confirmed leaves the allocation for manual review rather than guessing from its name. After a killed process, an operator reviews any leftover database on the designated test instance: there is no automatic orphan cleanup or durable allocation journal. Never bulk-drop by name prefix.

Keep existing SQL assembly serial guards, and run SQL projects sequentially. IntegrationTests, Foundation, Inventory, Security and Migrations have assembly guards; Catalog retains no assembly guard; all fixture consumers share its explicit serialized Catalog SQL collection. There is no cross-process lease. Concurrency scenarios retain independent SQL sessions and verified lock handshakes; their workers, transactions and readers must drain before database disposal or a future reset.

## Desktop / WinForms

Run Desktop cases directly from an interactive Windows session. Existing `StaTest`, `WinFormsTestGuard`, assembly serialization, bounded waits, dialog suppression and exception/DataError capture remain in the project. Execute one GUI command at a time; no external GUI launcher or global lease remains.

```powershell
$project = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Desktop restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --logger 'trx;LogFileName=desktop.trx' `
  --results-directory ('TestResults/gui-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed.' }
```

Add a real `FullyQualifiedName` filter for affected focused work; the example is unfiltered and is required only for an applicable broader scope. Fatal/noncooperative probes and packaging tests now use the Desktop-owned `GuiOwnedProcess` helper: suspended launch, job assignment before resume, bounded execution/cancellation, concurrent bounded output capture and verified tree/pipe drain. Compiler startup for temporary probes uses the same helper. `StaTest` still cannot kill a noncooperative STA; uncertain cleanup poisons further GUI admission. Run ordinary project tests; this is not a suite watchdog or restored runner. See [T01 evidence](../docs/GUI_P5_VERIFICATION.md) for source/row reconciliation and failed/aborted development trials. Automated GUI results do not replace attended packaged workflows or real packaged-login E2E explicitly selected by the current UI-NEXT validation scope. The L2DB-T07 correctness gate intentionally left 139 Desktop GUI invocations NOT RUN; that is a historical, frozen-revision count, not a permanent case total or a current PASS. GUI-P5-T01 reliability and T02 deterministic control coverage are complete in scope; executable `3a0621c` has 282 Desktop and 108 Session passes on net48/x86, including 129 new control invocations. [T02 ledger](../docs/GUI_P5_COVERAGE_MATRIX.md#appendix-c--gui-p5-t02-deterministic-control-closure) maps named cases and limitations. That T02 checkpoint left T03–T06 OPEN; the [completed GUI-P5 plan](../docs/archive/20261023-pre-ui-next/plans/20261019-001-full-winforms-gui-p5-acceptance.md) now records the later approved scope and explicit T05 Down NOT TESTED exception. Those historical passes do not establish BCF behavior or current-candidate acceptance; BCI has its own completed candidate-specific evidence. For explicit x86 checks, set both `-p:PlatformTarget=x86` and `-- RunConfiguration.TargetPlatform=x86` on each project command. Rebuild the graph or use separate outputs when switching back to AnyCPU/x64 so stale x86 dependencies are not loaded by an x64 testhost.

The two `GuiOwnedProcessTests` foreground recovery/refusal Facts optionally retain their synthetic target and UIA probe source/executable bytes before those focus launches. Set `MYDMSVN_GUI_PROBE_ARCHIVE_DIRECTORY` to an absolute, dedicated local evidence directory to enable copies, SHA256 manifests and retained stdout hash markers; unset/blank performs no archive writes. This is test-only evidence retention, not an attended observation or a product setting. The 2026-10-23 T08 focused invocation resolved `.local/t08/focus-rendezvous-02/probe-beforelaunch` under its recorded worktree and supplied that absolute path; each later context records its own directory.

### Existing Sales package probes and archived integration

`PackagedSalesAcceptanceTests.cs` extends the existing `PackagedGuiAcceptanceTests` owner with 19 rows: package metadata 1, native page-size 2, duplicate/gift/manual-price and independent cost oracle 2, stale Save/Post 4, direct Read/Write/Post Deny 6, uncertain SQL Post 2 and packaged schema transition 2. The runtime rows authenticate an ordinary user through actual Login and execute Light/Dark independently; UIA value assignment is not native per-character evidence.

Set `MYDMSVN_GUI_P5_PACKAGE` to an existing validated current schema10 package and `MYDMSVN_GUI_P5_TEST_REVISION` to the package manifest's 40-character `sourceCommit` (the variable has a legacy name). Record the test-driver source revision and raw source/test-DLL hashes separately; set `MYDMSVN_GUI_SAL_SCHEMA9_PACKAGE` to the verified genuine historical schema9 package for metadata/old-runtime/upgrade cases. The latter must retain source `95591071ea214be88813deb5227d216f1b0bca71` and its recorded manifest/file hashes. Never modify the historical package. `MYDMSVN_GUI_P5_EXPECTED_SCALE` declares actual 100 or 125; it does not change display scale. SQL uses the explicitly designated non-production master connection and the existing per-case owned allocation/drain/drop topology. The schema row allocates separate partial-refusal and pristine9-upgrade fixtures; do not heuristically repair partial DDL and call it pristine9.

Use focused direct Desktop project/filter runs during development, with both x86 flags above and actual TRX. `MYDMSVN_GUI_P5_TRACE_LOOKUPS=1` optionally forwards bounded owned lookup-state diagnostics for the Sales audit/coordinated probes; never log credentials or SQL identities. A clean baseline development package can prove a driver/oracle change only on that recorded package. The [development checkpoint](../docs/evidence/sal/20261023-t08-packaged-development-checkpoint.md) records actual failed/focused attempts and provenance limits. The archived T08 contract required its separately reviewed frozen source/package, complete prescribed integration scopes, native contexts and explicit Owner disposition; its remaining native125/attended/Owner gaps stay OPEN. These existing probes may be selected for a concrete UI-NEXT regression, but do not reactivate that integration queue.
### Historical BCF focused work and final integration

BCF-T00–T01 first add deterministic native-key proof and minimally repair binding in `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`. The driver must verify the actual native EDIT HWND/focus, type individual characters and observe caret/range across controllable responses and completion of a canceled preceding search. Whole `.Text`, `WM_SETTEXT` and UIA SetValue do not establish that behavior. Retain the intended failing baseline proof separately from later passing runs; compile/focus/driver failures and empty discovery are not the intended regression failure. Inline Product is a reference/regression target.

`GoodsReceiptLookupTypingTests` retains the T00 baseline of 15 intended RED rows ([evidence](../docs/evidence/bcf/20261021-t00-native-typing.md)). T01 implements `GoodsReceiptLookupBindingTests`: 92 binding/boundary rows plus the original 15 typing rows pass within the 172-row focused affected scope ([exact selectors, revisions and evidence](../docs/evidence/bcf/20261021-t01-lookup-binding.md)). These are hosted Windows results, not the T02 native100/125 Light/Dark and attended gate. Add the combined lookup selector below to the Desktop command above with `--filter`, using the explicit x86 parameters described above:

```powershell
$bcfFilter = 'FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests'
```

T02's implemented `LookupGateEnvironment` uses `MYDMSVN_BCF_EXPECTED_SCALE=100|125` and `MYDMSVN_BCF_THEME=Light|Dark` together. The receipt fixture applies/restores the requested theme; an owned PerMonitorV2 observer measures each lookup case's actual monitor DPI and rejects a declared scale mismatch. The test does not change Windows Display Scale or infer it from registry/virtualized `DeviceDpi`. Run one context at a time and retain its own TRX. Ordinary runs without these two variables remain hosted regressions, not native gate evidence. See [T02 execution evidence](../docs/evidence/bcf/20261021-t02-lookup-gate.md) for actual context results and limitations.

The opt-in `Bcf_attended_lookup_package_session_keeps_human_input_separate_from_automation` case additionally requires a validated frozen `MYDMSVN_GUI_P5_PACKAGE`, designated test SQL configuration, `MYDMSVN_GUI_P5_EXPECTED_SCALE` and an explicitly created empty `MYDMSVN_BCF_ATTENDED_DIRECTORY` for local session coordination. It is skipped without attended opt-in and excluded from the automated matrix selector. Setup/login is automated; the human supplies query keystrokes, independently reports all three controls in Light/Dark, and requests read-only native snapshots. The owned 22-minute session verifies process/output drain and no receipt/inventory writes before verified SQL teardown. Session completion alone does not certify human input; record those reports separately. Never create coordination markers as a substitute for absent human observations.

T04 adds 12 focused Products/shared-catalog rows in `BusinessCompactRefinementTests.Catalog.cs`. [Frozen T04 evidence](../docs/evidence/bcf/20261021-t04-products-inspector.md) reconciles native100 240 affected rows, native125 234+2=236 runnable rows, extra Dark 122 each and 43 Session rows; all executed rows pass on source 29cd2a1. Four strict 1680-wide shell/Products native125 rows remain unselected/NOT TESTED, and one stale query-error Minor is retained. The corrected native125 selector deliberately includes the two legacy fitted layout rows; their PASS does not certify the unavailable full viewport. See the evidence for exact selectors, hashes and the 14 reviewed captures. No new full project, SQL, package, Login or attended acceptance is implied.

Run affected existing control/Session cases as required by inspected changes and current UI-NEXT validation. The historical BCF-T02 matrix preceded T03 visual work; BCF-T03–T05 covered shell/theme, Products and receipt presentation in their recorded scope. That old phase gate is not restarted. Do not run full Desktop or all SQL owners after each binding/layout edit.

BCF-T02 is COMPLETE on frozen `67fff29`: 172/172 automated rows per native100/125 Light/Dark context (688 total), plus separate human all-three reports and two clean real-package sessions. See its evidence for source/hash reconciliation, input limits and the deferred Minor observer-budget issue. At the historical T03 checkpoint, shell/theme had [partial verification](../docs/evidence/bcf/20261021-t03-chrome-followup.md): native125 at 1680×900 Light/Dark remained OPEN because the available monitor clamped the requested client, and native chrome limits were recorded. Final BCF acceptance retains its separately signed bounded scope and exceptions. BusinessCompactRefinementTests uses actual hosted catalog data and guarded independent DPI/capture probes. The lookup gate is not visual/full BCF or release acceptance.

The historical BCF-T06 contract required one reconciled frozen integration round: complete Desktop.Session once, complete Desktop GUI at native100 and native125, both-theme actual cases, and the scoped existing bounded package/actual-Login and attended workflows. Preserve independent SQL checks/owned cleanup for package scenarios that require them; binding/visual work alone does not select the entire SQL suite. Record actual discovery/execution/outcomes, source/test/package hashes, native DPI/theme/client bounds and matched reference/app comparisons in the [BCF ledger](../docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md). Future-candidate native results remain NOT RUN until executed; neither command examples nor earlier candidate counts establish their PASS.

## Accepted reuse and evidence

RESP-T01 provides the compatible Respawn package and owned full-schema reset profile. RESP-T02 adds exclusive scopes, tracked workers/cleanup and xUnit collection adapter probes inside existing owners. Set `MYDMSVN_SQL_TEST_FIXTURE_MODE` to `fresh` or `reuse` for participating fixtures; absent/blank defaults to reuse, matching is case-insensitive with surrounding whitespace ignored, and any other value fails. T03–T04 adopt all 20 original eligible methods (Catalog 5, Inventory 7, Identity/Security 8); default reuse is now implemented. Dedicated exceptions continue using their own fresh helper in either mode. Keep the original behaviors/Theory rows and report ordinary TRX results plus lightweight allocation/migration/reset/seed/drop observations; do not recreate the removed framework.

Report exact revision, commands, project/TFM/filter, actual counts/outcomes and unresolved cleanup. Missing Windows, targeting packs, SQL or GUI resources is BLOCKED / NOT RUN. Keep connection data, raw TRX, Theory values, SQL identifiers and detailed diagnostics local; publish sanitized summaries. No old test total or benchmark result proves a new candidate has passed.

The RESP-T01 reset probes run directly in Foundation using `FullyQualifiedName~ReusableSqlFixtureIntegrationTests` (11 SQL Facts); the T01 snapshot had 27 Foundation cases; the T02 snapshot had 41 cases; the complete project after PR36 review has 44. They always allocate fresh owned databases to challenge reset/ownership/schema/failure boundaries. See [case map](../docs/RESPAWN_CASE_MAP.md#resp-t01-additions) and [verification](../docs/RESPAWN_VERIFICATION.md).

RESP-T02 local scope/parser tests use IntegrationTests with `FullyQualifiedName~ReusableSqlFixtureTests` (19 cases after PR36 review; 14 at T04); combine with `FullyQualifiedName~SqlTestDatabase` for 41 selected cases. T04 renames the parser test to `Mode_parser_uses_reuse_default_and_accepts_explicit_modes` and adds a local regression that rejects a seed assertion masquerading as the expected scenario failure. Foundation lifecycle probes use `FullyQualifiedName~ReusableSqlFixtureLifecycleIntegrationTests` (17 cases after PR36 review, including one local probe-cleanup regression). Catalog adapter probes use `FullyQualifiedName~SqlFixtureCollectionAdapterTests` (one Fact and two Theory rows). After T03, the Catalog `Category=FastSQL` selection and complete project each contain ten cases: five original business cases, three adapter cases and two contamination Theory rows. Every Theory row resets and seeds independently.

Participating fixtures migrate/prepare once per allocation, reset/seed/verify before every admitted body, then retain exclusive admission until tracked workers and resource cleanup drain. Register every background worker and owned resource before returning the scope. A cleanup/reset/seed/boundary failure poisons reuse; a body assertion failure allows the next reset only after successful drain and an unchanged final boundary. Fresh mode uses that same contract on a new database per case. Catalog initialization and all final-drop diagnostics use xUnit's fixture message sink; lazy Inventory/Security initialization is reported once in the first participating member's output. Per-case observations use test output. Do not count shared migration once per test or double-count runner summaries.

`MYDMSVN_SQL_TEST_ADAPTER_PROBE` is an opt-in fault seam for the selected Catalog collection consumers: `initialize-failure`, `reset-failure`, `seed-failure`, `schema-failure`, `trigger-failure`, `cleanup-failure`. Leave it unset for normal execution. In explicit reuse initialization failure affects every selected test. For the original five business methods plus three adapter rows, other fault selections must report eight failed cases, no skips; cleanup-only failure permits exactly the first body before terminal poison. The deliberate scenario-failure challenge is excluded from those eight-member fault selections. These expected failing development runs are recorded separately from passing verification. Restore the caller's prior process environment after changing either setting. See [T03 evidence](../docs/RESPAWN_VERIFICATION.md#resp-t03-execution-record--catalog-adoption).

The Catalog collection uses the complete APP reset profile, followed by an explicit empty catalog/User baseline; the three adapter cases additionally seed/assert one User. Employee linkage remains seeded inside the original CRUD scenario. All application-owned DI providers/scopes, UoWs and direct scenario connections register cleanup with the exclusive test scope; disposal failure poisons reuse. No business UoW or transaction is shared across cases.

`FullyQualifiedName~CatalogFixtureReuseIntegrationTests` selects the two reordered contamination rows (`reverse: False` / `reverse: True`). Each row deliberately commits catalog/User sentinels, catches the expected assertion failure after successful cleanup, then calls all five original business bodies in a controlled order and verifies a final empty baseline. This is **two xUnit cases / fourteen scenario scopes**, not fourteen results. A complete Catalog pass is **ten cases / twenty-two scopes**: reuse allocates/migrates/drops once; explicit fresh does so twenty-two times with identical business assertions. Per-case reset/seed/verify/cleanup counts cover every nested scope. Use the existing Foundation `Outstanding_worker_blocks_reset_until_drained` and `Reset_cancellation_drains_before_rejecting_the_waiting_scope` methods for the dedicated multi-session/drain check; business contention cases remain fresh.

RESP-T04 uses the linked `ApplicationSqlCollectionFixture` in Inventory and Security, with one independent APP allocation per participating owner/collection. Its xUnit initialization is lazy: selecting only a fresh exception in a mixed class does not evaluate the fixture mode/fault setting or allocate a reusable database. Existing assembly serialization remains. Query `SeedAsync` runs in the seed callback, followed by cached-boundary verification; other data scenarios start from an explicitly asserted empty 15-table APP baseline and retain their own deterministic arrangement. Returned IDs and actual rowversion reads remain the authority; database-wide counters do not rewind.

`FullyQualifiedName~ApplicationDataFixtureReuseIntegrationTests` selects one new SQL Fact in each owner. Each Fact checks complete 15-table contamination (including receipts/ledger/balances, permissions/revisions/audit/binding and sequence advancement), then an exact expected assertion failure, all eligible business scenarios in forward/reverse order and final empty baselines. It is one xUnit result with 18 Inventory or 20 Security scopes. The complete eligible selections therefore have **Inventory 8 cases / 25 scopes**, **Security 9 cases / 28 scopes**. Other methods in mixed classes remain fresh; select exact eligible methods from the [case map](../docs/RESPAWN_CASE_MAP.md#resp-t04-adoption-and-frozen-candidate) when checking reuse alone.

`MYDMSVN_SQL_TEST_APPLICATION_PROBE` is the opt-in Inventory/Security fault seam: `initialize-failure`, `reset-failure`, `seed-failure`, `schema-failure`, `trigger-failure`, `cleanup-failure`. Leave it unset for ordinary runs. Select the seven/eight original eligible methods (exclude contamination probes) for failure evidence; all selected members must fail, no skips. The seed fault fills all 15 mutable tables before throwing. Lazy setup means the first participating member exposes an initialization error and later members reject poison. Cleanup-only failure allows exactly one body. A focused fresh-exception selection must still pass with this fault set to initialize-failure, with no reusable allocation/observation.

The accepted RESP-T05 candidate has **265 cases** across the six SQL owners: Foundation 44, Catalog 10, Inventory 36, Security 53, Migrations 65, IntegrationTests 57. One complete sequential explicit fresh pass and one explicit reuse pass each passed all 265 with zero failed/skipped cases and successful cleanup. Focused parser/default adapters also passed. See [matched acceptance and exact baseline](../docs/RESPAWN_VERIFICATION.md#pr-36-review-follow-up), including wall-time/phase observations and their limits. Keep both fault settings unset for ordinary runs. Preserve this test baseline during L2DB. See [T04 evidence](../docs/RESPAWN_VERIFICATION.md#resp-t04-execution-record--frozen-data-fixtures).


PR36 review repairs add `verify-after` after tracked drain while admission is held, so final-case boundary drift fails that case. Scope stop/worker/resource cleanup shares a two-minute cooperative budget; timeout quarantines the allocation, rejects later admissions/teardown and requires manual review, with no automatic verify/reset/drop even after late completion. Stop/resource callbacks run off-thread; stop notification and actual worker drain are distinct. Current local parser/scope selection has19cases (combinedSqlTestDatabase selection41); Foundation lifecycle selection17cases and complete project44. See [review evidence](../docs/RESPAWN_VERIFICATION.md#pr-36-review-follow-up).

## L2DB-T06 oracle and dependency cutover

Active net48 product/test graphs now use LINQ to DB/native SqlClient only. `tests/SqlOracle` contains bounded raw-SQL test helpers, linked into the existing owners; Catalog uses the helper from its existing IntegrationTests reference. The per-command oracle has no product mappings, SQL generation, replacement-connection creation, retries or ambient transaction. NativeSql.Owned retains explicit scenario-level disposable database allocation/migration and guarded cleanup through the existing helper; it is not a fallback connection path. Foundation retains LINQ probes with independent native same-session/rollback observations. Historical pilot case names remain logical IDs but run the sole production implementation.

T06 source/row reconciliation and sequential six-owner results are in [verification](../docs/L2DB_VERIFICATION.md#l2db-t06--transitional-provider-and-test-graph-cleanup) and the [case ledger](../docs/L2DB_CASE_MIGRATION.md#t06-dependency-cutover-and-complete-source-reconciliation). The 265-case paragraph above is accepted RESP history, not the current source total. [T07 frozen-revision correctness/package/Local smoke acceptance](../docs/L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) is complete:581/0/0 required invocations, verified package and guarded runtime smoke cleanup. W139 was conditionally NOT RUN in T07's frozen scope. Later GUI-P5 acceptance is complete within its approved scope; BCI has its own completed frozen UI acceptance evidence and BCF has completed its bounded acceptance and SAL retains its archived incomplete acceptance; UI-NEXT is now the sole active roadmap, while [production release prerequisites](../docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.


### BCF-T05 receipt verification — 2026-10-21

Source/test `991530f` implements guarded receipt List/Detail. [T05 evidence](../docs/evidence/bcf/20261021-t05-receipt-presentation.md) records native100 301/301 and native125 295/295 runnable affected passes, with 116/116 measured Dark lookup rows at each scale. Native100 has 69 additional default-theme control/inline passes within its 185-row supplementary invocation; those 69 are not Dark evidence. All four frozen invocations total 897 executed passes, with 464 clean measured lookup observer/drain records and 26 reviewed captures. Discovery301/295 excludes only six strict wide native125 capture rows from the latter selector; fitted legacy layout rows stay selected. No rebuild occurs between contexts. Existing T02 attended acceptance is preserved for its own candidate; no new package/Login, attended receipt or T06/Owner acceptance is claimed.
