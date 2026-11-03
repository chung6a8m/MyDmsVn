# WEB6 read-only operation inspector

This net48 console application inspects one exact schema13 Web operation through the existing restricted operator stored procedure. It never records `NOT_EXECUTED`, changes the journal, creates a support case, or connects to another database as fallback.

Configure the dedicated operator connection outside source control:

```powershell
$env:MYDMSVN_WEB_OPERATION_OPERATOR_CONNECTION_STRING = '<restricted operator connection to the exact application database>'
$env:MYDMSVN_WEB_OPERATION_OPERATOR_DATABASE_USER = '<expected restricted database user>'
$env:MYDMSVN_WEB_OPERATION_OPERATOR_LOGIN = '<expected restricted SQL login>'
```

Run:

```powershell
MyDmsVn.WebOperation.Operator.exe inspect `
  --environment nonproduction `
  --database MyDmsVn_ExactTarget `
  --epoch bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb `
  --actor 123 `
  --operation aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa `
  --fingerprint 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
```

Use the real non-empty deployment epoch. `--timeout-seconds` accepts 1 through 25 and defaults to 15. This public bound expires before the Infrastructure read command's 30-second hard timeout; an actual SQL command timeout (`SqlException -2`) is also reported as the bounded timeout exit. The executable arms a process watchdog at the same deadline: if provider rollback/close/disposal does not return, the dedicated process terminates with exit 6 and reports the SQL outcome as unknown. Caller cancellation that completes before the deadline remains distinct at exit 130.

The tool validates that both the configured initial catalog and `DB_NAME()` equal `--database`; system databases, attach/user-instance connections, missing configuration and target mismatch fail closed. Before inspection it also requires exact configured `USER_NAME()` and `ORIGINAL_LOGIN()`, membership in `MyDmsVnWebOperationOperatorRole`, no additional direct database role/grant beyond normal `CONNECT`, and no effective runtime/migration/fixed-role, DML, broad read, schema/database/server control, impersonation/grant-option or unexpected execute/reference authority. Effective permissions are evaluated across nested roles, `PUBLIC` and role over-grants; only terminal-audit read plus the two existing schema13 operator procedures are allowed. Unknown or ambiguous membership is denied. Do not point this CLI at a runtime, migration, maintenance or administrator connection.

Exit codes: `0` authoritative COMMITTED/EXPIRED/CANCELLED or help, `2` invalid arguments, `3` denied identity/authority, `4` INDETERMINATE (including a missing/foreign row), `5` SQL/environment unavailable or cleanup failure, `6` bounded timeout, `130` caller cancellation. `environmentLabel` is explicitly accompanied by `environmentLabelSource=operator-supplied`; it is not an authoritative environment identity. Output contains bounded metadata only, never connection data, credentials, JWT/cookies or original business/result payload.
