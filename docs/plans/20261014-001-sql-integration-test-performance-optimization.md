# SQLPERF — SQL integration test performance optimization (post PR #18)

**Status:** Planned / not started (2026-10-14). **Type:** cross-phase test-infrastructure hardening, not a new business feature or a change to completed P3.1. **Base:** `main` after merged PR #18 (code baseline `130ab55787a2718cb590754662715393e094dade`; merge commit `810136c7aca65c2970194d8a6de87cf68353d53f`). **Target:** Windows, SQL Server 2017+, `net48` and `net8.0`. May proceed independently of P4.1, but must not delay or weaken P4.1/P5/P6 correctness gates.

Read before implementation: `AGENTS.md`, `docs/DEVELOPMENT_PLAN.md`, `docs/TEST_STRATEGY.md`, `docs/P3_1_VERIFICATION.md`, `docs/PERSISTENCE.md`, `docs/SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md`, this plan and relevant test source. Treat the 2026-10-14 operator diagnostics as an **observed local baseline**, not a guaranteed CI/runtime benchmark; its shareable ZIP and TRX were reviewed outside the repository.

## 1. Objective and scope

Reduce end-to-end duration of the SQL integration suite **without** reducing security, schema/migration, identity, inventory, race-condition, cleanup or two-TFM coverage. Diagnose first, then optimize one mechanism at a time with measured A/B evidence. Keep all production schema and SQL authorization semantics unchanged.

**Initial target (engineering goal, not an automatic release gate):** reduce median warm-run wall-clock by **20–30%** on *each* framework relative to same-machine, same-SQL-instance baseline, with no loss of assertions or increase in failures. If this is unattainable without sacrificing isolation, keep safety and document the measured tradeoff. Count execution time including fixture setup/teardown; optimizing only `dotnet test` launcher overhead has limited value.

### Measured baseline: 2026-10-14, PR #18 commit

| Metric | net8.0 | net48 |
|---|---:|---:|
| Passed / failed / skipped | 199 / 0 / 0 | 199 / 0 / 0 |
| Wall-clock | 538.96s | 532.72s |
| Sum of TRX test durations | 537.39s | 528.84s |
| `SchemaCompatibilityIntegrationTests` (35 cases) | 169.07s | 166.34s |
| `GoodsReceiptCommandIntegrationTests` (25 cases) | 139.38s | 136.64s |
| `DatabaseMigrationTests` (24 cases) | 54.40s | 54.59s |
| `SqlPrincipalProvisioningTests` (12 cases) | 44.00s | 44.08s |

- The first two classes account for about **57.4%** of net8.0 test duration; the first four about **75.7%**. This is a prioritization signal, not proof of setup cost.
- `GoodsReceiptCommandIntegrationTests.Deactivation_and_post_serialize_in_both_lock_orders_for_every_reference` took about **34s** in each TFM. The test exercises three reference types × two lock orders, and its test-only SQL triggers include `WAITFOR DELAY '00:00:05'`. SQL `WAITFOR` delta was about **55.2s** per TFM. This wait is intentional synchronization overhead; it is not itself evidence of slow SQL execution.
- SQL instance wait deltas included `LCK_M_*` about 68.4s/59.2s and `RESOURCE_SEMAPHORE_QUERY_COMPILE` about 40.5s/45.4s (net8/net48). Wait deltas can overlap and may include activity external to tests: **never add them directly to test wall-clock** or treat them as definitive root cause.
- SQL process sampled working set roughly **320 MB median**, **337 MB maximum**, with configured `max server memory = 1536 MB`. These samples do **not** establish that SQL has no memory pressure; in particular, working set is not the same as SQL memory committed/target. Do not change SQL memory settings solely from this observation.
- There was **no overlapping test execution** in TRX; the observed SQL test database count maximum was one. `dotnet test` wrapper overhead was small (roughly 1.6–3.9s).
- Reproduce with actual tests on the same laptop; compare against a *fresh* unmodified baseline if SQL instance/build/test count/environment have changed since this snapshot.

## 2. Source-derived bottlenecks and safety constraints

1. `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/AssemblyInfo.cs` currently specifies `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. This was introduced to avoid local SQL overload and command timeouts when many disposable databases/migrations run in parallel. **Do not remove it as a first optimization.**
2. `SqlTestDatabase.CreateAsync` issues `CREATE DATABASE` for each isolated test setup. `SqlTestDatabase.DisposeAsync` uses `SqlConnection.ClearAllPools()`, `ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE`, then `DROP DATABASE`. Repeated DDL, migration and global pool eviction plausibly cost time, but their per-stage durations are not yet measured.
3. `DatabaseMigrationRunner.Migrate` runs DbUp with session-scoped application lock, preflight and `WithTransactionPerScript()`; SQL schema manifests, rollback and lock correctness are part of production safety. Do not bypass DbUp, remove SQL locks/preflight, or mutate numbered migration scripts to make tests faster.
4. Some tests intentionally verify real multi-session locking, connection pooling, interrupted migration, schema drift, permission approval, audit and cleanup. They **must not** share dirty databases or be rewritten as mocks in place of existing real SQL acceptance checks.
5. SQL tests must continue to accept only an explicit `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` targeting `master`, create guarded disposable `MyDmsVn_Test_<GUID>` databases, and never delete an unowned database. Preserve failure cleanup, retryability and cancellation boundaries.
6. Leave `net48` and `net8.0` supported; SQL Server 2017 syntax and Windows/.NET Framework requirements remain in scope. Do not change the production database or release application behavior solely for test speed.

## 3. Implementation plan and PR slices

### SQLPERF-T01 — Repeatable measurement and stage-level instrumentation (PR 1)

- [ ] Reproduce full Release SQL suite on both frameworks **sequentially**, capturing TRX (including test names, counts, duration), wall-clock, environment metadata, Git SHA, SQL version/configuration and sanitized diagnostics. Do not claim the original local report is reproducible evidence without a rerun. Use the existing PowerShell collector only after reviewing/correcting its process exit-code and sensitive-data handling; move a maintainable version under `tests/tools/` or `scripts/` only if justified, with script tests.
- [ ] Instrument **test harness only**, using `Stopwatch` and an opt-in flag (e.g. `MYDMSVN_SQL_TEST_TIMINGS=1`); emit structured records of `database-create`, `migration`, `seed`, `permission-sync`, `test-body` if measurable without invasive hooks, `pool-cleanup` and `database-drop`. Record test identity, TFM, unique test DB ID/name or pseudonymous run ID, outcome, duration and cleanup errors, **not** connection strings, credentials, user data or password/hash content.
- [ ] Address timing coverage: generic test body already in TRX; for helper-based setup add a centralized migrator wrapper/timer instead of scattered stopwatch copies. Capture create/drop even on failure and cancellation. Instrumentation must be thread-safe, concurrency-safe, off by default and not change transactional semantics.
- [ ] Produce class/test-level reports with count, total, p50/p95, setup/teardown breakdown and contribution to total wall-clock. Exclude/label warmup and fixed-delay time distinctly, rather than simply assigning all duration to database operations.
- [ ] Collect before/after deltas for `WAITFOR`, lock waits, `RESOURCE_SEMAPHORE_QUERY_COMPILE`, CPU, I/O and SQL memory grants/compiler memory **only when permissions permit**; report unavailable DMV fields as unavailable, do not silently write zero. SQL Server 2017 DMV access may need `VIEW SERVER STATE`; never grant additional production permissions just for a profiler.
- [ ] Persist sanitized evidence summary to `docs/` and place full raw TRX/ETL/log artifacts **outside Git** or an ignored `artifacts/` path. Publish reproducible commands and an operator checklist, not credentials or host-sensitive paths.

**Gate T01:** Both full suites still pass with the same test inventory (at that commit), duration instrumentation covers all create/migrate/drop paths involved in the top four classes, and a before-change ranked cost breakdown exists. If duration overhead from tracing exceeds 5%, quantify and correct or explicitly account for it.

### SQLPERF-T02 — Replace fixed 5s waits in SQL concurrency probes (PR 2)

- [ ] Inventory every test-only SQL `WAITFOR DELAY '00:00:05'` use (currently in `GoodsReceiptCommandIntegrationTests.cs` trigger helpers), map each to the lock owner, blocked session, signal and assertion. Preserve six lock-order scenarios across `Warehouses`, `Employees` and `Products`, plus same-receipt, StockBalances and catalog races.
- [ ] **Pilot one isolated scenario first:** use an explicit test-controlled release gate/transaction or equivalent deterministic two-session barrier. Assert the lock holder has acquired the expected SQL lock, the competing session is blocked on the **expected resource** (via observable SQL lock state), and release only after both observations. Use deadlines and guaranteed `finally` cleanup. Do not replace 5s with a shorter arbitrary `WAITFOR` or a fixed `Task.Delay`.
- [ ] Where the current trigger is required to pause execution *inside a business command*, design a test-only controllable interception/signaling seam or transaction boundary without altering production authorization, SQL schema artifacts or UoW behavior. If a scenario genuinely cannot be made deterministic without intrusive production hooks, retain its fixed delay and document the reason; do not weaken the assertion.
- [ ] Prove both lock orders, race winner/loser, idempotent posting, rollback, no orphaned trigger/table/session and post-test database disposal. Explicitly test timeout/cancellation paths to prevent hung Codex/test runners and unobserved background tasks.
- [ ] Re-run each modified concurrency test at least **20 consecutive times per TFM** under development SQL conditions (or document environment limitation), then complete both SQL suites. Verify the intended SQL blocking was actually observed on each relevant iteration and failures retain useful redacted diagnostics.

**Gate T02:** No assertion deletion or skipped cases; synchronization correctness retained with bounded cleanup and measurable reduction in affected test duration. A faster but non-deterministic race test is a failure.

### SQLPERF-T03 — Database setup, migration, teardown and pool lifetime (PR 3)

- [ ] Using T01 timings, rank `CREATE DATABASE`, migration/preflight, `ClearAllPools`, `SINGLE_USER` and `DROP DATABASE` in the top classes. Optimize only stages accounting for meaningful time.
- [ ] Evaluate pool scope: the disposable test DB connection string sets `Pooling = false`, while other explicit/migrator connections may use different pools. Determine which pools are actually active and which must be cleared to make `DROP DATABASE` reliable. Consider targeted `SqlConnection.ClearPool` for **known** pools or tracked connections instead of indiscriminate `ClearAllPools()`, but preserve all connection ownership and fail-closed cleanup.
- [ ] Prove teardown with normal queries, outstanding/open connections, pooled migration sessions, interrupted migration, disposal retry, failure after CREATE and canceled tests. No leftover `MyDmsVn_Test_*` database may be silently ignored. Never use `DROP DATABASE` on a name not generated and owned by the harness.
- [ ] Consider avoiding duplicate migrations **within one test** only where verified no-op semantics are not under test. Migration/preflight correctness tests must still exercise the production path, including all eight scripts as shipped. Do not replace migration with `EnsureCreated`, hand-built schema, modified production scripts or disabling manifest checks.
- [ ] Keep global test serialization until T01/T03 benchmarks and ownership proofs support a more granular concurrency policy.

**Gate T03:** Test counts/invariants unchanged; create/drop reliability proven under injected failures; side-by-side timing and connection-pool measurements show an improvement or the original implementation is retained.

### SQLPERF-T04 — Optional database fixture reuse, strictly by test category (PR 4, conditional)

- [ ] Proceed **only if T01 shows setup+migration dominate** and T03 is insufficient. Classify tests explicitly: **pure read**, **transaction rollback/isolated data**, **committing data**, **schema mutation/drift**, **permission/security provisioning**, **multi-session concurrency**, **migration history/lock/lifecycle**. Inventory each class and specify safe isolation mode.
- [ ] Pilot a per-class or per-collection migrated fixture **only** for suitable tests. For each test, prove a clean baseline of table rows, identity/sequence values, schema objects, triggers, permissions and audit state, and no leaked sessions. A blanket `DELETE`/`TRUNCATE` reset is not sufficient without FK/sequence/trigger/permission analysis.
- [ ] Never share a database for schema mutation, migration history/locking, security bootstrap, revoked-grant tombstones, or concurrency cases requiring isolation unless a formally reviewed isolation design and independent adversarial tests prove equivalence. Retain fresh database per test wherever isolation cannot be demonstrated.
- [ ] Favor explicit fixture ownership/disposal with bounded lifetime; no global mutable singleton, no shared ambient UoW, no parallel writes into a shared fixture. Fault-injection test a failed assertion, abandoned transaction, canceled task, SQL connection leak and failed reset; next test must not inherit dirty state.
- [ ] Demonstrate equivalence by running the old isolated and new fixture strategy against the same behavioral assertions. If safety cannot be proven, **skip this optional optimization** and record why.

**Gate T04:** Per-test observational independence, schema/security integrity and disposal guarantees are demonstrated; performance improvement outweighs complexity. If skipped, that does **not** block completing the other tasks.

### SQLPERF-T05 — Targeted development test tiers; bounded concurrency experiment (PR 5)

- [ ] Introduce stable xUnit categories/traits or documented filter patterns for `FastSQL`, `MigrationSchema`, `SecurityProvisioning` and `SqlConcurrency`. Define tags from **actual test responsibility**, not current execution time. Maintain a manifest/verification check showing every SQL integration test is in at least one full-suite category and no test is lost by filtering.
- [ ] Provide Windows PowerShell helper and commands to run focused tests for Codex/dev iteration, with explicit `-f net48` / `-f net8.0` and test results/TRX. A focused green run is **not** a substitute for full tests at merge/release gates; prevent accidental `--filter`/Skip use in release verification.
- [ ] Optionally benchmark parallel execution of **disjoint independently owned databases** with a small, configurable cap, only after T03/T04 and with repeatable timeout/failure checks. Do **not** globally enable xUnit parallelization merely because it is available. Start with existing serial behavior as fallback; never run both full TFM suites simultaneously against a memory-constrained SQL instance without evidence.
- [ ] Clearly distinguish functional correctness, intentional lock wait, SQL performance and environmental capacity; throttle or revert if timeouts/SQL contention increase. Track the actual SQL instance and active disposable DB count.

**Gate T05:** Focused commands select exactly their documented cases; the unfiltered full suite and Windows-specific gates remain required; no flaky timeout/deadlock/release regression.

### SQLPERF-T06 — Final dual-framework verification and documentation (PR 6 or closure of prior PR)

- [ ] Build solution Release (zero new warnings/errors), then run full SQL integration tests **sequentially** for `net8.0` and `net48`, at least three warm runs per framework for a meaningful post-change comparison, on the same instance/memory limit and similar host load; collect TRX for every run. Include baseline and post-change commit IDs.
- [ ] Compare wall-clock median, p95 per test/class, stage-level timing, SQL waits and crash/hang/cleanup rate. Record counts of passed, failed, skipped and all modified test names. Investigate any regression larger than roughly **10%** for a high-cost class/test, taking measurement noise into account.
- [ ] Prove no orphaned test DBs/sessions or leftover SQL test triggers; no broad SQL permission change, production DB mutation or connection-string exposure. Run required application/architecture checks; WinForms test suite is unaffected by test-only changes, but do not claim it passed without executing it when the affected solution/gates require it.
- [ ] Update `docs/TEST_STRATEGY.md`, focused-test instructions and a sanitized performance evidence document with exact commands/results, changes adopted/rejected, remaining risks and whether the 20–30% goal was achieved. Record a short ADR only for an enduring test-isolation/parallelism policy change.
- [ ] Update this plan checkboxes only from recorded evidence. If optimization tradeoffs block the target, document measured gain, safety reason and separate follow-up rather than hiding or disabling tests.

**Final acceptance:** Both TFMs full-suite green with no reduction in validated test inventory; all security/schema/migration/concurrency protections preserved; comparable timing evidence shows actual gain or an explicit safety-based decision; no unowned DB changes, flakiness or sensitive diagnostics.

## 4. Benchmark protocol and representative commands

Run from repository root, on Windows with the correct SQL test environment variable explicitly targeting `master`. Never commit the value.

1. Record Git SHA, SQL Server version/edition, memory limit, SDK, laptop/SQL host state, test count and `DOTNET_CLI_*`/MSBuild switches that could change results. Build **once** before timing; no restore/build in measured test runs. Warm-up is labeled, not silently mixed into the median.
2. Run TFMs one after another, not concurrently. Use fixed order for one pass; if measuring both in repeated passes, alternate TFM order to reduce warm-cache/thermal bias. Capture native TRX and complete process wall-clock, exit code, test count, skips and noise.
3. For each candidate change, compare an unchanged baseline and optimized implementation with the **same** conditions (ideally three warm repetitions each). Never compare one release's 199 tests blindly to a later release with a different inventory; provide a fixed-test-set or normalized per-class view.
4. Sample waits at start/end of each pass and report permission gaps or unrelated workload. `RESOURCE_SEMAPHORE_QUERY_COMPILE` deserves targeted capture of concurrent compile requests/memory grants if the wait reproduces; do not conclude memory pressure from working set alone.

```powershell
dotnet restore MyDmsVn.sln --locked-mode --nologo -p:RestoreDisableParallel=true
dotnet build MyDmsVn.sln -c Release --no-restore

dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net8.0 --no-build --no-restore --logger "trx;LogFileName=sql-integration-net8.trx"
dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net48 --no-build --no-restore --logger "trx;LogFileName=sql-integration-net48.trx"
```

Note: choose distinct results directories or filenames for repeated runs; do not overwrite previous TRX. The test project contains both SQL-backed and non-SQL tests; report totals accurately. Do not run with a missing SQL connection environment variable and then treat skipped SQL tests as success.

## 5. Required safeguards, exclusions and reporting

- **No weaker tests:** preserve independent multi-session SQL tests, their actual blocking proof, permission deny/approval cases, migration fingerprints, history/drift, transaction rollback, identity/bootstrap and bounded cleanup. No `Skip`, hidden `--filter` or narrower assertions to achieve a performance number.
- **No dangerous shortcuts:** never bypass `SqlTestDatabaseName.EnsureDisposable`, connect to a real business database, rewrite migration 001–008, grant broad runtime SQL rights, disable schema checker, or add a production-only test hook without architectural review.
- **No unsafe parallelism:** retain the current assembly serial default until adversarial isolation/capacity evidence supports changing it; keep a simple revert switch.
- **No diagnostic secret leaks:** performance logs/TRX may contain exception messages. Sanitize connection strings, credentials, local paths, PII and any password/hash; raw evidence stays local, shareable ZIP is reviewed before publication.
- **No fabricated results:** report unsupported Windows/SQL/DMV environments as blocked/not run. Distinguish profiling hypotheses from verified savings.

For each PR/task, deliver a small before/after comparison table, changed files, exact test commands, passed/failed/skipped counts, timeout/cleanup behavior, SQL resource measurements if available, and a go/no-go decision for the next optimization. PR 1 should provide measurement before PR 2–4 alter test behavior.

## 6. Out of scope

Production query/index/schema tuning without application evidence, raising SQL Server RAM as the default fix, new business functionality, redesign of P3.1 permission synchronization or P4.1 Login, changing target frameworks, test mocks replacing mandatory SQL checks, and benchmarking unrelated WinForms/Desktop UI.
