# SQLPERF-T01 verification — 2026-10-14

## Outcome

The T01 safety and measurement tooling is implemented in an isolated worktree. Non-SQL automated gates pass. Live SQL acceptance is **blocked**, not passed: `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` exists locally, but `MYDMSVN_SQL_TEST_APPROVAL_FILE` was not supplied. No SQL test DDL or benchmark pass was attempted.

Gate T01 therefore remains open. SQLPERF-T02 must not use these changes as an accepted performance baseline until the approved-instance steps below are completed on the unchanged revision.

## Implemented evidence

- Live-identity preflight and in-harness mutation authorization target `master`, verify the independently approved fingerprint, and check the SQL-version-specific cross-session DMV permission.
- Durable, process-safe ownership transitions cover create intent, created, drop intent, disposed, cleanup failure and quarantine. Cleanup needs approval, marker and live metadata corroboration.
- Timing is explicit and opt-in; it uses monotonic timestamps, thread-safe JSONL, outcomes and parent linkage. Interval reconciliation reports union, overlap and residual without adding nested timings to TRX.
- The four high-cost classes have a reviewed 41-entry migration invocation manifest. Source coverage is bidirectional and includes direct DbUp, reruns and failure paths.
- Independent source/discovery/TRX primitives default cases to SQL-required, recognize derived xUnit attributes, and reconcile the current assembly, discovery and execution one-to-one. The method-scoped non-SQL exemption manifest is intentionally empty, which is fail-closed.
- Migration coverage is a multiset keyed by test case, TFM and invocation; expected invocation records are emitted separately from completed timing events, and missing, unexpected or duplicate events invalidate a pass. The runner also re-audits the reviewed source manifest, including direct DbUp call counts, before accepting each pass.
- Theory rows use redacted content hashes instead of discovery ordinals, so reordered rows reconcile while substitutions and duplicate/deferred rows fail closed. Migration expectations now come independently from the reviewed manifest plus executed TRX rows, including repeated helper calls.
- Migration expectations are row-scoped whenever a theory produces multiple executions. The five system-database rejection cases are named facts, and reconciliation rejects a row swap such as A=2/B=0 even when the method-level total still equals two.
- The runner uses a Windows Job Object with kill-on-close, explicitly terminates a suspended child when ownership assignment fails, and provides bounded commands with concurrent stdout/stderr drainage plus a four-hour hard overall baseline deadline. `baseline` runs one labeled warm-up plus three measured passes per TFM, alternates TFM order, gives every run unique TRX/timing artifacts, and applies mandatory preflight and result gates to every child.
- Every launched SQL test child receives an isolated ownership journal. A read-only post-run gate executes from the coordinator's `finally` path under an independent 30-second bound, accepts only approved `Disposed` records with absent live databases, and writes a sanitized recovery artifact for empty, unresolved, mismatched or uninspectable ownership. It never drops a database.
- Reporting is a required completion gate. It excludes warmups/invalid runs, computes literal p50/p95 plus interval and ranked test/class costs, labels unavailable DMV data, redacts secrets, hosts, paths and theory values, and atomically creates stable JSON/Markdown files for each TFM. Missing or failed report output makes `baseline` return exit code 3.
- Timing reconciliation scopes repeated logical operation IDs to run/TFM/test instances. Reports reconcile each accepted run independently and derive per-stage exclusive interval unions, including cross-stage parent/child subtraction.

## Commands and actual results

| Command/gate | Result |
|---|---|
| Fresh locked restore and Release solution build | Passed, 0 warnings / 0 errors |
| Harness tests, `net48` | 49 passed, 0 failed, 0 skipped |
| Harness tests, `net8.0` | 49 passed, 0 failed, 0 skipped |
| Migration coverage, `net48` | 9 passed, 0 failed, 0 skipped |
| Migration coverage, `net8.0` | 9 passed, 0 failed, 0 skipped |
| Runner synthetic/unit tests | 60 passed, 0 failed, 0 skipped |
| Server.Application, `net48` / `net8.0` | 133 / 133 passed, no failures/skips |
| Architecture | 5 passed, no failures/skips |
| Desktop, `net48` / `net8.0-windows` | 200 / 200 passed, no failures/skips |
| VSTest discovery, `net48` | exit 0, 210 cases |
| VSTest discovery, `net8.0` | exit 0, 210 cases |
| Runner preflight with approval removed | exit 2; stopped before connection/test launch/DDL |
| Integration guardrail run with approval removed, each TFM | 68 passed, 142 skipped, 0 failed; explicitly invalid for SQL acceptance |
| Approved live SQL runs | Blocked: approval file absent |

The 142 skips per TFM are expected in the deliberate missing-approval guardrail run and prove only that unapproved direct execution does not enter SQL-backed tests. They are not accepted benchmark evidence.

## Remaining approved-instance gates

1. Supply a separately maintained, unexpired non-production approval file matching the live instance and required permissions.
2. Run preflight plus the direct-run missing/wrong approval, wrong effective database, wrong identity and wrong-server-disposal proofs with zero destructive SQL.
3. Measure timing-disabled versus timing-enabled overhead; investigate or explicitly account for overhead above 5%.
4. Run one warm-up plus three accepted warm passes per TFM, sequentially and with alternating order. Each pass needs exit 0, complete unique TRX, zero failed/skipped mandatory cases, complete discovery/execution and migration-event reconciliation, watchdog success and clean ownership reconciliation.
5. Run the complete repository Release regression matrix after accepted SQL runs.

Until all five steps have evidence, no baseline median, DMV delta, optimization percentage or T01 completion claim is valid.
