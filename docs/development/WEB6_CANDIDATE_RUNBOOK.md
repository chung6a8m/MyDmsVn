# WEB6 — Pinned Windows/IIS Express/SQL candidate runbook

**Status: PLANNED, documentation only (2026-11-03).** This runbook specifies a nonproduction **production-like** candidate acceptance path for Codex Local on the existing Windows dev machine and protected Windows CI. It neither requires Docker Desktop nor demands a Windows Server VM now. [Backend roadmap](../plans/20261103-001-web6-recovery-quality-backend.md) · [acceptance](WEB6_BACKEND_ACCEPTANCE.md) · [WEB5 owned-host evidence](../evidence/web5/20261103-b05-owned-host-admission.md) · [React companion](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261103-001-web6-recovery-quality-web.md).

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
