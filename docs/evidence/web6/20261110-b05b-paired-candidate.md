# WEB6 B05b — paired candidate, cutover, workload and recovery evidence

**Disposition (2026-11-10): harness IMPLEMENTED / LOCALLY VERIFIED; live pair BLOCKED / NOT RUN.** All four exact enabled live selectors failed before allocation because their explicit private prerequisites were absent. They executed with **zero skips**; no IIS Express, SQL or browser resource was created. This record makes no live PASS, performance, RPO or RTO claim.

## Candidate provenance

| Component | Reviewed identity |
|---|---|
| Backend implementation | source `9598c75d60370bc73efaa14525deb42af3f0e8d2`; tree `889cc4c5acf70713f3f05fae1fa950016d3cffc3` |
| Frontend implementation | source `9eb7d37e8fdb7ced5871ae9eb6be00f3e6e7163d`; tree `bd6cda2d6f13912101a357ad5f243fa4082e6140` |
| Frontend reviewed evidence | commit `4496a991991ea7c1001dee8ccac80192a3a27f16`; ordinary `dist` digest `b2422fdce8d0b8645a3f6213952ce2b4c6bff288c0c8a217ed655b70be93ce88` |

The reviewed frontend record reports npm locked install PASS with six advisories (two low, four moderate), typecheck PASS, unit 80 files / 1,517 tests passed, ordinary build 2,579 modules with a chunk-size warning, and production Playwright 2/2 passed with zero skip/retry. Its live pair/cutover is BLOCKED/NOT RUN, workload is NOT MEASURED and recovery is NOT RUN. Those frontend results do not substitute for a backend live pair.

## Exact enabled selectors

Selectors were executed sequentially against the Task 7 backend checkout. Each row is a test failure representing a deliberate fail-closed prerequisite block, not a skipped or silently disabled live test.

| Gate and exact row | Executed result | Blocking prerequisites | TRX |
|---|---:|---|---|
| Pair — `Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL` | 0 passed / 1 failed / 0 skipped | candidate root/id, browser directory, publish directory, TLS thumbprint and pilot project absent | `TestResults/task-7-paired-blocked/task-7-paired-blocked.trx` |
| Cutover — `Web6_pinned_cutover_runs_protected_IIS_and_browser_lifecycle` | 0 / 1 / 0 | candidate identity, current/old publish and archives, frontend dist, build authority path/hash, SQL control parent, current/restored epochs, browser directory and TLS thumbprint absent | `TestResults/task-7-cutover-blocked/task-7-cutover-blocked.trx` |
| Workload — `Web6_paired_candidate_runs_bounded_workload_profiles` | 0 / 1 / 0 | publish/TLS/browser/pilot inputs, candidate enable/root/id, workload root/cohort and reviewed Task 4 acceptance path/hash absent | `TestResults/task-7-workload-blocked/task-7-workload-blocked.trx` |
| Recovery — `Web6_candidate_recovery_rehearses_distinct_owned_restore_before_service_admission` | 0 / 1 / 0 | publish/browser/TLS inputs, candidate root/id, reviewed Task 4 acceptance path/hash and recovery root absent | `TestResults/task-7-recovery-blocked/task-7-recovery-blocked.trx` |

## Implemented harness

- Pairing binds protected backend manifest/result identity to the frontend source/tree/dist evidence and the actual browser bridge. Six same-key manual-resend command kinds remain behind explicit candidate admission.
- Cutover models independent current, old-compatible and restored adapters, protected lifecycle admission and compatibility oracles. It does not perform an unsafe schema13 downgrade.
- Workload supplies bounded per-class metrics and up to 1,000 attempts, retained PR #95 host re-observation and outer cleanup. No measurements were collected here.
- Recovery uses a real source-outage coordinator, Task 2/PR #95 admission, HTTP Sales create/update/post, full business oracle, pinned recovery root, scoped evidence lease and permission preflight. No service recovery was rehearsed here.

## Known failures, aborts and retained allocations

The Task 2 complete-Security five-failure baseline, Task 3 aborted SQL run, Task 6 round 2 concurrent-suite interference and Task 6 round 4 initial 26/2 permission-preflight failure remain recorded in [B05a operations evidence](20261110-b05a-operations.md#failure-and-retained-resource-register). The two fresh round-4 allocations/evidence roots, all historical B03 retained resources and the two historical WEB5 SQL allocations remain untouched. A clean later affected selection cannot erase those dispositions.

## Gate disposition

| Gate | Disposition |
|---|---|
| B6-HOST-01 | BLOCKED / NOT RUN — no admitted published owned host |
| B6-COMP-01 | BLOCKED / NOT RUN — harness implemented, no actual old/current/restored lifecycle |
| B6-PAIR-01 | BLOCKED / NOT RUN — no actual SQL/browser pair or physical oracle |
| B6-LOAD-01 | NOT RUN / NOT MEASURED — no percentile, throughput or capacity claim |
| B6-REC-01 | BLOCKED / NOT RUN — no service RPO/RTO or restored-business admission claim |

The B03 dependency waiver authorized bounded implementation only; it is not a live PASS. Ordinary resend remains OFF. Staging is **NOT ACCEPTED** and production remains **NO-GO**. B06 remains unperformed.
