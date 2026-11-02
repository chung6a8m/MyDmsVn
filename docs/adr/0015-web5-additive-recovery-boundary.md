# ADR 0015 — WEB5 additive recovery compatibility and durable terminal audit

**Date:** 2026-11-02. **Decision:** additive compatibility and append-only schema 013 selected as the reviewed design direction for WEB5-01/F01 closure. Owner requested completion of WEB5-01 after remaining B01/B04-A pins, B02 design agreement and integration were reported. The compatibility/schema choice below is the implementer's source-reviewed decision within that scope; it is not a claim that Owner separately selected a wire shape. **Implementation:** B02 runtime, migration 013, new host mode and resolution HTTP/corpus NOT IMPLEMENTED / NOT RUN. [Owning plan](../plans/20261102-001-web5-operational-safety-backend.md) · [safety requirements](../design/WEB5_OPERATION_SAFETY_PROTOCOL.md) · [frontend consumer requirements](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/3b79a0037f6112e319151cb3a1098d69d129fc87/docs/design/WEB5_RECOVERY_AND_RELEASE_BOUNDARIES.md).

## Source and scope

Inspected production source at merged B01 `b320c10acc0ddb2e7cf4b87c153bdf51f25c4bf0` (unchanged by B01 and B04-A), and frontend `3b79a0037f6112e319151cb3a1098d69d129fc87`. Preserve all 48 WEB4 blobs, tree `fcab4a1e7505b150a132ce2899002930e9f2a6ee`. [B01 proof](../evidence/web5/20261102-b01-crash-commit.md) and [B04-A proof](../evidence/web5/20261102-b04a-protected-sql-ci.md) establish their bounded prerequisites, not a terminal-negative implementation.

- [SqlWebOperationJournal](../../src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationJournal.cs) fences epoch/actor/key in the business transaction and checks current actor/session/original-kind permission. Matching CANCELLED reaches ResultUnavailable before business execution; changed identity remains KeyConflict.
- [SqlWebOperationReadRepository](../../src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationReadRepository.cs) maps CANCELLED to RESULT_UNAVAILABLE, preserving original stored-kind Write/Post authorization even when ordinary Read is denied. COMMITTED original result bytes are separately protected.
- [OperationsController](../../src/MyDmsVn.Server.HttpHost/OperationsController.cs) exposes exact v1 context/status. The frontend operationsContext decoder strictly requires web4-v1 and rejects extra fields. New resolution/capability metadata cannot be appended to that response.
- [012 journal DDL](../../src/MyDmsVn.Server.DbMigrator/Scripts/012_WebOperationJournal.sql) permits immutable CANCELLED INSERT but lacks an operation-specific operator/case/reason audit. [008 SecurityAuditEvents](../../src/MyDmsVn.Server.DbMigrator/Scripts/008_Security_Administration.sql) restricts targets to User/Role integer identities. [SqlWebOperationMaintenance](../../src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationMaintenance.cs) invokes an external attempt callback; that is not transaction-owned durable terminal audit.

## Decisions

1. **Additive browser compatibility.** A future web5 host preserves all six frozen v1 mutation contracts, their original success/errors, the exact web4-v1 context and existing protected v1 status. Existing cached/open WEB4 clients continue their safe behavior. No fields are added to strict v1 envelopes. New resolution and capability discovery use a separate versioned surface, published by B02 before F02 fetch/decoder implementation.
2. **Separate exact13 server admission.** Select append-only migration 013 for durable operation-resolution audit and necessary authority constraints, plus an explicit future web5/exact13 host/migrator path. Do not modify012 or silently call schema 13 exact12. Detailed DDL, immutable triggers, privilege definitions and the actual fingerprint are B02 deliverables, not fabricated by this ADR. Existing web4/exact12, legacy-web11/exact11 and Desktop/exact10 modes remain explicit and separate.
3. **One terminal transaction.** Operator negative resolution acquires the SAME existing epoch/actor/key fence and lock order as original writers. It validates the original immutable request/identity and authority; missing row alone is insufficient. Insert CANCELLED in the existing v1 journal identity and an immutable audit decision atomically, then verify through an independent read after Commit. Audit records bind epoch/actor/key/fingerprint/kind/target, separate operator identity, case/correlation, reason/evidence reference and timestamp. A current authorized read must verify the matched committed pair before reporting NOT_EXECUTED; a missing/mismatched audit remains indeterminate.
4. **Maintenance authority stays separate.** Runtime/browser identity cannot issue operator decisions or mint audit certificates. Schema013 must enforce that boundary even though runtime currently has journal INSERT for ordinary completion; merely adding an audit table is insufficient authority proof. No general admin web endpoint or broad grants. COMMITTED/EXPIRED cannot become negative; audit failure rolls back; lost operator acknowledgement stays UNKNOWN until independently reread. No retry or manual row removal resolves uncertainty.
5. **Keep immutable v1 identities.** Existing browser schema-v1 intent bytes, v1 fingerprint and lineage remain valid. Do not rehash them into a new contract, replace operation IDs, relabel DISPATCHED as REJECTED or rotate database epoch for a code deployment. Negative tombstones cannot expire into an absent/re-executable key.

## Agreed F01 consumer contract requirements (design only)

This is the cross-repository semantic agreement required by F5-CON-04. It does not publish an implemented route or canonical wire corpus. Exact method/route, headers, DTO field names/serialization, status/error mappings and new vectors remain B02 deliverables. Any example route in frontend design remains illustrative and cannot be implemented from this ADR alone.

| Evidence from the separate versioned read | Required consumer behavior |
|---|---|
| Authenticated COMMITTED original result | Bind actor/session/epoch/key, original fingerprint, stored kind and target. Return original Create/Update Write detail/version/line IDs, Sales Post version and Receipt Post without a fabricated version. Persist settlement with exact local CAS before applying it. |
| Committed CANCELLED plus matched immutable operator audit | Only the new versioned surface may issue durable NOT_EXECUTED. Bind the exact original request; atomically release only that local intent, retain raw draft, then require fresh authorized baseline/version and explicit new user intent before a new mutation. |
| Absent row/404, timeout, in-flight or unknown Commit | Remain UNKNOWN/protected. Never infer rollback or dispatch a new key. |
| EXPIRED or COMMITTED result unavailable | Remain protected. Operator business-ticket closure is not editable settlement and cannot fabricate an original result. |
| Wrong actor/session/epoch/fingerprint, corrupted state or revoked original permission | Fail closed without cross-actor disclosure, unlock or business effect. |

Protected read authorization is derived from the stored original operation kind: Create/Update require original Write; Post requires original Post. Ordinary Read denial alone is a positive readback case when that original grant is valid. A later fresh document baseline Get may separately require Read. Client-supplied kind, persisted permissions or a locally stored certificate cannot authorize recovery. An absent original row alone cannot supply trustworthy kind or authorize a negative.

F02 must enforce authenticated identity/generation and transaction-complete IndexedDB CAS across edit revision, actor/epoch and tabs. Storage abort/quota or a late response cannot mutate a replacement session. Same-key resend remains DISABLED; this agreement authorizes neither a manual resend UI nor any automatic retry.

## Compatibility matrix and rejected alternatives

| Pair | Selected requirement; runtime verification remains pending |
|---|---|
| Existing WEB4 browser → future web5/exact13 | Preserve exact v1 context, six writes and positive status. CANCELLED remains unavailable via v1, never an edit unlock. Prove late original versus operator ordering in B02/B04-B. |
| New browser → current web4/exact12 | New extension unavailable; negative controls stay disabled. Existing authorized v1 positive recovery remains available; no downgrade/replay of UNKNOWN. |
| Existing v1 UNKNOWN → new browser/new host | Retain original bytes/fingerprint/raw draft. Authorized original COMMITTED may settle through exact CAS; durable negative requires the separately published extension. |
| Current web4 server binary → schema 13 | Refuse startup. Browser additive compatibility does not make old server binaries schema 13-compatible. Drain before upgrade; rollback only a matched supported binary/database pair, never force downgrade/erase journal. |
| legacy-web11/exact11 or Desktop/exact10 | Preserve separate admission and explicit migration gates; no unkeyed fallback or automatic migration. |

Strict cutover is not selected: it adds per-mutation version fencing and a recovery-only transition for existing v1 records that the current host does not implement. A later switch to strict requires a new reviewed decision and restores all strict-only gates. Staying physically on schema 12 with external/generic audit is rejected: no inspected source establishes the required atomic operation decision/audit authority, and extra objects cannot be silently admitted as exact12.

## Applicability and remaining executable gates

- **F01:** consume this ADR and reconcile actual B01/B04-A pins; F5-CON-04 and the design portion of F5-COMP-01 can close after cross-repository review. This does not certify runtime compatibility.
- **Additive policy:** strict-only B5-COMP-03/04, F5-COMP-03/04 and F5-RES-14 are NOT_APPLICABLE by this dated decision, not PASS or skipped required tests. Common B5-COMP-01/02 and runtime F5-COMP-01/02 remain mandatory.
- **Schema013 selected:** B5-MIG-01/02 are mandatory NOT RUN: exact12 source/history/object/trigger/fingerprint and maintenance/writer-drain admission, fresh13/allowed10/11 paths, corrupt/partial/newer schemas, interrupted upgrade/rerun, failed grants and matched rollback. Never modify frozen012.
- **B02/B04-B:** publish separately versioned HTTP/corpus and implement all six command kinds × both lock orders (12 arrangements), physical source/journal/ledger/audit oracle, current authorization, audit/Commit loss and restart/expiry negatives. Run applicable gates on the exact combined B02 candidate before its acceptance. B04-A 103-case counts cannot substitute.
- **F02/B05/F04:** real client/storage and deployed old/new compatibility, full IIS and applicable operational acceptance remain separate. No executable B02, resend, operator unlock, production deployment or CEX work is selected by this docs-only closure.
