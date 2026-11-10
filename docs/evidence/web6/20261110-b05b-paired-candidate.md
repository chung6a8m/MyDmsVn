# WEB6 B05b — paired candidate, cutover, workload and recovery evidence

**Disposition (2026-11-10): bootstrap IMPLEMENTED / PURE GATES VERIFIED; Task 6 BLOCKED / NO LIVE ALLOCATION.** The new controlled bootstrap attempt was refused at the protected SQL-approval boundary before artifact preparation or any live selector. It produced no Prepared or CandidateVerified receipt. Task 6 remains open. The earlier four enabled-selector prerequisite failures are preserved below as separate history; this attempt did not rerun them. No live PASS, performance, RPO or RTO is claimed.

## Task 6 controlled bootstrap attempt

Owner approved the nonproduction bootstrap plan and destructive drill scope, including the separate authorization step. Candidate `web6-20261110-25a1cf46`, attempt `attempt01`, ran once at `2026-11-10T05:01:14Z`. The Owner authority covered HTTPS localhost ports 44321/44322, the pair/cutover/workload/recovery drills, new private task/SQL parents, the four exact source pins below and expiry `2026-11-10T13:01:13Z`. That authority is not a pilot-enable decision.

| Pin or local record | Exact identity |
|---|---|
| Backend source / tree | `9351942bd7c144f0f221b2cc4c78577c5f4d92f4` / `270455fa95a309fe705248665f8739d13c030c59` |
| Frontend source / tree | `5f7c0051497e8631925b68127ff556d2d066cd5c` / `158c5ec562b8bf66e7be3ea8db41cbae32b7e5c2` |
| Old backend / frontend pins | `a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2` / `ea104582a5aa4bcdf0d04999296a30a3c43e450e` |
| Release/net48 bootstrap executable SHA-256 | `f56be7ad8b604f4c6e134b28407cec2f0c206748b13c4f7a000dae52dc856c61` |
| Owner authority SHA-256 | `628f0795fc78212697f293a08d45bd5ffd07b6531cc597504da083a05ff49085` |
| Strict request SHA-256 | `66fb71c14d5842173afdefe64b6c4f0a3d5d9eb7d449064927acb3e8fe6f7bfa` |
| Existing SQL approval SHA-256 | `bcc3ae3947a8913057252613b31e3d319f6876752c0d8a498ba8c209c0389fb5` |
| Private attempt summary SHA-256 | `d30cfc8cfb72d498585d511c78a188f5e55d73f6d30ea492f6ea9e0ef8281080` |

Both source worktrees were clean and detached. Windows 11 Home x64 build 26300, .NET SDK `8.0.427`, Node `22.14.0`, npm `11.6.0` and IIS Express `10.0.26013.1000` were observed. A read-only designated SQL identity check reached effective `master` on SQL Server `14.0.2140.2`; the legacy approval's nonproduction attestation, unexpired validity, server/machine/instance and stored fingerprint were checked. Its endpoint spelling differed from the connection's DataSource spelling and was not reinterpreted as approval. These observations did not authorize allocation.

The blocking fact is the legacy SQL approval's **unprotected inherited DACL**: current-user owner, four inherited ACEs, including an ACE permitting modification by another principal. Its legacy JSON schema also differs from the required WEB6 schema. The original bytes and ACL were rechecked unchanged. No copy, ACL repair, derived authority or replacement was created. A read-only diagnostic invoked the exact bootstrap authority/request/tool/certificate readers, then observed `IOException: Bootstrap file is outside the protected Owner ACL.` at `protected-sql-approval-read`; it did not invoke prepare again.

Commands, with private paths represented by named variables:

```powershell
dotnet build tools/MyDmsVn.Web6.CandidateBootstrap/MyDmsVn.Web6.CandidateBootstrap.csproj --configuration Release --framework net48 --no-restore --disable-build-servers -nr:false
& $bootstrap authorize --input $ownerInput --output $ownerAuthority --acknowledge 'I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS'
& $bootstrap prepare --request $request --authority $ownerAuthority --authority-sha256 $ownerAuthoritySha256
```

Build: exit 0, 0 warnings/errors. Authorize: exit 0, `status=AUTHORIZED`. Prepare: exit 4, `status=BLOCKED_PREFLIGHT`. The task root did not exist afterward and the new SQL control parent had zero children. The prepare code opens protected SQL approval before worktree/artifact creation and before its SQL identity query. No `prepared.json`, detached candidate worktree, publish/dist artifact or live run receipt was produced. `run` was not invoked. The current attempt has no artifact hash or fresh ordinary-artifact build proof; ordinary resend remains OFF in source, and no candidate artifact was admitted.

| Required live invocation | Discovered / executed / passed / failed / skipped in this attempt | Disposition |
|---|---:|---|
| Pair Chromium | 0 / 0 / 0 / 0 / 0 | NOT RUN; blocked at prepare |
| Pair Edge | 0 / 0 / 0 / 0 / 0 | NOT RUN; blocked at prepare |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN; blocked at prepare |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED; no class samples or percentiles |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED; no RPO/RTO |

There is no business/journal/ledger oracle, host queue admission or cleanup PASS for this attempt. No live SQL database/login, IIS worker/queue or browser context was allocated. Only the new private authority/request/diagnostic records and empty parent directories were retained. Historical quarantines and failed-attempt resources were untouched. The [runbook remediation contract](../../development/WEB6_CANDIDATE_RUNBOOK.md#7-controlled-bootstrap-and-protected-sql-approval) is required before a new attempt; an unprotected legacy approval cannot be promoted to authority.

### Task 6 pure verification at the source pins

All commands were fresh at the source pins above. Backend test children had both SQL connection/approval environment variables removed; these are pure tests, not SQL acceptance. Actual TRX total/executed/pass counts matched and no selected case skipped. Tests used Debug/net48; the CLI build above separately used Release/net48.

| Gate | Result |
|---|---|
| Application complete project | 363 / 363 passed, 0 failed/skipped |
| HttpHost complete project | 635 / 635 passed, 0 failed/skipped |
| Four bootstrap Security classes: Authority, Preflight, Runner, Orchestrator | 218 / 218 passed, 0 failed/skipped |
| Frontend `npm.cmd run typecheck` | PASS |
| Frontend `npm.cmd run test:unit` | 81 files / 1,552 tests passed, 0 failed |

Backend command form was `dotnet test <project> --no-restore --logger 'trx;LogFileName=<name>.trx' --results-directory <local-results>`. The exact Security filter was `FullyQualifiedName~Web6CandidateBootstrapOrchestratorTests|FullyQualifiedName~Web6CandidateBootstrapRunnerTests|FullyQualifiedName~Web6CandidateBootstrapAuthorityTests|FullyQualifiedName~Web6CandidateBootstrapPreflightTests`. Local TRX SHA-256: Application `711045ca60364e5129182672add8b207a51f5879ef76ced2a3a25256999c1447`; HttpHost `5147e7517894d8d168458fac7ef61e4ef4475b428cfedd7323de224a909d72ba`; bootstrap `2f8ef49ca416a8cbba3210569f09cb3447a8401aeeca4a0095fdd9520047b3ac`. Raw TRX and private records are not committed. The [frontend record](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/web6-f04-paired-cutover/docs/evidence/web6/20261110-f04b-measured-quality.md) retains its earlier evidence and has a Task 6 addendum for the same blocked candidate. This branch link identifies the pending documentation update; no push or merge is claimed here.

No roadmap status advances. Full IIS and unperformed Narrator remain NOT RUN within their existing bounded exceptions; staging remains NOT ACCEPTED and production NO-GO. Task 6 cannot complete without PIN_MATCHED preparation, five passing live invocations, the required workload samples, measured recovery and CandidateVerified.

## Candidate provenance

**Earlier harness checkpoint, before the Task 6 source pins above.**

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
