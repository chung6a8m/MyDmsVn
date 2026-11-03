# WEB5 B05-A owned-host admission — bounded candidate evidence

**Disposition:** B05-A owned-host admission is **DONE / VERIFIED NONPRODUCTION (bounded)**. The mechanism passed first on a private IIS Express site and then admitted the actual published WEB5 host with a fresh owned disposable SQL allocation for startup/health only. Backend B05 remains **IN PROGRESS / NOT ACCEPTED**: security/limiter/audit workload, compatibility/cutover, recovery and release gates were not executed. Staging remains not accepted and production remains `NO-GO / DEFERRED`.

## Candidate and environment

- Backend base: `233ce368b368729e2b6c7f3adeb4bf197c6637c3` (merged PR #94).
- Implementation source: `b7a8c5e941a73a3ebd36b1c79d4cf2016ffe21f2`; tree `82d130c4f8c8be769b0030777425b61642a29494`; branch `codex/web5-b05-owned-host`.
- Implementation commits: `47abec8` supervisor admission phases; `ce12c29` HTTP.sys queue parser; `1df7b74` exact IIS Express admission; `9b861bc` read-only quarantine forensics; `2c5d27a` packaged-host gate and review fixes; `45c9e1f` fail-closed URL-count hardening; `b7a8c5e` competing HTTP.sys route rejection.
- Frontend reference only: `492d60958e918867bb5a733faf51da89d10f800d`; no frontend file or artifact was used or changed.
- Windows `10.0.26300` build `26300`; .NET SDK `8.0.427`; test target `.NET Framework 4.8`; IIS Express `10.0.26013.1000`.
- Existing localhost:44321 machine certificate binding was read into the test process and used only for exact TLS pinning. Its thumbprint and private material were not logged or committed.

Microsoft documents HTTP.sys as the kernel HTTP listener and request queues as the boundary to which worker processes attach. The test therefore uses `netsh http show servicestate view=requestq verbose=yes` plus the exact IIS Express process, not TCP PID 4, a port, health response or challenge alone. References: [IIS architecture](https://learn.microsoft.com/iis/get-started/introduction-to-iis/introduction-to-iis-architecture), [`netsh http` request-queue service state](https://learn.microsoft.com/windows-server/networking/technologies/netsh/netsh-http).

## Implemented boundary

1. `Web5OwnedSupervisor` now enforces `Allocate -> Start -> VerifyHost -> Admit`; a mismatch, false result, timeout, ignored cancellation or phase fault quarantines and never advances to admission. Existing finish ordering remains `StopAdmission -> Drain -> VerifyOwnershipAndQuiescence -> Oracle -> RemoveOwned -> VerifyAbsent`.
2. A bounded, no-shell reader invokes the absolute system `netsh.exe`, rejects nonzero exit, oversized/malformed/localized output and ambiguous queue structure, and understands concrete plus valid HTTP.sys wildcard URL prefixes without weakening exact origin matching.
3. Admission binds the HTTPS root origin to exactly one active request queue, one controller and one attached worker, both equal to the fixture-owned PID. It also rechecks PID, WMI creation time, installed executable, exact command line/config/site, config SHA-256 and live process identity around each queue snapshot.
4. Direct fixture requests set `Proxy = null`, disable redirects and use the pinned existing localhost certificate. The live probe verifies the exclusive queue before the request, while a delayed TLS request is actually in flight, after the response, and verifies process plus queue absence after stop.
5. The packaged-host runner now captures and admits that exact process/queue/config before its first health request, reverifies after the selected scenario, and proves process/queue absence before its owned SQL cleanup. The B05 admission-only wrapper performs startup/health/teardown without any unkeyed write or workload claim.
6. Historical race-manifest forensics is read-only. It validates private ACL/reparse boundaries, exact filename/operation/database/trigger schema and duplicate/mixed identities; the SQL reader only observes server/database ID/create date, marker presence and two session/lock snapshots. It exposes no DROP, delete or cleanup-authority method.

## Test evidence

The final SQL-free run used:

```text
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter "(FullyQualifiedName~Web5OwnedSupervisorTests|FullyQualifiedName~Web5HttpServiceStateTests|FullyQualifiedName~Web5IisExpressHostAdmissionTests|FullyQualifiedName~Web5BackupSafetyTests|FullyQualifiedName~Web5OriginalFirstControlTests|FullyQualifiedName~Web5OriginalRaceForensicsTests)&Category!=Web5OwnedHostAdmission&Category!=Web5HistoricalForensics" --logger "trx;LogFileName=web5-b05a-review-fix-sql-free.trx"
```

TRX `web5-b05a-review-fix-sql-free.trx`, SHA-256 `267E95637AEF35F10026877DAC8F47D6DC9F1FE902E5D88175DA025BD51708F9`: **79 discovered / 79 executed / 79 passed / 0 failed / 0 skipped**.

The final private live opt-in run selected `Category=Web5OwnedHostAdmission` and used no SQL, published application or foreign endpoint. TRX `web5-b05a-review-fix-live-owned-host.trx`, SHA-256 `7D9BB0D53E4C7DFE09F0FA90C78A6079DAE32A4C745F4BB884AA282BD3875F67`: **1 discovered / 1 executed / 1 passed / 0 failed / 0 skipped**. It reported exact owned PID `39208`, origin `https://localhost:44321`, three exclusive queue snapshots and proxy disabled.

The final published-host admission-only run selected `Published_WEB5_host_is_admitted_before_HTTP_and_absent_before_SQL_cleanup`. TRX `web5-b05a-review-fix-published-web5.trx`, SHA-256 `CC928D691D62A5186CE00B0C8FA94FC73D81F2774BDC77D4990742702DD38407`: **1 discovered / 1 executed / 1 passed / 0 failed / 0 skipped**. It created a fresh owned disposable SQL database, admitted the immutable published host before health, proved absence before cleanup and reported `WEB5_OWNED_SQL_CLEANUP verified=1 remaining=0`. Published artifact hashes were HTTP host DLL `862E6F60EAFC45D8A3E4260BE4D42A334B65DBE6F37B41C6FDFF6DD1B45826AF` and `Web.config` `8FEDF54AA653B05D6F8D9D0155D3AB5322F460B9BCADD5B1554A60E85CAEB4FE`. A separate post-run read found **0 IIS Express processes** and **0 listeners on port 44321**.

Test-first controls were observed rather than inferred:

- Supervisor tests failed before `VerifyHost`/`Admit` existed, then passed 17/17.
- HTTP.sys tests failed to compile before the parser existed. A deliberate mutation removing PID/controller equality produced **2 failures / 12 passes**, then the restored gate passed.
- The first live RED exposed valid foreign prefix `HTTP://*:80/`; regression tests retained strict exact-origin matching.
- A later live RED exposed trailing spaces emitted by actual `netsh` headers; the parser now accepts only trailing space/tab while retaining exact English request-queue header text. Localized-header negative coverage remains.
- Independent review first found that the packaged runner bypassed admission and did not prove queue absence before SQL cleanup, plus PID-reuse, structured URL-group, bounded-output and deterministic in-flight gaps. Those findings were reproduced and fixed in `2c5d27a`. The parser URL-count observation failed first and passed after `45c9e1f`. Owner review then found a P1 routing-precedence gap: foreign active strong-wildcard or longer explicit registrations could capture owned paths. Four admission/absence cases failed before the fix, then passed after `b7a8c5e`; the full focused and live evidence above was regenerated.

The complete Security project and broad SQL suites were not run. The focused classes, private live wrapper and single fresh-owned-SQL published-host admission wrapper provide nonzero, zero-skip evidence for this B05-A scope. No compatibility, security/limiter workload or cutover suite was run.

## Cleanup and quarantine disposition

The final private and published fixtures stopped their exact workers, proved process/queue absence and removed their own run directories/databases. Three temp directories from earlier private live RED attempts remain under test-owned temporary roots because manual recursive removal was blocked by execution policy. They contain only generated IIS Express config/site probe artifacts, not SQL, credentials or a running listener; they are not treated as clean teardown evidence.

The two historical SQL database/private-manifest allocations were not located by prefix, queried, adopted, dropped or deleted. `MYDMSVN_WEB5_RACE_PARENT_DIRECTORY` was not supplied, so the explicit read-only historical runner is **NOT RUN**. Even if later observations show marker presence and two zero-activity snapshots, the legacy manifest lacks the original SQL ownership token and database creation tuple; cleanup authority therefore remains **BLOCKED**.

## Gate disposition and residual work

| Gate | Disposition from this candidate | Remaining boundary |
|---|---|---|
| B05-A supervisor and queue/process admission mechanism | `PASS / VERIFIED NONPRODUCTION (bounded)` | Private SQL-free IIS Express fixture only |
| Actual published WEB5 candidate admission | `PASS / VERIFIED NONPRODUCTION (bounded)` | Fresh owned disposable SQL, startup/health/teardown only; no workload or staging acceptance |
| B5-IIS-04 single-worker topology | `PARTIAL / bounded admission proof` | Actual candidate queue had exactly one worker; limiter behavior and any multiworker claim remain unverified |
| Historical quarantine | `BLOCKED`; live review `NOT RUN` | Exact private parent, original ownership token/create tuple and valid cleanup authority are absent |
| B5-COMP-02 / B5-IIS-07 workload and cutover | `NOT RUN / DEFERRED` | Admission prerequisite is satisfied; representative workload, cutover, browser and independent oracle were not authorized or executed in B05-A |
| Full IIS and unperformed Narrator | `OWNER_ACCEPTED_EXCEPTION / NOT RUN` | Not PASS |
| Reverse-proxy deployment/topology | `NOT_APPLICABLE` | Selected topology has no proxy; direct spoof rejection remains applicable |
| Audit destination/retention, service recovery/RPO/RTO, B03/B06 | `NOT RUN / DEFERRED` | Separate implementation, rehearsal and Owner decisions required |
| B05 overall / staging / production | `IN PROGRESS / NOT ACCEPTED`; staging not accepted; production `NO-GO / DEFERRED` | No release authorization |

No same-key resend, automatic write retry, runtime endpoint, production DI registration, frontend change, schema/contract change, SQL grant, workload or release decision is introduced by this candidate.
