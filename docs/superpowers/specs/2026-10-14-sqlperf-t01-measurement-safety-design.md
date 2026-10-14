# SQLPERF-T01 Measurement and Safety Design

**Status:** Approved in-chat design, pending written-spec review  
**Date:** 2026-10-14  
**Implements:** `docs/plans/20261014-001-sql-integration-test-performance-optimization.md`, SQLPERF-T01 only

## 1. Purpose

SQLPERF-T01 establishes trustworthy, repeatable measurements for the SQL integration suite before any performance optimization changes test behavior. It also closes the existing safety gap in which direct `dotnet test`, IDE, or VSTest execution can reach database DDL after validating only a database name and a connection-string catalog.

The implementation is test infrastructure only. It must not change production migrations, schema, authorization, transaction semantics, target frameworks, or business behavior. It must preserve sequential full-suite execution and real SQL isolation.

## 2. Success criteria

T01 is complete only when all criteria in the source plan's Gate T01 are supported by recorded evidence. In particular:

- Database DDL fails closed unless the connected SQL instance matches a separately maintained non-production approval and the effective database is `master`.
- The DDL guard applies inside the test harness, including direct test execution without the benchmark runner.
- A durable ownership intent is flushed before `CREATE DATABASE`; ambiguous ownership is quarantined and never auto-deleted.
- A source/discovery/TRX-derived SQL test inventory reconciles every mandatory case for both TFMs without relying on SQL traits or hard-coded counts.
- Test-harness stage timings have explicit attribution and do not double-count nested or overlapping work.
- Every migration invocation in the four audited high-cost classes has a matching timing event or coverage is reported incomplete.
- External commands are bounded by a process-tree watchdog, and timeouts, missing TRX, skipped mandatory SQL tests, or incomplete reconciliation fail the run.
- On an approved environment, each TFM has one labeled warm-up and at least three valid warm baseline passes. Missing SQL, approval, DMV access, targeting packs, or other mandatory environment support is reported as blocked, never passed.

The four audited classes are `SchemaCompatibilityIntegrationTests`, `GoodsReceiptCommandIntegrationTests`, `DatabaseMigrationTests`, and `SqlPrincipalProvisioningTests`, matching the source plan's measured baseline.

## 3. Architecture

### 3.1 Test-harness core

Add a test-only library shared by the integration assembly and benchmark runner. It targets an API surface compatible with both `net48` and `net8.0` test hosts and owns these responsibilities:

- Parse operator approval configuration and normalize an expected instance fingerprint.
- Connect to the supplied administrator connection, verify effective `DB_NAME()`, actual server identity, endpoint metadata where available, SQL version, operator non-production attestation, required server permissions, and approval expiry/configuration integrity.
- Guard each create, alter, and drop boundary by revalidating the live connection and approval. A runner-provided flag is never sufficient authorization.
- Persist and reconcile database ownership state.
- Emit structured timing records through an explicit measurement context passed by the caller.
- Redact sensitive values before any diagnostic can enter shareable output.

The core remains under `tests/`; production projects do not reference it.

### 3.2 Operator approval

`MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` remains the only SQL connection input and must explicitly target `master`. A second environment variable points to an operator-owned JSON approval file outside tracked source. The approval contains:

- schema version and approval identifier;
- explicit `nonProductionAttestation: true`;
- expected canonical identity fields returned by SQL Server;
- expected endpoint identity when SQL exposes it;
- optional validity window and operator note;
- a fingerprint derived from the canonical identity, not from the connection string.

No command auto-enrolls or writes an approval from the supplied connection string. Missing, malformed, expired, ambiguous, or mismatched approval fails closed. Published diagnostics contain only approval ID, a shortened fingerprint, SQL major version, and redacted failure category.

Preflight distinguishes invalid configuration, connection failure, identity mismatch, wrong effective database, unreadable identity, and missing permissions. Required DMV permission is version-aware: SQL Server 2017 uses `VIEW SERVER STATE`; newer versions are checked for their applicable equivalent. The harness reports missing permission and does not grant it.

### 3.3 Mutation guard and ownership journal

`SqlTestDatabase.CreateAsync` validates approval on its real open administrator connection before any DDL. It then creates a database identity consisting of `RunId`, operation/test identity, generated database name, generated ownership token, approved-instance fingerprint, and timestamp.

The journal uses an operator-local ignored directory, one atomically replaced state document per database, a process-safe lock, write-through/flush semantics, and explicit transitions:

```text
create-intent -> created -> drop-intent -> disposed
                         \-> cleanup-failed
```

`create-intent` must be durable before `CREATE DATABASE`. After creation, the harness writes a database-level ownership marker and records live creation metadata before transitioning to `created`. If SQL creation succeeds but the marker or durable transition cannot be confirmed, the database is ambiguous and quarantined.

Before `ALTER DATABASE` or `DROP DATABASE`, the harness obtains a fresh approved connection and corroborates all of the following: approved instance fingerprint, journal identity, exact database name, ownership token/marker, live database identity/creation metadata, and exclusive ownership by the current cleanup operation. Name format, GUID, journal presence, or prefix alone never authorize deletion. Wrong-server, partial-journal, stale, conflicting, active-other-run, and unowned cases perform no destructive SQL.

Recovery classifies entries as absent, positively owned, ambiguous, unowned, or active in another run. Only positively corroborated, exclusively owned entries may be automatically cleaned. All others remain recorded for operator review without `DROP`, `ALTER`, or session killing.

### 3.4 Measurement context and event model

Timing is opt-in through `MYDMSVN_SQL_TEST_TIMINGS=1`. A test creates an explicit immutable measurement context containing `RunId`, stable `TestCaseId`, TFM, and anonymized database ID. The context is passed to database lifecycle and migration helpers, including across `await`; no thread ID, static mutable current-test value, or ambient transaction is used.

Each operation records:

- `RunId`, `TestCaseId`, `OperationId`, and optional `ParentOperationId`;
- `Stage` (`database-create`, `migration`, `seed`, `permission-sync`, `pool-cleanup`, or `database-drop`);
- monotonic start/end ticks, `DurationMs`, `Outcome`, TFM, and anonymized database ID.

Event writing is concurrency-safe and flushes records required for failure diagnosis. It never contains connection strings, credentials, raw host names, business data, passwords, hashes, or theory parameter values.

TRX duration remains the authoritative inclusive test duration. Reporting computes exclusive stage time from parent/child intervals, labels overlapping intervals, and reports residual/unclassified time separately. It never adds nested stage totals to TRX or wall-clock. Direct test-body timing is omitted unless it can be captured without invasive xUnit hooks; its time remains visible through TRX residual.

### 3.5 Migration coverage

Add a centralized test-only wrapper around the existing production `DatabaseMigrationRunner`; the wrapper only times and delegates. Direct DbUp builders, repeat calls, expected failures, interrupted-lock paths, and bespoke migration paths in the four audited classes receive explicit timing scopes without changing production migration behavior.

A source-controlled invocation manifest identifies each audited call site by exact test method, source location, TFM applicability, and invocation multiplicity rule. Runtime reporting reconciles migration invocation IDs against emitted migration events per test and TFM. Missing, duplicate, or unmatched events mark coverage incomplete. Expected-throw and cancellation paths emit their actual outcome. A negative test proves an intentionally uninstrumented invocation is rejected by reconciliation.

### 3.6 Independent SQL test inventory

The inventory does not consume `SqlServerFact`, `FastSQL`, or future SQL category traits. It is built from three independent views:

1. Test methods reflected from the compiled integration test assembly.
2. Expanded cases returned by VSTest/xUnit discovery for the current TFM.
3. Actual cases in the completed TRX.

Every discovered `[Fact]` or `[Theory]` method/case defaults to SQL-required. A source-controlled manifest can exempt only an exact method, with rationale, evidence, and a method-body fingerprint. No class or namespace wildcard is allowed. A changed fingerprint makes the exemption stale. A direct/transitive call-graph audit rejects exemptions that reach known SQL connection, disposable database, migration, provisioning, or SQL fixture entry points.

Stable identities use fully qualified method identity plus an opaque hash of the normalized discovery row identity. Reports expose row ordinals/hashes, not parameter values. Deferred theory discovery is accepted only when method-level and executed-row counts remain reconcilable; otherwise the run is incomplete.

After each unfiltered TFM run, the runner requires real exit code zero, a complete TRX, zero failed cases, zero skipped mandatory SQL cases, and one-to-one reconciliation among source methods, discovery, mandatory inventory, and execution. Counts are derived dynamically for the current commit and TFM.

### 3.7 Benchmark runner and watchdog

Place a thin PowerShell entry point under `tests/tools/`. Process control, TRX parsing, inventory reconciliation, reporting, and redaction live in a testable runner component rather than ad hoc shell expressions.

The runner:

- records Git SHA, TFM, SDK, approved SQL fingerprint, relevant sanitized environment switches, and operator-supplied machine/load notes;
- verifies the SQL environment before launching any test that can issue DDL;
- passes connection and approval configuration to the child testhost and verifies their presence there;
- supports per-command and hard overall deadlines;
- drains stdout and stderr concurrently and waits for the true process exit code;
- tracks only its owned process tree and terminates that tree on timeout;
- preserves partial logs/TRX and marks the run failed/incomplete;
- reopens the journal after abnormal termination and performs only freshly authorized, positively corroborated cleanup;
- runs frameworks sequentially and writes unique artifacts for every warm-up or measured pass.

Restore and build are performed once per revision outside timed test runs. The measured protocol alternates TFM order across comparable passes where practical. Warm-ups, fixed intentional delays, and invalid runs are separately labeled and excluded from medians.

### 3.8 Reports

Raw artifacts remain in ignored local directories. A sanitized evidence report contains:

- commit, TFM, SDK, shortened approved-instance fingerprint, and sanitized environment context;
- discovered, mandatory SQL, executed, failed, skipped, and reconciled counts;
- process wall-clock and TRX totals;
- class/test count, total, p50/p95, and contribution;
- inclusive/exclusive stage breakdown, overlap, and residual;
- migration expected/observed coverage;
- watchdog, journal reconciliation, cleanup, and quarantine outcomes;
- DMV metrics when available, explicitly `unavailable` otherwise;
- warm-up/measurement label and validity decision.

No single run is used to claim a baseline or improvement. T01 records at least three valid warm baseline runs per TFM before any later optimization task uses them.

## 4. Failure behavior

All safety and completeness failures are fail-closed:

- Approval, identity, database, or permission failure prevents DDL.
- Journal persistence failure prevents `CREATE DATABASE`.
- Corroboration failure prevents cleanup and creates a quarantine record.
- Timeout, nonzero exit, missing/incomplete TRX, failed test, skipped mandatory SQL case, inventory mismatch, or migration-event mismatch invalidates the run.
- Missing environment capabilities are reported as blocked with the exact category; they never become a skipped-success result.

The runner never grants SQL permissions, kills unrelated SQL sessions, deletes by name pattern, or exposes secrets to diagnostics.

## 5. Test strategy

Implementation follows red-green-refactor. Pure tests use fakes at the administrative-command boundary and assert that zero destructive commands are issued for missing/revoked approval, mismatched identity, wrong effective database, unreadable identity, direct-run bypass, and wrong-server disposal.

Additional automated coverage includes:

- approval parsing, canonical fingerprint comparison, expiry, and redaction;
- durable write/flush transitions, partial/interrupted writes, idempotent replay, and concurrent writers;
- crash windows before create, after create, before marker, and before durable `created`;
- unowned similarly named databases, conflicting journals, overlapping active runs, and quarantine;
- explicit timing context propagation, nested/overlapping exclusive-time calculation, failure/cancellation outcomes, and timing-disabled behavior;
- migration expected-success, expected-failure, repeated calls, and missing-event detection;
- fact/theory discovery, expanded row identity, deferred discovery, new unknown method defaulting to SQL, stale exemptions, and transitive SQL access;
- complete/failed/skipped/missing TRX reconciliation and real exit-code handling;
- synthetic hung child and grandchild termination without touching unrelated processes;
- redaction of connection strings, credentials, host details, paths, and test parameters.

Approved-instance SQL tests verify the live guard, marker, journal, create/drop lifecycle, required DMV preflight, wrong-server cleanup rejection, and direct `dotnet test` behavior. They use only the explicitly approved non-production SQL instance.

Verification builds Release and runs relevant unit/integration tests for both `net48` and `net8.0`. Full baseline measurement runs sequentially and retain unique TRX files. If the required Windows, SQL, DMV, or targeting-pack environment is unavailable, those gates remain open and are reported honestly.

## 6. Planned file boundaries

Exact names may be refined in the implementation plan, but responsibilities remain separated:

- `tests/MyDmsVn.SqlTestHarness/`: approval, preflight, mutation guard, ownership journal, timing model, redaction, and reconciliation primitives.
- `tests/MyDmsVn.SqlTestHarness.Tests/`: pure and synthetic-process tests, dual-targeted where the behavior runs in both testhosts.
- `tests/tools/MyDmsVn.SqlTestRunner/`: command watchdog, discovery/TRX/inventory reconciliation, reporting, and benchmark orchestration.
- `tests/tools/Invoke-SqlIntegrationBenchmark.ps1`: Windows entry point with parameter validation and no duplicated safety logic.
- `tests/tools/sql-test-nonsql-exemptions.json`: exact method-scoped non-SQL exemptions and fingerprints.
- `tests/tools/sqlperf-t01-migration-invocations.json`: audited migration call-site inventory.
- `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs`: delegate lifecycle decisions to the shared guard/journal and accept explicit measurement context.
- Four audited integration classes: pass explicit measurement context and route every migration invocation through a timed wrapper/scope.
- `.gitignore`: ignore local journals, raw benchmark artifacts, and operator approval files.
- `tests/README.md`, `docs/TEST_STRATEGY.md`, the SQLPERF plan, and a sanitized T01 evidence document: usage, safeguards, actual commands/results, open gates, and next task.

## 7. Scope exclusions

T01 does not optimize database reuse, pooling, migration frequency, intentional waits, test categorization for focused execution, or parallel execution. Those belong to SQLPERF-T02 through T05. It does not change production SQL, migrations 001-008, production permissions, P3.1 synchronization, P4.1 login, or any business feature.

## 8. Delivery sequencing

Implementation proceeds in independently reviewable vertical slices within SQLPERF-T01:

1. Approval/preflight and in-harness mutation guard.
2. Durable ownership journal, marker, recovery, and quarantine.
3. Explicit stage timing and complete audited migration coverage.
4. Independent inventory, discovery, TRX reconciliation, and exemptions.
5. Watchdog runner, reporting, and synthetic process-control tests.
6. Dual-TFM verification and, when the approved environment is available, warm-up plus three valid warm baseline passes per TFM.

No later slice may weaken an earlier safety boundary. Plan checkboxes change only after the corresponding evidence exists.
