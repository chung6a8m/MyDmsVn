# WEB6 B03 Maintenance & Recovery Rehearsal Design

**Date:** 2026-11-09 (Asia/Bangkok)  
**Status:** OWNER APPROVED 2026-11-09  
**Roadmap:** `docs/plans/20261103-001-web6-recovery-quality-backend.md`, B03  
**Acceptance:** `docs/development/WEB6_BACKEND_ACCEPTANCE.md`, B6-OPS-05 and B6-MNT-01 through B6-MNT-03

## Intent

B03 turns the existing maintenance primitives and WEB5 recovery fixtures into an attended, bounded nonproduction workflow. It must let an authorized operator preview and compact one exact elapsed COMMITTED result, reconcile a COMMITTED operation whose result bytes are unavailable, and rehearse backup/restore plus epoch rotation on newly owned disposable resources. The workflow exists to prove recovery safety and support operations; it does not enable manual resend, staging, or production.

## Goals

- Add guarded `compact-preview` and `compact-execute` commands to the isolated net48 operator executable.
- Use a separately provisioned, short-lived maintenance identity with database `CONTROL` only on the exact owned test database; keep the B02 operator and web runtime denied.
- Preserve actor, operation key, request fingerprint, stored epoch, journal tombstone, source rows, stock, ledger, valuation and COGS.
- Produce durable, sanitized attempt/outcome evidence whose failure semantics distinguish pre-mutation denial, transaction uncertainty, known Commit, readback and post-Commit audit delivery failure.
- Add positive reconciliation for COMMITTED/RESULT_UNAVAILABLE without synthesizing response bytes, versions, line IDs, browser settlement or a new write admission.
- Rehearse backup and restore into a different owned database, rotate epoch before writer admission on the restored clone, re-provision separate principals, and verify old/current admission boundaries.
- Prove exact ownership and quiescence before cleanup; quarantine uncertainty and leave historical WEB5/B04 resources untouched.

## Non-goals and anti-goals

- No schema14 migration, new stored procedure or permanent maintenance role in this bounded rehearsal.
- No `CONTROL`, backup, restore or audit-file access for the B02 operator or web runtime identity.
- No automatic compaction scheduler, bulk selection, journal DELETE, tombstone deletion or prefix-based database cleanup.
- No fake ticket-system integration and no raw payload, result bytes, JWT, cookie, connection string, password, SQL token or business document dump in evidence.
- No fabricated HTTP outcome, client `SETTLED` state, unlock, same-key resend or new operation ID.
- No claim that database restore duration alone is service RPO/RTO, and no staging/production readiness claim.

## Approaches considered

### 1. Short-lived database-CONTROL maintenance identity — selected

Provision a unique SQL login/user only for a newly owned disposable database, grant database `CONTROL`, run one bounded attended rehearsal, revoke/drop it, and independently verify removal. This reuses the current guarded `SqlWebOperationMaintenance.CompactAsync` contract without changing schema13. Its cost is explicit lifecycle and audit proof for a powerful temporary credential.

### 2. New narrow stored procedure and maintenance role — deferred

A schema migration could encapsulate compaction and grant only EXECUTE. This is a better candidate for a durable production model, but it changes the exact13 compatibility boundary, frozen deployment assumptions and permission surface. It requires a separate ADR, migration, downgrade/mixed-client analysis and protected migration evidence; B03 does not smuggle that work into a rehearsal.

### 3. Reuse migrator/admin or broaden the B02 operator — rejected

Reusing a standing high-privilege identity hides the authority boundary; granting `CONTROL` to the ordinary operator or runtime violates the roadmap. The restore controller still needs fixture-owner authority for BACKUP/RESTORE, but that authority remains outside the operator CLI and cannot be used for routine compaction.

## Owner decisions

- The immutable journal tombstone produced by the same compaction transaction is the canonical durable in-SQL mutation record. Operator identity, correlation and delivery status belong to the separately durable external audit. B03 does not add an audit table or change schema13.
- Reconciliation distinguishes an exact current business-oracle match from a provably superseded document. A superseded conclusion is allowed only when a later matching COMMITTED journal lineage for the same target is independently established; missing lineage or contradictory physical state is UNKNOWN/ESCALATE.
- Primary audit/case evidence and one separately owned backup copy are retained for 90 days. Deletion is attended and exact-manifest/hash-bound; age, prefix or directory enumeration alone never authorizes cleanup.

## Authority model

| Identity | Scope | Allowed | Explicitly denied |
|---|---|---|---|
| Web runtime | Existing exact13 runtime grants | Normal authenticated application operations | Operator procedures, `CONTROL`, BACKUP/RESTORE, evidence files |
| B02 operator | Existing `MyDmsVnWebOperationOperatorRole` | Inspect and guarded terminal negative procedures | Direct table DML/read beyond procedures, `CONTROL`, compaction, BACKUP/RESTORE |
| B03 maintenance identity | One exact owned disposable database and rehearsal lifetime | Connect to that database; database `CONTROL`; run guarded compaction and exact readback | Other databases, server-wide administration, runtime use, implicit reuse |
| Recovery fixture owner | Test harness/runbook only, bound to fresh source/clone ownership manifests | Create owned DBs and backup device, BACKUP/RESTORE exact owned names, epoch rotation, principal provisioning, cleanup after proof | Operator CLI use, arbitrary database names, historical quarantine adoption |

The maintenance connection is supplied through a dedicated secret environment boundary, never a command-line argument or output. The command must bind an explicit environment and database name and verify server/database identity, schema13 fingerprint, current epoch and effective login before preview or execution. Provisioning and teardown record only hashes and bounded identifiers.

## Compaction command design

### `compact-preview`

Inputs bind environment, database, actor, operation ID, stored epoch, current epoch, fingerprint and a new correlation ID. The command validates canonical formats, verifies maintenance authority and exact schema/database identity, takes the journal key fence in an explicit read/rollback transaction where required, and reports one of:

- `ELIGIBLE`: exact COMMITTED row, elapsed `ResultExpiresAtUtc`, retained result bytes, matching actor/key/epoch/fingerprint.
- `NO_CHANGE`: already EXPIRED with immutable tombstone matching the request.
- `DENIED`: wrong identity, authority, epoch, fingerprint, disposition or retention time.
- `UNKNOWN/ESCALATE`: timeout, cancellation or provider/session uncertainty.

Preview never calls `CompactAsync`, changes a journal row, writes a success audit or claims that a later execute will win a race.

### `compact-execute`

Execute requires a fresh invocation and an exact typed composite confirmation containing command, environment, database, actor, operation, stored/current epoch, fingerprint and correlation. It revalidates all state, opens one explicit transaction, emits a durable sanitized attempt event, calls `CompactAsync` once, and commits only when exactly one eligible row changed. After known Commit it opens a fresh connection and verifies:

- disposition `EXPIRED`;
- `ResultBytes IS NULL`;
- actor/key/fingerprint/stored epoch and immutable source/business state unchanged;
- no terminal negative audit was created;
- correlation/outcome evidence is present.

Zero changed rows are a denied/stale race, never success. A Commit-started exception or failed fresh readback is `UNKNOWN/ESCALATE`; the CLI never retries. If SQL Commit is known but post-Commit evidence delivery fails, the command reports committed state plus `AUDIT_DELIVERY_FAILED/ESCALATE`, instructs independent inspection, and never claims rollback.

## Audit and case evidence

The bounded rehearsal uses an Owner-designated private local evidence root created for the run. The run verifies it is an ordinary non-reparse directory, applies an explicit current-operator/administrators ACL, refuses an existing foreign path, and writes UTF-8 sanitized records with create-new/append semantics. Records contain event version, UTC time, command, environment/database hashes, effective login hash, actor, abbreviated operation/correlation identifiers, epochs, fingerprint hash, phase, transaction certainty, readback outcome and evidence-file hash. They exclude secrets and business payload.

Attempt evidence is durable before mutation. The same-transaction transition of the exact COMMITTED journal row to its immutable EXPIRED tombstone is the canonical durable in-SQL record of the state mutation; no separate SQL audit table or terminal-negative audit is created. SQL Commit and fresh readback are separate external outcome events. The final manifest hashes every event and records delivery status. This is a manual structured case, not a ticket-system claim. Failure to create the attempt record prevents mutation; failure after known Commit cannot undo SQL and becomes an explicit reconciliation requirement.

Each accepted case is copied to a second separately created, ownership-verified, non-reparse private backup root. Primary and backup manifests contain the same event hashes plus their reciprocal copy identifiers. Both copies have a 90-day retention period measured from the final manifest UTC time. Purge is a separate attended action that requires exact path, creation identity, case ID, final manifest hash, expiration and matching backup disposition; it never scans or deletes by prefix. Failed copy or purge leaves the case retained and reports escalation.

## COMMITTED/RESULT_UNAVAILABLE reconciliation

`reconcile-committed` is read-only and accepts the exact actor/epoch/key/fingerprint plus a manual case ID. It requires a COMMITTED journal/tombstone whose result is expired or unavailable and independently proves the business effect appropriate to the original kind:

- source header and stable line identities;
- Posted state where applicable;
- Sales/Receipt semantic constraints;
- stock balance, stock ledger, valuation and COGS invariants for Post;
- actor, target, kind, deployment epoch and request fingerprint.

The result is `BUSINESS_COMMITTED_RESULT_UNAVAILABLE` only when every applicable current oracle matches. If the current document differs, the command may instead return `BUSINESS_COMMITTED_RESULT_UNAVAILABLE_SUPERSEDED` only when one or more later COMMITTED journal records for the same operation target establish a monotonic lineage and the current source/ledger state matches that later durable state. It writes a sanitized case record but returns no response bytes/version/line IDs and performs no journal, source, permission or client-state mutation. Missing later lineage, target reuse ambiguity, deleted source without an immutable business oracle, or any contradictory physical state is `UNKNOWN/ESCALATE`.

## Backup, restore and epoch rehearsal

The recovery fixture owner extends the existing owned `Web5BackupFixture`/`Web5BackupRestoreTests` pattern rather than exposing BACKUP/RESTORE through the operator CLI:

1. Allocate a fresh exact13 source database and record creation identity/ownership token.
2. Seed representative journal states, tombstones, source documents, stock, ledger, valuation and COGS plus one compacted key and one positive reconciliation case.
3. Drain writers and create a new non-reparse backup device in a run-owned private directory; issue `COPY_ONLY, CHECKSUM` backup and verify file identity/hash.
4. Restore to a different generated database with separately owned data/log paths; never overwrite or attach the source.
5. Keep writer admission closed, validate schema and physical oracle, rotate `WebDeploymentState.DeploymentEpoch` exactly once on the clone, and provision fresh runtime/operator/maintenance test identities.
6. Prove old epoch/token and stale writer/status requests are rejected; prove current epoch and grants admit only the selected bounded operation. A normal host restart does not rotate epoch.
7. Re-run compact/reconcile read-only checks on the clone, record recovery timestamps honestly, drain every process/session, and remove only resources whose manifests still match.

Failure before exact ownership, quiescence or path identity is proven quarantines the affected run-owned resource. The two historical quarantines and unrelated IIS probe directories are never queried for deletion.

## Test and acceptance strategy

### Pure/parser and lifecycle tests

- Exact command/mode/input/confirmation validation and secret-free output.
- Preview cannot mutate; execute cannot run without confirmation or maintenance authority.
- Attempt audit failure prevents mutation; Commit/readback/post-Commit audit failures preserve certainty rules and zero retry.
- B02 operator, runtime and wrong-database identities are denied.
- Evidence root rejects reparse, foreign pre-existing, broad ACL and path traversal cases.

### Real SQL tests

- Eligible COMMITTED result compacts once; second attempt is `NO_CHANGE`; tombstone identity remains immutable.
- Not elapsed, wrong actor/key/epoch/fingerprint, active writer, lock timeout, cancelled token and audit failure leave bytes/state unchanged.
- Concurrent writer/compactor serialization and explicit rollback have no partial effect.
- Positive reconciliation covers all six operation kinds and detects source/ledger/stock/valuation/COGS contradictions.
- Restricted principals are probed from the Release executable, not inferred from in-process permissions.

### Recovery rehearsal

- Backup/restore source and clone identities, file hashes and schema fingerprint are recorded.
- Old epoch/session/grant denial and current admission are exercised after restore.
- Source and clone physical oracles match before the deliberate clone epoch rotation.
- Process/session/database drain and verified owned cleanup are mandatory; skipped cleanup is not PASS.

Protected hosted SQL evidence is required for counts, TRX hashes, artifact hashes, restricted-principal capability and owned cleanup. Local SQL evidence does not substitute for the protected gate. B6-OPS-05 and B6-MNT-01 through B6-MNT-03 remain BLOCKED until actual selected runs and Owner acceptance.

## Delivery slices

1. Guarded compaction CLI plus temporary maintenance-principal provisioning and lifecycle tests.
2. Sanitized audit/case evidence and COMMITTED-result-unavailable reconciliation.
3. Owned backup/restore/epoch rehearsal, protected workflow and final B03 evidence.

Each slice keeps schema13 and runtime behavior unchanged, uses focused RED→GREEN tests, and may be reviewed independently. B03 completion does not enable manual resend, accept staging or approve production.

