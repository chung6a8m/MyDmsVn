# WEB5 — Process-crash, terminal disposition and operator safety design

**Design proposal / NOT IMPLEMENTED (2026-11-02).** Backend is authoritative. This document specifies conditions that future B01/B02/B03 PRs must prove; it does not claim a safe negative operator path already exists. [Owning plan](../plans/20261102-001-web5-operational-safety-backend.md) · [WEB4 frozen contract](WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) · [WEB4 operator handoff](../development/WEB4_OPERATOR_HANDOFF.md) · [backend acceptance](../development/WEB5_BACKEND_ACCEPTANCE.md).

## 1. Baseline source facts (observed at main 8de36c4)

- `SqlWebOperationJournal` has `AdmitAsync`, `CompleteAsync` and `FenceAsync`; it requires the existing business SQL transaction, holds epoch/actor/key serialization and current session/permissions. Original successful result bytes and business effects commit once. The journal has seven-day result availability and an immutable actor/key tombstone.
- `SqlWebOperationReadRepository` protects status by original actor/session/permission and fingerprint/epoch. A missing row is NOT rollback proof; `EXPIRED` is not NOT_EXECUTED.
- `SqlWebOperationMaintenance.CompactAsync` requires an explicit maintenance transaction + CONTROL and audited callback. It is **not registered in runtime DI**, has no deployed operator CLI, and does not itself commit the caller transaction.
- Current WEB4 mode is exact schema12, legacy mode is exact11; Desktop exact10. B01 canonical 48 blobs remain frozen. Proposed `NOT_EXECUTED`, CLI, and any `web5` mode below **do not exist yet**.

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

## 3. Proposed terminal-negative protocol and invariant

**Required invariant:** The system may claim `CONFIRMED_NOT_EXECUTED` only after the administrator has durably committed a terminal **CANCELLED/DO_NOT_EXECUTE** record under the **same actor/operation per-key transaction fence** used by ALL original writers. All future late originals and same-key requests must observe that record and be rejected before source/journal/ledger writes. A read of absent journal rows cannot provide this guarantee. This is a **safety protocol**, not a UI action, support guess or SQL UPDATE patch.

Proposed state meanings:
- **COMMITTED:** original immutable successful result and business transaction (possibly outcome expired). Never convert to negative.
- **RESULT_EXPIRED/UNAVAILABLE:** readback incomplete; do not fabricate version or negative.
- **NOT_EXECUTED:** verified durable negative tombstone with exact epoch, actor, key, fingerprint, kind, method/target, operator identity, evidence and commit timestamp. It cannot later become COMMITTED.
- **INDETERMINATE:** no sufficient record or proof; keep UNKNOWN and block future new-key write of the same logical pending document.

**Proposed transaction order for administrative close:**

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

Backend `web4-v1` contract corpus and route `GET /api/v1/operations/{id}` are frozen. **Do not add misleading COMMITTED responses for negative cases or widen its body silently.** The F02 consumer needs a separately agreed versioned, authenticated resolution read (example design: `GET /api/v2/operations/{id}/resolution`; exact path and schema MUST be fixed by B02 ADR) with actor + current epoch + original fingerprint, direct permission and no cross-user inference.

Possible addition only if DDL requires: `013` migration and an explicit `web5`/exact13 runtime mode. Decide after inspecting current 012 CHECK/trigger and migration validators. Always preserve:
- Desktop exact10 and Local writer, no implicit connection to a WEB4/WEB5 database.
- `legacy-web11` exact11 retains old client contract and denies keyed writes.
- `web4` exact12 retains old WEB4 behavior, absent negative resolution is not proof.
- New mode (if created) must require matched binary, exact schema/grants, updated pinned corpus and frontend. No mixed old client write against a new mode.
- Keep original Sales/Receipt Create/Update result/line identities, Sales Post version, Receipt Post no version, exact int64 and decimal strings.

To release a local UNKNOWN after terminal negative, frontend must verify the **committed negative certificate**, current actor+epoch+fingerprint, and same owned immutable local intent. It may then preserve draft raw and request fresh authorized server baseline/rowversion before enabling a **new user-initiated** mutation with **new operation key**. This is never automatic. For irrevocable/expired COMMITTED without original Write result, preserve lock pending separate authoritative reconstruction and approval. Existing same-key resend remains independently gated by B01 crash proof and explicit UX acceptance.

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

Release decision table:

| State | Meaning |
|---|---|
| PLANNED | Spec/design only, zero WEB5 runtime acceptance |
| VERIFIED NONPRODUCTION | Specific crash/SQL/browser cases pass on a pinned test candidate |
| STAGING ACCEPTED | Full IIS + operational/negative/SQL oracle + attended candidate acceptance pass |
| PRODUCTION GO | Named Owner approves a separate deployment decision after dossier review |
| BLOCKED | Required negative proof, environment or operator result missing; cannot be counted as PASS |

The Owner's ROADMAP selection and the WEB4 bounded archive do not waive any WEB5 stage. Cite [B01–B06 plan](../plans/20261102-001-web5-operational-safety-backend.md) and [acceptance matrix](../development/WEB5_BACKEND_ACCEPTANCE.md) for executable requirements.
