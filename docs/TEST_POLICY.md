# Test policy — v1a

**Effective:** 2026-10-18. **Authority:** the owner's decision to rebuild the test policy around the two active plans, RESP and L2DB. This replaces `CODEX_TEST_EXECUTION_POLICY.md`, `TEST_GATE_MATRIX.md` and their duplicated execution rules. [TEST_STRATEGY](TEST_STRATEGY.md) owns coverage; [tests/README](../tests/README.md) owns actual command capabilities.

## 1. Purpose

Provide useful feedback quickly while proving the behavior that a change can affect. Reuse migrated SQL fixtures where their state can be reset completely. Validate shared infrastructure at the appropriate integration milestone. A green focused run proves its selected scope; it does not prove an unexecuted module or platform.

The policy is effective for selecting work now. Respawn and any runner changes are **planned implementation**, not delivered by this document. Until their tasks are accepted, the checked-in tools retain their current constraints.

## 2. Active platform and resources

| Project family | Active target / environment |
|---|---|
| Product, application/architecture tests, SQL module tests, Desktop/session tests, harness tests | `net48`, Windows with .NET Framework 4.8 targeting/runtime support |
| Shared `MyDmsVn.SqlTestHarness` library | `netstandard2.0`, consumed by net48 tests; not a test execution target |
| SQL runner and runner tests | `net8.0`, tooling only |
| Product `net8.0` / GUI `net8.0-windows`, AppCore | PARKED / NOT RUN; no reactivation under RESP or L2DB |

Run database tests against the approved local test environment. No new CI, duplicate CI SQL suite or cloud database deployment is included in this roadmap. Unavailable Windows, SQL approval, targeting pack or GUI session is recorded as BLOCKED / NOT RUN.

## 3. Choose scope from the change

Before executing, record the changed behavior, affected dependencies, projects/TFMs and selected cases. Use the smallest runnable scope that covers those risks; expand when shared dependencies make that scope insufficient.

| Change | Required verification | Broaden when |
|---|---|---|
| Markdown/documentation only | Diff, links, commands against source, roadmap/status consistency, encoding | A script, manifest, source file or executable configuration also changed |
| Pure domain/application logic | Focused behavior/validation/authorization cases and affected build | Shared pipeline, contract or actor/session semantics affect other callers |
| One repository or query module | Affected module's real-SQL CRUD/projection/transaction/error cases plus relevant Application tests | Shared UoW, mapping conventions, provider configuration or cross-module transactions changed |
| UoW, DAL provider, connection ownership or shared mappings | Foundation and every affected SQL module, transaction/rollback and relevant concurrency cases | Final L2DB cutover requires all active SQL projects |
| Fixture/reset/seed, SQL ownership, runner/discovery or lifecycle accounting | Focused harness/runner checks and real-SQL lifecycle/failure cases during work | RESP acceptance requires all active SQL projects and same-case fresh/reuse comparison |
| Migration/schema/security principal changes | Dedicated fresh-database migration, schema and privilege tests, plus all affected consumers | Shared schema or schema compatibility affects every SQL module |
| Pure ViewModel/session logic | Focused non-GUI tests, fake-clock/actor cases where relevant | Changes touch actual controls, binding, focus, STA or host composition |
| WinForms/host composition/release package | Relevant guarded GUI/STA checks and packaged smoke when required by the plan | An approved release milestone requires the broader shipped-host matrix |

Do not automatically run `dotnet test MyDmsVn.sln`, full SQL, full GUI or a full repository regression after an edit, every task, or every code PR. Do not replace meaningful real-SQL behavior with mocks to avoid SQL execution. A separately mergeable change still needs the verification required by its own affected scope.

## 4. Acceptance for the two active plans

**RESP acceptance:** execute the complete active SQL project inventory on `net48`, plus affected harness/runner tests. Prove that accepted eligible tests default to reuse and the explicit exceptions still use fresh databases. Keep an explicit fresh mode for controlled comparisons or deliberately selected fallback runs. Include no-contamination, journal preservation, seed/trigger integrity, failed initialization, orphan cleanup and real multi-session semantics. Preserve source/discovered/executed case identity, including Theory rows; fixture sharing must not shrink or duplicate coverage.

The planned comparison uses the same frozen candidate and same logical cases once in explicit test-only `fresh` mode and once in `reuse` mode. Report allocation/migration/reset/seed/drop counts and wall-clock observations. A single matched pair is an observation, not a statistical guarantee; a speedup claim needs actual measured improvement. Repeat only to investigate an invalid run, regression or a separately requested benchmark. Do not import old SQLPERF thresholds or mandatory warm-up-plus-three-pass protocols into routine acceptance.

**L2DB acceptance:** use the accepted RESP fixture baseline. Require all active SQL projects, affected Application/architecture/contract coverage, and the plan's net48 package/host smoke. Preserve rollback, rowversion, locking, permission and stored-procedure semantics. This final breadth is justified by the shared persistence replacement; individual module edits use focused checks.

Acceptance evidence belongs to an identified candidate revision and actual scope. A later relevant code/test/schema/harness/build change invalidates the evidence it can affect; rerun that scope. A documentation correction alone does not invalidate unrelated runtime evidence. Never label missing, skipped, timed-out, aborted or unreconciled mandatory cases as passed.

## 5. Reusable database contract

The [RESP plan](plans/20261018-002-respawn-fixture-reuse.md) owns implementation details and [ADR 0010](adr/0010-respawn-fixture-reuse-and-test-policy.md) records the decision.

- One reusable fixture owns a uniquely allocated disposable database, keyed to the run, test assembly, active TFM and collection. It never adopts an existing arbitrary database or shares a mutable database across processes.
- Allocate and migrate once per fixture. Create its Respawn checkpoint only after schema creation. Reset and seed before every test, including the first; do not depend only on successful previous teardown. Bind the actual method, reviewed Theory row and attempted execution before reset/seed, and reject missing or ambiguous identity.
- All tests sharing a fixture join the same xUnit v2 collection. Keep current SQL assembly guards and cross-process SQL leases. RESP changes reuse, not runner parallelism. Pure resource-independent tests retain their normal behavior; GUI resources remain serialized.
- Reset all mutable business, reference, assignment, binding and audit rows. Preserve only the verified DbUp journal and ownership metadata. The checkpoint's table/schema boundary is explicit and revalidated; do not ignore a difficult table to obtain a green run.
- The full schema contains an append-only StockLedger trigger. A narrowly scoped, test-only reset profile must restore and verify it before any test body, even when the current Catalog test left the ledger empty. Otherwise the fixture is ineligible for reuse. Never change production scripts/trigger behavior to help reset.
- Deterministic seed belongs to test infrastructure and runs after reset. Reset a sequence only through the explicitly owned test profile; do not expect rowversion, time or arbitrary database state to rewind. Assertions use returned IDs/tokens unless a tested identity contract requires fixed values.
- Test readers, transactions, connections, application scopes, timers and concurrency workers are drained/disposed before the next reset. Reusing the database does not reuse a business transaction or application UoW between tests.
- Reset/seed/schema verification failure poisons the fixture. The current test initialization fails and later bodies cannot execute on uncertain state. No silent fresh-database retry, ignored exception or skip-to-pass behavior.
- Collection teardown drops only the positively owned allocation. Existing owned-process termination and ownership reconciliation handle interrupted runs; uncertain ownership is quarantined.

Fresh databases remain required for migration/history/schema changes, SQL principals/permissions, destructive lifecycle probes and tests whose changes cannot be fully restored. A `SqlConcurrency` category alone neither permits nor forbids reuse: preserve the scenario's separate sessions and its explicitly reviewed reset boundary. The initial RESP plan conservatively retains fresh allocation for its listed command/race/fault exceptions.

## 6. SQL and GUI execution controls

SQL mutation requires `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective database `master` plus valid operator-maintained `MYDMSVN_SQL_TEST_APPROVAL_FILE`. Validate the approved instance before creating, resetting or dropping owned test databases. Preserve ownership journals/markers, global resource leases, finite timeouts, process-tree watchdogs and bounded reconciliation. Do not print the configuration values or fabricate approval records.

Respawn 4's reset method has no cancellation-token overload. Set a finite reset command timeout, retain an outer process watchdog, and drain/terminate the owned operation before releasing fixture ownership. A timed-out `Task.WhenAny` alone does not stop SQL work.

WinForms execution uses STA, serialized GUI resources, deterministic disposal and no unattended modal exception/DataError/dialog paths. Focused STA checks are not a claim of attended packaged-user acceptance. Do not launch GUI suites for unrelated Markdown or DAL-only edits unless the active plan names a meaningful host/package check.

## 7. Transition from existing tooling

The reset does not remove code, package references, existing assertions, SQL scripts, `tests/test-gates.v1.json` or runner inventories. Existing wrappers still enforce their own clean-revision, manifest, fingerprint, source/discovery/TRX and ownership checks. **A policy change is not permission to bypass a tool failure.**

- The current focused SQL wrapper selects one project and one responsibility category, not an arbitrary method filter. Always pass `-Frameworks @('net48')`; its default still mentions the parked modern target.
- Current manifest Task/Final accepts only registered exact IDs. RESP/L2DB task registrations do not exist at the assessment revision. Their owning implementation tasks must add/review the necessary entries and lifecycle metadata before claiming those commands work.
- `Final -PreflightOnly` validates metadata; it does not execute SQL or provide acceptance evidence. Historical Final IDs remain tool configuration, not active roadmap instructions.
- Moving migration into fixture setup changes runner attribution. Adapt lifecycle/inventory/timing reconciliation with tests before moving callers; never relabel one fixture migration as many test migrations or erase mandatory cases to satisfy the old accounting.
- The legacy full Final envelope and multi-pass benchmark remain callable. This policy no longer requires them for every code PR. Use a current guarded scope that meets the selected requirement, or implement the needed guarded support in the active task; report blocked if no valid path exists.

The RESP plan owns the minimal changes needed for fixture-aware guarded execution. General manifest-v2 redesign and further tooling simplification stay deferred.

## 8. Evidence record

Each task or PR reports:

- Task/revision and behavior changed.
- Projects, active TFMs, selectors/case scope and why that scope is sufficient.
- Exact commands actually run; discovered/executed/passed/failed/skipped counts and duration where useful.
- SQL approval outcome, allocation/reset/seed/cleanup outcome and watchdog result where applicable.
- Checks not run, their reason, and any acceptance blocker.

Keep credentials, approval contents, raw TRX, theory data, ownership journals and instance-identifying diagnostics local. Publish reviewed sanitized summaries only. Do not manufacture performance results or reuse another revision's test totals as current evidence.
