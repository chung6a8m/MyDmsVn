# WEB6 — Manual Same-key Resend, Operator Recovery and Candidate Safety Protocol

**Status: proposed implementation design, 2026-11-03; no runtime capability enabled.** Owner selected development of explicit manual same-key resend, continuing to prohibit automatic HTTP mutation retry. This document freezes *required behavior and proof obligations*, not production admission. See [owning roadmap](../plans/20261103-001-web6-recovery-quality-backend.md), [acceptance](../development/WEB6_BACKEND_ACCEPTANCE.md), [WEB5 B02 terminal proof](../evidence/web5/20261102-b02-terminal-resolution.md), [WEB5 B01 crash/fence](../evidence/web5/20261102-b01-crash-commit.md), and [operator/tool runbook](../development/WEB6_CANDIDATE_RUNBOOK.md).

## 1. Invariants inherited, and the legitimate meaning of a resend

There are **three separate actions**:

| Action | Purpose | Allowed behavior |
|---|---|---|
| Safe recovery read (`GET original protected status/resolution`) | Determine original durable COMMITTED/NOT_EXECUTED/UNKNOWN | May be retried with bounded backoff/abort; never dispatches business writes |
| **Manual same-key resend** | On a specifically reviewed pending UNKNOWN immutable intent, user consents to at most one additional HTTP transmission of the *same exact request* | May execute business operation **once** if original never committed, or replay the original COMMITTED result; SQL journal/fence ensures no second business effect |
| **New intent after genuine NOT_EXECUTED** | Explicitly create a different operation after authoritative durable negative and reviewed baseline | A new GUIDv4 and new draft admission; **not** resend of old key |

No HTTP mutation may retry automatically for network timeout, ambiguous 4xx/5xx, 401 token refresh, service worker/reload, UI double-click, scheduled task, or automated offline queue. Automatic safe GET readback is distinct. A naive "retry on timeout" destroys intent semantics.

Existing server `SqlWebOperationJournal.AdmitAsync`: transaction-owned epoch then actor/key SQL application lock; current session/operation grant; compare target/kind/epoch/fingerprint; a matching COMMITTED entry replays original result bytes; a CANCELLED/EXPIRED/malformed entry rejects; absence permits original transaction to proceed subject to full server validation. `CompleteAsync` writes the business operation and COMMITTED result into caller-owned transaction. Do not introduce a second dedupe table, queue or resend endpoint that bypasses this path. Current WEB4 immutable original request and POST/PUT route remain wire-authoritative.

**Important edge:** original service state `NOT_OBSERVED`/HTTP404 cannot prove rollback; a valid same-key manual send still might be the first actual business execution. User confirmation must explicitly say this. SQL key fence prevents double effect **only when all effects and COMMITTED journal are committed atomically in one SQL transaction**, with persistent key/tombstone retention. Verify this for all six commands; don't assume tests of only Create cover Post.

## 2. Eligibility gate — source and runtime

Explicit eligibility is an intersection, not a single status code:

1. Owner-approved bounded feature flag defaults OFF, pinned to the candidate and exposed only in an approved nonproduction build until F02/F03/B04 acceptance. A frontend feature flag is a UX gate, **not** a server security boundary. The backend must validate authorization/actor/session/epoch/grants on every actual original POST/PUT.
2. Exact decoded `DISPATCHED` original immutable record: current authenticated actor key and deployment epoch, valid GUIDv4 original operation ID, allowed kind/method/route/target/version, recomputed canonical frozen fingerprint, matching draft localDocumentId/editRevision/generation and no mutation or negative lineage conflict. No stale document or changed body.
3. Latest protected status/resolution read and current security admission. Do not infer eligibility from status404; if no trustworthy server retention/compatibility guard exists, **B01 must specify and B04 must implement an additive, authenticated, read-only, versioned eligibility gate**. It is a short-lived hint, not a reservation; the write SQL fence remains final arbiter. Do not extend `web4-v1` context or the 40-file `web5-resolution-v1` corpus in-place. Reject non-web5 mode, unsupported old host, old client, stale epoch or unavailable server.
4. Bounded result retention determined from trusted server state or verified policy and server clock, not local Date.now alone. Before taking consent, stop for COMMITTED/retained result (GET settlement), COMMITTED/EXPIRED/RESULT_UNAVAILABLE (operator case; remains PROTECTED), CANCELLED/verified NOT_EXECUTED (no resend; separate new intent), fingerprint/key mismatch, denied grant/session, expired key or missing feature flag.
5. Clear confirmation presented in the current owned document tab: operation kind, original target, sanitized summary, original operation ID abbreviation, potential business effect and no automatic replay. User must explicitly opt in at that moment; no prechecked checkbox, hidden close/Enter trigger, auto-approve on reload or blanket consent for future attempts.
6. A one-time durable local resend claim is committed atomically in IndexedDB **before the network dispatch** and is scoped to actor/epoch/operation ID/fingerprint/document generation. Claims are not authority to send if conditions later fail. A second tab loses the transaction and performs only GET; an ambiguous aborted/started request consumes the consent and remains UNKNOWN. Do not make a network fetch inside an IndexedDB transaction.
7. Latest actor, session, original Write/Post grant, server epoch, tab generation and AbortSignal are rechecked after any asynchronous preflight and immediately before transmission. Fresh SERVER authorization is independently checked by existing transaction path; a stale in-memory grant never grants server permission. A midflight logout/dispose may hide response; the local intent remains protected.
8. Server result is parsed with current original wire codec: matching COMMITTED response settles through atomic draft+operation CAS; direct, strict and proven business rejection uses existing rejection path; ambiguous/expired/unknown/timeout leaves durable `DISPATCHED` plus the consumed resend claim, triggers readback only. Never synthesize result document IDs or rowversion.

**A decision to allow UI manual resend is not a decision to allow production deployment.** Keep an independent `featureEnabled` and recorded Owner acceptance ID in candidate manifest; explicitly disable on unsupported old host/failed pin.

## 3. Backend admissibility and SQL tests

Before UI enablement, test and annotate exact behavior of `SqlWebOperationJournal`, `FenceAsync`, WebOperation result status and operator procedure. Use real SQL-backed HTTP command harness with unique disposable database, actor, operation keys and physical SQL oracle. The original and resend must use identical JSON body bytes and semantic fingerprint; if canonical spellings differ, the frozen original body—not current UI values—is authoritative.

**Six-command matrix:** Sales.Create, Sales.Update, Sales.Post, Receipt.Create, Receipt.Update, Receipt.Post. Test:
- A: original COMMITTED but HTTP response lost -> resend replay exact original result, zero new source/ledger/stock/COGS mutations.
- B: original killed before Commit with independent SQL rollback/quiescence -> manual same-key send may commit once; negative status404 alone was never used as proof.
- C: original dies near Commit -> both possible durable outcomes classified from independent SQL; never guess outcome from child exit; one effect maximum.
- D: original-first held transaction and authorized same-key contender -> contender blocks under fence, obtains replay or proceeds after rollback, never double effect.
- E: manual contender-first and delayed original -> both serialized on same actor/key fence; one effect and consistent outcome.
- F: operator-first durable CANCELLED followed by delayed original/resend -> neither performs business mutation.
- G: original-first and terminal operator waits -> operator cannot override durable COMMITTED; case inspection correctly reports original outcome.
- H: expired/compacted COMMITTED tombstone, result bytes unavailable, actor/session revoked, stale epoch, changed fingerprint, wrong operation kind/target/version, permission/grant denied, SQL lock timeout, request cancelled, host down -> DENY/UNKNOWN with no unsafe local unlock.

Check each command's response specifics (Sales Post version, Receipt Post no version; stable line IDs for saves), duplicate Sales products/gifts, Receipt unique product constraint, moving-average quantity/value/COGS and SQL transaction isolation.

**Do not claim exactly-once HTTP transport.** The target is *at most one client transmission per durable user consent* plus *at most one committed SQL business effect per original operation key* through backend journal. A malicious client can send HTTP more often; backend idempotency and auth must still keep one effect.

## 4. Operator SQL authority and runtime segregation

Existing schema013:
- `WebOperationTerminalAudit` contains immutable decision records and matched journal CANCELLED rows, protected by SQL triggers.
- `RecordWebOperationNotExecuted` requires exact epoch/actor/key/kind/target/fingerprint, existing role or CONTROL, transaction-owned epoch/key fence, active actor and no prior journal/audit.
- `InspectWebOperationResolution` is a separate role-restricted read-only inspection, returns INDETERMINATE for absence and exposes only bounded metadata/retention hints.
- `SqlWebOperationResolutionMaintenance` explicitly constructs on operator connection/UoW; web DI must not expose it.
- `SqlWebOperationMaintenance.CompactAsync` only sets eligible elapsed COMMITTED rows to EXPIRED and nulls ResultBytes under fence/transaction, preserving tombstone.

For WEB6 CLI, require:
- Two-principal separation: restricted web runtime vs separate operator identity with only required EXECUTE; migration identity distinct. Never pass sysadmin/CONTROL to web runtime. Provision with `deployment/sql/Provision-MyDmsVnWebOperationOperator.sql` only on explicitly selected owned app DB.
- Execute commands require one explicit SQL transaction; do not treat procedure completion as Commit evidence. After a known successful Commit, independently inspect exact key; after Commit uncertainty report UNKNOWN and require authorized reconciliation.
- `--dry-run` must inspect/preview without writing CANCELLED audit/compaction; cannot falsely say "will succeed" after a race. `--execute` with typed confirm is separate invocation and rechecks current DB state and operator role inside transaction.
- Audits: attempt, transaction result and post-commit verification are separate bounded events; immutable SQL audit is the authority for CANCELLED, while external file/Event Log is operational evidence. If audit sink fails before destructive state change, fail closed per approved policy; after SQL Commit, record an explicit audit-delivery failure and open reconciliation, not rollback claims.
- Positive COMMITTED-but-result-unavailable business case closure is **not a journal status change** and cannot SETTLE or unlock browser drafts. Store support reference with actor/epoch/key/fingerprint, source+stock+ledger proof and sanitized audit metadata, without raw business payload.
- Retention expiry is not deletion. No bulk journal DELETE, no unbounded selection, no automated database drop or quarantine adoption.

## 5. Recovered and rotated database policy

A normal host restart/redeploy **does not** rotate `WebDeploymentState.DeploymentEpoch`. Cloning or rewinding a database does require a deliberate epoch barrier **before admitting writers**, after SQL service quiescence/drain and policy approval; prove stale browser and tokens cannot write/read an incompatible restored DB. Do not rotate merely to bypass UNKNOWN.

Restore rehearsal steps on a newly owned **separate** DB: snapshot original schema13+business+journal; controlled backup and artifact ownership; restore to another disposable owned DB; validate source/stock/ledger/tombstones; revoke/rotate credentials as appropriate and assign the new epoch before resumed writers; verify runtime and operator SQL principals; test old/new epoch denials and new epoch success. Destroy only resources created and verified by that run. The two historical WEB5 SQL allocations and earlier IIS Express probe directories are **out of scope for cleanup**.

Service RPO/RTO measurement must define start/end timestamps from failure detection through useful restored authenticated business service, with workload and state reconciliation; database backup/restore seconds alone are not business-service RPO/RTO.

## 6. Production-like host and observability limits

The selected test host remains direct HTTPS/IIS Express on Windows with net48 and schema13, no reverse proxy. Full IIS W3SVC/WAS and unperformed Narrator are explicitly **NOT RUN exceptions** for bounded nonproduction only. Require PR #95 owned host admission and HTTP.sys URL routing-precedence checks before any HTTP workload; recheck process/config/queue on cutover and absence before SQL cleanup. Never use TCP PID4/health-only proof.

Current `AuthAttemptLimiter` is per worker and `TraceSecurityAuditSink` writes to TraceSource with operator-configured listeners. They **do not imply production multiworker limiter or durable audit destination/retention**. A single-worker IIS Express topology may be accepted for bounded pilot when exclusive worker count is measured; a multiworker claim needs a cross-worker atomic limiter and actual multiworker abuse tests. Audit requires a verified destination, rotation/ACL, retention/redaction and outage behavior on the real host.

**Release register states:** `IMPLEMENTED`, `SQL-VERIFIED NONPRODUCTION`, `BROWSER-VERIFIED NONPRODUCTION`, `MANUAL-RESEND ENABLED FOR APPROVED PILOT`, `STAGING ACCEPTED`, `PRODUCTION GO`. None can be inferred from a green CI, PR merge or docs-only roadmap selection. Exact candidate/source binary pairs, Owner targets and measured load/error/RPO/RTO, outage drills and separate release decision are mandatory.

## 7. No-go/fail-closed reference cases

| Observation | Mandatory decision |
|---|---|
| HTTP 404/status NOT_OBSERVED | UNKNOWN; never unlock, never assert NOT_EXECUTED; manual resend only if **all separate gates** pass |
| COMMITTED/retained result | Read/settle original; no manual duplicate POST needed |
| COMMITTED/expired/unavailable result | Protected and support escalation; no automatic mutation or fabricated projection |
| Matched SQL CANCELLED/NOT_EXECUTED | Do **not** resend original key; fresh baseline/new consent/new intent only |
| Old Web4 host lacks versioned eligibility | Manual resend disabled; protected positive read fallback only |
| Original immutable record missing/corrupt or different actor/epoch | Deny; retain safe metadata; never re-create the payload/key |
| IDB transaction aborted/QuotaExceeded/versionchange/two tabs | No dispatch; once claimed but network result unclear -> UNKNOWN, read-only recovery |
| Credential refresh/401 during mutation | No automatic refresh-and-replay mutation; original locked |
| Historical cleanup target not exactly owned | QUARANTINE; no deletion |
| Missing full IIS/attended environment | Mark NOT RUN/DEFERRED for those gates; do not pretend a source defect or a PASS |
