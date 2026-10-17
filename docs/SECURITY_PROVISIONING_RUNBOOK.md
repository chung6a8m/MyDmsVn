# Security provisioning runbook

Status: Current SEC-REBUILD T01 bootstrap and binding workflow. The historical P3.1 synchronization workflow is retained as evidence in `docs/P3_1_VERIFICATION.md`; it is not part of the current executable path.

## 1. Fresh installation

1. Confirm the approved server and application database, maintenance window, backup/recovery point and release artifact.
2. Provision separate runtime and migration SQL principals as described in [configuration and SQL security](CONFIGURATION_AND_SQL_SECURITY.md).
3. Set `Security__SuperuserUsername` in the protected deployment environment. Use the intended permanent account name; the value is normalized for binding.
4. Run the migrator with its maintenance connection configuration. The active fresh schema is scripts `001`–`007`; migration `002_Identity.sql` creates the protected singleton binding table.
5. Run the one-time bootstrap command. It prints the requested target database and configured username, then requires typing the exact database name. The password is read through a masked prompt and is never accepted as a command-line argument.
6. Verify the user and singleton binding through a separately approved administrative procedure. Do not add a role or permission grants for the configured superuser.
7. Start the desktop with the same `Security__SuperuserUsername` value and runtime SQL principal. Startup verifies that the configured name, bound normalized name and current database account name agree.

```powershell
$env:Security__SuperuserUsername = '<configured-account-name>'
$env:MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE = 'C:\ProgramData\MyDmsVn\migration.connection'
dotnet MyDmsVn.Server.DbMigrator.dll

dotnet MyDmsVn.Server.DbMigrator.dll security bootstrap-superuser --target <application-database>
```

The bootstrap command requires an empty `dbo.Users` table and an empty `dbo.SystemSecuritySettings` table. It inserts one active user and one binding in a serializable transaction protected by a SQL application lock. Any failure rolls back both inserts. The password is stored only as the current one-way hash; `PasswordSalt` remains for the legacy schema contract and is set to an empty value for this new hash format. The command requires the migration principal to have `CONTROL` in the application database.

## 2. Runtime behavior

- Runtime can `SELECT` the binding table and cannot insert, update or delete its rows.
- Each authorization request reads current account activity and binding from SQL. A disabled or renamed bound account is denied until an approved maintenance operation updates the binding/configuration together.
- `Administrator` is an ordinary role name. It does not receive the superuser bypass.
- The configured active superuser bypasses role/direct-user permission decisions only for the exact bound database user id and normalized username.
- Ordinary users use explicit direct decisions and active-role grants. Direct Deny overrides role grants; no matching grant means deny. Permission definitions are supplied by the running application composition, not synchronized into SQL.

## 3. Failure and recovery

- A bootstrap attempt against a nonempty user store or existing binding fails without mutation. Do not delete users or binding rows to retry.
- A configuration/binding mismatch blocks desktop startup with `Security.SuperuserBindingMismatch`. Confirm the deployment setting and database identity. If a bound account was intentionally renamed or disabled, use a separately reviewed maintenance procedure; do not run bootstrap again.
- A failed insert rolls back the user and binding together. Review safe logs and the database state before a new approved attempt.
- The runtime SQL principal cannot repair or replace the binding. Keep recovery limited to the maintenance principal, with a recorded owner, target database confirmation and post-change verification.
- Never place a password in a command argument, environment variable, script, approval file, ticket, log or screenshot.

## 4. Permission changes

Permission keys are owned by modules and composed into an immutable code-defined registry. New keys begin active in application code but remain denied to ordinary users until an operator assigns an active role or direct-user grant through the security-administration workflow. Deprecated or unknown keys are denied. New catalog/import workflows are governed by the later SEC-REBUILD tasks; this runbook does not define permission synchronization or file import.
