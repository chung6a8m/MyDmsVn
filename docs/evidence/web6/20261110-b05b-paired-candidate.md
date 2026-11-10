# WEB6 B05b — paired candidate, cutover, workload and recovery evidence

**Latest disposition (2026-11-10): attempt04 PREPARED / PIN_MATCHED, then FailedRetained at the first Chromium selector; Task 6 OPEN.** One owned SQL allocation and runtime login plus host-runtime configuration are retained. IIS/browser launch was not reached, no later selector ran, and no CandidateVerified receipt exists. Attempts01–03 and earlier prerequisite failures remain separate retained history below. No live PASS, performance, RPO or RTO is claimed.

## Task 6 attempt04 prepared then FailedRetained

After explicit Owner authorization to issue independent protected WEB6 SQL approval and review of the manifest-publication correction, candidate `web6-20261110-182d4285` used fresh attempt04 roots and fresh Owner authority/request. The earlier roots were not reused or altered. The Owner-approved SQL approval and private copy of existing Node/npm were reused only after exact ACL/schema/digest/expiry/effective-master and whole tool-tree identity/hash/ACL/link checks. The legacy SQL approval bytes and ACL remain unchanged.

| Candidate input or output | Exact identity |
|---|---|
| Backend source / tree | `18672300d642eef7944fbd540a42b3654783ba5b` / `f6a790770454112a27692220d359aef5e6e483c4` |
| Frontend source / tree | `13179f56a3381275c0652f6edaf29258f18927f2` / `f493e5911494a3eb4361397e3c65c72ae37b87e9` |
| Release/net48 bootstrap executable SHA-256 | `d845a2a937ff4b07fcf8202a4c2bb5d2b48570a7e5476652657f01a9eb225c24` |
| Owner authority / request SHA-256 | `3864549865977dbba9fb61084208ac37aae6712972ec062caaed191bfe6da449` / `2a6897681c29bfeb36a354ca83b84beeae5b3eaf7b24335b21a0a4d54f33e415` |
| Independent protected SQL approval SHA-256 | `faaf1fa43e63bb17eca3d13116e96312fbf21a8bc32fe9e8f4a8d0b91325724b` |
| Prepared receipt SHA-256 | `0a7376b0175c9e5fc1fa0fac254a9edeb2c3ca7b3293e83089ad4d9579e28c93` |
| Ordinary dist / manifest SHA-256 | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest SHA-256 | `6efbddbe9391ade739710512e19423e6117e2183d99360f50641026dd0d7c9bf` / `bba31a3da72df6932133bd282dca9508330422bd13d896631a7a794dac4e71c5` |
| Current / old backend publish SHA-256 | `c7b2ba4ea6fbe186f97a834c0edb8d02d25fcdf216f07dd61216ba0b95b3491d` / `1edfb6b04bf9a10ca224f02d20a9e16028a44ccd5272ccd3bcc7ee7852441f1f` |
| Old frontend dist SHA-256 | `da573ea101e95b9a98850cc1b29270d89a24e6f4d54a8c2044949cfdb9289882` |
| Cutover build-authority SHA-256 | `af58f7c6d3187319c6524bf81c7a25514ec9d7a3b7d7f4d00c4856a9ffa28fdc` |

The archive pins remain backend `a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2` and frontend `ea104582a5aa4bcdf0d04999296a30a3c43e450e`. Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, Node `22.14.0`, npm `11.6.0`, IIS Express `10.0.26013.1000`, SQL Server `14.0.2140.2`; prepared browser binaries Chromium `156.0.8078.4` and Edge `157.0.4341.42`. The 2,337-file copied tool tree digest is `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`; no installation or global configuration change occurred. Owner/SQL authority expiry is `2026-11-10T15:10:28Z`.

Authorize ran once, exit 0. Prepare ran once from `07:31:40.0910864Z` to `07:34:39.6501042Z`, exit 0, `PREPARED / PIN_MATCHED`. It verified clean detached source pins, ordinary marker `candidateId:null` (OFF), exact candidate marker, existing tool/browser/TLS pins and independent effective-master authority. All ten candidate dist files independently matched the complete published `wwwroot` set. SQL control parent and all live roots were empty/absent before run. This prepared result resolves the earlier preparation defects; it does not establish live acceptance.

```powershell
& $bootstrap authorize --input $ownerInput --output $ownerAuthority --acknowledge 'I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS'
& $bootstrap prepare --request $request --authority $ownerAuthority --authority-sha256 $ownerAuthoritySha256
& $bootstrap run --prepared $prepared --prepared-sha256 $preparedSha256
```

Run was invoked exactly once, from `07:35:43.6601902Z` to `07:36:27.1863867Z`, exit 5. It restored/built the pinned Security selector, then invoked Chromium only. Actual exact row: `FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL`, via `dotnet test <pinned Security project> --configuration Release --framework net48 --no-build --no-restore --disable-build-servers -nr:false --filter <exact row> --logger 'trx;LogFileName=result.trx' --results-directory <new private results>`.

| Required invocation | Discovered / executed / passed / failed / skipped | Actual boundary |
|---|---:|---|
| Pair Chromium .NET selector | 1 / 1 / 0 / 1 / 0 | FAILED after SQL setup, before IIS/browser launch; native browser cases NOT RUN |
| Pair Edge | 0 / 0 / 0 / 0 / 0 | NOT RUN; stopped by first failure |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Root exception: `FileNotFoundException: CleanArchitecture.sln`, from `WebHostIisExpressTests.FindRootFile` at source line 527, called at line 196. The repository solution is `MyDmsVn.sln`. This lookup occurs after owned database allocation/migration, runtime-login creation and fixture seed, and after writing host configuration; it precedes candidate-manifest construction and IIS Express process launch. The aggregate failure was preserved. No application HTTP/browser scenario or independent business/journal oracle ran.

Terminal receipt is `FailedRetained`, SHA-256 `07b1fa91391a2177aa6dfd81fa54df928b0f19cd1fea12ce5e7e2376f859c8a7`, with `NonzeroExit`, `processTreeDrained=true`, `requiresDispositionProof=true`. Actual TRX SHA-256 is `54bdb2ebe299cb65d36101046a92508292748af5c63b2dc772ee13a8154f4eae`. Protected allocation evidence SHA-256 is `131ca868bcfd837c95483a68518d4ea733aeac15801496bda85d6dcd45e698b8`, bound to original SQL control SHA-256 `a00ffa11d966a592908d81b3909e36a81f634e957bbd89adcebb206d2210eb86`.

**Retained resources:** one owned database, one mapped runtime SQL login, one private SQL control root and the host-runtime directory/configuration, together with prepared worktrees/artifacts and all receipts/TRX. Read-only inspection of that exact allocation matched original server/database ID/creation tuple and ownership token, and observed one mapped SQL login plus retained host configuration. This is observation only, with no deletion authority. Fixture cleanup was refused after the failure; no manual cleanup, retry or receipt replacement occurred. Process-tree drain does not establish SQL/HTTP-queue/final disposition acceptance. Historical quarantines and attempts01–03 remain untouched.

No workload samples/percentiles, recovery RPO/RTO, accepted Task4 receipt, final independent disposition or CandidateVerified exists. A further live attempt requires **a reviewed source correction and new explicit Owner authorization**, then a new candidate/root/request; never patch or rerun the retained attempt. Ordinary artifact resend remains OFF. Full IIS and unperformed Narrator remain NOT RUN within their bounded exceptions; staging NOT ACCEPTED, production NO-GO. B05/F04 and Task 6 do not advance to complete.

### Fresh pure verification after attempt04

At backend `1867230` / frontend `13179f5`, the requested pure gates passed: Application **363/363**, HttpHost **635/635**, four bootstrap Security classes **227/227**, each with zero failed/skipped and matching actual TRX total/executed counts. Backend commands used the same direct Debug/net48 `dotnet test <project> --no-restore --logger ... --results-directory ...` form and exact four-class filter documented in the earlier checkpoint below; both SQL environment variables were removed only from the test children. Frontend `npm.cmd run typecheck` passed and `npm.cmd run test:unit` passed **81 files / 1,558 tests**, zero failures. No live selector was repeated.

TRX SHA-256: Application `c7a0a06507e1cadf6fcf6ae37997520b0bf59e42c5d1c93aa7c92b9233767520`; HttpHost `2634b9011cbd833b2b4476f8370e87bd3a426891d4e7eb89b24485701a00ff70`; bootstrap `a9f43a233a46d138fe6ee4ba2dc56137aa0e5e3ef3f865bf118e6ebba0e5600a`. These pure results do not replace the retained Chromium failure or establish SQL/host/browser acceptance. Raw TRX and private records remain outside committed evidence.

### Retained preparation attempts before attempt04

- Attempt01 `web6-20261110-25a1cf46`: protected legacy-SQL-approval refusal before task-root creation, detailed below. Owner later explicitly authorized issuing independent four-field protected SQL approval; the legacy file was not copied or repaired.
- Attempt02 `web6-20261110-e5cd8acd`: new independent SQL approval passed; prepare refused before task-root creation because Node's adjacent npm was `10.9.2`, while shell `npm.cmd` resolved installed npm `11.6.0` by prefix. A separately authorized task-local copy of existing Node/npm resolved this prerequisite in a fresh root, without install/global mutation.
- Attempt03 `web6-20261110-01a86302`: ordinary build succeeded, then manifest hard-link publication failed `EBUSY` while the protected parent-directory lease was held. A separate diagnostic output succeeded unheld and failed held with the same child environment. Five clean worktrees, ordinary artifact and diagnostic records remain retained. Reviewed backend `1867230` / frontend `13179f5` changed bootstrap to verified exact UTF-8 stdout plus backend protected create-new publication; attempt04 proved that corrected preparation path.
- A preliminary local issuance-script type-binding failure created only new private directories/error evidence, no approval/prepare/live invocation; those records were also retained.

## Task 6 controlled bootstrap attempt

**Attempt01 history; the latest disposition is attempt04 above.**

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
