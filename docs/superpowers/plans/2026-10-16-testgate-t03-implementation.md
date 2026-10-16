# TESTGATE-T03 Gate Orchestration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the versioned local-only Task/Final test-gate manifest, durable logical-case reconciliation, and cross-process SQL serialization required by TESTGATE-T03.

**Architecture:** Extend `MyDmsVn.SqlTestRunner` with strict manifest, identity, reconciliation, lease, orchestration, and reporting components. Keep `Invoke-TestGate.ps1` as a non-broadening wrapper and reuse the T02 `BenchmarkRunCoordinator` for strict SQL acceptance.

**Tech Stack:** C#/.NET 8 runner, xUnit, `System.Text.Json`, PowerShell, VSTest discovery/TRX, Windows named mutex and Job Objects.

**Spec:** `docs/superpowers/specs/2026-10-16-testgate-t03-design.md`

## Global Constraints

- Keep the runner and runner tests on `net8.0`; preserve integration tests on `net48;net8.0`.
- Do not change production code, SQL schema/migrations, authentication/permission behavior, or CI workflows.
- Reuse T02 `pr-acceptance`; do not alter the distinct multi-pass `baseline` profile.
- Task selection comes only from the checked-in manifest; never infer a solution-wide run or accept project/TFM/filter/category overrides.
- SQL execution remains serial and local-only; no SQL or GUI parallelism is introduced.
- Raw TRX, approval, ownership, timing, lease, path, PID, database, test-name, and Theory-argument data remain private.
- C# and Markdown changes use CRLF and repository-required UTF-8 BOM; JSON and PowerShell follow `.editorconfig`/`.gitattributes`.
- L1 uses only the named runner test class; L2 runs the runner project once on `net8.0`; real SQL runs only in the frozen L3 Final gate unless a focused live-SQL escalation is independently justified.
- Every task uses RED → GREEN → REFACTOR and ends in a focused commit.

## Review Focus

- A manifest path that is lexically inside the repository but resolves outside through `..` or a link must be rejected; Task 1 pins canonical containment.
- Two overloads with the same VSTest method name must fail as ambiguous rather than bind to the first method; Task 2 pins this case.
- A Theory display locator that changes, duplicates, or maps to two opaque row IDs must fail without exposing arguments; Tasks 2 and 3 pin it.
- A second local process and an abandoned owner must never start SQL until lock/recovery proof succeeds; Task 4 pins contention and inconclusive recovery.
- Cancellation after SQL launch must retain the lease through child termination and ownership reconciliation; Task 6 pins the ordering.

---

## File Structure

### New production/tool files

- `tests/test-gates.v1.json` — reviewed v1 gates, logical cases, mappings, and exemptions for current projects only.
- `tests/tools/Invoke-TestGate.ps1` — repository-facing Task/Final wrapper.
- `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateManifest.cs` — immutable manifest and selection records/enums.
- `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateManifestLoader.cs` — strict JSON loading.
- `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateManifestValidator.cs` — semantic/path/project/TFM/selection validation.
- `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateCoordinator.cs` — ordered build/discovery/execution/reconciliation dispatch.
- `tests/tools/MyDmsVn.SqlTestRunner/Gates/SqlExecutionLease.cs` — named mutex, durable lease, and recovery gating.
- `tests/tools/MyDmsVn.SqlTestRunner/Inventory/LogicalCaseIdentity.cs` — normalized signatures, row locators, and durable keys.
- `tests/tools/MyDmsVn.SqlTestRunner/Inventory/LogicalCaseBinder.cs` — source/discovery/TRX to manifest binding.
- `tests/tools/MyDmsVn.SqlTestRunner/Reconciliation/LogicalCaseInventoryReconciler.cs` — per-project and global bijection.
- `tests/tools/MyDmsVn.SqlTestRunner/Reporting/TestGateReportExporter.cs` — sanitized aggregate evidence.

### Existing tool files modified

- `tests/tools/MyDmsVn.SqlTestRunner/Program.cs` — strict `test-gate` CLI dispatch and injectable runner overload.
- `tests/tools/MyDmsVn.SqlTestRunner/Inventory/TestMethodInventory.cs` — normalized method signature in source descriptors.
- `tests/tools/MyDmsVn.SqlTestRunner/Inventory/VstestDiscoveryParser.cs` — non-secret physical row locator.
- `tests/tools/MyDmsVn.SqlTestRunner/Trx/TrxResultParser.cs` — matching physical row locator.
- `tests/tools/MyDmsVn.SqlTestRunner/Benchmark/BenchmarkRunCoordinator.cs` — expose already-produced framework evidence to global reconciliation without changing baseline execution.

### New test files

- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateManifestTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Inventory/LogicalCaseIdentityTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reconciliation/LogicalCaseInventoryReconcilerTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/SqlExecutionLeaseTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateCoordinatorTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateScriptTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reporting/TestGateReportExporterTests.cs`
- `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateRepositoryContractTests.cs`

### Documentation modified after evidence

- `AGENTS.md`
- `docs/CODEX_TEST_EXECUTION_POLICY.md`
- `docs/TEST_GATE_MATRIX.md`
- `docs/TEST_STRATEGY.md`
- `docs/adr/0006-task-scoped-test-execution-and-module-projects.md`
- `docs/plans/20261016-001-task-gates-and-modular-tests.md`
- `tests/README.md`

---

### Task 1: Strict manifest model, loader, and selection

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateManifest.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateManifestLoader.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateManifestValidator.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateManifestTests.cs`

**Interfaces:**
- Produces: `TestGateManifestLoader.Load(string path) -> TestGateManifest`.
- Produces: `TestGateManifestValidator.Select(TestGateManifest manifest, string repositoryRoot, TestGateMode mode, string planId, string taskId) -> TestGateSelection`.
- Produces: `TestGateMode.Task`, `TestGateMode.Final`, `TestGateSelection.Gates`, and typed definitions for gates, build prerequisites, resources, selection, logical cases, mappings, and exemptions.

- [ ] **Step 1: Write failing strict-loader tests**

Add facts/theories named `Load_rejects_unknown_or_missing_schema_fields`, `Load_rejects_duplicate_gate_and_identity_ids`, and `Load_accepts_the_exact_v1_shape`. Assert unknown JSON members, schema other than `1`, missing required members, duplicate IDs, and unsupported enum strings throw `InvalidDataException`.

```csharp
Assert.Throws<InvalidDataException>(() => TestGateManifestLoader.Load(invalidPath));
Assert.Equal(1, TestGateManifestLoader.Load(validPath).SchemaVersion);
```

- [ ] **Step 2: Run the loader tests and verify RED**

Run: `dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --filter FullyQualifiedName~TestGateManifestTests`

Expected: FAIL because the manifest types/loader do not exist.

- [ ] **Step 3: Implement the strict v1 model and loader**

Use `System.Text.Json` with case-sensitive names, unmapped-member rejection, explicit enum conversion, and post-deserialization required-field validation. Do not accept defaults for omitted required fields.

- [ ] **Step 4: Write failing semantic-selection tests**

Add tests named `Select_rejects_unknown_or_omitted_plan_task_and_empty_gate_set`, `Select_rejects_paths_outside_repository_and_missing_projects`, `Select_rejects_missing_declared_tfm_and_empty_filter`, and `Select_returns_only_exact_task_or_final_gates`. Include a canonicalized `..` escape and a link/reparse-point escape when supported; assert neither can pass containment.

```csharp
Assert.Throws<InvalidDataException>(() => TestGateManifestValidator.Select(manifest, root, mode, plan, task));
Assert.All(selection.Gates, gate => Assert.Equal("TESTGATE-T03", gate.TaskId));
```

- [ ] **Step 5: Run semantic-selection tests and verify RED**

Run the Task 1 filter. Expected: the new selection tests FAIL while the loader tests stay green.

- [ ] **Step 6: Implement `Select` and project/TFM validation**

Resolve canonical paths, require repository-relative project paths, parse the declared `TargetFramework`/`TargetFrameworks`, reject zero gates, and preserve manifest order. An explicit `UnfilteredProject` selection is valid; an omitted selection is not.

- [ ] **Step 7: Run focused tests**

Run the Task 1 filter again. Expected: PASS, zero skipped.

- [ ] **Step 8: Commit the manifest slice**

Commit: `feat(TESTGATE): add strict v1 gate manifest`

---

### Task 2: Durable logical identity and physical row binding

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/LogicalCaseIdentity.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/LogicalCaseBinder.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/TestMethodInventory.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/VstestDiscoveryParser.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Trx/TrxResultParser.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Inventory/LogicalCaseIdentityTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Inventory/SqlTestInventoryTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Trx/TrxResultParserTests.cs`

**Interfaces:**
- Consumes: manifest `LogicalCaseDefinition` and `TheoryRowDefinition` from Task 1.
- Produces: `LogicalCaseIdentity.Create(int version, string tfm, string normalizedSignature, string rowKey, IReadOnlyCollection<string> classification) -> string`.
- Produces: `LogicalCaseIdentity.NormalizeSignature(MethodInfo method) -> string` and `LogicalCaseIdentity.CreatePhysicalRowLocator(string methodName, string displayName) -> string`.
- Produces: `LogicalCaseBinder.BindSource(IReadOnlyCollection<TestMethodDescriptor> source, IReadOnlyCollection<LogicalCaseDefinition> expected, string ownerProject, string tfm) -> LogicalCaseBindingResult`.
- Produces: matching `BindDiscovery(IReadOnlyCollection<DiscoveredTestCase> discovered, ...)` and `BindExecution(IReadOnlyCollection<TrxTestResult> executed, ...)` overloads.
- Produces: `ExpectedLogicalCase`, `BoundLogicalCase`, `ExecutedLogicalCase`, and `LogicalCaseBindingResult` records used by Task 3.

- [ ] **Step 1: Write failing identity tests**

Pin that project/assembly/TRX GUID/display order do not affect the durable key; TFM, parameter type, opaque row ID, or sorted reviewed classification do. Pin equivalent category order to the same key and Facts to row key `fact`.

```csharp
Assert.Equal(keyBeforeMove, keyAfterMove);
Assert.NotEqual(net48Key, net8Key);
Assert.Equal(categoryABKey, categoryBAKey);
```

- [ ] **Step 2: Run the identity class and verify RED**

Run: `dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --filter FullyQualifiedName~LogicalCaseIdentityTests`

Expected: FAIL because identity/binder APIs do not exist.

- [ ] **Step 3: Implement normalized signatures and durable keys**

Include fully qualified declaring type, method name, generic arity, and normalized parameter types. Hash the canonical versioned payload with SHA-256 and prefix the result with `v1:`; never include physical owner data.

- [ ] **Step 4: Write failing row-binding tests**

Add `Bind_rejects_overloaded_discovery_name_as_ambiguous`, `Bind_maps_reordered_theory_rows_by_opaque_locator`, `Bind_rejects_duplicate_locator_or_row_id`, `Bind_rejects_missing_and_unexpected_rows`, and `Binder_does_not_return_raw_display_or_arguments`.

```csharp
Assert.Contains(LogicalCaseBindingFailure.AmbiguousMethod, result.Failures);
Assert.Equal(new[] { "case-a", "case-b" }, reordered.Cases.Select(item => item.RowKey).OrderBy(value => value));
Assert.DoesNotContain("secret-argument", serializedResult, StringComparison.Ordinal);
```

- [ ] **Step 5: Run row-binding tests and verify RED**

Run the `LogicalCaseIdentityTests` filter. Expected: the new binder tests FAIL while key-generation tests stay green.

- [ ] **Step 6: Extend source/discovery/TRX descriptors and implement binding**

Add normalized signature to `TestMethodDescriptor`. Add the same SHA-256 physical locator to discovered and TRX cases while retaining current T02 stable IDs for backward compatibility. Bind only when the method and row resolve exactly once.

- [ ] **Step 7: Run affected inventory/TRX tests**

Run: `dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~LogicalCaseIdentityTests|FullyQualifiedName~SqlTestInventoryTests|FullyQualifiedName~TrxResultParserTests"`

Expected: PASS, zero skipped.

- [ ] **Step 8: Commit the identity slice**

Commit: `feat(TESTGATE): add durable logical case identity`

---

### Task 3: Global reconciliation and checked-in current inventory

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reconciliation/LogicalCaseInventoryReconciler.cs`
- Create: `tests/test-gates.v1.json`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reconciliation/LogicalCaseInventoryReconcilerTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateRepositoryContractTests.cs`

**Interfaces:**
- Consumes: Task 1 selection and Task 2 bound inventories.
- Produces: `LogicalCaseInventoryReconciler.Reconcile(IReadOnlyCollection<ExpectedLogicalCase> expected, IReadOnlyCollection<BoundLogicalCase> source, IReadOnlyCollection<BoundLogicalCase> discovered, IReadOnlyCollection<ExecutedLogicalCase> executed) -> LogicalCaseReconciliation`.
- Produces: `LogicalCaseReconciliation.IsComplete` and ordered `LogicalCaseReconciliationFailure` values.

- [ ] **Step 1: Write failing reconciliation tests**

Cover exact bijection, duplicate global owner across projects, missing source/discovery/execution, extra TRX case, failed/skipped outcome, wrong TFM, classification drift, stale source fingerprint, overlapping categories, invalid exemption, and missing/ambiguous old→new mapping.

```csharp
Assert.True(complete.IsComplete);
Assert.Contains(LogicalCaseReconciliationFailure.DuplicateGlobalOwner, duplicate.Reasons);
Assert.Contains(LogicalCaseReconciliationFailure.StaleFingerprint, stale.Reasons);
```

- [ ] **Step 2: Run reconciliation tests and verify RED**

Run the `LogicalCaseInventoryReconcilerTests` filter. Expected: FAIL because the reconciler is absent.

- [ ] **Step 3: Implement multiplicity-preserving reconciliation**

Compare keys per physical project/TFM first and globally per TFM second. Return all deterministic failure reasons; do not reduce the comparison to set totals.

- [ ] **Step 4: Build the declared fixture projects**

Run Release builds for the runner tests and integration project on every TFM required by the manifest. Do not execute tests in this step.

- [ ] **Step 5: Capture bounded discovery only**

Run `--list-tests` for the runner tests on `net8.0` and integration tests on `net48` and `net8.0`. These are non-GUI discovery commands and execute no SQL test bodies. Preserve the output privately only long enough to author/review row locators.

- [ ] **Step 6: Add the reviewed v1 repository manifest**

Declare only `Task / 20261016-001 / TESTGATE-T03` and `Final / 20261016-001 / TESTGATE-T03`. Add every current source method, framework, reviewed classification, fingerprint, Fact row or opaque Theory row mapping, current physical owner, and current exemptions. Leave `identityMappings` empty unless an existing intentional key transition is evidenced.

- [ ] **Step 7: Add repository contract tests**

`Current_manifest_matches_source_and_expanded_discovery_for_every_declared_tfm` independently loads built assemblies and bounded discovery, then proves every manifest method/row resolves exactly once. Also add mutations proving a removed case, duplicate owner, category typo, missing project, missing TFM, stale fingerprint, and unknown row fail.

- [ ] **Step 8: Run Task 3 tests**

Run the reconciliation and repository-contract class filters. Expected: PASS, zero skipped, and no SQL DDL/DML.

- [ ] **Step 9: Commit global inventory**

Commit: `feat(TESTGATE): reconcile global checked-in inventory`

---

### Task 4: Cross-process SQL lease and abandoned-owner recovery

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Gates/SqlExecutionLease.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/SqlExecutionLeaseTests.cs`

**Interfaces:**
- Produces: `ISqlExecutionLeaseFactory.AcquireAsync(SqlExecutionLeaseRequest request, CancellationToken cancellationToken) -> Task<SqlExecutionLeaseAcquireResult>`.
- `SqlExecutionLeaseRequest` contains approved instance fingerprint, gate identity, private artifact root, resource timeout, and recovery callback.
- `SqlExecutionLeaseAcquireResult` distinguishes acquired, timed out, cancelled, recovered, and blocked recovery without exposing the fingerprint.
- `IAbandonedSqlRunRecovery.ReconcileAsync(AbandonedSqlRun lease, CancellationToken cancellationToken) -> Task<bool>` is injected for tests and delegates to existing ownership reconciliation in production.

- [ ] **Step 1: Write failing lock/lease tests**

Add tests for same-fingerprint contention, different-fingerprint independence, bounded wait, cancellation, lease sanitization, and release on normal disposal. Use a temporary lease store injected instead of the real local-app-data directory.

```csharp
Assert.Equal(SqlExecutionLeaseAcquireStatus.TimedOut, contended.Status);
Assert.Equal(SqlExecutionLeaseAcquireStatus.Acquired, independent.Status);
Assert.DoesNotContain(instanceFingerprint, serializedLease, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the lease class and verify RED**

Run the `SqlExecutionLeaseTests` filter. Expected: FAIL because the lease does not exist.

- [ ] **Step 3: Implement named mutex and durable local lease**

Hash the approved instance fingerprint into the mutex/lease filename, write schema-versioned lease state atomically, and retain the mutex until `DisposeAsync`. Never log the resource key, fingerprint, PID, or private artifact path.

- [ ] **Step 4: Add failing abandoned-owner tests**

Have a thread acquire a named mutex and terminate without release. Assert the next acquisition calls recovery before reporting acquired. Add corrupt/missing/mismatched lease, live previous PID, unresolved ownership, and recovery cancellation cases; all must block execution.

```csharp
Assert.Equal(new[] { "recover", "acquire" }, events);
Assert.Equal(SqlExecutionLeaseAcquireStatus.BlockedRecovery, inconclusive.Status);
```

- [ ] **Step 5: Run abandoned-owner tests and verify RED**

Run the `SqlExecutionLeaseTests` filter. Expected: new abandoned-owner cases FAIL while normal lock cases stay green.

- [ ] **Step 6: Implement recovery-only acquisition**

On `AbandonedMutexException`, validate the durable lease, verify the prior owner is gone, call the injected ownership recovery, mark recovered atomically, then permit acquisition. Do not delete or infer ownership of an unverified database.

- [ ] **Step 7: Run focused lease tests**

Run the Task 4 filter. Expected: PASS, zero skipped.

- [ ] **Step 8: Commit the lease slice**

Commit: `feat(TESTGATE): serialize SQL gates across processes`

---

### Task 5: Task-mode orchestration, CLI, wrapper, and sanitized evidence

**Files:**
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateCoordinator.cs`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reporting/TestGateReportExporter.cs`
- Create: `tests/tools/Invoke-TestGate.ps1`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Program.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateCoordinatorTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateScriptTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reporting/TestGateReportExporterTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/ProgramTests.cs`

**Interfaces:**
- Produces: `TestGateCoordinator.RunAsync(TestGateRunOptions options, CancellationToken cancellationToken) -> Task<TestGateRunResult>`.
- `TestGateRunOptions` contains repository, manifest, mode, plan/task IDs, artifact root, optional approval, and optional revision.
- `TestGateRunResult.Status` is `Succeeded`, `ResourceDenied`, or `Incomplete`; `Program` maps these to `0`, `2`, or `3` respectively.
- Produces: `ITestGateReportExporter.ExportAsync(TestGateReportEvidence evidence, CancellationToken cancellationToken) -> Task<TestGateReportExportResult>`.
- CLI exit contract: success `0`, preflight/resource denial `2`, valid incomplete gate `3`, usage error `64`.

- [ ] **Step 1: Write failing Task coordinator tests**

Pin ordered prerequisites, exact manifest project/TFM, explicit unfiltered/filtered selection, bounded `--list-tests`, unique TRX, real exit propagation, and source/discovery/TRX reconciliation. Add empty/unexpected discovery, missing/partial TRX, timeout, failed/skipped case, and unsupported selection tests.

```csharp
Assert.Equal(new[] { "build", "discover", "execute", "reconcile", "report" }, events);
Assert.Equal(TestGateRunStatus.Incomplete, partialTrx.Status);
Assert.DoesNotContain(".sln", recordedCommands.Single().Arguments, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run coordinator tests and verify RED**

Run the `TestGateCoordinatorTests` filter. Expected: FAIL because orchestration is absent.

- [ ] **Step 3: Implement non-SQL Task execution**

Use `OwnedProcessRunner` for build, discovery, and execution. Create one private artifact directory per gate/TFM, parse the complete TRX, reconcile logical keys, stop on the first unsafe/incomplete gate, and never substitute a solution command.

- [ ] **Step 4: Write failing strict CLI/wrapper tests**

Pin exact `test-gate` arguments, duplicate/missing/unknown option rejection, omitted plan/task, Task without approval, Final requiring approval/revision, and exit-code propagation. Pin that PowerShell passes no project/TFM/filter/category and that Final checks clean Git plus exact HEAD.

- [ ] **Step 5: Run CLI/wrapper tests and verify RED**

Run filters for `TestGateScriptTests` and the new `ProgramTests`. Expected: FAIL because the command and wrapper are absent.

- [ ] **Step 6: Implement the strict CLI and wrapper**

Add the `test-gate` dispatch, reject duplicate/unknown/incomplete options before coordinator creation, map `TestGateRunStatus` exactly, and keep PowerShell limited to repository, manifest identity, artifacts, approval, and revision.

- [ ] **Step 7: Write failing sanitized report tests**

Require schema/identity versions, plan/task/mode/gate, revision, manifest fingerprint, relative logical labels, counts, duration, and outcome flags. Assert serialized output excludes supplied secrets, absolute paths, approval/fingerprint values, PID, database name, raw names/arguments/locators, and GUIDs.

```csharp
Assert.Contains("\"identityVersion\": 1", json, StringComparison.Ordinal);
Assert.DoesNotContain(secretConnectionString, json, StringComparison.Ordinal);
Assert.DoesNotContain(privateArtifactPath, json, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 8: Run report tests and verify RED**

Run the `TestGateReportExporterTests` filter. Expected: FAIL because the exporter is absent.

- [ ] **Step 9: Implement sanitized aggregate export**

Serialize only the spec-approved aggregate fields, scan the rendered JSON against known private inputs before atomic publication, and fail the gate if expected report creation or sanitization fails.

- [ ] **Step 10: Run affected Task/CLI/report tests**

Run filters for `TestGateCoordinatorTests`, `TestGateScriptTests`, `TestGateReportExporterTests`, and `ProgramTests`. Expected: PASS, zero skipped.

- [ ] **Step 11: Commit Task orchestration**

Commit: `feat(TESTGATE): run manifest-owned task gates`

---

### Task 6: Final-mode integration with T02 strict acceptance

**Files:**
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateCoordinator.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Benchmark/BenchmarkRunCoordinator.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Program.cs`
- Modify: `tests/test-gates.v1.json`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Gates/TestGateCoordinatorTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Benchmark/BenchmarkRunCoordinatorTests.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Acceptance/PrAcceptanceRunTests.cs`

**Interfaces:**
- Consumes: Task 4 `ISqlExecutionLeaseFactory` and existing `BenchmarkRunCoordinator`.
- Produces: Final `TestGateRunResult` containing runner regression and per-TFM strict SQL aggregate evidence.
- Preserves: `BenchmarkRunOptions` profile `WarmupPasses=0`, `MeasuredPasses=1`, frameworks exactly `net48`, then `net8.0` for `pr-acceptance`.

- [ ] **Step 1: Write failing Final orchestration tests**

Assert approval is loaded before lock-key derivation, lease acquisition precedes SQL preflight, T02 coordinator runs once, both TFMs remain sequential, global logical reconciliation consumes its discovery/TRX evidence, and release occurs only after ownership/report completion.

```csharp
Assert.Equal(
    new[] { "approval", "lease", "preflight-net48", "run-net48", "preflight-net8.0", "run-net8.0", "ownership", "reconcile", "report", "release" },
    events);
```

- [ ] **Step 2: Add failure-order tests**

Pin absent/expired approval, contention timeout, abandoned recovery denial, SQL preflight denial, process timeout/cancellation, ownership failure, report failure, and reconciliation mismatch. Assert no later gate starts and cancellation does not release the lease before cleanup finishes.

```csharp
Assert.Equal(TestGateRunStatus.ResourceDenied, absentApproval.Status);
Assert.True(events.IndexOf("cleanup") < events.IndexOf("release"));
Assert.DoesNotContain("run-net8.0", events);
```

- [ ] **Step 3: Run Final-focused tests and verify RED**

Run: `dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~TestGateCoordinatorTests|FullyQualifiedName~BenchmarkRunCoordinatorTests|FullyQualifiedName~PrAcceptanceRunTests"`

Expected: new Final tests FAIL; existing T02 tests remain green.

- [ ] **Step 4: Integrate Final with the shared T02 path**

Acquire one instance-wide lease, call `BenchmarkRunCoordinator` in-process with the exact T02 acceptance profile, expose its already-produced framework discovery/TRX evidence without duplicating execution, run T03 global reconciliation, export the aggregate report, then complete/dispose the lease.

- [ ] **Step 5: Prove baseline remains unchanged**

Run the Task 6 filter plus existing baseline profile assertions. Expected: PASS; no test observes `baseline` using the lease or changing warm-up/measured passes.

- [ ] **Step 6: Commit Final integration**

Commit: `feat(TESTGATE): orchestrate guarded final acceptance`

---

### Task 7: Repository contract, L2, frozen L3, and evidence documentation

**Files:**
- Modify: `AGENTS.md`
- Modify: `docs/CODEX_TEST_EXECUTION_POLICY.md`
- Modify: `docs/TEST_GATE_MATRIX.md`
- Modify: `docs/TEST_STRATEGY.md`
- Modify: `docs/adr/0006-task-scoped-test-execution-and-module-projects.md`
- Modify: `docs/plans/20261016-001-task-gates-and-modular-tests.md`
- Modify: `tests/README.md`

**Interfaces:**
- Consumes: the complete Task/Final command and evidence contract from Tasks 1–6.
- Produces: reviewed operator commands and final evidence status; T04/T05 remain proposed.

- [ ] **Step 1: Run the single planned T03 L2 gate**

Run: `dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0`

Expected: all tests pass, zero skipped. Record totals and duration. Do not run real SQL at L2 because the harness and production SQL behavior are unchanged; if implementation did change the harness, add both harness TFMs before continuing.

- [ ] **Step 2: Update operator documentation without claiming L3**

Document the exact checked-in Task/Final command, manifest ownership, failure meanings, private artifact handling, and implemented limitation boundaries. Keep T03 unchecked/not verified until L3 succeeds; keep T04/T05 proposed.

- [ ] **Step 3: Run static checks on the frozen inputs**

Run `git diff --check`, verify JSON parsing through the repository contract test, verify no CI files or production migrations changed, and inspect `git status --short`.

- [ ] **Step 4: Commit the frozen gate-affecting revision**

Commit: `docs(TESTGATE): document T03 gate operation`

- [ ] **Step 5: Execute one clean-revision local Final invocation**

With an approved non-production SQL instance and operator-managed approval file:

```powershell
$artifactRoot = '.sqlperf/testgate-t03-final-' + [guid]::NewGuid().ToString('N')
& ./tests/tools/Invoke-TestGate.ps1 `
  -Mode Final `
  -PlanId '20261016-001' `
  -TaskId 'TESTGATE-T03' `
  -ArtifactRoot $artifactRoot `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
```

Expected: one runner regression; one strict unfiltered SQL pass on `net48`, then one on `net8.0`; zero mandatory skips; exact source/discovery/TRX reconciliation; preflight, watchdog, lease/recovery, ownership, and sanitized report all complete. Raw artifacts remain local.

- [ ] **Step 6: Review evidence and update status**

Check report fields and totals against private TRX/inventory without copying raw identifiers. Confirm exactly one SQL execution per TFM, no remaining recovery artifact/owned database, and that the tested Git revision is the frozen revision. If any gate fails or a gate-affecting input changes, repair with focused tests and rerun the affected complete Final gate; never mark stale evidence green.

- [ ] **Step 7: Record evidence in documentation-only commit**

Only after verified L3, check T03 plan items and record sanitized totals, revision, command, approval/preflight/watchdog/lease/ownership outcomes, artifact-retention statement, and remaining T04/T05 status.

Commit: `docs(TESTGATE): record T03 acceptance evidence`

- [ ] **Step 8: Perform completion review**

Use `superpowers:verification-before-completion`. If the user selected or explicitly authorized subagent review, also use `superpowers:requesting-code-review`; otherwise perform a native whole-branch review. Review from the pre-T03 commit through the evidence commit, with special attention to no silent selection broadening, durable IDs, secret sanitization, and cleanup ordering.
