# SQLPERF Review Round 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the five second-round PR findings without weakening dual-TFM SQL inventory, timing coverage, or report correctness.

**Architecture:** Replace ordinal theory identities with redacted content hashes, derive migration expectations independently from the reviewed manifest plus executed TRX cases, and reconcile timing per run with instance-scoped parent/child intervals. Aggregate report values only after each accepted run is reconciled independently.

**Tech Stack:** C#/.NET 8 runner, netstandard2.0 timing harness, xUnit, Newtonsoft.Json.

**Spec:** `docs/plans/20261014-001-sql-integration-test-performance-optimization.md`

## Global Constraints

- Preserve `net48`, `net8.0`, and real SQL isolation gates.
- Never expose theory parameter values or SQL connection details.
- Every behavior change follows a red-green TDD cycle.
- Live SQL acceptance remains blocked without operator approval.

## Review Focus

- Reordered theory rows reconcile; missing/duplicated rows fail closed.
- A manifest call site missing both invocation and timing records fails coverage.
- Repeated logical operation IDs across tests remain separate instances.
- Warm-up, invalid, other-TFM, and other-run timing never enters accepted metrics.
- Cross-stage child time is subtracted from its parent exactly once.

---

### Task 1: Stable theory-row identity

**Files:**
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/TheoryRowIdentity.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Inventory/VstestDiscoveryParser.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Trx/TrxResultParser.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reconciliation/SqlTestInventoryReconcilerTests.cs`

**Interfaces:**
- Produces: `TheoryRowIdentity.Create(string methodName, string tfm, string displayName)`.

- [x] Add A/B versus A/A, reordered-row, privacy, and deferred-discovery negative tests.
- [x] Run the focused tests and observe the ordinal implementation fail.
- [x] Hash normalized row display content and reject duplicate discovery identities.
- [x] Run the focused inventory/reconciliation tests green.

### Task 2: Independent migration expectations

**Files:**
- Modify: `tests/tools/sqlperf-t01-migration-invocations.json`
- Create: `tests/tools/MyDmsVn.SqlTestRunner/Reconciliation/MigrationInvocationManifest.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Reconciliation/MigrationInvocationManifestAuditor.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Reconciliation/MigrationTimingArtifactReconciler.cs`
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Benchmark/BenchmarkRunCoordinator.cs`
- Test: runner reconciliation tests.

**Interfaces:**
- Consumes: executed `TrxTestResult` rows, TFM, manifest, invocation/timing JSONL.
- Produces: exact expected multiset keyed by test method, TFM, and invocation ID.

- [x] Add a negative test with two manifest expectations and one entire invocation/timing pair absent.
- [x] Run it red.
- [x] Add explicit helper caller/count expectations and reconcile manifest → invocation records → timing events.
- [x] Verify helper repetitions, failure paths, and both TFMs green.

### Task 3: Instance-scoped timing reconciliation

**Files:**
- Modify: `tests/MyDmsVn.SqlTestHarness/Timing/TimingIntervalReconciler.cs`
- Test: `tests/MyDmsVn.SqlTestHarness.Tests/Timing/TimingIntervalReconcilerTests.cs`

**Interfaces:**
- Produces: per-logical-operation and per-stage exclusive duration after instance-scoped graph reconciliation.

- [x] Add duplicate OperationId tests across test cases and nested cross-stage tests.
- [x] Run them red.
- [x] Scope parent lookup by run/TFM/test and keep multiple operation instances.
- [x] Run both harness TFMs green.

### Task 4: Per-run report aggregation

**Files:**
- Modify: `tests/tools/MyDmsVn.SqlTestRunner/Reporting/SqlPerformanceReportBuilder.cs`
- Test: `tests/tools/MyDmsVn.SqlTestRunner.Tests/Reporting/SqlPerformanceReportBuilderTests.cs`

**Interfaces:**
- Consumes: accepted `BenchmarkSample` keyed by `RunId` + `Tfm` and matching timing events.
- Produces: aggregate totals from independently reconciled accepted runs.

- [x] Add warm-up/measured/invalid/multi-TFM filtering and 40/60 parent-child exclusive tests.
- [x] Run them red.
- [x] Reconcile each accepted run against its own wall clock, then sum metrics/stage exclusive values.
- [x] Run runner tests green.

### Task 5: Verification and review closure

**Files:**
- Modify: `docs/SQLPERF_T01_VERIFICATION.md`

- [x] Run locked restore and Release solution build.
- [x] Run harness and migration coverage on `net48`/`net8.0`, runner tests, and application/architecture/desktop regressions.
- [x] Request independent code review, fix any Critical/Important issues, commit, and push.
- [x] Reply to and resolve each round-2 inline thread with exact regression evidence.
