# Security administration runbook

Status: Current SEC-REBUILD implementation guidance. The supported baseline uses a code-defined permission catalog, explicit RBAC assignments, and one configured superuser. The retired P3.1 synchronization workflow is historical evidence only; see [P3.1 verification](P3_1_VERIFICATION.md) and [ADR 0007](adr/0007-code-defined-permissions-and-single-superuser.md).

## 1. Prepare a fresh database

1. Confirm the target server, instance, database name, owner, deployment window, backup/recovery point, and release artifact with the database owner.
2. Use a newly provisioned empty application database for a new installation. This baseline does not migrate old permission synchronization data or assignment tables. Do not run a reset against a database that contains data to preserve.
3. For a disposable development/test database only, record its disposable designation and confirm no process is using it before the DBA drops and recreates it. Never infer that a database is disposable from its name alone.
4. Provision separate runtime and migration principals using [configuration and SQL security](CONFIGURATION_AND_SQL_SECURITY.md). The migrator uses the maintenance principal; the desktop uses the least-privilege runtime principal.
5. Set `Security__SuperuserUsername` in the protected deployment environment. Choose the permanent account name before bootstrap.
6. Apply the current scripts 001 through 009 using the packaged DbMigrator with the migration connection configuration. Review schema compatibility and migration output before opening the application.
7. Bootstrap only when `dbo.Users` and `dbo.SystemSecuritySettings` are empty:

```powershell
$env:Security__SuperuserUsername = '<configured-account-name>'
$env:MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE = 'C:\ProgramData\MyDmsVn\migration.connection'
dotnet MyDmsVn.Server.DbMigrator.dll
dotnet MyDmsVn.Server.DbMigrator.dll security bootstrap-superuser --target <application-database>
```

The command confirms the target database interactively and reads the initial password through a masked prompt. Passwords are not accepted as arguments, saved in environment variables, or included in logs. Bootstrap creates one active user and the protected binding in a serializable transaction; it creates no role or permission grants. It refuses a nonempty user store or already-bound settings.

8. Configure every desktop host with the same normalized `Security__SuperuserUsername` and the runtime SQL connection. Startup checks the setting against the protected binding and the current database account.
9. Log in with the configured superuser and verify the permission catalog before creating ordinary users.

The configured superuser is an account identity, not an `Administrator` role. It is not a role assignment and does not appear as an ordinary grant. There is no default password, automatic startup seed, or automatic re-enrollment.

## 2. Catalog and deliberate access

Permission keys are composed from module-owned definitions in the running application. An Active key appearing in a later release does not change any user, role, membership, or assignment row. New keys remain denied to ordinary accounts until an authorized operator explicitly grants them. Unknown and Deprecated keys deny; a direct user Deny overrides every role grant.

To inspect/export the catalog from the matching release package:

```powershell
dotnet MyDmsVn.Server.DbMigrator.dll permissions list --format json
```

The output is catalog metadata only; it contains no user assignments and does not modify the database. Keep the output with the release review if useful, and treat it as a versioned release artifact. Do not import the catalog as authorization state.

Use the Desktop Security Administration screen to create ordinary users and roles, change role membership, and set direct or role permission decisions. Review the exact target, key, decision and revision before applying. Each successful mutation is authorized in Application, revision-checked, and audited. To give access to a new key, assign it deliberately to a role or user; verify effective access by logging in as that account. A direct Deny remains effective even when a role grants the key.

For a reviewed additive bulk assignment, use the JSON import controls in the same administration screen. The input contract and example are in [permission assignment import example](examples/permission-assignment-import-v1.example.json). Review the preview, target, additions, catalog digest, and current revision. Apply only the reviewed preview. Import adds assignments; it cannot silently overwrite a conflicting direct decision or remove unrelated grants. Re-preview after the target revision or catalog changes.

## 3. Protected superuser rename and recovery

The maintenance commands require the migration connection, protected `Security__SuperuserUsername`, the expected bound user ID, and interactive confirmation of both the exact database name and user ID. Confirm the current database state and take an approved recovery point first. These commands operate only on the currently bound account and fail closed if the account ID, normalized identity, configuration, or binding do not agree.

Rename the bound account through the maintenance command:

```powershell
dotnet MyDmsVn.Server.DbMigrator.dll security rename-superuser --target <application-database> --expected-user-id <id> --new-username <new-name>
```

After a successful rename, update `Security__SuperuserUsername` to the new name on every host and restart all hosts before login. The command changes the account name and protected binding in one transaction; it does not create another superuser or assignment.

Recover a disabled or inaccessible bound account through the maintenance command:

```powershell
dotnet MyDmsVn.Server.DbMigrator.dll security recover-superuser --target <application-database> --expected-user-id <id>
```

The password is read with a masked prompt. Recovery updates the password hash, marks that same bound account active, and preserves its identity binding. Existing sessions must authenticate again. Do not use bootstrap as a recovery operation, create a second superuser, or edit the binding with the runtime principal.

If recovery fails, stop and preserve the state. Review safe logs and the bound account using an independently approved DBA procedure; do not retry against another user ID until the owner has reviewed the identity and target.

## 4. Login, session, and Local trust boundary

- Login with the configured superuser first, then create an ordinary user and role and grant only the required keys.
- Test the ordinary account and verify default deny, role grant, and direct Deny behavior.
- Logout clears the current Local session. Expiry requires reauthentication; verify it with the applicable acceptance walkthrough.
- Local Mode runs application authorization inside the desktop process while the desktop has SQL connectivity. A person who controls that machine/process can use the runtime SQL credential outside the UI. Least privilege limits the database blast radius but does not create a trusted server boundary. Protect the desktop, runtime connection configuration, SQL network access, and backups. P7 HTTP service is the planned server-side trust boundary.

## 5. Audit and evidence

Review security changes through the administration UI and `dbo.SecurityAuditEvents` using an approved read-only/audit procedure. Preserve who changed which target and key, the before/after decision, revision, and timestamp. Export only sanitized evidence needed for the change record.

Never publish passwords, password hashes/salts, full connection strings, approval files, ownership journals, raw TRX, SQL exception chains, or user data. Keep test approval and ownership evidence local; share only the independently reviewed sanitized aggregate. A focused SQL category is task evidence, not final unfiltered SQL acceptance.

## 6. Failure handling

- A configuration/binding mismatch blocks startup. Check the configured name, database target and protected binding; do not bypass the check.
- A failed bootstrap or maintenance transaction rolls back. Confirm state with the database owner before another attempt.
- Keep runtime and migration principals separate. Runtime can read the superuser binding but cannot change it or run migrations.
- Do not restore or reset a database without an owner-approved recovery point and a verified disposable/new target.
- Keep packaged GUI checks and final SQL/GUI release gates open until their evidence is recorded in the active [SEC-REBUILD plan](plans/20261016-003-permission-system-rebuild.md).
