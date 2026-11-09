# WEB6 guarded Web operation operator CLI

This isolated net48 console application uses the schema13 restricted operator procedures. It can inspect one exact Web operation, preview a terminal-negative decision without mutation, or record one audited `NOT_EXECUTED` decision as a matched `CANCELLED` journal/audit pair. It is not part of HttpHost, does not create a support case or new client intent, and does not enable manual resend.

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
| 0 | Help, authoritative inspect state, or known-Commit terminal decision with matched readback |
| 2 | Invalid, missing, duplicate, unknown or confirmation-mismatched arguments; SQL is not opened |
| 3 | Denied authority, identity, durable conflict, epoch or fingerprint |
| 4 | Inspect-only `INDETERMINATE` |
| 5 | SQL/environment/cleanup unavailable before a known terminal outcome |
| 6 | Bounded timeout; outcome unknown, inspect independently |
| 7 | Completed read-only dry-run preview; still not proof of `NOT_EXECUTED` |
| 8 | `UNKNOWN/ESCALATE` for unverified pre-Commit rollback/session cleanup, uncertain Commit, or failed post-Commit verification |
| 130 | Caller cancellation; inspect independently before further action |

Output is bounded and sanitized. It never prints the connection string, credential, JWT/cookie, fingerprint, original request/result bytes or business payload. `environmentLabelSource=operator-supplied` explicitly states that the label itself is not authoritative environment identity.
