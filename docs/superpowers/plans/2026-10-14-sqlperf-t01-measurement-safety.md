# SQLPERF-T01 Measurement and Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a fail-closed, dual-TFM SQL test safety and measurement harness and use it to produce a reconciled repeatable SQL integration baseline for SQLPERF-T01.

**Architecture:** A test-only `netstandard2.0` library owns live SQL approval, mutation authorization, durable database ownership, explicit timing contexts, and redaction so both `net48` and `net8.0` testhosts execute the same guard. A .NET 8 Windows runner and thin PowerShell entry point own discovery/TRX reconciliation, Job Object process-tree control, reporting, and repeated benchmark orchestration. Production migration, schema, authorization, and application projects remain unchanged.

**Tech Stack:** C# 12, `netstandard2.0`, `net48`, `net8.0`, xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, Microsoft.Data.SqlClient 7.0.2, Dapper 2.1.79, Newtonsoft.Json 13.0.4, DbUp 7.2.0, Windows PowerShell, VSTest TRX.

**Spec:** `docs/superpowers/specs/2026-10-14-sqlperf-t01-measurement-safety-design.md`

## Global Constraints

- Implement only SQLPERF-T01; do not begin SQLPERF-T02 through T06 optimizations.
- Keep application projects on their existing `net48`, `net8.0`, and `net8.0-windows` targets; do not move to .NET 10.
- Do not modify production migrations 001-008, production authorization, transaction semantics, or application behavior.
- SQL DDL may run only on a separately approved, operator-attested non-production instance and only after live identity and effective `master` validation.
- `SqlTestDatabaseName.EnsureDisposable` validates naming only and never establishes ownership or deletion authority.
- Do not grant SQL permissions, infer approval from a connection string, auto-enroll a server, delete by prefix/GUID/journal alone, or kill unrelated SQL/process sessions.
- Preserve sequential SQL tests and full unfiltered `net48` and `net8.0` gates.
- Use explicit measurement context parameters; no global mutable current test, thread-ID attribution, or ambient/static `AsyncLocal` transaction.
- Keep connection strings, credentials, raw host identities, local paths, PII, password/hash data, and theory parameter values out of shareable diagnostics.
- C#/Markdown/PowerShell files use repository encoding and CRLF rules; JSON is UTF-8 with CRLF.

## Review Focus

- A correct-looking connection string that resolves through an alias to a different server must fail before any DDL; covered in Task 1 live identity mismatch tests.
- A crash after SQL creates a database but before marker/state confirmation must quarantine it without `ALTER`, `DROP`, or `KILL`; covered in Task 2 crash-window tests.
- A new unmarked SQL-backed `[Theory]` row must enter mandatory inventory and a stale non-SQL exemption must fail reconciliation; covered in Task 4 discovery tests.
- A timed-out testhost with a grandchild must terminate only the owned Job Object and leave unrelated processes alive; covered in Task 5 synthetic process tests.
- Nested or overlapping timing operations must not inflate TRX/wall-clock totals and an uninstrumented migration call must prevent a complete-coverage claim; covered in Task 3 reconciliation tests.

---

### Task 1: Shared approval, live preflight, and mutation authorization

**Files:**
- Create: `tests/MyDmsVn.SqlTestHarness/MyDmsVn.SqlTestHarness.csproj`
- Create: `tests/MyDmsVn.SqlTestHarness/SqlTestHarnessEnvironment.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Approval/SqlInstanceApproval.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Approval/SqlInstanceApprovalLoader.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Approval/SqlInstanceIdentity.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Approval/SqlInstanceFingerprint.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/ISqlAdministrativeSession.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/SqlClientAdministrativeSession.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/SqlPreflightRequirements.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/SqlPreflightFailure.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/SqlPreflightResult.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/SqlPreflightValidator.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Preflight/SqlMutationAuthorization.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Diagnostics/SensitiveValueRedactor.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Approval/SqlInstanceApprovalLoaderTests.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Preflight/SqlPreflightValidatorTests.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Diagnostics/SensitiveValueRedactorTests.cs`
- Modify: `MyDmsVn.sln`
- Create: `tests/MyDmsVn.SqlTestHarness/packages.lock.json`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/packages.lock.json`

**Interfaces:**
- Consumes: `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`; new `MYDMSVN_SQL_TEST_APPROVAL_FILE`; SQL metadata from an open administrator connection.
- Produces: `SqlInstanceApprovalLoader.Load(string path)`, `SqlInstanceFingerprint.Create(SqlInstanceIdentity identity)`, `SqlPreflightValidator.ValidateAsync(string connectionString, SqlInstanceApproval approval, SqlPreflightRequirements requirements, CancellationToken cancellationToken)`, and `SqlMutationAuthorization` containing only the validated canonical identity/fingerprint and approval ID.
- `ISqlAdministrativeSession` exposes explicit `OpenAsync`, identity/effective-database/permission reads, and parameterized administrative execution; fakes record attempted commands so tests can prove zero DDL.

- [x] **Step 1: Scaffold the two projects and write failing approval/redaction tests**

Add the projects to `MyDmsVn.sln`. The harness targets `netstandard2.0`; its tests target `net48;net8.0`. Use the existing centrally pinned Newtonsoft.Json, Microsoft.Data.SqlClient, xUnit, and Microsoft.NET.Test.Sdk versions.

Tests must assert literal outcomes for: missing file, malformed JSON, schema-version mismatch, false/missing attestation, expired approval, canonical identity mismatch, endpoint mismatch, and shortening/redaction that never emits supplied secrets or raw identity fields.

- [x] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~SqlInstanceApprovalLoaderTests|FullyQualifiedName~SensitiveValueRedactorTests"
```

Expected: compilation/test failure because approval loading, fingerprinting, and redaction types do not exist.

- [x] **Step 3: Implement immutable approval parsing, canonical fingerprinting, and redaction**

The operator JSON schema is exact and rejects unknown/missing security fields:

```json
{
  "schemaVersion": 1,
  "approvalId": "operator-chosen-id",
  "nonProductionAttestation": true,
  "validUntilUtc": "optional ISO-8601 UTC",
  "identity": {
    "serverName": "exact SERVERPROPERTY value",
    "machineName": "exact SERVERPROPERTY value",
    "instanceName": "exact or null SERVERPROPERTY value",
    "endpoint": "exact endpoint identity when available"
  },
  "fingerprintSha256": "lowercase SHA-256 of canonical identity"
}
```

Do not add an approval-generation command. Reject a fingerprint not independently present in the file.

- [x] **Step 4: Run approval/redaction tests on both TFMs and verify GREEN**

Run the Step 2 command for `net8.0`, then repeat with `-f net48`. Expected: all selected tests pass, zero skipped.

- [x] **Step 5: Write failing preflight and mutation-authorization tests**

Use a recording fake session and name the break each test catches. Cover: connection string absent/unparseable, missing explicit `master`, effective `DB_NAME()` not `master`, unreadable identity, approval mismatch, alias/endpoint mismatch, missing version-aware DMV permission, revoked/expired approval, changed live identity between validations, and permission success. For every failure assert the fake received no command containing `CREATE DATABASE`, `ALTER DATABASE`, `DROP DATABASE`, or `KILL`.

- [x] **Step 6: Run preflight tests and verify RED**

Run:

```powershell
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~SqlPreflightValidatorTests"
```

Expected: failure because live preflight and typed failure categories are not implemented.

- [x] **Step 7: Implement live preflight and short-lived mutation authorization**

Query effective database and canonical identity from the actual open connection. Check SQL major version and the applicable cross-session DMV permission, returning typed failures rather than granting anything. Authorization is valid only for the exact canonical identity, approval fingerprint/config digest, and validation instant; every mutation boundary must request fresh validation rather than trusting an environment flag.

- [x] **Step 8: Run Task 1 tests and solution build**

Run:

```powershell
dotnet restore MyDmsVn.sln --locked-mode --nologo -p:RestoreDisableParallel=true
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0 --no-restore
dotnet build MyDmsVn.sln -c Release --no-restore
```

Expected: zero failures, zero skipped harness tests, zero build warnings/errors.

- [x] **Step 9: Commit Task 1**

```powershell
git add MyDmsVn.sln tests/MyDmsVn.SqlTestHarness tests/MyDmsVn.SqlTestHarness.Tests
git commit -m "test: guard SQL test mutations with approved preflight"
```

---

### Task 2: Durable ownership journal, database marker, recovery, and lifecycle guard

**Files:**
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/DatabaseOwnershipState.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/DatabaseOwnershipRecord.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/IDatabaseOwnershipJournal.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/FileDatabaseOwnershipJournal.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/DatabaseOwnershipMarker.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/DatabaseOwnershipReconciler.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Ownership/DatabaseRecoveryClassification.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Ownership/FileDatabaseOwnershipJournalTests.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Ownership/DatabaseOwnershipReconcilerTests.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabaseMutationTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlServerFactAttribute.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabaseLifecycleTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: Task 1 `SqlPreflightValidator`/`SqlMutationAuthorization`, generated `RunId`, exact database name/token, operator-local journal directory.
- Produces: `IDatabaseOwnershipJournal.WriteCreateIntentAsync`, `TransitionAsync`, and `ReadAllAsync`; `DatabaseOwnershipReconciler.ClassifyAsync`; guarded `SqlTestDatabase.CreateAsync(string, CancellationToken)` and retryable `DisposeAsync()`.
- Journal states are exactly `CreateIntent`, `Created`, `DropIntent`, `Disposed`, `CleanupFailed`, and `Quarantined`; transitions are monotonic except retry from `CleanupFailed` after fresh corroboration.

- [x] **Step 1: Write failing journal durability and concurrency tests**

Assert: create-intent is readable after reopening; temp/partial state is detected rather than accepted; same operation replay is idempotent; invalid transition is rejected; two writers cannot corrupt a record; no record includes a connection string/raw host/credential; a failed flush reports failure and does not authorize creation.

- [x] **Step 2: Run journal tests and verify RED**

Run:

```powershell
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~FileDatabaseOwnershipJournalTests"
```

Expected: failure because journal types do not exist.

- [x] **Step 3: Implement one atomic state file per database**

Use an ignored operator-local root, a lock scoped to the canonical journal path, same-volume temporary write, `FileOptions.WriteThrough`, explicit flush-to-disk, and atomic replace/move. Persist only fingerprint/approval IDs and opaque database/run/ownership IDs. Detect orphan temp files, invalid JSON, invalid transitions, and conflicting identities as ambiguous.

- [x] **Step 4: Write failing recovery and mutation tests**

Use fake SQL metadata/command sessions. Cover absent, positively owned, ambiguous, unowned, and other-active-run databases; create-before-marker crash; marker-before-created-state crash; stale journal; wrong approved instance; live creation metadata mismatch; same-prefix unowned database; and disposal retry. Every non-positive class must assert zero `ALTER`, `DROP`, and `KILL`. A direct call to `SqlTestDatabase.CreateAsync` with missing/revoked approval must assert zero destructive SQL independent of runner state.

- [x] **Step 5: Run recovery/mutation tests and verify RED**

Run the harness tests filtered to `DatabaseOwnershipReconcilerTests`, then the integration project filtered to `SqlTestDatabaseMutationTests` on `net8.0`. Expected: missing marker/reconciliation and current unguarded DDL cause failures.

- [x] **Step 6: Implement marker, reconciliation, and guarded lifecycle**

Before create: validate live approval, persist/flush `CreateIntent`, then issue parameter-safe guarded database creation. After create: write a database-level extended-property marker containing opaque run/ownership IDs, read back live database ID/create date/marker, and transition to `Created`. On any uncorroborated crash window, write `Quarantined` and issue no cleanup DDL.

Before alter/drop: open a fresh administrator connection, rerun Task 1 validation, transition to `DropIntent`, corroborate journal + marker + live metadata + exclusive operation ownership, then execute guarded cleanup. Record `Disposed` or `CleanupFailed`. Preserve retry semantics without losing ownership.

Update `SqlServerFactAttribute` to skip when either connection input or approval-file input is missing, while retaining an explicit reason. Acceptance reconciliation later treats skipped mandatory SQL as failure.

- [x] **Step 7: Run Task 2 tests on both TFMs**

Run harness unit tests and integration pure mutation/name/lifecycle validation tests on `net48` and `net8.0`. If an approved disposable SQL instance is configured, also run `SqlTestDatabaseLifecycleTests` sequentially on each TFM; otherwise record the live cases as blocked, not passed.

- [x] **Step 8: Commit Task 2**

```powershell
git add .gitignore tests/MyDmsVn.SqlTestHarness tests/MyDmsVn.SqlTestHarness.Tests tests/MyDmsVn.Server.Infrastructure.IntegrationTests
git commit -m "test: journal SQL test database ownership"
```

---

### Task 3: Explicit timing context and audited migration coverage

**Files:**
- Create: `tests/MyDmsVn.SqlTestHarness/Timing/SqlTestMeasurementContext.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Timing/SqlTestTimingEvent.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Timing/ISqlTestTimingSink.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Timing/JsonLinesSqlTestTimingSink.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Timing/SqlTestOperationScope.cs`
- Create: `tests/MyDmsVn.SqlTestHarness/Timing/TimingIntervalReconciler.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Timing/SqlTestOperationScopeTests.cs`
- Create: `tests/MyDmsVn.SqlTestHarness.Tests/Timing/TimingIntervalReconcilerTests.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MeasuredDatabaseMigrationRunner.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MigrationTimingCoverageTests.cs`
- Create: `tests/tools/sqlperf-t01-migration-invocations.json`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/TestDatabaseMigrationRunner.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SchemaCompatibilityIntegrationTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/GoodsReceiptCommandIntegrationTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/DatabaseMigrationTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlPrincipalProvisioningTests.cs`

**Interfaces:**
- Consumes: explicit `SqlTestMeasurementContext` passed from each audited test/helper into lifecycle and migration operations; Task 2 lifecycle transitions.
- Produces: `SqlTestMeasurementContext.Create(runId, testCaseId, tfm, anonymizedDatabaseId, sink)`, `context.StartOperation(stage, operationId, parentOperationId)`, `TimingIntervalReconciler.Reconcile(trxDuration, events)`, `SqlTestDatabase.CreateAsync(string, SqlTestMeasurementContext?, CancellationToken)`, and `MeasuredDatabaseMigrationRunner.Migrate(context, invocationId, connectionString)`.
- The migration manifest keys each invocation by class, exact test method, stable call-site ID, TFM rule, and expected invocation rule; source line is recorded as review metadata but stable IDs do not depend on shifting line numbers.

- [x] **Step 1: Write failing timing event and interval tests**

Assert exact event fields and outcomes for success, expected exception, and cancellation; timing-disabled context writes no event; parent/child exclusive time subtracts nested intervals once; overlapping siblings are labeled overlap rather than summed; residual is `TRX - union(measured intervals)` with negative sampling residual explicitly labeled; concurrent sinks produce complete JSON lines without secrets.

- [x] **Step 2: Run timing tests and verify RED**

Run the harness tests filtered to `SqlTestOperationScopeTests|TimingIntervalReconcilerTests` on `net8.0`. Expected: missing types.

- [x] **Step 3: Implement timing context, sink, scopes, and interval reconciliation**

Use `Stopwatch.GetTimestamp()` and `Stopwatch.Frequency` for monotonic calculations. Use immutable context values and explicit parameters only. Record create, migration, seed, permission-sync, pool-cleanup, and drop operations even on failure/cancellation. Do not instrument production `DatabaseMigrationRunner`.

- [x] **Step 4: Write failing migration coverage tests**

Add tests that compare the four-class manifest against source/runtime invocation IDs. Cover a normal production migration, `TestDatabaseMigrationRunner`, direct DbUp `PerformUpgrade`, repeated migration, expected-throw migration, canceled/failing operation, and a deliberately omitted timing scope. The omitted case must return incomplete coverage rather than pass.

- [x] **Step 5: Run migration coverage tests and verify RED**

Run `MigrationTimingCoverageTests` on `net8.0`. Expected: failure listing the current uninstrumented call sites.

- [x] **Step 6: Inventory and instrument every migration path in the four audited classes**

Replace only test call sites with timed delegation/scopes. Preserve exact DbUp builders, locks, transactions, scripts, exception behavior, rerun counts, and assertions. Introduce a caller-member helper for stable test method identity and explicit row/instance key where needed. Populate the manifest with every direct and helper-mediated invocation and its expected multiplicity.

- [x] **Step 7: Run Task 3 tests on both TFMs**

Run harness timing tests and `MigrationTimingCoverageTests` on `net48` and `net8.0`. With approved SQL, run the four audited classes with timing enabled and assert 100% manifest-to-event reconciliation per TFM; without it, record the SQL portion blocked.

- [x] **Step 8: Commit Task 3**

```powershell
git add tests/MyDmsVn.SqlTestHarness tests/MyDmsVn.SqlTestHarness.Tests tests/MyDmsVn.Server.Infrastructure.IntegrationTests tests/tools/sqlperf-t01-migration-invocations.json
git commit -m "test: instrument SQL test lifecycle stages"
```

---

### Task 4: Independent source, discovery, and TRX SQL inventory

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/TestMethodInventory.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/VstestDiscoveryParser.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/TheoryRowIdentity.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/NonSqlExemptionManifest.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/IlSqlCallGraphAuditor.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Trx/TrxResultParser.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reconciliation/SqlTestInventoryReconciler.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Inventory/SqlTestInventoryTests.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Trx/TrxResultParserTests.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reconciliation/SqlTestInventoryReconcilerTests.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Fixtures/vstest-discovery-*.txt`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Fixtures/results-*.trx`
- Create: `tests/tools/sql-test-nonsql-exemptions.json`
- Modify: `MyDmsVn.sln`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/packages.lock.json`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/packages.lock.json`

**Interfaces:**
- Consumes: compiled integration test assembly, VSTest list/discovery output, completed TRX, exact method-scoped exemption manifest.
- Produces: `TestMethodInventory.Read(assemblyPath)`, `VstestDiscoveryParser.Parse`, `TrxResultParser.Parse`, `IlSqlCallGraphAuditor.Audit`, and `SqlTestInventoryReconciler.Reconcile` returning counts, mandatory stable case IDs, and typed completeness failures.
- Stable case ID is fully qualified method + opaque SHA-256 of normalized discovery identity + row ordinal when needed; diagnostics never emit theory parameter values.

- [x] **Step 1: Write failing discovery/inventory tests with fixture assemblies and TRX**

Fixtures must cover `[Fact]`, inline/member-data `[Theory]`, multiple rows, deferred discovery, duplicate display names, new unmarked SQL fact/theory, explicit exact-method exemption, stale method fingerprint, deleted method, wildcard exemption rejection, and an exemption whose IL call graph reaches `SqlConnection`, `SqlTestDatabase`, migration, provisioning, or SQL fixture code.

- [x] **Step 2: Run inventory tests and verify RED**

Run:

```powershell
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release --filter "FullyQualifiedName~SqlTestInventoryTests"
```

Expected: missing inventory types.

- [x] **Step 3: Implement independent method/discovery inventory and exemption audit**

Read xUnit attributes from assembly metadata/reflection without consulting traits. Default every discovered method/case to SQL-required. Exempt only exact methods with rationale, evidence, and method-body fingerprint. Resolve direct/transitive IL calls within the test assembly and reject known SQL roots. Unknown/deferred rows fail unless method and executed row counts can be reconciled.

- [x] **Step 4: Write failing TRX and three-way reconciliation tests**

Cover: complete run, nonzero runner exit, missing/corrupt TRX, failed case, skipped mandatory SQL, skipped exempt non-SQL, discovered-not-executed, executed-not-discovered, missing theory row, duplicate result, TFM mismatch, and display-name variation that normalizes to the same opaque identity.

- [x] **Step 5: Run TRX/reconciliation tests and verify RED**

Run the runner tests filtered to `TrxResultParserTests|SqlTestInventoryReconcilerTests`. Expected: missing parser/reconciler.

- [x] **Step 6: Implement TRX parsing and fail-closed reconciliation**

Require real exit code zero, one complete TRX, zero failed, zero skipped mandatory SQL, and a one-to-one mandatory discovered/executed mapping. Derive all counts from current artifacts. Return typed incomplete reasons; never downgrade an unknown row/method to non-SQL.

- [x] **Step 7: Audit and populate the current exact-method exemption manifest**

Enumerate all current integration `[Fact]`/`[Theory]` methods. For each proposed non-SQL method, inspect direct/transitive calls and record exact rationale/evidence/fingerprint. Run reconciliation against both built TFM assemblies and `dotnet test --list-tests` output. Do not use `SqlServerFact` or future traits as evidence.

- [x] **Step 8: Run Task 4 tests and build**

Run runner unit tests, both harness TFMs, and Release solution build. Expected: zero failures/warnings; inventory reconciliation succeeds for discovery on both TFMs even when execution artifacts are intentionally absent from the discovery-only command.

- [x] **Step 9: Commit Task 4**

```powershell
git add MyDmsVn.sln tests/tools/MyDmsVn.SqlTestRunner tests/tools/MyDmsVn.SqlTestRunner.Tests tests/tools/sql-test-nonsql-exemptions.json
git commit -m "test: reconcile independent SQL test inventory"
```

---

### Task 5: Windows Job Object watchdog and benchmark orchestration

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Process/CommandSpec.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Process/CommandResult.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Process/WindowsJobObject.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Process/OwnedProcessRunner.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Benchmark/BenchmarkRunOptions.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Benchmark/BenchmarkRunCoordinator.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Program.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Process/OwnedProcessRunnerTests.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Benchmark/BenchmarkRunCoordinatorTests.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Fixtures/HangingProcessTree/`
- Create: `tests/tools/Invoke-SqlIntegrationBenchmark.ps1`

**Interfaces:**
- Consumes: Task 1 preflight, Task 2 journal/reconciler, Task 4 discovery/TRX reconciler, checkout/project path, TFM order, command and overall deadlines, artifact root, and sanitized operator context.
- Produces: CLI verbs `preflight`, `discover`, `run`, `reconcile`, and `baseline`; each returns nonzero for blocked/failed/incomplete and writes a machine-readable sanitized run manifest.
- `OwnedProcessRunner.RunAsync(CommandSpec, CancellationToken)` assigns the root process to a Windows Job Object before work proceeds, drains both streams concurrently, returns the real exit code after exit, and terminates only that job on deadline.

- [x] **Step 1: Write failing process-tree tests**

The synthetic fixture launches a child and grandchild that block without interactive input. Assert normal command exit/output, stdout+stderr drainage under volume, per-command timeout, hard overall cancellation, root/descendant termination, partial-log preservation, and survival of a separately launched unrelated process.

- [x] **Step 2: Run watchdog tests and verify RED**

Run `OwnedProcessRunnerTests`. Expected: missing Job Object/process runner.

- [x] **Step 3: Implement Windows Job Object process control**

Create the process suspended or otherwise guarantee assignment before it can spawn unowned descendants; set kill-on-job-close; resume only after assignment. On deadline terminate the job, drain streams, wait for exit, enumerate/report owned killed PIDs without broad `dotnet` lookup, and return timeout/incomplete even if a partial TRX exists.

- [x] **Step 4: Write failing coordinator tests**

Use fake process/preflight/journal components. Cover: child environment contains both required paths/IDs, preflight occurs before test launch, unique TRX names, build excluded from timed run, sequential TFMs, alternating order across passes, warm-up excluded from median, invalid run replacement, missing TRX, skipped mandatory SQL, watchdog timeout, journal quarantine after death, and no benchmark continuation after blocked preflight.

- [x] **Step 5: Run coordinator tests and verify RED**

Run `BenchmarkRunCoordinatorTests`. Expected: missing coordinator behavior.

- [x] **Step 6: Implement CLI coordinator and thin PowerShell wrapper**

The PowerShell script only validates user-facing parameters and invokes the runner; it contains no alternate safety decisions. The runner preflights before every DDL-capable child, records child environment configuration without values, runs unfiltered `dotnet test` with unique TRX, invokes Task 4 reconciliation, and reopens Task 2 journal after abnormal exit. It never interprets timeout, missing output, or `dotnet test` exit zero alone as success.

- [x] **Step 7: Run Task 5 automated tests and a no-DDL negative CLI check**

Run all runner tests. Then invoke `preflight` and `baseline` with missing approval against a harmless fake/unreachable connection configuration and assert nonzero exit, no test launch, no journal create-intent, and no DDL command. Do not connect to any unapproved server for this negative check.

- [x] **Step 8: Commit Task 5**

```powershell
git add tests/tools/MyDmsVn.SqlTestRunner tests/tools/MyDmsVn.SqlTestRunner.Tests tests/tools/Invoke-SqlIntegrationBenchmark.ps1
git commit -m "test: add watchdog SQL benchmark runner"
```

---

### Task 6: Sanitized reporting, dual-TFM acceptance, and baseline evidence

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reporting/SqlPerformanceReport.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reporting/SqlPerformanceReportBuilder.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reporting/SqlDmvSnapshotReader.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reporting/SqlPerformanceReportBuilderTests.cs`
- Create: `docs/SQLPERF_T01_VERIFICATION.md`
- Modify: `tests/README.md`
- Modify: `docs/TEST_STRATEGY.md`
- Modify: `docs/plans/20261014-001-sql-integration-test-performance-optimization.md`

**Interfaces:**
- Consumes: valid run manifests, TRX, timing events, migration coverage, journal reconciliation, optional DMV snapshots, source commit/config/environment metadata.
- Produces: sanitized JSON/Markdown reports with per-class/test count, total, p50/p95, wall-clock contribution, exclusive stage time, overlap, residual, inventory counts, migration coverage, cleanup/quarantine, DMV availability, and validity decision.

- [x] **Step 1: Write failing report tests**

Use hand-checked fixtures. Assert literal p50/p95 and class totals; nested timing exclusion; overlap/residual labels; unavailable DMV values rendered as `unavailable`, not zero; warm-up excluded; invalid run excluded; no summing per-test durations as parallel wall-clock; and complete redaction of secrets, raw hosts, paths, and theory values.

- [x] **Step 2: Run report tests and verify RED**

Run `SqlPerformanceReportBuilderTests`. Expected: missing report builder.

- [x] **Step 3: Implement report builder and permission-aware DMV snapshots**

Read waits, CPU, I/O, memory grants/compiler memory only after Task 1 permission validation. Missing fields/permissions remain unavailable. Keep raw artifacts ignored/local and generate only sanitized shareable output.

- [x] **Step 4: Run all non-SQL automated gates**

Run fresh Release restore/build and all harness/runner tests:

```powershell
dotnet restore MyDmsVn.sln --locked-mode --nologo -p:RestoreDisableParallel=true
dotnet build MyDmsVn.sln -c Release --no-restore
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net48 --no-build --no-restore
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0 --no-build --no-restore
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release --no-build --no-restore
```

Expected: zero warnings/errors/failures/skips for these mandatory unit/synthetic gates.

- [x] **Step 5: Perform approved-instance preflight and direct-run negative proof**

If and only if the operator supplies the connection string, approval file, non-production attestation, and required permissions, run preflight and record only sanitized identity. Execute a deliberately unapproved direct `dotnet test` mutation test and prove it returns nonzero/blocked with zero database-level DDL. Execute wrong-database, wrong-identity, and wrong-server-disposal negative cases. If configuration is absent, record these live gates blocked and stop before DDL.

- [x] **Step 6: Measure instrumentation overhead**

On the approved instance, build once and run matched timing-disabled and timing-enabled passes with identical inventory. Quantify wall-clock overhead; if it exceeds 5%, reduce/diagnose it or record an explicit accounting before baseline acceptance.

- [x] **Step 7: Run one warm-up plus three valid warm baseline passes per TFM**

Use `Invoke-SqlIntegrationBenchmark.ps1` sequentially, alternating order across passes. Every accepted pass must have exit zero, complete unique TRX, zero failed, zero skipped mandatory SQL, complete source/discovery/execution reconciliation, complete migration invocation/event reconciliation, watchdog success, and clean/positively reconciled ownership. Repeat invalid passes; do not include them in medians.

The T01 instrumentation revision is the unchanged performance baseline for SQLPERF-T02 onward. Record Git SHA, SDK, TFM, shortened approval fingerprint, SQL version/edition, memory limit, sanitized machine/load context, inventory, per-run wall clock, TRX duration, dispersion, and stage report.

- [x] **Step 8: Run full repository regression gates**

After accepted SQL runs, execute the repository's complete Release test matrix, including application, architecture, desktop Windows tests, and both unfiltered SQL TFMs. Run SQL TFMs sequentially. Report every failure/skip by name; do not claim unavailable Windows GUI gates passed.

- [x] **Step 9: Update documentation and plan status from evidence only**

Document exact commands/results, counts, timing overhead, baseline table, invalid runs, timeout/cleanup outcomes, DMV availability, risks, and go/no-go for SQLPERF-T02 in `docs/SQLPERF_T01_VERIFICATION.md`. Update `tests/README.md` and `docs/TEST_STRATEGY.md` with operator configuration and runner usage. Check T01 boxes/Gate only if every required live and repeated-run criterion has evidence; otherwise leave them open and label precisely what remains blocked.

- [x] **Step 10: Commit Task 6**

```powershell
git add tests/tools/MyDmsVn.SqlTestRunner tests/tools/MyDmsVn.SqlTestRunner.Tests tests/README.md docs/TEST_STRATEGY.md docs/SQLPERF_T01_VERIFICATION.md docs/plans/20261014-001-sql-integration-test-performance-optimization.md
git commit -m "test: verify SQLPERF-T01 measurement baseline"
```

---

## Final verification checklist

- [x] Re-read the T01 source plan and map every bullet/Gate clause to code, a named test, or recorded blocked evidence.
- [x] Confirm `git diff --check` is clean and the working tree contains no raw approval, journal, TRX, connection string, host identity, credential, or local benchmark artifact.
- [x] Confirm fresh Release restore/build has zero warnings/errors.
- [x] Confirm harness and runner automated suites pass with zero mandatory skips.
- [x] Confirm both unfiltered SQL TFMs have current-commit discovery/TRX reconciliation and zero skipped mandatory SQL cases.
- [x] Confirm synthetic hung process tree, direct-run bypass, interrupted journal, crash-window quarantine, wrong-server disposal, and missing migration-event negative gates all fail closed.
- [x] Confirm one warm-up plus at least three valid warm passes per TFM exist on the same approved non-production SQL instance, or report T01 incomplete/blocked without checking its Gate.
- [x] Confirm next roadmap work remains SQLPERF-T02 only after T01 evidence is complete; do not claim P4.1/P5/P6 completion.
