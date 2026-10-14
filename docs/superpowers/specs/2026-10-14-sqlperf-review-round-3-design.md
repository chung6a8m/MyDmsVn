# SQLPERF-T01 Review Round 3 Design

## Goal

Close the three third-round review findings without weakening SQL isolation, dual-TFM inventory gates, artifact privacy, or the existing rule that live SQL acceptance remains blocked without an operator approval file.

The completed baseline command must fail closed when migration coverage cannot be attributed to each executed test row, when a run leaves unresolved database ownership, or when the required sanitized performance report cannot be persisted.

## Constraints

- Keep `net48`, `net8.0`, and the existing disposable non-production SQL approval model.
- Do not reconstruct or persist raw theory values, connection strings, database names, credentials, machine paths, or server identities in shareable reports.
- Do not automatically drop a database during post-run reconciliation. Cleanup requires the existing positive ownership proof and remains an explicit recovery action.
- Raw TRX, timing JSONL, and ownership journals remain local artifacts. Shareable reports contain only sanitized identifiers and aggregates.
- All three changes require red-green regression tests before implementation.
- T01 remains open until approved-instance live SQL gates, timing overhead, warm baselines, and cleanup evidence are run on the unchanged revision.

## Root Causes

### Expanded theory rows collapse at the migration boundary

Inventory reconciliation hashes each discovered/TRX display row, but migration invocation records currently use `CallerMemberName` or `nameof`, so every row of one theory emits the same `TestCaseId`. The manifest reconciler multiplies an expected method count by executed rows and then compares one method-level multiset. One row can therefore emit another row's missing invocation without detection.

The current migration theory also contains connection strings whose VSTest display names are truncated. Reconstructing that display inside the test would duplicate runner formatting and risk retaining sensitive values.

### Process completion is not ownership completion

The coordinator gates process exit, TRX, inventory, and timing artifacts but never reads the durable ownership journal after a child exits. A successful or terminated child can leave `CreateIntent`, `Created`, `DropIntent`, `CleanupFailed`, or `Quarantined` records and a live database while the benchmark remains accepted.

### Reporting code is disconnected from the command

Timing reconciliation and report models exist only as unit-tested library code. `baseline` does not retain execution duration/run identity in a reporting input, parse accepted TRX/timing artifacts, or persist a required report. Consequently a complete command can exit zero without the evidence T02 needs.

## Design

### 1. Row-scoped migration coverage

Migration artifact reconciliation will derive expected keys from each executed `TrxTestResult`, not only from an execution count. A manifest target with one executed row may continue using its reviewed method-level test case ID. A target with multiple expanded rows must use each row's stable hashed identity in both invocation and timing records; method-level records are rejected as missing/unexpected.

The existing parameterized migration guard test will be replaced by five named facts that call a shared assertion helper. Each fact has a stable, non-sensitive method identity, so the live suite no longer depends on reproducing VSTest's truncated theory display. The manifest will list the five named facts through a reviewed caller rule. Future migration theories remain safe: unless they explicitly emit the same stable row identity used by TRX, the benchmark fails closed.

Regression evidence will include two executed synthetic theory rows where row A emits two complete pairs and row B emits none. Reconciliation must reject the artifact even though aggregate counts match.

### 2. Post-run ownership gate

Each baseline invocation will create an isolated journal directory beneath its artifact root and pass it to every child through `MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY`. The coordinator will invoke a new ownership gate after every test execution, including nonzero exit and watchdog timeout, before deciding whether to return. Once a child has launched, reconciliation runs from a `finally` path with its own short bounded token so the overall benchmark deadline cannot silently bypass journal inspection.

The production gate will:

1. Read all durable journal records and select the current execution ID.
2. Open a read-only administrative session using the already approved test connection.
3. Read live ownership metadata for every selected database.
4. Accept only records whose durable state is `Disposed` and whose database is absent.
5. Classify every other record with the existing ownership reconciler where applicable, but perform no DROP.
6. Persist a sanitized JSON recovery artifact containing run ID, anonymized operation ID, durable state, live-presence flag, classification, and reason. It must not contain database/server names or connection details.
7. Return incomplete when any unresolved record exists or when journal/live inspection cannot be completed.

The gate will be dependency-injected for synthetic coordinator tests. Pure tests will cover a passed child that leaves `Created` plus a matching live database and a timed-out child that leaves creation state. Both must invoke reconciliation and make the run incomplete. A clean `Disposed`/absent record must pass.

### 3. Required per-TFM report export

`CommandResult` will carry monotonic elapsed duration, and `BenchmarkFrameworkRun` will retain its execution ID plus artifact paths. After all execution/result/ownership gates pass, the coordinator will invoke a required report exporter. Export failure or missing accepted evidence makes `BenchmarkRunResult.IsComplete` false.

The exporter will parse measured accepted runs only. For each TFM it will:

- build `BenchmarkSample` values from the execution ID, TFM, and measured wall-clock duration;
- parse matching timing JSONL and reconcile only events with the same run ID and TFM;
- parse TRX test durations, normalize display names to method identities, and aggregate sanitized per-test and per-class costs;
- emit deterministic ranked costs plus wall-clock p50/p95, measured union, overlap, residual, exclusive stage totals, accepted-run count, and `dmvStatus: unavailable` when no snapshot exists;
- write sanitized JSON and Markdown atomically beneath the artifact root.

The report file names will be stable and TFM-specific. The CLI will return exit code `0` only when the coordinator, ownership gates, and report exporter all complete successfully.

Tests will exercise the real exporter with synthetic TRX/timing files and assert that warm-up/invalid/other-TFM data is absent, ranking is deterministic, sensitive theory parameters are absent, and both JSON and Markdown files exist. A coordinator/CLI boundary test will verify that a missing or failed required report prevents a usable baseline.

## Interfaces

- `MigrationTimingArtifactReconciler` consumes executed TRX rows and produces row-scoped expected keys.
- `IBenchmarkOwnershipGate.ValidateAsync(BenchmarkOwnershipEvidence, CancellationToken)` returns a typed result with completeness and the recovery-artifact path.
- `BenchmarkOwnershipEvidence` carries repository/artifact/journal roots, approval path, execution ID, TFM, and process outcome; it never carries a shareable connection string.
- `IBenchmarkReportExporter.ExportAsync(BenchmarkReportEvidence, CancellationToken)` returns success plus generated paths.
- `BenchmarkFrameworkRun` carries `RunId`, resolved TRX/timing paths, pass kind/number, TFM, and measured command duration.

## Failure Semantics

- Ambiguous theory-row attribution: reject the pass.
- Missing or malformed journal: reject the run and write recovery evidence when possible.
- Unresolved ownership state or live database: reject the run; never drop automatically.
- Timeout/cancellation after child launch: run ownership reconciliation before returning incomplete unless the caller cancellation prevents all safe I/O; the isolated journal remains for manual recovery.
- Missing/malformed TRX or timing report input: reject report export and baseline completion.
- JSON/Markdown write failure: reject baseline completion and leave raw local artifacts intact.

## Verification

- Focused red-green tests for each review finding.
- Runner synthetic/unit suite.
- Harness and migration coverage tests on `net48` and `net8.0`.
- Release locked restore/build with zero warnings/errors.
- Server.Application, architecture, and desktop regression suites on all existing target frameworks.
- Independent final code review before push.
- Approved live SQL acceptance remains explicitly blocked unless the operator supplies a valid approval file.
