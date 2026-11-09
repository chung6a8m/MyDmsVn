# WEB6 guarded Web operation operator CLI

This isolated net48 console application separates schema13 restricted operator procedures from guarded maintenance commands. It can inspect one exact Web operation, record one audited `NOT_EXECUTED` decision as a matched `CANCELLED` journal/audit pair, or compact one elapsed COMMITTED result under a dedicated maintenance identity. Both mutating commands have separate read-only previews. It is not part of HttpHost, does not create a support case or new client intent, and does not enable manual resend.

## Restricted connection

Configure the dedicated operator connection outside source control:

```powershell
$env:MYDMSVN_WEB_OPERATION_OPERATOR_CONNECTION_STRING = '<restricted operator connection to the exact application database>'
$env:MYDMSVN_WEB_OPERATION_OPERATOR_DATABASE_USER = '<expected restricted database user>'
$env:MYDMSVN_WEB_OPERATION_OPERATOR_LOGIN = '<expected restricted SQL login>'
```

The connection must target the explicit application database and use a separate member of `MyDmsVnWebOperationOperatorRole`. The tool rejects system databases, attach/user-instance connections, target/identity mismatch, runtime/migration/fixed roles, direct or inherited DML/broad reads, `PUBLIC`/nested-role over-grants, schema/database/server/custom-securable authority and grant option. It disables pooling and never falls back to `master` or another database.

## Read-only inspect

```powershell
MyDmsVn.WebOperation.Operator.exe inspect `
  --environment nonproduction `
  --database MyDmsVn_ExactTarget `
  --epoch bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb `
  --actor 123 `
  --operation aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa `
  --fingerprint 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
```

`INDETERMINATE` or a missing journal row is unknown. It is never proof of rollback or `NOT_EXECUTED`.

## Terminal decision dry-run

Dry-run validates the complete decision, target and restricted SQL identity, then calls only the read-only inspection procedure inside a rolled-back transaction. It does not insert/update/delete journal, audit or business data and cannot reserve execute against a later race.

```powershell
MyDmsVn.WebOperation.Operator.exe record-not-executed --dry-run `
  --environment nonproduction `
  --database MyDmsVn_ExactTarget `
  --epoch bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb `
  --actor 123 `
  --operation aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa `
  --kind 1 `
  --target NONE `
  --fingerprint 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef `
  --case CASE-123 `
  --reason ORIGINAL_NOT_EXECUTED `
  --evidence case://CASE-123/evidence-1 `
  --decision dddddddd-dddd-4ddd-8ddd-dddddddddddd
```

Kinds are `1` Sales Create, `2` Sales Update, `3` Sales Post, `4` Goods Receipt Create, `5` Goods Receipt Update and `6` Goods Receipt Post. Create requires `--target NONE`; Update/Post require a canonical positive target ID. Operation and decision IDs must be different GUIDv4 values; the decision ID is audit metadata and never replaces the original operation ID or any login session identity.

## Guarded execute and confirmation

Execute is a separate invocation. There is no default execute and no dry-run-to-execute transition. `--confirm` must exactly equal this case-sensitive composite built from the same validated values:

```text
record-not-executed|environment=<environment>|database=<database>|epoch=<epoch>|actor=<actor>|operation=<operation>|kind=<1..6>|target=<NONE-or-id>|fingerprint=<lowercase-sha256>|decision=<decision-guid>|case=<case>|reason=<reason>|evidence=<evidence>
```

Example shape:

```powershell
MyDmsVn.WebOperation.Operator.exe record-not-executed --execute `
  --environment nonproduction --database MyDmsVn_ExactTarget `
  --epoch bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb --actor 123 `
  --operation aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa --kind 1 --target NONE `
  --fingerprint 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef `
  --case CASE-123 --reason ORIGINAL_NOT_EXECUTED --evidence case://CASE-123/evidence-1 `
  --decision dddddddd-dddd-4ddd-8ddd-dddddddddddd `
  --confirm 'record-not-executed|environment=nonproduction|database=MyDmsVn_ExactTarget|epoch=bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb|actor=123|operation=aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa|kind=1|target=NONE|fingerprint=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef|decision=dddddddd-dddd-4ddd-8ddd-dddddddddddd|case=CASE-123|reason=ORIGINAL_NOT_EXECUTED|evidence=case://CASE-123/evidence-1'
```

Execute first performs a new inspection, then opens a separate explicit SQL transaction and calls only `dbo.RecordWebOperationNotExecuted`. Schema13 owns epoch validation, the actor/key application lock, active-actor check, durable conflict fence and atomic matched audit/journal insert. Success is printed only after Commit returns and a fresh restricted connection re-inspects the exact identity as `CANCELLED` with the matching decision and case.

## Timeout, cancellation and unknown Commit

`--timeout-seconds` accepts 1 through 25 and defaults to 15. The process watchdog uses the same bound. Exit `6` means the SQL/process deadline expired and the outcome is unknown. Exit `8` means pre-Commit rollback/session cleanup, Commit, or post-Commit readback could not be proved and is explicitly `UNKNOWN/ESCALATE`. For unverified pre-Commit cleanup, verify process/session quiescence and independently inspect the original operation before any further action.

For either outcome:

1. Do not automatically execute again and do not create a new operation ID for the same-key action.
2. Wait for process/SQL ownership to drain under the approved operator procedure.
3. Run an independent `inspect` with the same database, epoch, actor, original operation and fingerprint.
4. Escalate COMMITTED/EXPIRED/CANCELLED/INDETERMINATE according to the recovery protocol. A 404, timeout, process exit or missing row is never rollback evidence.

## Exit codes and output

| Code | Meaning |
|---:|---|
| 0 | Help, authoritative inspect state, or known Commit with matched readback and required evidence delivery |
| 2 | Invalid, missing, duplicate, unknown or confirmation-mismatched arguments; SQL is not opened |
| 3 | Denied authority, identity, durable conflict, epoch or fingerprint |
| 4 | Inspect-only `INDETERMINATE` |
| 5 | SQL/environment/cleanup unavailable before a known terminal outcome |
| 6 | Bounded timeout; outcome unknown, inspect independently |
| 7 | Completed read-only dry-run preview; still not proof of `NOT_EXECUTED` |
| 8 | `UNKNOWN/ESCALATE` for unverified pre-Commit rollback/session cleanup, uncertain Commit, or failed post-Commit verification |
| 9 | Known compaction Commit with failed outcome/manifest/backup delivery; independent inspection required |
| 130 | Caller cancellation; inspect independently before further action |

Output is bounded and sanitized. It never prints the connection string, credential, JWT/cookie, fingerprint, original request/result bytes or business payload. `environmentLabelSource=operator-supplied` explicitly states that the label itself is not authoritative environment identity.


## B03 bounded result compaction

`compact-preview` and `compact-execute` operate on one exact owned nonproduction schema13 allocation. The maintenance identity has database `CONTROL` for that allocation only; it remains distinct from runtime, migration and the B02 procedure operator. Provision it using [the maintenance script](../../deployment/sql/Provision-MyDmsVnWebOperationMaintenance.sql). This rehearsal does not authorize a production principal or deployment.

The dedicated environment settings are `MYDMSVN_WEB_OPERATION_MAINTENANCE_CONNECTION_STRING`, `MYDMSVN_WEB_OPERATION_MAINTENANCE_DATABASE_USER`, `MYDMSVN_WEB_OPERATION_MAINTENANCE_LOGIN` and `MYDMSVN_WEB_OPERATION_MAINTENANCE_TARGET_IDENTITY`. The target identity binds the environment, live server/database creation tuple and owned allocation token, as documented in the provisioning script. The connection factory verifies that identity, exact schema13 and the restricted maintenance principal on every connection. There is no fallback to B02 credentials.

Both commands require `--environment`, `--database`, `--current-epoch`, `--stored-epoch`, `--actor`, `--operation`, `--fingerprint`, `--correlation` and `--case`. `--timeout-seconds` accepts 1–25 and defaults to 15. GUIDs and SHA-256 must be lowercase; operation/correlation must be distinct GUIDv4 values. Execute additionally requires this exact composite, including the resolved timeout:

```text
compact-execute|environment=<environment>|database=<database>|current-epoch=<guid>|stored-epoch=<guid>|actor=<id>|operation=<guid-v4>|fingerprint=<sha256>|correlation=<guid-v4>|case=<id>|timeout-seconds=<seconds>
```

Preview is read-only and returns `ELIGIBLE` for an elapsed COMMITTED result or `NO_CHANGE` for its exact EXPIRED tombstone. Execute re-previews, records the verified effective login as a hash in durable attempt evidence, and then opens one ReadCommitted mutation transaction. The existing maintenance primitive acquires the canonical epoch/key fence; its callback checks the exact fingerprint/eligibility and captures immutable journal metadata before the single compaction call updates the row. Exactly one changed row and a final cancellation check are required before Commit. No path retries mutation.

Before execute, the operator must provision two distinct, non-nested private local fixed-NTFS directories with the exact protected current-operator/Administrators ACL required by `MaintenanceEvidencePolicy`. Set their canonical absolute paths through `MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_ROOT` and `MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_BACKUP_ROOT`. The CLI never creates or hardens these roots. The evidence policy retains both copies for exactly 90 days; this command does not purge them. A new case directory is create-new, so evidence from an earlier attempted mutation cannot be overwritten.

Known Commit is followed by a fresh, factory-verified connection checking exact EXPIRED/NULL result storage and every immutable journal field, including document ID and timestamps. Only verified readback plus durable transaction/readback evidence and a verified primary/backup manifest pair produces `status=COMPLETED` (exit 0). An already compacted exact tombstone produces `status=NO_CHANGE` (exit 7), without mutation or a second evidence case.

Pre-Commit rollback/close/dispose uncertainty and Commit-started exceptions produce `UNKNOWN/ESCALATE` (exit 8); cleanup is attempted without retry. Known Commit with failed fresh readback also remains `UNKNOWN/ESCALATE`. If transaction/readback event delivery or final manifest/backup delivery fails after known Commit, output explicitly reports `COMMITTED/AUDIT_DELIVERY_FAILED/ESCALATE; inspect independently` (exit 9). This does not mean rollback. Cancellation/deadline can prevent evidence delivery; retained attempt/partial files are escalation evidence, not success markers. Independently inspect the exact operation and drain the owned session before any attended follow-up. No missing response, partial file or process exit authorizes compaction retry or business command resend.

The immutable EXPIRED tombstone is the canonical SQL mutation record. External files own effective-login/correlation/delivery evidence; no terminal-negative audit row is created. Payload/result bytes and connection credentials never enter the bounded output or evidence envelope. Real-SQL tests independently compare business/source/terminal-audit bytes; the CLI does not run a broad business-table hashing operation.

## B03 read-only committed business reconciliation

`reconcile-committed` uses the same dedicated maintenance connection, exact schema13/owned target verification and private primary/backup evidence roots described above. It accepts the same nine exact identity/case options and optional bounded timeout as compaction preview. There is no confirmation, execute mode, caller-supplied kind/target, result export, SQL mutation or automatic retry. Run `reconcile-committed --help` for its argument shape.

The command records an attempt, checks the current deployment epoch, locks the original actor/key and matches the stored epoch/fingerprint. Create targets derive from `ResultDocumentId`; Update/Post must have equal positive request/result targets. The source read transaction is explicitly rolled back before sanitized readback evidence and the verified primary/backup manifests can authorize a positive CLI outcome. An evidence or rollback failure yields `UNKNOWN/ESCALATE`, never a successful case closure.

The bounded outcomes are:

| Output | Meaning |
|---|---|
| `BUSINESS_COMMITTED_RESULT_UNAVAILABLE` | Original SalesPost or ReceiptPost has retained committed identity, an unavailable/elapsed result, and independently verified current Posted source/line/actor/time, ledger, valuation, frozen COGS and affected stock balances. |
| `BUSINESS_COMMITTED_RESULT_UNAVAILABLE_SUPERSEDED` | A Create/Update baseline has unambiguous retained committed lineage for the same actor/epoch/module/target ending in a valid Post with the same physical proof. |
| `UNKNOWN/ESCALATE` | Any missing/contradictory evidence, current Draft result loss, Update-only later history, ambiguous reuse/order/ownership, mutation after terminal Post, unsupported history size or evidence-delivery failure. |

Retained committed lineage means a canonical COMMITTED row with bounded result storage, or its exact canonical elapsed EXPIRED/NULL-result tombstone. EXPIRED does not mean raw result bytes remain. The complete inspected target history must have one owner/epoch, strictly increasing completion times, at most one initial Create and one terminal Post. Equal timestamps, later CANCELLED and cross-owner/epoch histories are refused. Other modules' numeric document IDs are separate identities.

The independent physical oracle checks complete affected stock-key histories, exact receipt value rounding, Sales duplicate-Product/gift quantity and frozen cumulative COGS allocation, full depletion and current Q/V balances. It uses bounded reads (200 target lines, 1,024 target journal rows, 100,000 movements per affected key); exceeding a bound is UNKNOWN. It uses the existing inventory writer barrier and held source/history locks. It does not compare application timestamps to journal completion timestamps or reconstruct fingerprints, original versions, response bytes or original Draft line identities.

**Schema13 proof limitation:** a coherent current Draft cannot prove the original Create/Update state after result loss; there is no retained rowversion/line witness. Those four current kinds always remain UNKNOWN. Superseded closure proves the retained committed baseline and final immutable posted effect, not a reconstructed historical response or an absent intermediate Local write. This is a support-case outcome only: browser drafts stay protected; no SETTLED state, editable version, line IDs, resend permission or production acceptance is created. Exit 0 means a bounded positive case plus delivered evidence; exit 8 means UNKNOWN; cancellation/deadline retain their existing exit codes.

## Candidate verification and retained gaps (2026-11-09)

[The B03 candidate record](../../docs/evidence/web6/20261109-b03-maintenance-recovery.md) pins local Release/net48 SQL/EXE results, counts and artifact hashes. Protected exact-HEAD CI is NOT RUN / BLOCKED; B6-OPS-05 and B6-MNT-01..03 remain BLOCKED pending hosted evidence and Owner acceptance. The Owner-tagged workflow neither schedules maintenance nor enables production commands.

All four current Draft Create/Update kinds remain UNKNOWN. Superseded retained-committed lineage to immutable Post cannot exclude an unjournaled Local/admin delete-recreate; it does not prove original historical response bytes. Restore tests admit only a restricted-runtime journal Admit+Complete probe. Full restored HTTP/business handlers, service RPO/RTO and provider/network/process-crash compaction proof are NOT RUN. Task 2 retains a deferred Minor: reject default DateTimeOffset before evidence creation. Capability-gated TEST_CLEANUP_ONLY is test hygiene following asserted deterministic injected failures, never workflow success. Manual resend OFF; staging NOT ACCEPTED; production NO-GO.

**Historical B03 full-run stop (2026-11-09):** [candidate evidence](../../docs/evidence/web6/20261109-b03-maintenance-recovery.md) records 344 executed, 342 passed / 2 failed / 0 skipped. A net48 path-length failure in the last race arrangement caused its guard to retain a new database and refuse further admission. Fresh quiescence does not replace the missing original in-memory allocation tuple or authorize cleanup. That failed full run remains historical; later bounded remediation does not convert it to PASS. Protected CI/Owner acceptance remain BLOCKED; resend OFF, staging NOT ACCEPTED, production NO-GO.

**Historical intermediate safety checkpoint:** three earlier SQL-free attempts passed 8/9 and retained synthetic ACL-drift cases; no new SQL was allocated. Those failures and retained resources remain unchanged.

**Current bounded disposition:** the [candidate evidence](../../docs/evidence/web6/20261109-b03-maintenance-recovery.md) records independently accepted R2e NativeSafety and R2f ATTEMPT_05 focused allocation/ownership-manifest results. R2f verifies one administrative-master lifecycle, not restricted-runtime/operator proof. Full 12-selection / 28-checkpoint candidate integration, protected exact-HEAD workflow, and durable private Intent/Original delivery outside the hosted runner remain **BLOCKED / NO-GO**. No prior quarantine gains cleanup authority, and no fresh full-candidate PASS is claimed.

The shared evidence regression defaults to common WEB5/B02/B04 checks; the B03 workflow explicitly requests `-B03` and remains intentionally RED. The four historical R2f private-controller regression scripts are local-only evidence, excluded from the tracked package because their private run-once dependencies are not published. Tracked checkpoint descriptor and sandbox cleanup memory regressions run from repository source. Source/pure fixes do not replace native acceptance or the retained historical hashes. Manual resend OFF; staging NOT ACCEPTED; production NO-GO.
