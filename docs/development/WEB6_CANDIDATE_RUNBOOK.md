# WEB6 — Pinned Windows/IIS Express/SQL candidate runbook

**Status: attempt04 PREPARED / PIN_MATCHED, then FailedRetained at the first Chromium selector; Task 6 OPEN (2026-11-10).** Owned SQL and host-runtime configuration remain retained; IIS/browser launch was not reached and no CandidateVerified receipt exists. Earlier preparation failures remain separate history. This runbook specifies a nonproduction **production-like** candidate acceptance path for Codex Local on the existing Windows dev machine and protected Windows CI; see [B05a operations](../evidence/web6/20261110-b05a-operations.md) and [B05b paired candidate](../evidence/web6/20261110-b05b-paired-candidate.md). It neither requires Docker Desktop nor demands a Windows Server VM now. [Backend roadmap](../plans/20261103-001-web6-recovery-quality-backend.md) · [acceptance](WEB6_BACKEND_ACCEPTANCE.md) · [WEB5 owned-host evidence](../evidence/web5/20261103-b05-owned-host-admission.md) · [React companion](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261103-001-web6-recovery-quality-web.md).

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
