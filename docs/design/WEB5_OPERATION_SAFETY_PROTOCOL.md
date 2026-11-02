# WEB5 — Process-crash, terminal disposition and operator safety design

**2026-11-02: B01 is IMPLEMENTED / VERIFIED NONPRODUCTION; B02 is IMPLEMENTED / LOCAL VERIFICATION PASS; hosted B04-B remains PENDING (NOT RUN).** [B02 evidence](../evidence/web5/20261102-b02-terminal-resolution.md) and [B04-B evidence](../evidence/web5/20261102-b04b-candidate-sql.md) own exact candidate results; B02 acceptance and B04 completion remain pending the protected hosted gate. B03 CLI/operational rehearsal remains NOT RUN. Backend is authoritative; this document separates implemented safety mechanisms from later operational/release requirements. [Owning plan](../plans/20261102-001-web5-operational-safety-backend.md) · [WEB4 frozen contract](WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) · [WEB4 operator handoff](../development/WEB4_OPERATOR_HANDOFF.md) · [backend acceptance](../development/WEB5_BACKEND_ACCEPTANCE.md).

**Current implementation:** [ADR0015](../adr/0015-web5-additive-recovery-boundary.md) selects additive v1 compatibility, append-only013 and explicit web5/exact13 admission. The [separate versioned contract](../contracts/web5-resolution-v1/README.md) publishes the exact v2 context/resolution reads without changing the frozen 48 WEB4 blobs. Operator-only `InspectWebOperationResolution` and `RecordWebOperationNotExecuted` run under the existing epoch/actor/key fence, with immutable audit and separate runtime/operator authority. Strict-only requirements are N/A; local proofs do not replace [hosted candidate verification](../development/WEB5_SQL_CANDIDATE.md), frontend acceptance or full IIS.

## 1. Baseline source facts (observed at main 8de36c4)

- `SqlWebOperationJournal` has `AdmitAsync`, `CompleteAsync` and `FenceAsync`; it requires the existing business SQL transaction, holds epoch/actor/key serialization and current session/permissions. Original successful result bytes and business effects commit once. The journal has seven-day result availability and an immutable actor/key tombstone.
- `SqlWebOperationReadRepository` protects status by original actor/session/**permission derived from the stored original operation kind** (`SqlWebOperationJournal.AuthorizeOperationAsync` → `WebOperationContext.Permission(kind)`), fingerprint and epoch; Create/Update require their Write permission, Post requires Post permission. An ordinary document Read grant is **not** required for this protected original-result readback (WEB4 W4-OP-12), although a later fresh document Get may require Read. Missing row is NOT rollback proof; `EXPIRED` is not NOT_EXECUTED.
- `SqlWebOperationMaintenance.CompactAsync` requires an explicit maintenance transaction + CONTROL and audited callback. It is **not registered in runtime DI**, has no deployed operator CLI, and does not itself commit the caller transaction.
- At this historical baseline, WEB4 mode was exact schema12, legacy mode exact11 and Desktop exact10; `NOT_EXECUTED` and web5 mode were not implemented. Current B02 adds web5/exact13 and the separate resolution contract described above; CLI/rehearsal remains B03. The canonical 48 WEB4 blobs stay frozen.

## 2. Crash classification and exactly-once observational rule

| Checkpoint | Possible SQL reality | Safe client/operator decision |
|---|---|---|
| Before durable local intent | No HTTP dispatch expected | Do not claim a server-side operation |
| After local intent, before HTTP | Original might be sent later from another tab/worker | UNKNOWN; status absence does not unlock |
| After SQL transaction begins, before Commit | Business/journal may roll back on process death | Oracle must observe; never assume rollback from killed PID |
| During Commit or lost database acknowledgement | Commit success is **indeterminate** to caller | Fresh independent read of journal+business/ledger under proper access |
| After Commit, before HTTP acknowledgement | SQL has committed but client does not know | Exact protected COMMITTED result, no second business effect |
| Same-key competing request/replay | First might still be running | Both serialize under the same transaction-owned fence; at most one effect |
| Result expired | Historic commit possible with unreadable original result | Preserve EXPIRED tombstone; never mark not executed |

Test hooks must be restricted to **test-host binaries or injected test seams** that cannot be enabled in a published release. Supervisor only kills its own disposable process and verifies clean child/SQL cleanup; a generic kill-switch route in real OWIN host is forbidden. Commit outcome is not always deterministically controllable; classify observed result rather than forcing a test to expect one side.

## 3. Terminal-negative protocol and invariant

**Required invariant:** The system may claim `CONFIRMED_NOT_EXECUTED` only after the administrator has durably committed a terminal **CANCELLED/DO_NOT_EXECUTE** record under the **same actor/operation per-key transaction fence** used by ALL original writers. All future late originals and same-key requests must observe that record and be rejected before source/journal/ledger writes. A read of absent journal rows cannot provide this guarantee. This is a **safety protocol**, not a UI action, support guess or SQL UPDATE patch.

State meanings (the published contract owns exact wire values):
- **COMMITTED:** original immutable successful result and business transaction (possibly outcome expired). Never convert to negative.
- **RESULT_EXPIRED/UNAVAILABLE:** readback incomplete; do not fabricate version or negative.
- **NOT_EXECUTED:** verified durable negative tombstone with exact epoch, actor, key, fingerprint, kind, method/target, operator identity, evidence and commit timestamp. It cannot later become COMMITTED.
- **INDETERMINATE:** no sufficient record or proof; keep UNKNOWN and block future new-key write of the same logical pending document.

**Required transaction order for administrative close:**

~~~text
Separate authorized maintenance operator (not browser runtime)
  -> inspect exact current epoch/actor/key/frozen intent and request identity
  -> BEGIN TRANSACTION on maintenance connection
  -> fence on current deployment epoch (same lock order as writer)
  -> acquire same actor/key transaction-owned exclusive applock
  -> re-read journal, grants/ownership and independently necessary source evidence
       COMMITTED/EXPIRED: stop, do NOT write negative
       wrong key/fingerprint/epoch/privilege: stop, do NOT write negative
       absent but not proven safe: stop as INDETERMINATE
       absent AND safe: insert permanent negative tombstone + durable operator audit
  -> COMMIT one transaction
  -> independent readback of negative tombstone and no source/ledger effect
  -> issue signed/authorized correlated resolution certificate (not editable result)
~~~

If a late writer held the applock first, operator waits and must observe its COMMITTED row; if operator held it first and committed the negative, writer must see terminal tombstone and fail before mutation. Cover a kill immediately before/after both commits. If the implemented lock/DDL cannot enforce this under two processes, **do not ship the negative operation**.

**Security:** require separate maintenance principal, least privilege, explicit user identity, dual-step operator confirmation, case/reason/correlation and deterministic audit. Keep journal immutable to normal accounts; no manual DELETE, stale key reuse, UPDATE to COMMITTED, direct ledger adjustment or operator impersonation. Do not expose SQL maintenance via a browser-facing generic admin endpoint.

**Evidence handling:** record audit *inside the terminal commit* (append-only SQL row or equivalent transaction-owned durable mechanism) plus optional external log after commit. An external callback before Commit only proves an attempt, not outcome; failed after-commit log does not reverse the SQL tombstone. Record unknown acknowledgement separately and require reread rather than duplicate operations.

## 4. Compatibility and versioning

Backend `web4-v1` contract corpus and route `GET /api/v1/operations/{id}` are frozen. **Do not add misleading COMMITTED responses for negative cases or widen its body silently.** The published backend contract for F02 is the separately versioned authenticated [resolution read](../contracts/web5-resolution-v1/README.md), `GET /api/v2/operations/{id}/resolution` with actor + current epoch + original fingerprint, direct permission and no cross-user inference.

B02 implements append-only `013` and explicit `web5`/exact13 runtime/migrator admission, following ADR0015. Preserve:
- Desktop exact10 and Local writer, no implicit connection to a WEB4/WEB5 database.
- `legacy-web11` exact11 retains old client contract and denies keyed writes.
- `web4` exact12 retains old WEB4 behavior, absent negative resolution is not proof.
- `web5` requires a matched exact13 binary/schema/principal. Under additive compatibility, frozen WEB4 keyed clients retain their exact v1 write/status contracts; new negative recovery uses the separately pinned v2 contract. Old server binaries refuse13.
- Keep original Sales/Receipt Create/Update result/line identities, Sales Post version, Receipt Post no version, exact int64 and decimal strings.

To release a local UNKNOWN after terminal negative, frontend must verify the **committed negative certificate**, current actor+epoch+fingerprint, and same owned immutable local intent. It may then preserve draft raw and request fresh authorized server baseline/rowversion before enabling a **new user-initiated** mutation with **new operation key**. This is never automatic. For irrevocable/expired COMMITTED without original Write result, preserve lock pending separate authoritative reconstruction and approval. Existing same-key resend remains independently gated by B01 crash proof and explicit UX acceptance.

### 4.1 Positive business-commit case closure without editable result (P1-01)

Seven days after an operation finishes, WEB4 status can report `RESULT_EXPIRED` even if physical bytes have not yet been compacted; expired is positive historical business evidence, never a rollback or terminal negative. When COMMITTED source/journal/ledger is independently proven but original Create/Update detail/version is missing, a separately authorized operator may close a SUPPORT CASE as `BUSINESS_COMMITTED_RESULT_UNAVAILABLE` using actor+epoch+operationId+fingerprint, source and Post journal/valuation/COGS, operator/correlation/UTC timestamp and a durable restricted audit trail. **Closing a support case is not settling, deleting, unlocking, retrying or allowing new-key writes for the existing UNKNOWN**. Client remains PROTECTED pending a separately approved local archival/recovery policy.

A future user-consented local archival may retain minimal secure evidence/case ID and hide a protected document from active workspace only after exact actor/epoch/CAS, without deleting authoritative journal/tombstone or fabricating rowversion. A new editable session needs current permission and a fresh server baseline/version; Write-without-Read cannot infer this from an expired result. Otherwise leave protected and escalate. Record escalation before the seven-day window when possible; no fixed support SLA is currently authorized. Test expired/missing result and failed archival separately from NOT_EXECUTED.

### 4.2 Browser contract compatibility versus strict cutover (P1-02)

ADR0015 selects policy A below. Policy B is retained only as a conditional reference for a separately reviewed future change:

- **A — additive:** keep old `web4-v1` six write/status contracts semantically safe; a loaded WEB4 client may keep working on the new host despite new terminal-negative capability. Require real old-client/new-host and new-client/old-host tests including delayed original, existing UNKNOWN, current authorization and old status safely failing closed on negative tombstone.
- **B — strict:** enforce the new authenticated protocol/version fence **on every mutation** before business/journal writes and on new-version protected status; old clients/headers cannot fall back to unkeyed or WEB4 writes. **Exception is a deliberately narrow, read-only legacy WEB4 recovery/status route** for immutable WEB4 operations created before cutover; it authenticates current actor/session/**the original stored operation kind's Write/Post permission (not ordinary Read)**, epoch, operation key and original SHA-256 fingerprint, cannot write/replay, and never treats absence as rollback. Its contract and retirement policy must be frozen in B02 ADR and verified in B04-B. Test cached old JS, open tabs, session refresh, in-flight old command, status across upgrade, rollback and fresh new client. Database `DeploymentEpoch` is unrelated to application bundle version; rotate it only for database clone/rewind.

The current backend `WebHostOptions` accepts `legacy-web11`, `web4` and `web5`, with exact11/12/13 admission respectively. The original context remains exactly `web4-v1`; the new context is separate `web5-resolution-v1`. This additive design does not implement a strict mutation-version cutover fence. Published B02 vectors govern F02/F04; the 48 frozen WEB4 blobs remain immutable.

### 4.3 Schema12 source admission and six-command race proof (P2-01/P2-03)

Existing `012_WebOperationJournal.sql` permits `CANCELLED` INSERT but trigger permits only COMMITTED→EXPIRED updates; never update a success row to negative. ADR0015 rejects external/generic audit as sufficient authority and B02 adds explicit exact12→13 preflight in `ValidateWeb5SchemaStateBeforeMigration`; the separate original Web12 preflight retains its exact11/exact10 paths. Validate exact12 objects/history/fingerprint/grants before 013, plus fresh13, admitted 10/11 sources, partial/spoofed/too-new, failed-migration rollback/rerun and unchanged old schema modes.

B5-NEG-08 must exercise all six Sales.Create/Update/Post and Receipt.Create/Update/Post, each in **writer-first and operator-first** fence order (12 arrangements). Verify mutually exclusive COMMITTED and permanent CANCELLED outcomes, transaction-owned audit, original Write version/lines, Sales Post version versus Receipt Post no version, physical ledger/valuation/COGS and absence of unintended effects. Additional crash/revocation cases are separate tests; do not assert test case count just because 12 arrangements were described.

### 4.4 Strict-cutover legacy recovery-only status (round-2 P1-01)

A **strict mutation cutover must not strand WEB4 `DISPATCHED/UNKNOWN` intents that were durably created before deployment**. The existing backend `OperationsController.Status` + `SqlWebOperationReadRepository.ReadStatusAsync` use a transaction-owned operation fence and authorize current actor, session, **permission of the original stored operation** (Sales/Receipt Create/Update → Write; Sales/Receipt Post → Post), deployment epoch, operation ID and original fingerprint. The ordinary Read permission is not a prerequisite for protected operation-result readback; never derive permission from a caller-supplied kind. The client currently persists `schemaVersion: 1` / `DISPATCHED` with a frozen `web4-v1` request and validates `contractVersion: "web4-v1"` in its operations context. None of that constitutes a newly implemented WEB5 recovery route; **B02 must define and prove the transition**.

In B02 ADR, specify **one narrow read-only legacy recovery compatibility mechanism** for strict mode: either keep the original authenticated `GET /api/v1/operations/{id}` as a recovery-only v1 status read, or introduce a separately versioned recovery endpoint that can read the **original** stored WEB4 journal outcome with the **original** v1 fingerprint. Both must reject any old `POST`/`PUT` mutation before the business transaction, and must not reclassify a v1 intent as v5 or fabricate a new operation ID, fingerprint, body, route, expectedVersion or actor/epoch. The route may return an authorized original `COMMITTED` result and operation-specific IDs/versions if still retained; no synthetic result when unavailable. Revoke legacy recovery access only after a separately reviewed retention/migration-support policy: do **not** use the 7-day result expiry as automatic permission to delete all status/tombstones.

**State and authorization matrix (mandatory B5-COMP-03/04):**

| Legacy v1 intent at strict cutover | Backend recovery-only action | Client consequence |
|---|---|---|
| Exact actor/epoch/key/fingerprint, valid session **and original Write/Post grant**, committed outcome still readable; ordinary document Read may be denied | Protected read of original WEB4 `COMMITTED` bytes/command-specific result; Sales Post retains original version, Receipt Post has none; no writes | New client may settle via original outcome + current local CAS |
| Journal absent, original request delayed or still in-flight | Fence/serialize with live writer; return nonaffirmative `NOT_OBSERVED`/unknown (do not attest rollback) | Remain `UNKNOWN`, never resend with old or new key |
| Original committed but result is `RESULT_EXPIRED`/`RESULT_UNAVAILABLE` | Report the true positive/indeterminate state without fabricating version/line data | Client stays protected, operator positive case reconciliation |
| Original key terminal `CANCELLED` (if supported) | Old v1 response must fail closed, new versioned negative certificate only under B02 authority | No old automatic unlock/re-Post |
| Wrong actor/current session, **revoked original Write/Post grant** for stored kind, wrong epoch/fingerprint/corrupt key | Deny or indistinguishable absence without cross-actor leakage (ordinary Read denial **alone** is not this case) | No settlement, no disclosure, no mutation |
| New client/old backend or old cached JS/new strict backend | Compatible read only for frozen original v1; no protocol downgrade that enables old writes | Explicit upgrade/recovery UX; no second business effect |

**Boot and upgrade path:** the new frontend may negotiate a new operations context while it has old v1 IndexedDB `DISPATCHED` records; it must not discard or rewrite them just because the runtime `web4-v1` context literal is superseded. Read only after fresh authentication/epoch/capability checks, decode and rehash the unmodified v1 frozen request, and apply the original result only after atomic local CAS. A v1 status read is never a permission to dispatch a new legacy operation. Invalid, missing, expired, denied, storage-aborted or cross-tab race states remain protected and explicitly documented.

**Cutover gate (strict-only B5-COMP-03/04):** B02/B04-B must test old `web4-v1` pending operation → strict backend/new browser load → original positive status within retention, with **Read denied but original Create/Update Write or Post still granted** as an allowed case; also absent/in-flight, expired, CANCELLED, wrong scope, **revoked original Write/Post**, invalid session, fingerprint and stale epoch. For additive ADR mark strict-only B5-COMP-03/04 `NOT_APPLICABLE` with reason and separately prove common B5-COMP-01/02; do not count N/A as PASS or a skipped mandatory SQL test. B05/F04 repeat the old opened tab/new hosted bundle matrix on full IIS. If strict cutover cannot preserve this protected readback, B02 is **BLOCKED** and strict production admission is not allowed. This requirement does **not** authorize a general unversioned status bypass or change the frozen WEB4 corpus.

## 5. Operator command and maintenance safety

Initial CLI commands are proposals to be verified against actual project/DB design, not runnable commands today:

~~~text
web5-operator inspect --epoch ... --actor ... --operation ... --correlation ...
web5-operator resolve-not-executed --case ... --epoch ... --actor ... --operation ... --fingerprint ... --dry-run
web5-operator resolve-not-executed --case ... --epoch ... --actor ... --operation ... --fingerprint ... --confirm ...
web5-operator compact --epoch ... --actor ... --operation ... --dry-run
web5-operator rotate-epoch --backup-manifest ... --dry-run
~~~

Tool must never use raw browser token, generic service runtime connection, unchecked business DB, prefix-delete or SQL log containing secrets. Source-controlled CLI and tests must prove exact argument identity, denied authority, dry-run non-mutating, idempotent repeated inspection, operator transaction rollback, successful reread, audit integrity and finite teardown.

## 6. Release topology, retention and nonclaims

FULL IIS staging requires Windows IIS components (W3SVC/WAS), app pool, ASP.NET/System.Web mapping, TLS and versioned published `dist`; IIS Express evidence from WEB4 is not transferable. Re-test CSP nonce for Dockview/AG Grid, Zod non-JIT, asset routing, cookie/Origin/CSRF, proxy/header spoofing and SQL principal/startup mismatch on the actual staged process. A two-worker topology requires a shared atomic limiter or a documented one-worker bound; an in-memory per-worker limiter is not multi-worker proof. Audit sink ACL, retention/backup/restore and maintenance tooling need **actual rehearsal**, not merely documentation.

### 6.1 Measurable production-readiness thresholds (P2-04)

B05 must exercise a representative measured workload on actual full IIS with concurrent authenticated actors and Sales/Receipt Create/Update/Post, status, refresh and Inventory reads, both steady and restart/limiter/compaction stress. Record per-command p50/p95/p99 latency, timeouts/errors, throughput, worker CPU/RAM, SQL pool/locks/deadlocks, journal growth and maintenance impact. Rehearse consistent backup/restore and incident alert/escalation; measure actual RPO/RTO and unresolved UNKNOWN queue age. Publish exact runner/SQL/IIS configuration, test duration and sample sizes.

**No arbitrary SLA values are preapproved.** B05 submits proposed numeric acceptance targets from baseline and representative risk; B06 obtains an explicit Owner threshold decision and compares each **target versus observed final candidate**. Missing gate/failed target is NOT RUN, BLOCKED or Owner DEFERRED, never green from historical WEB4 or overall averages.

Release decision table:

| State | Meaning |
|---|---|
| PLANNED | Spec/design only, zero WEB5 runtime acceptance |
| VERIFIED NONPRODUCTION | Specific crash/SQL/browser cases pass on a pinned test candidate |
| STAGING ACCEPTED | Full IIS + operational/negative/SQL oracle + attended candidate acceptance pass |
| PRODUCTION GO | Named Owner approves a separate deployment decision after dossier review |
| BLOCKED | Required negative proof, environment or operator result missing; cannot be counted as PASS |

The Owner's ROADMAP selection and the WEB4 bounded archive do not waive any WEB5 stage. Cite [B01–B06 plan](../plans/20261102-001-web5-operational-safety-backend.md) and [acceptance matrix](../development/WEB5_BACKEND_ACCEPTANCE.md) for executable requirements.
