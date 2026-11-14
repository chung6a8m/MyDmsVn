# WEB6 — Pinned Windows/IIS Express/SQL candidate runbook

**Status (2026-11-14): attempt24 reached PairPassed, then FailedRetained at the Chromium `restoreEpoch` restored-session copy because test-oracle `DateTime` parameters were sent as SQL `datetime`, losing `datetime2(7)` ticks before exact metadata verification.** Source correction `221b4da6098871ae3219a61c43d2d569b5a59177` uses cutover-local `DateTime2` parameters with scale 7 and sanitized field-name-only mismatch diagnostics; focused local verification passed. Task 6 remains open, and protected CI plus new explicit Owner authorization are required before any fresh attempt. The controlled nonproduction runbook applies to Codex Local and protected Windows CI using IIS Express and SQL Server. [B05a operations](../evidence/web6/20261110-b05a-operations.md) Â· [B05b paired candidate](../evidence/web6/20261110-b05b-paired-candidate.md) Â· [Backend roadmap](../plans/20261103-001-web6-recovery-quality-backend.md) Â· [Acceptance](WEB6_BACKEND_ACCEPTANCE.md) Â· [WEB5 owned-host evidence](../evidence/web5/20261103-b05-owned-host-admission.md) Â· [React companion](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261103-001-web6-recovery-quality-web.md).

## 1. Candidate manifest required before any live test

Record a committed, versioned `docs/evidence/web6/<date>-candidate-manifest.md` before running the pair. Include:

| Field | Required value |
|---|---|
| Backend | exact source SHA/tree, code/tests SHA, net48 toolchain, npm N/A, dependency-lock hashes, publish settings and backend DLL/`Web.config` SHA-256 |
| Frontend | exact source SHA/tree, package-lock hash, Vite build command, built `dist` manifest, published `wwwroot` file-by-file hash match |
| Protocol | frozen `web4-v1` original hash/corpus, additive `web5-resolution-v1` corpus/manifest, `web5` mode + exact schema13 definition fingerprint, SQL dialect/compatibility |
| Actors | separately named **restricted test runtime** and **restricted operator**, migration identity if needed, no shared login, no printed secrets |
| Transport | direct same-origin HTTPS, localhost certificate pin, exact IIS Express exe/path/cmdline/config hash, unique test site/bindings, HTTP.sys queue/process admission source |
| SQL | exact disposable DB allocation token, creation/server/database identity, test ownership and connection privilege, current epoch, backup artifact path and cleanup policy |
| Test matrix | exact filter/spec, planned cases, browsers and versions, workload profile/actor count, time budget and hard stop conditions |
| Approval | Owner-approved nonproduction test data/ports and any destructive drill; manual resend feature flag OFF except explicit bounded pilot |
| Retained constraints | old WEB5 SQL quarantines **excluded**, unperformed full IIS/Narrator remain NOT RUN, production NO-GO |

Treat source SHA and binary hash separately; matched source tree alone does not prove identical published DLL/JS assets. Do not clone original credentials in public evidence. Never reuse a "passing" main head's CI as proof of new untested binary changes.

## 2. Safe setup

1. Use a clean worktree at the approved pair, existing Windows machine and SQL Server 2017 test environment (or a separately authorized test instance). Require explicit SQL owned-fixture opt-in; **do not provision Docker, full IIS, Windows features, global cert bindings or remote hosts automatically**.
2. Verify locked restore for net48 projects and npm. Build immutable Release backend host + React artifact; compare `dist` to published `wwwroot` including all assets, not only entry HTML. Publish into a private test directory with current user restricted ACL.
3. Allocate a **new** disposable SQL database with original creation token, recorded server/database ID/createdAt and tracked epoch. Apply migration013 with the proper migrator; provision runtime and separately scoped operator. Stop if wrong exact schema definition fingerprint, wrong grant, unexpected prior DB or no ownership proof. Never use a historical quarantine database.
4. Build dedicated IIS Express site/config under owned private root, verify exact cert before HTTP, reject preexisting foreign binding. Run merged PR #95 `VerifyHost → Admit`: parse actual `netsh http show servicestate view=requestq verbose=yes`, enforce exact origin and all competing strong-wildcard/longer-path routing guards, matching process WMI start/command/config hash, exclusive queue/controller/worker. **Only then** perform health and authenticated HTTP.
5. Enforce bounded cancellation, site shutdown, process+queue absence and independent SQL quiescence and journal/business oracle before deleting **only this run's owned resources**. On timeout/quiescence/ownership mismatch, quarantine new fixture, report leftover exact identifiers in private evidence and stop further destructive cleanup.

## 3. Acceptance execution order and scenario packs

| Pack | Runner/target | Mandatory proof |
|---|---|---|
| A — SQL-free | Windows net48 xUnit, frontend unit/TypeScript/production build | no unauthorized web operator, no auto retry, frozen codec/positive-negative endpoints, permission/identity failures |
| B — SQL candidate | Protected SQL tests for B02/B03/B04, owned disposable schema13 | all six idempotency/races, terminal audit, original result, backup and compact tombstones; physical SQL oracle |
| C — actual host | owned IIS Express direct HTTPS + SQL (non-browser) | startup hash, exact13 mode/principal, wrong header/Origin/cookie/security negative tests, host queue admission and teardown |
| D — browser | independently pinned Chromium then Edge against *the same published pair* (fresh owned fixture per run) | six original commands, real manual same-key consent, double tab/reload/keyboard, operator-first/original-first, SQL oracle |
| E — cutover | controlled old/new built bundles and matched host/DB mode | cached v1 and current v2/WEB5 positive status, new→old safe fallback, pending UNKNOWN/revoked/grants/epoch, rollback to compatible pair |
| F — load | bounded authenticated mixed actors on same admitted host | Create/Update/Post/status/refresh and stock reads with p50/p95/p99, throughput/errors, memory/CPU/SQL pool/locks/journal |
| G — recovery | dedicated owned backup/restore + host interruption + support/alert drill | measure service RPO/RTO, exact journal/ledger consistency, stale epoch rejection, UNKNOWN age and support handling |
| H — attest | B06/F05 separate decisions | exact targets vs observations and independent Owner dispositions; no release implied |

Do not run concurrency against an ordinary foreground developer server, an unaudited PID4 listener or another test agent's database. No original manual resend UI toggle outside the feature-flagged owned nonproduction instance.

### Cutover specifics

- Base ADR0015 is **additive**: old cached WEB4 bundle may use original v1 protected read/write only within matched supported host and actor/epoch permissions; new frontend vs old `web4`/schema12 host must find missing v2/resend and keep that feature disabled.
- Original v1 `DISPATCHED/UNKNOWN` persists across updated browser assets. Readback uses its original Create/Update Write or Post grant, **not ordinary Read as substitute**. Denied ordinary Read but original Write/Post allowed is a positive status case; revoked original grant denies.
- Test old tab in flight across host switch, old/new assets cache behavior and old host restart with same epoch; no code deploy epoch rotation. A schema13 database cannot run an old host that requires exact12: test rejection or use a **separately prepared compatible disposable DB** for rollback, not in-place unsafe downgrade.
- On negative/expired/rejected/wrong grant, the client remains PROTECTED. No new key/mutation is dispatched from lifecycle events.

### Representative load protocol

Start with a **smoke profile** to prove host admission and teardown, then a separately named **measurement profile** with explicit test script/version and stable actor cohorts; document CPU, RAM, DB compatibility mode and timeout/limits. Suggested design profiles, **not SLA targets**: concurrency 1, 4, 8 and 16 actors, continuous bounded duration and enough observations per operation type to report percentiles honestly. For p99, aim ≥1,000 observations per critical class where resources allow; otherwise mark `INSUFFICIENT_SAMPLE/NOT MEASURED`, not PASS. Measure independently: per-type count, p50/p95/p99, timeout/HTTP 4xx/5xx, throughput, SQL locks/deadlocks/connection pool, journal count/result-byte footprint, audit/limiter response and worker memory/CPU. Stop on runaway data growth, unexplained two business effects, unknown host identity or unowned SQL resource.

An approved target register must be set by Owner **after a baseline study and before declaring staging acceptance**. Do not invent SLAs or turn a laptop's measured localhost p99 into production capacity.

## 4. Audit, limiter and recovery drills

- Existing runtime `AuthAttemptLimiter` is per-worker; validate **one verified worker** and fail-closed behavior for bounded direct IIS Express. A multiworker production assertion needs a shared atomic limiter plus real multiworker proof; not implicitly covered.
- Existing `TraceSecurityAuditSink` uses operator-selected TraceSource listeners; confirm an actual protected destination, ACL/rotation/retention, redaction, unavailable-sink policy and durable incident correlation. Do not print JWT, HttpOnly refresh token, bearer access, SQL connection string, user payload or complete business documents.
- Operator `inspect` and `record-not-executed` use an independent restricted SQL principal, with dry-run and typed execute. **`SqlWebOperationMaintenance.CompactAsync` requires database `CONTROL` today**: do not run it with or elevate the ordinary operator/web runtime identity. Use an explicitly approved separate maintenance principal on the exact owned DB, or an independently reviewed narrow-permission migration before exercising compaction. Capture valid/denied attempts and authoritative SQL audit and case status **separately** from frontend UX. COMMITTED-result-unavailable support closure never marks original client operation settled.
- Backup clone into a newly owned DB and rotate epoch **before writer admission** only when clone/rewind; leave normal host restarts unchanged. Measure service-level failure detection → trusted restored authenticated service, compare ledger/journal/stock and unresolved intents. Distinguish backup time, restore time, RTO and RPO.
- Two historical WEB5 SQL allocations/manifests and retained RED probe directories remain blocked/untouched; new isolated smoke cleanup `verified=1 remaining=0` never means the old quarantines were deleted.

## 5. Evidence, cleanup and decisions

Use separate dated reports for packs B–H under `docs/evidence/web6/`, with sanitized selected commands, test source SHAs, actual TRX/run URLs, native browser/SQL discovery counts and oracle checks. Store logs containing secrets, DB connection settings and test payload outside Git. Never merge a PR that claims protected SQL PASS from skipped tests or silently disabled CI.

Report each gate as `PASS / FAIL / BLOCKED / NOT RUN / NOT_APPLICABLE / OWNER_ACCEPTED_EXCEPTION` with exact owner and source. Add a failure reproduction/retained quarantine row for failed runs; do not erase them after a later successful rerun. On both repos verify tree hash, all support links, frozen WEB5 docs unchanged and CEX status untouched.

**Final separation:** a *nonproduction* user-confirmed same-key resend pilot may be Owner accepted after B04 and F02/F03 tests even when full IIS is unavailable; this does not make STAGING ACCEPTED or PRODUCTION GO. B06 assembles the measured final pair and asks for a new, explicit Owner decision. Missing full IIS/Narrator gates stay NOT RUN exceptions, and missing audit/limiter/restore/target evidence stays NOT RUN/DEFERRED rather than being waived.

## 6. B05 execution disposition — 2026-11-10

**Earlier checkpoint, before the Task 6 bootstrap attempt below.**

The implementation now provides sealed candidate admission, protected audit/health behavior, paired browser bridge, current/old/restored cutover adapters, bounded metrics and owned recovery orchestration. SQL-free local gates passed, including Application 363/363 and HttpHost 635/635. The pure affected Security selection passed 253 with one intentionally disabled live-recovery skip.

No private candidate manifest/root, published artifacts, certificate, browser installation pin, owned SQL control root or recovery/workload authority was supplied for a live run. Consequently each exact opt-in selector was enabled and executed serially, then failed its prerequisite contract with 0 pass / 1 fail / 0 skip before allocation. Pair/cutover/recovery remain BLOCKED / NOT RUN and workload remains NOT MEASURED. No live host, SQL, browser, performance, RPO or RTO result exists. Retained failed-attempt resources and historical quarantines were not cleaned or adopted. Ordinary resend remains OFF; staging is NOT ACCEPTED and production remains NO-GO.

## 7. Controlled bootstrap and protected SQL approval

The implemented net48 test-support executable is `tools/MyDmsVn.Web6.CandidateBootstrap`; it is not a shipped runtime or operator tool. Use its exact `authorize`, `prepare`, then `run` commands with new private roots and approved exact source commits. `authorize` requires the exact acknowledgement `I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS` and creates only Owner authority. It does not create independent SQL authority or approve pilot enablement.

The existing designated local SQL approval is a legacy document. On the Task 6 attempt it had an unprotected inherited ACL and a different schema, so it could not authorize WEB6 allocation. Owner identity, a nonproduction boolean, a matching stored fingerprint or a successful read-only connection to master do not waive that refusal. Keep its bytes and ACL unchanged. Do not copy it into protected storage, repair its ACL, reinterpret its endpoint or derive a WEB6 approval from it.

The Owner subsequently explicitly authorized issuing a **new independent protected SQL approval**, which passed the checks below for attempts02–04. The legacy document was not copied or repaired. Any newly issued approval must retain all of these properties; an existing independent approval may be reused only under explicit authority after rechecking exact ACL/schema/digest/expiry/effective-master binding:

- Absolute local file path outside Git, no reparse components or hard-linked file, UTF-8 without BOM, size at most 16 KiB, and exactly the fields shown below. Keep connection strings and credentials outside this file.
- Current Windows user's SID owns the file. The file DACL is protected from inheritance and contains exactly one non-inherited Allow ACE granting that same SID FullControl, with no inheritance/propagation flags and no other ACEs.
- `serverSha256` is lowercase SHA-256 of UTF-8 bytes of the exact value returned by `SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'))` on the approved instance. Bind it to independent Owner/operator verification of the designated server; do not hash a connection string or treat an unverified legacy document as authority.
- `expiresUtc` is an unexpired ISO-8601 timestamp at most 24 hours ahead when validated. Purpose is exactly `web6-cutover`, version is integer `1`; unknown fields are refused.
- The explicitly designated `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` must specify and actually reach `master` on that same approved server. Attachment, user-instance and failover-partner options are refused. Allocator, restricted runtime, operator and maintenance responsibilities remain separate.

Schema template only; placeholders are not executable authority:

```json
{"version":1,"purpose":"web6-cutover","serverSha256":"<64 lowercase hex characters>","expiresUtc":"<approved unexpired UTC timestamp>"}
```

Use a new candidate attempt/root and new Owner authority/request with that approval path; retain the failed attempt. The strict request must select both browser projects, `actorCohort=web.smoke`, `workloadSeconds=60` and recovery authorization. Its Owner authority must cover the approved localhost origins 44321/44322. CLI syntax, using protected path variables:

```powershell
& $bootstrap authorize --input $ownerInput --output $ownerAuthority --acknowledge 'I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS'
& $bootstrap prepare --request $request --authority $ownerAuthority --authority-sha256 $ownerAuthoritySha256
# Only after fresh PREPARED / PIN_MATCHED and ordinary-artifact OFF proof:
& $bootstrap run --prepared $prepared --prepared-sha256 $preparedSha256
```

Fresh prepare must validate the reviewed frontend/archive pins, clean detached source worktrees, ordinary OFF/candidate ID markers, full publish/dist equality and existing tool/browser/TLS pins. It allocates no live SQL/IIS/browser resources. A BLOCKED result is not a prepared receipt; do not invoke run or hand-create a receipt to bypass it.

Run invokes exactly five selectors serially: Chromium pair, Edge pair, cutover, workload, recovery. Each must report 1 passed / 0 failed / 0 skipped, workload must cover profiles 1/4/8/16 and at least 1,000 successful observations for each of the six mutation classes, and recovery must observe zero-loss RPO with positive RTO. Only the final independent disposition can produce CandidateVerified. Any live failure/uncertainty stops further selectors and retains resources according to ownership receipts; no automatic rerun or historical cleanup is authorized.

#### Cutover deadline and reset protocol

Only the exact protected cutover selector has a 3,600-second work gate. Its 3,660-second process ceiling reserves the extra minute for owned process termination and output drain. The 3,300-second parent covers both serial browser packs and their handoff. Each browser has a 1,560-second pack inside its own 1,620-second controller.

The cached-WEB4 and current-client/old-host scenarios each use 420 seconds including 180 seconds reserved for reset. The restart/revoke/restore scenario uses 690 seconds including a 60-second reset reserve. Each request carries integer `requestAllowanceMs` sampled before connect and clamped by the remaining body or reset budget and its action cap. The action caps are 180 seconds for the four host on/off actions, `restoreEpoch` and `resetCutover`, 210 seconds for `sameEpochRestart`, 60 seconds for `committedUnavailable`, and 45 seconds for `revokeGrantOn`, `revokeGrantOff` and `verifyNoCutoverMutation`. An allowance of 5,000 ms or less is refused before connect. The controller reserves 5,000 ms for the response and acknowledges success only after handler completion and post-verification. A failed or timed-out request is never replayed.

The scenario cleanup hook can send one `resetCutover` request from the remaining reserve. It suppresses reset after an in-flight or failed control request. The controller admits reset only after prior work has joined and the session is stable. The first two scenarios may need one verified switch back to the current host. After a successful third scenario, reset verifies the restored host in place. The in-body `revokeGrantOff` still precedes `restoreEpoch`. If the controller, reset or drain cannot prove stable completion, retain the attempt and stop subsequent selectors. [Bounded candidate evidence](../evidence/web6/20261114-cutover-deadline-bounded-candidate.md).

Task 6 candidate `web6-20261110-25a1cf46` ran authorize once (exit 0) and prepare once (exit 4, `BLOCKED_PREFLIGHT`). A read-only diagnostic confirmed the protected-SQL-approval refusal after authority/request/tool/certificate binding. The task root remained absent, SQL control parent empty and all five live selectors NOT RUN. See [exact hashes, counts and retained records](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-controlled-bootstrap-attempt). Task 6 remains open; ordinary resend OFF, full IIS/Narrator NOT RUN, staging NOT ACCEPTED and production NO-GO.

### Attempt04 terminal boundary

Candidate `web6-20261110-182d4285`, backend `18672300d642eef7944fbd540a42b3654783ba5b` / frontend `13179f56a3381275c0652f6edaf29258f18927f2`, prepared successfully with ordinary OFF, candidate ID and full publish/dist equality. Prepared SHA-256: `0a7376b0175c9e5fc1fa0fac254a9edeb2c3ca7b3293e83089ad4d9579e28c93`. The reviewed preparation fix streams the exact manifest bytes to the backend's protected create-new writer; it preserves directory leases instead of using Node hard-link publication beneath a held parent.

Its one run ended `FailedRetained` (exit 5): Chromium selector 0 passed / 1 failed / 0 skipped. `FindRootFile("CleanArchitecture.sln")` failed after owned SQL setup and host-runtime configuration creation; the checked-in solution is `MyDmsVn.sln`. IIS/browser launch and all later selectors were NOT RUN. One database/runtime login, SQL control root and host-runtime configuration remain retained under private allocation evidence; the terminal receipt requires disposition proof despite successful child-process drain. No cleanup or rerun was performed. See [full attempt evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt04-prepared-then-failedretained).

Do not repair retained artifacts or replay this run. A new live attempt requires a reviewed source fix **and new explicit Owner authorization**, fresh candidate/root/Owner authority/request and revalidated independent SQL/tool inputs. The failed receipt cannot advance to CandidateVerified. Ordinary resend OFF, staging NOT ACCEPTED and production NO-GO remain unchanged.

### Attempt05 terminal boundary

After reviewed backend source-admission correction `914b2196d41999f6f465ae076e4fcbe4ef524a87` and new explicit Owner authorization, candidate `web6-20261110-300ef832` prepared with exact frontend source `13179f56a3381275c0652f6edaf29258f18927f2`. Frontend evidence head `cdae4f4` is distinct from the executable reviewed pin; a docs descendant does not change that pin. A new SQL approval was issued through the net48 protected create-new writer in existing Windows PowerShell, with current-user-only protected ACL and fresh effective-master binding. A preliminary PowerShell 7 issuance-only failure remains retained separately.

Prepare passed `PIN_MATCHED` with ordinary OFF and complete dist/wwwroot equality; SHA-256 `25323e16d014875c8b8e8617749028c05a6d407153f4c7d98fbd184b57b1d042`. The one run stopped `BlockedNoAllocation` because installed Edge bytes differed from the prepared tool pin (prepared `157.0.4341.42`, observed `157.0.4341.46`). Terminal SHA-256 `5066422846b7aff633a0cac7093a564816745d689ab2e872d4ad94aacb991844`. All five selectors are NOT RUN; no new SQL/host/browser resource was allocated. All prepared artifacts/authorities/receipts are retained, and attempt04 resources remain untouched. See [attempt05 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt05-prepared-then-blockednoallocation).

Never refresh a terminal prepared tool pin, alter its receipt, disable the guard or replay its root. A future attempt needs new explicit Owner authorization and fresh candidate/root/SQL approval/Owner authority/request with independently revalidated machine inputs. No browser installation, update-policy change or global mutation is implied. Task 6 stays OPEN; ordinary resend OFF, staging NOT ACCEPTED, production NO-GO.

### Attempt06 terminal boundary

The Owner authorized one new attempt after a bounded read-only Edge stability preflight. Four observations over 30 seconds matched installed Edge `157.0.4341.46`; no locks, copies or updater-policy changes were introduced. Candidate `web6-20261110-2f863589` used the same reviewed source pins (`914b2196` / `13179f5`), fresh roots and new independently verified protected SQL/Owner authorities. Prepare passed `PIN_MATCHED`; run started 0.888 seconds after prepare completed. Prepared SHA-256 `0b953a41bbbcf2e6be81e7b0f78ab92edf3c251f8b203a40f66ca6d9782b0850`.

The sole Chromium wrapper failed **0 passed / 1 failed / 0 skipped** after IIS Express and Chromium launched; nested Playwright had 1 failure and 2 tests not run. Candidate control surfaced SQL oracle error `Invalid column name 'SaleNumber'`: `Web6CandidateOracle.VerifyResultProjection` queries `SaleNumber`, while migration/mapping uses `SaleNo`. Cleanup is `Quarantined`; one owned SQL database/runtime login, control root, host-runtime and candidate evidence/artifacts remain retained. Terminal SHA-256 `cdb76de3ec813e5106512f2872e46e5e85c1538117a4eb0de3599ee4a57bbe0b`; all later gates NOT RUN. No manual cleanup/retry occurred. See [attempt06 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt06-chromium-failure-and-retained-quarantine).

Do not patch retained resources or replay this attempt. A future attempt requires a reviewed source correction and new explicit Owner authorization, fresh candidate/root/SQL approval/Owner authority/request, and revalidated machine inputs. Process drain is not final disposition or CandidateVerified. Ordinary resend OFF, staging NOT ACCEPTED, production NO-GO remain unchanged.

### Attempt07 terminal boundary

With new explicit Owner authorization after the reviewed Sales header projection correction, candidate `web6-20261110-dc006b02` used exact backend `0e090643e2d9de01bcbd98ae5dcfd08174b29216` and frontend `13179f56a3381275c0652f6edaf29258f18927f2`. Edge stability and complete private Node/npm validation passed before new root/approval creation. Fresh protected SQL/Owner authorities were issued; prepare passed `PIN_MATCHED`, ordinary OFF and full dist/wwwroot equality. Prepared SHA-256 `33d4e18dad8fb7e41a61e8cd9cec3bf0d994a39e2cd1cac1e38db987fa6f4dcd`; run started 1.181 seconds later.

The sole Chromium wrapper failed **0 passed / 1 failed / 0 skipped**. Nested Playwright had 1 failed/2 not run; control drain reported SQL syntax error near unquoted `LineNo` in the oracle Sales line projection. The Receipt line projection also contains unquoted `LineNo`; source correction is outside this retained run. Candidate cleanup is `Quarantined`; SQL database/runtime login/control, host-runtime and evidence/artifacts remain retained. Terminal SHA-256 `c879eb79433a040230027eeeda2c8596ab4ff33a9830a8390fe5d7d14e70851d`; later gates NOT RUN. See [attempt07 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt07-sql-syntax-failure-and-retained-quarantine).

Do not rerun or patch the retained candidate. A reviewed oracle SQL correction and new explicit Owner authorization are required before a fresh root/approval/candidate chain. No historical resource cleanup/adoption or global browser change is implied. Task 6 remains OPEN; ordinary resend OFF, staging NOT ACCEPTED, production NO-GO.

### Attempt08 terminal boundary

After complete oracle identifier review and new explicit Owner authorization, candidate `web6-20261110-8599b4e1` used backend `6988a81af82e8b625b6a8806eccb1186653ffc51` / frontend `13179f56a3381275c0652f6edaf29258f18927f2`. Read-only Edge stability, complete private Node/npm validation and independent effective-master binding passed before new root/approval issuance. Fresh protected SQL/Owner authorities were created. Prepare passed `PIN_MATCHED`, ordinary OFF and full dist/wwwroot equality; SHA-256 `cf9efc189f94b5312e1213ee8513166e6b808257e5dbb7dc103e7001c8d614df`. Run started 1.211 seconds later.

The sole Chromium wrapper failed **0 passed / 1 failed / 0 skipped**. Nested Playwright passed the six-operation and committed-response-loss tests, then failed the two-tab/candidate/grant/HTTP boundary test at the shared control socket-failure callback: **2 passed / 1 failed / 0 not run**. No SQL oracle exception appears in the current TRX. The log does not distinguish timeout, socket error or EOF, or identify the failing control action. The source's 10-second socket timeout versus longer host-switch waits is a hypothesis, not a proven cause.

Cleanup is `Quarantined`; SQL database/runtime login/control, host-runtime and prepared/candidate evidence remain retained. Terminal SHA-256 `59be45ab81cc586cc26fe9f7d5c27b537a34bc2d5cfb20b05fc53c5ed71a9a45`; later gates NOT RUN. Process drain is not accepted cleanup or CandidateVerified. See [attempt08 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt08-partial-chromium-results-and-retained-quarantine).

Do not replay or patch retained resources. A reviewed control-boundary correction and new explicit Owner authorization are required before a fresh candidate/root/approval chain. No post-failure SQL query, manual cleanup or historical-resource adoption occurred. Task 6 stays OPEN; ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt09 terminal boundary

After reviewed sanitized socket diagnostics and new explicit Owner authorization, candidate `web6-20261110-69528e92` used backend `9665dfd6aafab3ccaca1aed3f928067b87e25373` / frontend `a8afa96dd81e3a282c6351fd6d1786d331a6479d`. Preissuance Edge stability, complete private tool validation and independent effective-master binding passed; new protected SQL/Owner authorities were issued. Prepare passed `PIN_MATCHED`, ordinary OFF/full dist equality, SHA-256 `34d18b61ba52767b5816673b297ac4042bf843c30d00b581ebb92a46a437f235`. Run began 0.813 seconds later.

Chromium wrapper: **0 passed / 1 failed / 0 skipped**; nested cases **2 passed / 1 failed / 0 not run**. The third case diagnostic is `{"action":"candidateMismatchOn","terminal":"timeout","phase":"response-header","elapsedMs":10000,"category":"socket-timeout"}`, with no optional code. This identifies the client timeout boundary; backend restart failure/completion is not established. No SQL oracle exception appears in current TRX.

Terminal `FailedRetained` SHA-256 `27d59e9907e7ef32272e2f7cbb8d2b4a3580deb4cf818353846ebfb454948b90`; cleanup `Quarantined`, later gates NOT RUN. Retain original SQL database/runtime login/control, host-runtime and all candidate/prepared evidence. No retry, post-failure SQL query, manual cleanup or historical-resource access occurred. See [attempt09 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt09-diagnosed-control-timeout-and-retained-quarantine).

A reviewed control-boundary correction and new explicit Owner authorization are required for any fresh candidate/root/approval chain. Do not alter terminal artifacts or expand cleanup authority. Task 6 OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt10 terminal boundary

With new explicit Owner authorization after reviewed mismatch response budgets, candidate `web6-20261110-dd12de6d` used backend `aa59990db966c450378c1a5981190174258ef9c8` / frontend `b91aba84b8e42b8aac78bd113c732ad9c5bff715`. Read-only preissuance Edge/tool/master verification passed; new protected SQL/Owner authorities were created. Prepare passed `PIN_MATCHED`, ordinary OFF/full dist equality; SHA-256 `b27cce07365b1e7f1a2885436f3e3a6acebb5faa0b1f22e692d6035371c33cd4`. Run began 0.611 seconds later, with reviewed 170-second mismatch header/480-second third-case budgets and no external termination.

Chromium wrapper: **0 passed / 1 failed / 0 skipped**; nested cases **2 passed / 1 failed / 0 not run**. Third case failed `expect(firstResend).toBeEnabled()` at `web6-manual-resend-live.spec.ts:238:73` after 5,000 ms because the element was absent. Current snapshot shows login UI; current audit ends in denied refresh. Exact boundary-loop iteration/HTTP status is not identified. No socket-failure diagnostic or SQL oracle exception appears; no further root cause is claimed.

Terminal `FailedRetained` SHA-256 `c1425096003bbde5610ebf1ce1c86736830b837bc875744b68c79459f71928fd`; cleanup `Quarantined`, later gates NOT RUN. Retain SQL database/runtime login/control, host-runtime and all candidate/prepared evidence. No retry, post-failure SQL query, manual cleanup or historical-resource access occurred. See [attempt10 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt10-boundary-assertion-failure-and-retained-quarantine).

A reviewed boundary correction plus new explicit Owner authorization is required before another fresh candidate/root/approval chain. Do not alter terminal artifacts or expand cleanup authority. Task 6 OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt11 terminal boundary

After reviewed auth/recovery UI-boundary correction and new explicit Owner authorization, candidate `web6-20261110-dcc4ff39` used backend `87d756de1e2c8567b6ddfac052b418790d6454c5` / frontend `7ab16026ca2cf963ef378a843c609fa5a4b69cd0`. Preissuance Edge/tool/master validation passed; new protected SQL/Owner authorities were created. Prepare passed `PIN_MATCHED`, ordinary OFF/full dist equality, SHA-256 `333786b44c6c82331ae9531076bcfe458a4465610f607b2f04f755e279ad16d2`. Run began 0.838 seconds later, with reviewed budgets and no external termination.

Chromium wrapper: **0 passed / 1 failed / 0 skipped**; nested **2 passed / 1 failed / 0 not run**. Third case failed the named `eligibility401` prepare predicate at spec `274:97` after 5,000 ms. Diagnostic: context/eligibility/refresh statuses null, login false, recovery true, resend disabled, confirmation false, additional mutations 0. These are last observed response/UI values; neither absence of requests nor a root cause is established. Re-login/restore/500 continuation did not complete.

Terminal `FailedRetained` SHA-256 `cf358502bfc840352d7a53537672c76592e2f518071b7a15bc44add0d82dcdab`; cleanup `Quarantined`, later gates NOT RUN. Retain original SQL database/runtime login/control, host-runtime and all candidate/prepared evidence. No retry, post-failure SQL query, manual cleanup or historical-resource access occurred. See [attempt11 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt11-named-auth-boundary-failure-and-retained-quarantine).

A reviewed boundary correction plus new explicit Owner authorization is required before a fresh candidate/root/approval chain. Do not alter terminal artifacts or expand cleanup authority. Task 6 OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt12 terminal boundary

After reviewed trigger-request correlation and new explicit Owner authorization, candidate `web6-20261110-3fff3d2f` used backend `0711d0b2fcd44b86fef48cc9dae5dadfc0a7089d` / frontend `4a6541c419b1293a04c57d67bc6faaf1254ba391`. Preissuance Edge/tool/master validation passed; new protected SQL/Owner authorities were created. Prepare passed `PIN_MATCHED`, ordinary OFF/full dist equality, SHA-256 `c33ae71822620a90fa247c62505178293a24427486ad8df27b47c233f6a72bd4`. Run began 0.821 seconds later with reviewed 50/170/480-second budgets and no external termination.

Chromium wrapper: **0 passed / 1 failed / 0 skipped**; nested **2 passed / 1 failed / 0 not run**. Third case failed named `eligibility500` after the 5-second predicate window at spec `260:79` / observer `69:19`. Correlated trigger responses were context 200 and eligibility 503; predicate requires 500. Recovery/resend remained visible/enabled, confirmation false, additional mutations 0; no omitted observer events. Prior 401 re-login/restore completion follows from serial control flow, without establishing full pair acceptance.

Terminal `FailedRetained` SHA-256 `806abca4f404fa70267ba2dd8ee88c9cad3dacd787eb4c21d4909c6bc3a5470c`; cleanup `Quarantined`, later gates NOT RUN. Retain original SQL database/runtime login/control, host-runtime and all candidate/prepared evidence. No retry, post-failure SQL query, manual cleanup, status broadening or historical-resource access occurred. See [attempt12 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt12-correlated-eligibility-status-mismatch-and-quarantine).

A reviewed boundary correction plus new explicit Owner authorization is required before another fresh chain. Do not alter terminal artifacts or expand cleanup authority. Task 6 OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt13 terminal boundary

After reviewed eligibility-unavailable HTTP503 correction and new explicit Owner authorization, candidate `web6-20261110-85909668` used backend `9823bb4f78f81f5b725c23e94f75aa4e7a8a4702` / frontend `9a1a2ba17414fabadace5edfb16e85ade6ecffbc`. Preissuance Edge/tool/master checks passed; new protected SQL/Owner authorities were created. Prepare passed `PIN_MATCHED`, ordinary OFF/full ten-file dist equality, SHA-256 `7accfe43981c680123a05ed0e554be8163f2bf1965cfc8c19e7a9882eb6d5ca2`. Run began 0.808 seconds later, reviewed budgets respected, no external termination.

Chromium wrapper: **0 passed / 1 failed / 0 skipped**; nested **2 passed / 1 failed / 0 not run**. Exact control diagnostic: `{"action":"verifyOneEffect","terminal":"eof","phase":"response-header","elapsedMs":10,"category":"incomplete-response"}`. Current TRX records `Candidate committed identity is missing or duplicated.`; for this action the guard combines missing expected operation, different kind, or journal count other than one. The failing condition and SQL commit state remain unproven. Prior HTTP503/two-tab assertions returning is a serial-source inference, not full native acceptance.

Terminal `FailedRetained` SHA-256 `c9a5fd3c176ed318d52f502c4fbd605f3c008919a0ca3eb616d11a881ee349fe`; cleanup `Quarantined`, later gates NOT RUN. Retain original SQL database/runtime login/control, host-runtime and all candidate/prepared evidence. No retry, post-failure SQL query, manual cleanup or historical-resource access occurred. See [attempt13 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt13-committed-identity-verification-failure-and-quarantine).

A reviewed boundary correction plus new explicit Owner authorization is required before a fresh candidate/root/approval chain. Do not alter terminal artifacts or expand cleanup authority. Task 6 OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt14 terminal boundary

After reviewed one-effect response/commit correction and new explicit Owner authorization, candidate `web6-20261110-37528a15` used backend `a5f264dd5ca3108a3be50fd8bf60c5d82dcc01af` / frontend `0dc8a2ff0d88218af9bc156298d971fd3e60e715`. Preissuance Edge/tool/master checks passed; new protected SQL/Owner authorities were created. Prepare passed `PIN_MATCHED`, ordinary OFF/full ten-file equality, SHA-256 `fac7d517d26fd5feca7049c6dc7b476c3d8072f92593ff4a2d9d4a7aadbe2d20`. Run began 0.770 seconds later, reviewed budgets respected, no external termination.

Chromium wrapper: **0 passed / 1 failed / 0 skipped**; nested **2 passed / 1 failed / 0 not run**. Third case failed `candidateMismatchOn/action=prepare` at spec `263:79` / observer `69:19` after 5,000 ms. All response statuses remained null; login false, recovery true, resend disabled, confirmation false, additional mutations 0. Trace ends `request:trigger:context` with zero omitted events. A request was observed, but no correlated response before failure; null statuses do not establish a server outcome/root cause. First six-operation case using the revised helper passed; third-case two-tab response/commit checks were not reached.

Terminal `FailedRetained` SHA-256 `e36421c41c20a18a206badb6e08b1b0385b653e41500def7ed0e0b0da2fb32a1`; cleanup `Quarantined`, later gates NOT RUN. Retain original SQL database/runtime login/control, host-runtime and all candidate/prepared evidence. No retry, post-failure SQL query, manual cleanup or historical-resource access occurred. See [attempt14 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt14-candidate-mismatch-observation-failure-and-quarantine).

A reviewed boundary correction plus new explicit Owner authorization is required before a fresh candidate/root/approval chain. Do not alter terminal artifacts or expand cleanup authority. Task 6 OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt15 terminal boundary

With new explicit Owner authorization after reviewed boundary/parent budgets, candidate `web6-20261110-f1330b4a` used backend `08123da11f21f57be73cd24992a57d7bce9c6d61` / frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0`. Preissuance Edge/tool/master verification and new protected SQL/Owner authorities passed. Prepare `PIN_MATCHED`, ordinary OFF/full ten-file equality; SHA-256 `6976f8a22906abb9213b4175c5cd8847b97a221d3921db3c3ef547cbc928e3b1`. Run began 0.791s later. Reviewed 35/50/65s boundaries, dispatch 65s/response 35s, mismatch 170s and 32/39/41/44m hierarchy were respected; no external termination.

Chromium and Edge each **1 passed / 0 failed / 0 skipped**, native ACCEPTED; state `PairPassed`. Cutover **0 passed / 1 failed / 0 skipped**, build-authority IOException before cutover allocation/host launch. Read-only native hash comparison found all source/tree/artifact-to-authority checks matching; only archived frontend dist versus full current publish equality fails. No byte drift was observed, no authority/source/digest patch or rerun occurred.

Terminal `FailedRetained` SHA-256 `03e9cbc1f87284cac0f597752bff681c0e4079448212c9e94491139921fb3bff`. Both pair native receipts certify cleanup; their runtime directories are absent and SQL control parent is empty. No cutover allocation receipt/root exists. Prepared artifacts/authorities/receipts remain retained; no accepted final independent disposition. Do not mark accepted pair cleanup Quarantined. See [attempt15 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt15-browser-pair-pass-and-cutover-build-authority-failure).

A reviewed cutover correction plus new explicit Owner authorization is required for a fresh candidate/root/approval chain. No post-failure SQL query/manual cleanup or historical-resource access occurred; this evidence transfers no cleanup authority. Task 6 OPEN, workload/recovery NOT RUN, CandidateVerified absent; ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO.

### Attempt16 terminal boundary

Candidate `web6-20261113-8d732d8c` used backend `4644b8e9416c2e34d8b7a57e40cdd62e35b052be` and frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0`. The supplied private tool path was absent. An authorized copy from installed Node/npm matched the reviewed digests, counts and protected ACLs. Read-only Edge and effective-master checks passed before a new protected SQL approval and one authorize/prepare/run chain. Prepare `PIN_MATCHED`, ordinary resend OFF, all 10 dist/wwwroot files equal, zero live allocation. Prepared SHA-256 `ba23575e22b600768d1b375e446635f8143eb243d7e3f285a0fbc38e30a9402d`.

Chromium and Edge each passed one test with native `ACCEPTED` and accepted cleanup. The `PairPassed` SHA-256 is `18983f570ab895626134cd1cb65b78b363d8d39ae296cc985fea00419163d82c`. Cutover passed the paired-artifact admission and confirmed all three database and all three login allocations, then failed one test in the current epoch seed before IIS/browser launch. Terminal `FailedRetained` SHA-256 `0442269c4fac79e7b9aa8efaf951b2976abc48683e685d56f174764e9d4ac78a`. Cutover retained three databases, three logins and their SQL control roots. The protected records provide ownership evidence and carry no deletion authority. Workload/recovery did not run. Final independent disposition and `CandidateVerified` are absent.

Task 6 remains open. A new chain requires new explicit Owner authorization after review of the epoch seed failure. Keep attempt16's resources under their existing ownership evidence. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. [Attempt16 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt16-browser-pair-pass-and-cutover-epoch-seed-failure).

### Attempt17 terminal boundary

Candidate `web6-20261113-0fa528e7` used backend `485570368afd64d814d024f0fb435542ff366712` and frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0`. The private tool layout, Edge and effective-master identity were revalidated before the new protected approval. One authorize/prepare/run chain ran. Prepare `PIN_MATCHED`, ordinary resend OFF, ten dist/wwwroot files equal and zero live allocation. Prepared SHA-256 `0004c675924dee2627778a64f8d04557ca349020431d3b2be6eabb5db901a148`.

Chromium and Edge each passed one selector with native `ACCEPTED` and verified cleanup. `PairPassed` SHA-256 `d5ef840228c4c93a354556e6beafcaad4bb680fbc218b34b17e81263f9181b16`. Cutover admitted the pinned artifacts and all three seed calls returned. The current baseline snapshot passed a null SQL JSON scalar to `JArray.Parse` and failed before IIS/browser launch. Terminal `FailedRetained` SHA-256 `5042f06f3e890d92b6c0ebab249e80d8a02fd3a814973ed92aa78f4f07d9a7e3`. Three cutover databases, three runtime logins and three SQL control roots are retained with linked ownership records.

Workload/recovery did not run and `CandidateVerified` is absent. Task 6 remains open. A fresh chain needs new explicit Owner authorization after review of the snapshot failure. [Attempt17 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt17-browser-pair-pass-and-cutover-snapshot-failure).

### Attempt18 terminal boundary

Candidate `web6-20261113-0f79b843` used backend `c1afa4fbb81b17bee08b5e880b16ffca21d448df` and frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0`. Approved Edge, Node/npm and SQL master checks preceded a new protected SQL approval and one authorize/prepare/run chain. Prepare `PIN_MATCHED`, ordinary resend OFF, ten dist/wwwroot files equal and zero live allocations.

Chromium and Edge each passed one selector with native `ACCEPTED` and accepted cleanup. `PairPassed` SHA-256 `d13016f10c080941cb0f17ccd8497ead0f8e2930fcf27740ae7c5261d1b46211`. Cutover passed seed and initial snapshot reads and stopped at the current deployment contract path check before IIS/browser launch. Terminal `FailedRetained` SHA-256 `fbb8cfad196013c926268edb288e86c6ffe5204f5c27d6bf6dcb68cc1915713c`. Three databases, three runtime logins, three SQL control roots and current cutover control/runtime/browser staging directories remain retained.

Workload/recovery did not run and `CandidateVerified` is absent. Task 6 remains open. A new chain needs fresh explicit Owner authorization after review of the path refusal. [Attempt18 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt18-browser-pair-pass-and-cutover-path-refusal).

### Attempt19 terminal boundary

Candidate `web6-20261113-b93cedcb` used backend `be8c61b43e2311ee0bb59d8ea1c152df3722ffcc` and frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0`. Approved Edge, Node/npm and SQL master checks preceded a new protected SQL approval and one authorize/prepare/run chain. Prepare `PIN_MATCHED`, ordinary resend OFF, ten dist/wwwroot files equal and zero live allocations.

Chromium and Edge each passed one selector with native `ACCEPTED` and accepted cleanup. `PairPassed` SHA-256 `f1a919bcab6d53627d22ee91474ca7152696084320decbe67a0f911eadee9bda`. Cutover completed artifact sealing and started IIS Express. The process command-line check refused admission before any cutover browser case. Terminal `FailedRetained` SHA-256 `a7af67ac022d439c3d0d95ad1521e57cb317606f43faea20f5187d016b38e24c`. Three databases, three runtime logins, three SQL control roots and all nine cutover control/runtime/browser staging directories remain retained.

Workload/recovery did not run and `CandidateVerified` is absent. Task 6 remains open. A new chain needs fresh explicit Owner authorization after review of the host identity refusal. [Attempt19 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt19-browser-pair-pass-and-cutover-iis-identity-refusal).

### Attempt20 terminal boundary

Candidate `web6-20261113-11ddfd6c` used backend `512035c8e1f610bf5f9a6ddbe47e57d30ae0d46a` and frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0`. Approved Edge, Node/npm and SQL master checks preceded a new protected SQL approval and one authorize/prepare/run chain. Prepare `PIN_MATCHED`, ordinary resend OFF, ten dist/wwwroot files equal and zero live allocation.

Chromium and Edge pair each passed one selector with native `ACCEPTED` and accepted cleanup. `PairPassed` SHA-256 `e1f5428bcfcae2bcb0c573b17178b5e8974722b31c01e1305b69fd140d0470af`. The shared IIS launch-plan capture admitted the first cutover host. Chromium cutover then failed the cached WEB4 case with `Owned WEB6 cutover control failed.`. Two remaining Chromium cutover cases and the Edge cutover pack did not run. Terminal `FailedRetained` SHA-256 `a814d811123fbb057e5c32b6d4ebcd5d96641624cc4a1f20f72ce8746d8874d1`. Three databases, three runtime logins, three SQL control roots and all nine cutover control/runtime/browser staging directories remain retained.

Workload/recovery did not run and `CandidateVerified` is absent. Task 6 remains open. A new chain needs fresh explicit Owner authorization after review of the cutover control failure. [Attempt20 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt20-browser-pair-pass-and-cutover-control-failure).

### Attempt21 terminal boundary

Candidate `web6-20261113-18bc21f2` used backend `8c6ff31bd8734df07f71d1b89f8e028da7d3252f` and frontend `66b935488d00ee78ec3ffbd183f474da06aa3404`. Approved Edge, Node/npm and SQL master checks preceded a new protected SQL approval and one authorize/prepare/run chain. Prepare `PIN_MATCHED`, ordinary resend OFF, ten dist/wwwroot files equal and zero live allocation.

Chromium and Edge pair each passed one selector with native `ACCEPTED` and accepted cleanup. `PairPassed` SHA-256 `f2360c53081a97363441ad24f503418f877ed47bd8c416e17736569c82f8370a`. Chromium cutover failed in `oldClientNewHostOn` at the response-header deadline (15,004 ms, `socket-timeout`). TRX records a broken pipe in the server response write. Two remaining Chromium cases and the Edge cutover pack did not run. Terminal `FailedRetained` SHA-256 `917e9cb8b158c7e2036adca41e39976f0dab1c43626314e39b5fa2199ab3f704`. Three databases, three runtime logins, three SQL control roots and all nine cutover staging directories remain retained.

Workload/recovery did not run and `CandidateVerified` is absent. Task 6 remains open. A new chain needs fresh explicit Owner authorization after review of the cutover control timeout. [Attempt21 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt21-browser-pair-pass-and-cutover-response-header-timeout).

### Attempt22 terminal boundary

Candidate `web6-20261114-3408cf4b` used backend `a7c8bac5e6d97fa40b8ee9dba5976b9a5ca3e672` and frontend `fa032ece3d47af029d5095ae39b21fb43d0f1afc`. Chromium and Edge pair each passed one selector with native `ACCEPTED` cleanup. Chromium cutover passed two cases, then its unscoped resend locator matched both the current recovery button and a disabled historical-row button; strict Playwright selection refused the click. Terminal state was `FailedRetained`; three cutover databases, three runtime logins, three SQL control roots and nine cutover directories remain retained. Workload/recovery and Edge cutover did not run. [Attempt22 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt22-current-recovery-region-locator-correction--2026-11-14).

### Attempt23 terminal boundary

Candidate `web6-20261114-3046899d` used backend `39525cb6b4f11ffb3555decdbe19b9411fa97d15` and frontend `e5e083ffd1bf8f70ce49e2e9bad81661e9c6e669`. Fresh protected tool, SQL and Owner inputs passed; Authorize, Prepare and Run each ran exactly once. Prepare returned `PIN_MATCHED` with ordinary OFF, ten-file dist equality and zero live allocation. Chromium and Edge pair each passed 1/1 with native `ACCEPTED` cleanup; PairPassed SHA-256 `adf6b17abcaa10018c4c3fe4c8738b758405bb400ac99b4adf2257b844e0af16`.

Chromium cutover passed its first two cases. The third reached `restoreEpoch`, then the control returned EOF / `incomplete-response` after 1,285 ms. The retained stack records a null SQL JSON scalar passed to `JArray.Parse` in `CopyRestoredSession.ReadTarget`, before the existing-zero-row branch could run. Terminal `FailedRetained` SHA-256 `f6a63f83ebc6e29c57bf1d539578115891b0214f5f25e5fa5b9af21edbb77d36`. Three databases, three runtime logins, three SQL control roots and nine cutover directories remain retained with deletion authority false. Workload/recovery and Edge cutover did not run. No post-failure SQL query, cleanup, retry or earlier-retained-resource access occurred. [Attempt23 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt23-restored-session-null-scalar-failure--2026-11-14).

Source commit `be49fc7e104d0f1b6bcacce6ddfd7fe8baaf2075` adds a regression-tested null-to-empty target-session parser without changing product runtime, schema, grants or retry behavior. Local SQL-free verification passed the focused case 1/1 and cutover class 115/115; source validators reported 77 passed / 0 failed / 1 environment-dependent skip. Protected push run `43596344995` and pull-request run `43596349570` passed on branch head `590400f8888be9f4a82a5769e291f9a94b02971d`. New explicit Owner authorization is still required before a fresh root/approval/candidate chain. Do not repair or replay this retained candidate. Task 6 remains OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED and production NO-GO.

### Attempt24 terminal boundary

Candidate `web6-20261114-de43c0c7` used backend `6f77186dd39cac03ec0574520758ba1a6ce92228` and frontend `e5e083ffd1bf8f70ce49e2e9bad81661e9c6e669`. Fresh protected tool, SQL and Owner inputs passed; Authorize, Prepare and Run each ran exactly once. Prepare returned `PIN_MATCHED` with ordinary OFF, ten-file dist equality and zero live allocation. Chromium and Edge pair each passed 1/1 with native `ACCEPTED` cleanup; PairPassed SHA-256 `9fe32bfc78bb00c401b04f5350e166c92125b90beab7d9a3b3c2b69bd1162d81`.

Chromium cutover passed its first two cases. The third reached `restoreEpoch`, then the control returned EOF / `incomplete-response` after 1,619 ms. The null-scalar parser correction was exercised, but the row read after insertion failed `Web6CutoverSessionCopy.Verify` exact metadata equality at the final line 523 call. The retained log does not identify the differing field; timestamp parameter precision remains an unproven hypothesis. Terminal `FailedRetained` SHA-256 `88c4c72607a1ab05574384835b116920492e6f8a68ba87f643f6ec86789c2db3`. Three databases, three runtime logins, three SQL control roots and nine cutover directories remain retained with deletion authority false. Workload/recovery and Edge cutover did not run. No post-failure SQL query, cleanup, retry or earlier-retained-resource access occurred. [Attempt24 evidence](../evidence/web6/20261110-b05b-paired-candidate.md#task-6-attempt24-restored-session-exact-metadata-failure--2026-11-14).

A reviewed diagnosis and source correction plus new explicit Owner authorization are required before a fresh root/approval/candidate chain. Do not repair or replay this retained candidate. Task 6 remains OPEN; CandidateVerified absent, ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED and production NO-GO.

The authorized post-attempt24 diagnosis reproduced the loss without querying attempt24: the RED read-only scalar test sent `2026-11-10T01:02:03.1234567Z` through the existing test-oracle parameter path and SQL returned `2026-11-10T01:02:03.1233333`; the three restored-session timestamps were observed as `SqlDbType.DateTime`. Source correction `221b4da6098871ae3219a61c43d2d569b5a59177` scopes explicit `SqlDbType.DateTime2`, scale 7 parameters to `Web6CutoverSessionCopy`, leaving the shared oracle mapping and product runtime unchanged. Exact-verify failures now report only allowlisted differing field names, never values. GREEN verification passed the four focused cases **4/4** and the complete `Web6CandidateCutoverTests` class **117/117**, zero fail/skip. A broader SQL-excluded Security run produced 1,459 pass / 1 unrelated existing crash-cleanup poison failure / 20 gated skips; the exact failing safety test reproduced unchanged with the DateTime correction temporarily removed, so it is not claimed fixed or part of this correction. No fresh live attempt, retained-resource query, cleanup or retry occurred.

Protected CI remains required before this correction is review-complete. Even after CI, a fresh live chain requires new explicit Owner authorization and new roots/authorities; this correction does not authorize or accept attempt24.
