# WEB6 read-only operation inspector

This net48 console application inspects one exact schema13 Web operation through the existing restricted operator stored procedure. It never records `NOT_EXECUTED`, changes the journal, creates a support case, or connects to another database as fallback.

Configure the dedicated operator connection outside source control:

```powershell
$env:MYDMSVN_WEB_OPERATION_OPERATOR_CONNECTION_STRING = '<restricted operator connection to the exact application database>'
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

Use the real non-empty deployment epoch. `--timeout-seconds` accepts 1 through 60 and defaults to 15. The tool validates that both the configured initial catalog and `DB_NAME()` equal `--database`; system databases, attach/user-instance connections, missing configuration and target mismatch fail closed.

Exit codes: `0` authoritative COMMITTED/EXPIRED/CANCELLED or help, `2` invalid arguments, `3` denied identity/authority, `4` INDETERMINATE (including a missing/foreign row), `5` SQL/environment unavailable, `6` bounded timeout, `130` caller cancellation. Output contains bounded metadata only, never connection data, credentials, JWT/cookies or original business/result payload.
