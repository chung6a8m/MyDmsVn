# Security administration runbook

**Current source:** `v1a`, updated 2026-10-19. SEC-REBUILD accepted in PR #34: code-defined permissions, deliberate assignments and one configured superuser. [HISTORY](HISTORY.md) retains superseded P3.1 synchronization; [ADR 0007](adr/0007-code-defined-permissions-and-single-superuser.md) owns security design. Pending GUI/P5 and P6 operations/release acceptance are now explicitly planned in [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) and [REL/P6](plans/20261019-002-local-v1-p6-release-acceptance.md), not deferred.

The active packages are `desktop-net48-win-x86` (`MyDmsVn.Desktop.App.exe`) and `admin-migrator-net48-win-x86` (`MyDmsVn.Server.DbMigrator.exe`), both requiring .NET Framework 4.8. AppCore is parked outside the solution/release. Commands below run from the admin package directory in an interactive PowerShell console. Replace every quoted placeholder with its reviewed value.

## 1. Prepare a fresh database

1. Confirm the target server, instance, database name, owner, deployment window, backup/recovery point, and release artifact with the database owner.
2. Use a newly provisioned empty application database for a new installation. This baseline does not migrate old permission synchronization data or assignment tables. The current migrator rejects older prefixes 001–008 with `Migration.UnsupportedExistingDatabase`; follow the [migration/recovery runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md), not an automatic retry or journal rewrite. Do not run a reset against a database that contains data to preserve.
3. For a disposable development/test database only, record its disposable designation and confirm no process is using it before the DBA drops and recreates it. Never infer that a database is disposable from its name alone.
4. Have the DBA create separate runtime and migration logins/database users and grant the maintenance identity `CONTROL` in this application database, as described in [configuration and SQL security](CONFIGURATION_AND_SQL_SECURITY.md). The runtime object-grant script requires the completed schema, so apply it after step 6 migration succeeds. Keep maintenance credentials outside Desktop.
5. Set `Security__SuperuserUsername` in the protected deployment environment. Choose the permanent account name before bootstrap.
6. Apply the current scripts 001 through 009 using the packaged DbMigrator with the migration connection configuration. Review runtime schema compatibility and migration output, then apply [Provision-MyDmsVnPrincipals.sql](../deployment/sql/Provision-MyDmsVnPrincipals.sql) and verify the runtime grant matrix before opening the application. RELEASE-MANIFEST-01 is closed narrowly: the publisher now reads the shipped runtime policy and clean-commit d8013030 artifacts report schema9 / scripts001–009 ([evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed)). This metadata check does not prove runtime startup or release acceptance.
7. Bootstrap only when `dbo.Users` and `dbo.SystemSecuritySettings` are empty:

```powershell
$env:Security__SuperuserUsername = '<configured-account-name>'
$env:MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE = 'C:\ProgramData\MyDmsVn\migration.connection'
& .\MyDmsVn.Server.DbMigrator.exe
if ($LASTEXITCODE -ne 0) { throw 'Migration did not complete; stop before bootstrap.' }
& .\MyDmsVn.Server.DbMigrator.exe security bootstrap-superuser --target '<application-database>'
```

The command confirms the exact target database interactively and reads the initial password through a masked prompt. Passwords are not accepted as arguments, saved in environment variables, or included in logs. Bootstrap creates one active user and the protected binding in a serializable transaction; it creates no role or permission grants. It refuses a nonempty user store or already-bound settings. Check `$LASTEXITCODE`: security bootstrap/maintenance commands return `0` for success, `1` for cancelled confirmation and `2` for invalid arguments/configuration or failure. The no-argument migration command has its own exit-code table in the migration runbook.

8. Configure every deployed net48 desktop with the same normalized `Security__SuperuserUsername` and the runtime SQL connection. Startup checks the setting against the protected binding and the current database account.
9. Log in with the configured superuser and verify the permission catalog before creating ordinary users.

The configured superuser is an account identity, not an `Administrator` role. It is not a role assignment and does not appear as an ordinary grant. There is no default password, automatic startup seed, or automatic re-enrollment.

## 2. Catalog and deliberate access

Permission keys are composed from module-owned definitions in the running application. An Active key appearing in a later release does not change any user, role, membership, or assignment row. New keys remain denied to ordinary accounts until an authorized operator explicitly grants them. Unknown and Deprecated keys deny; a direct user Deny overrides every role grant.

To inspect/export the catalog from the matching release package:

```powershell
& .\MyDmsVn.Server.DbMigrator.exe permissions list --format json
```

The output is catalog metadata only; this command does not require SQL configuration, query the database or contain user assignments. Keep the output with the release review if useful, and treat it as versioned metadata. Do not import the catalog as authorization state. This offline behavior comes from [PermissionCatalogCommand](../src/MyDmsVn.Server.DbMigrator/PermissionCatalogCommand.cs), which dispatches before connection loading.

Use the Desktop Security Administration screen to create ordinary users and roles, change role membership, and set direct or role permission decisions. Review the exact target, key, decision and revision before applying. Each successful mutation is authorized in Application, revision-checked, and audited. To give access to a new key, assign it deliberately to a role or user; verify effective access by logging in as that account. A direct Deny remains effective even when a role grants the key.

For a reviewed additive bulk assignment, use the JSON import controls in the same administration screen. The input contract and example are in [permission assignment import example](examples/permission-assignment-import-v1.example.json). Review the preview, target, additions, catalog digest, and current revision. Apply only the reviewed preview. Import adds assignments; it cannot silently overwrite a conflicting direct decision or remove unrelated grants. Re-preview after the target revision or catalog changes.

## 3. Protected superuser rename and recovery

The maintenance commands require the migration connection, protected `Security__SuperuserUsername`, the expected bound user ID, and interactive confirmation of both the exact database name and user ID. Confirm the current database state, take an approved recovery point and stop/drain the clients first. They validate the maintenance principal's `CONTROL` permission, operate only on the currently bound account under a serializable transaction/application lock, and fail closed if the account ID, normalized identity, configuration or binding do not agree. Keep the configured username at the current value until rename succeeds.

Rename the bound account through the maintenance command:

```powershell
& .\MyDmsVn.Server.DbMigrator.exe security rename-superuser `
  --target '<application-database>' --expected-user-id '<positive-bound-user-id>' `
  --new-username '<new-name>'
```

After a successful rename, update `Security__SuperuserUsername` to the new name on every host and restart all hosts before login. The command changes the account name and protected binding in one transaction; it does not create another superuser or assignment.

Recover a disabled or inaccessible bound account through the maintenance command:

```powershell
& .\MyDmsVn.Server.DbMigrator.exe security recover-superuser `
  --target '<application-database>' --expected-user-id '<positive-bound-user-id>'
```

The password is read with a masked prompt. Recovery updates the password hash, marks that same bound account active, and preserves its identity binding. Keep clients stopped through maintenance, then restart and require sign-in again; do not treat a password change as a replacement for the operator's stop/drain procedure. Do not use bootstrap as a recovery operation, create a second superuser, or edit the binding with the runtime principal.

If recovery fails, stop and preserve the state. Review safe logs and the bound account using an independently approved DBA procedure; do not retry against another user ID until the owner has reviewed the identity and target.

## 4. Login, session, and Local trust boundary

- Login with the configured superuser first, then create an ordinary user and role and grant only the required keys.
- Test the ordinary account and verify default deny, role grant, and direct Deny behavior.
- Logout clears the current Local session. Expiry requires reauthentication; verify it with the applicable acceptance walkthrough.
- Local Mode runs application authorization inside the desktop process while the desktop has SQL connectivity. A person who controls that machine/process can use the runtime SQL credential outside the UI. Least privilege limits the database blast radius but does not create a trusted server boundary. Protect the desktop, runtime connection configuration, SQL network access, and backups. P7 HTTP service is [deferred](DEFERRED_WORK.md#3-p7--http-and-remote-client), not an implemented server-side trust boundary.

## 5. Audit and evidence

Review security changes through the administration UI and `dbo.SecurityAuditEvents` using an approved read-only/audit procedure. Preserve actor, target, operation, before/after state, timestamp and correlation, with the target revision/preview recorded in the change review. Security administration/import mutation and SQL audit are transactional; a SQL audit failure rolls the mutation back. Local `audit-*.json` files have a separate post-commit failure model described in [diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md). Export only sanitized evidence needed for the change record.

Never publish passwords, password hashes/salts, full connection strings, raw TRX, SQL exception chains, database identifiers or user data. Keep detailed test configuration and ownership diagnostics local; share only the independently reviewed sanitized aggregate. Test scope and required acceptance follow the sole [TEST_POLICY](TEST_POLICY.md); report a focused category as its actual selected evidence, not an unfiltered result.

## 6. Failure handling

- A configuration/binding mismatch blocks startup. Check the configured name, database target and protected binding; do not bypass the check.
- A failed bootstrap or maintenance transaction rolls back. Confirm state with the database owner before another attempt.
- Keep runtime and migration principals separate. Runtime can read the superuser binding but cannot change it or run migrations.
- Do not reset a database containing data to preserve. A restore requires owner-approved recovery point/RPO, a verified destination and the reviewed recovery procedure; confirm the consequence for writes after that point.
- SEC-REBUILD's accepted PR #34 remains historical completed work. RESP/L2DB are also complete in plan scope. Pending installed GUI/permission-user walkthroughs, actual credential rotation, recovery and owner sign-off are **OPEN** under [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) and [REL/P6](plans/20261019-002-local-v1-p6-release-acceptance.md); neither runbook nor earlier test evidence closes those release gates.
