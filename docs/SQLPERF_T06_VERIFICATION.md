# SQLPERF-T06 final verification

Date: 2026-10-15

## Scope and revisions

This closes SQLPERF-T06 only. It does not complete P4.1, authenticated P5 acceptance or P6 release gates.

- Unchanged baseline: `2a139c258027f336cb1ded66a071a68c491ce08b`.
- Benchmark candidate: `972674f` (the T04/T05 work plus the cross-TFM cancellation normalization found by the first candidate attempt). The final reviewed head additionally contains the focused-run hardening described below; it does not change the timed SQL test execution path.
- Both revisions were restored and built in separate Codex-managed worktrees. Submodules were initialized at their recorded revisions.
- The same operator-approved non-production SQL instance, User-scope connection/approval configuration and runner protocol were used. Raw connection strings, instance identity, approval path, database names, TRX, timing JSONL and journals remain ignored local evidence under `.sqlperf/`.
- Build/restore time was excluded. Each revision ran sequentially with one warm-up per TFM followed by three measured runs per TFM. Measured order was `net8.0 -> net48`, `net48 -> net8.0`, `net8.0 -> net48` after the `net48 -> net8.0` warm-ups.

The accepted benchmark command shape was:

```powershell
$env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING =
    [Environment]::GetEnvironmentVariable('MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING', 'User')
$env:MYDMSVN_SQL_TEST_APPROVAL_FILE =
    [Environment]::GetEnvironmentVariable('MYDMSVN_SQL_TEST_APPROVAL_FILE', 'User')

dotnet run `
  --project tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj `
  -c Release --no-build -- baseline `
  --repo (Get-Location).Path `
  --project tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  --artifacts <ignored-revision-specific-directory> `
  --approval $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
```

Every accepted pass completed approval/preflight, watchdog, independent source/discovery inventory, TRX result, migration coverage and ownership reconciliation gates. The candidate runner additionally enforced the T05 category classification gate on both TFMs. DMV samples were unavailable on this approved environment and are reported as unavailable, never zero.

## Results

Runner wall-clock values are authoritative for the A/B result. TRX ranges show measured-run dispersion; p95 is the interpolated runner statistic over three measured runs and is contextual rather than a high-confidence tail estimate.

| TFM | Revision | Executed per run | Runner p50 | Runner p95 | Measured TRX range |
|---|---|---:|---:|---:|---:|
| `net48` | baseline | 213 / 213 | 570.687 s | 575.731 s | 544.930–574.960 s |
| `net48` | candidate | 222 / 222 | 506.604 s | 508.768 s | 487.980–508.150 s |
| `net8.0` | baseline | 213 / 213 | 545.294 s | 548.810 s | 540.640–548.120 s |
| `net8.0` | candidate | 222 / 222 | 498.631 s | 505.570 s | 477.280–505.000 s |

| TFM | Full-suite p50 gain | Full-suite p95 gain | Matched identities | Matched p50-sum gain |
|---|---:|---:|---:|---:|
| `net48` | 11.23% | 11.63% | 199 | 13.01% |
| `net8.0` | 8.56% | 7.88% | 199 | 10.22% |

The full candidate inventory is deliberately larger, so this is a conservative non-identical-inventory A/B. The reports contain 199 common stable test identities, zero baseline-only identities and nine candidate-only identities. The new cases cover the SQL concurrency gate's observation/release/cleanup failure paths and exact-pool teardown. The matched view sums each common identity's p50 duration; it is a fixed-set normalization, not a substitute for process wall-clock.

The nine candidate-only methods are:

- `SqlConcurrencyGateTests.Canceled_observation_releases_gate_and_drains_the_holder_before_database_disposal`
- `SqlConcurrencyGateTests.Canceled_release_can_be_retried_without_leaving_the_controller_lock_held`
- `SqlConcurrencyGateTests.Cleanup_attempts_every_step_after_task_completion_even_when_release_fails`
- `SqlConcurrencyGateTests.Cleanup_keeps_root_provider_alive_until_started_tasks_are_drained`
- `SqlConcurrencyGateTests.Cleanup_reports_task_that_remains_stalled_after_guarded_cleanup`
- `SqlConcurrencyGateTests.Failure_after_confirmed_business_block_cancels_tasks_before_database_disposal`
- `SqlConcurrencyGateTests.Holder_waits_for_controller_release_when_observation_is_delayed_past_ten_seconds`
- `SqlConcurrencyGateTests.Observer_identifies_app_gate_holder_and_business_block_without_reading_uncommitted_signal`
- `SqlTestDatabaseLifecycleTests.Teardown_does_not_clear_an_unrelated_master_connection_pool`

Across the three measured candidate runs, stage events totaled:

| TFM | Create | Migration | Drop | Migration events |
|---|---:|---:|---:|---:|
| `net48` | 141.418 s | 559.203 s | 33.057 s | 321 / 321 reconciled (273 expected success, 48 expected failure) |
| `net8.0` | 144.413 s | 567.746 s | 30.075 s | 321 / 321 reconciled (273 expected success, 48 expected failure) |

The audited 43 migration call sites remain in the source-controlled manifest. The strict runner verified every manifest entry and runtime invocation/event reconciliation on every accepted TFM pass, including direct DbUp and expected-failure paths. Migration is still the largest measured setup stage. The principal saving remains the T02 replacement of fixed lock delays with observable gates; T03's exact-pool cleanup removes global pool eviction without weakening guarded teardown.

## Safety, cleanup and failed-attempt handling

The first candidate sequence at `5139cd2` was rejected: its eighth TRX had 221 passed and one failed `net48` cancellation test. `Microsoft.Data.SqlClient` surfaced explicit command cancellation as `InvalidOperationException` on `net48`; the gate only normalized `SqlException`. Commit `972674f` added the narrow caller-cancellation normalization. The failing sequence was not included in any median. The focused cancellation test then passed 1 / 1 on each TFM before the complete candidate sequence was restarted in a new artifact directory.

All 1,144 baseline and 1,192 candidate benchmark ownership records ended `Disposed`; the runner reconciled each run against live SQL before accepting it. The final direct full-regression run added 298 / 298 `Disposed` records. No accepted run timed out, skipped a mandatory SQL case, left an incomplete journal or reported an orphaned owned database/session. No ambiguous candidate was auto-deleted.

The independent direct-run negative command deliberately omitted the approval variable and did not use the benchmark runner:

```powershell
Remove-Item Env:MYDMSVN_SQL_TEST_APPROVAL_FILE -ErrorAction SilentlyContinue
dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -c Release -f <net48-or-net8.0> --no-build --no-restore -m:1 `
  --filter "FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.SqlTestDatabaseMutationTests.Direct_create_with_missing_approval_issues_no_destructive_sql"
```

It passed 1 / 1 on each TFM. The test exercises the harness directly with a recording SQL session and asserts both zero administrative commands and an empty ownership journal, proving that missing approval reaches no database-level DDL at the mutation boundary.

No production migration, schema, authorization behavior, SQL permission or connection configuration changed. Per-test disposable databases, assembly-level SQL serialization, positive ownership checks, durable journals and guarded `SINGLE_USER WITH ROLLBACK IMMEDIATE` remain intact. Raw local artifacts were not committed.

## Final regression

At the final reviewed head:

```powershell
dotnet build MyDmsVn.sln -c Release --no-restore -m:1 -v:minimal
dotnet test MyDmsVn.sln -c Release --no-build --no-restore -m:1 `
  --logger "console;verbosity=minimal"
```

- Build: 0 warnings, 0 errors.
- Server Application: 133 / 133 on `net48`; 133 / 133 on `net8.0`.
- Desktop automated tests: 200 / 200 on `net48`; 200 / 200 on `net8.0-windows`.
- Architecture: 5 / 5.
- Server Infrastructure integration: 222 / 222 on `net48` in 8m26s; 222 / 222 on `net8.0` in 8m15s; zero skipped.
- SQL test harness: 52 / 52 on each TFM.
- SQL test runner: 81 / 81.

These are automated WinForms/Desktop tests; no authenticated interactive P5 acceptance is claimed.

## Decision

The engineering target of 20–30% median improvement on each TFM was **not achieved**. The accepted gain is 8.56–11.23% for a larger full inventory, or 10.22–13.01% on the 199 common identities. Further speed would require fixture reuse or parallel SQL load. T04 found no safe reset boundary for fixture reuse, and T05 found no capacity evidence supporting parallel execution on this instance. Both remain rejected: the cost is a slower suite, while the benefit is preserving real migration, permission, locking, cleanup and per-test isolation evidence.

SQLPERF-T01–T06 are complete under that explicit safety-based decision. Any future optimization should be a separately measured change with a new matched baseline; it must not reopen the rejected strategies without new isolation or capacity evidence.

## Final review remediation

The required fresh whole-branch review found three T05 infrastructure gaps. The fix pass made reviewed-code fingerprints cover async/generated test bodies plus reviewed harness/application source (excluding generated output so rebuild/commit metadata stays stable), routed focused discovery/execution through the existing owned-process-tree watchdog, and moved focused ownership validation into a bounded `finally` path that runs after every launched execution regardless of timeout or result-gate failure. Classification now runs before SQL test launch.

Three targeted tests were added RED then GREEN for changed async/helper code, stale classification before launch, and timeout/result-gate ownership reconciliation. The runner suite increased from 77 to 81 tests. A real post-fix `FastSQL` run passed 37 / 37 with zero skipped and complete ownership reconciliation on each TFM. These changes strengthen developer-run failure handling; they do not alter the already accepted benchmark timings or production behavior.

PR review then identified additional workflow defects. The follow-up narrows each exemption fingerprint from the repository-wide source tree to its declaring source, same-assembly compiled call graph and reached external project/dependency boundary; keys source resolution by assembly plus fully-qualified type; includes reached field/type tokens and source-visible inlined constants and enum members; validates both benchmark TFM classifications before launching any process; and preserves caller-owned run/journal/timing environment variables in the focused PowerShell wrapper. Regression coverage proves that async/helper/field/constant changes—including helpers declared in differently named source files, multi-declarator constants, inherited constant access, enum values, imported and qualified namespace collisions, nested constants, and attributed nested enum members—remain fail-closed, unrelated or same-simple-name source changes do not invalidate exemptions, comments and literals cannot create false type matches, a stale later-TFM classification blocks before the first process launch, and the wrapper restores all caller state. The runner suite now passes 91 / 91, and a real post-review `FastSQL` run passes 37 / 37 with zero skipped on each TFM. These review fixes do not alter the accepted benchmark measurements or production code.
