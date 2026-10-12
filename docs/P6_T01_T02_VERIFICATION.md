# P6-T01/T02 verification — secure configuration and schema control

Date: 2026-10-13

Scope: detailed P6-T01 and P6-T02 in `docs/plans/20261008-007-phase-6-local-hardening.md`. This is not P6 release sign-off.

## Implemented

- Purpose-specific SQL configuration loader for desktop runtime and the separate migrator. Direct environment value overrides an environment-selected file, then `%ProgramData%\MyDmsVn\runtime.connection` or `migration.connection`. There is no runtime/admin fallback.
- Safe Vietnamese startup/configuration diagnostics that never echo the connection string. Both desktop hosts run the same read-only schema preflight before registering SQL persistence or opening the shell. Headless `--smoke-test` configuration failures return exit code 2 without a modal dialog.
- Idempotent SQL role/grant script and permission matrix. Runtime can perform required P3/P5 DML, read `SchemaVersions` for preflight and inspect database metadata so unmanaged objects cannot be hidden by SQL metadata visibility, but cannot create users, deactivate accounts, mutate the journal, alter schema, impersonate another database/server principal or read unlisted data. Password replacement is limited to five named columns, including an explicit upgrade-time revoke of legacy table-wide `Users` grants. Provisioning and desktop startup reject identical/elevated runtime identities, including direct object-level DDL and principal-control permissions; migration role is separate and has database `CONTROL` for the controlled deployment step.
- Exact schema manifest for immutable scripts 001–007 plus type-safe schema-scoped user-object inventory (including synonyms and database-scoped DDL triggers), enabled/trusted CHECK groups, exact primary/unique/foreign-key definitions and enabled append-only trigger checks. Empty/old/current/new/forked/partial histories receive distinct safe statuses; an appended future journal plus future objects is classified `TooNew`, and current is the only desktop-compatible state.
- Migrator-side principal validation and schema preflight under an exclusive session-owned SQL application lock. The lock, preflight and all DbUp transaction-per-script work share one SQL session, so session loss stops the active script instead of leaving it running without the lock. Database-level `CONTROL` is mandatory even for a no-op; unmanaged, future, forked and partial targets are rejected before DbUp mutation. Valid empty/old/current targets are allowed; DbUp remains `WithTransactionPerScript()`.
- Administrator configuration/security guide and migration/recovery/rollout runbook. Baseline policy requires downtime because no prior production client combination has been approved for mixed-version operation.

## RED→GREEN evidence

Focused tests were first observed failing for missing production APIs/artifacts, then passing on both applicable TFMs:

- configuration precedence, purpose isolation, invalid/system target redaction and unreadable-file security failures;
- SQL runtime/migration role grants, distinct/elevated identity rejection including direct object `ALTER` and `IMPERSONATE` on the migration user, upgrade from legacy broad `Users` grants, column-scoped password update, denied user creation/deactivation, journal read-only access, denied DDL and unlisted-object data access, admin/runtime parity when detecting an unlisted object without data grants, plus migrator rejection of the runtime role even for a no-op;
- schema policy for empty, exact, old, future, reordered/forked and partial states;
- SQL preflight for current/no-op, known prefix, interrupted prefix, future with future objects, forked history, cross-schema unmanaged objects, synonyms, database-scoped DDL triggers and allowlist-name/type spoofing; missing/wrong primary and unique keys; wrong filters; wrong FK mappings/actions; disabled/untrusted CHECK/foreign-key constraints; disabled append-only trigger; and journal/artifact mismatch;
- migration application lock, competing owner rejection and fault injection proving that killing the lock-holding session also stops/rolls back the active DbUp script before a clean one-script recovery;
- unmanaged database, future history and partial schema rejected without changing existing data/history;
- an injected SQL failure after committed scripts 001–006 rolls back its own artifact and leaves the journal at 6; that state blocks clients, while an explicit forward migration applies production script 007 and preserves existing business data;
- both desktop target test assemblies exercise the shared startup coordinator and runtime/admin configuration separation.

## Final commands and actual results

Environment:

- Windows `10.0.26200.0`, x64
- .NET SDK `8.0.425`
- SQL Server `14.0.2130.4`, Developer Edition (64-bit)
- SQL tests used `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` and the guarded disposable `MyDmsVn_Test_<guid>` lifecycle.

Commands were sequential; two target frameworks were never tested concurrently.

```text
dotnet restore MyDmsVn.sln --locked-mode
Result: succeeded; all projects up to date.

dotnet build MyDmsVn.sln -c Release --no-restore -m:1
Result: succeeded; 0 warnings, 0 errors.

dotnet test tests/MyDmsVn.Architecture.Tests/... -c Release -f net8.0 --no-build --no-restore
Result: 5 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Application.Tests/... -c Release -f net48 --no-build --no-restore
Result: 120 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Application.Tests/... -c Release -f net8.0 --no-build --no-restore
Result: 120 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Desktop.Tests/... -c Release -f net48 --no-build --no-restore --blame-hang-timeout 60s
Result: 186 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Desktop.Tests/... -c Release -f net8.0-windows --no-build --no-restore --blame-hang-timeout 60s
Result: 186 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/... -c Release -f net48 --no-build --no-restore --blame-hang-timeout 120s
Result: 154 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/... -c Release -f net8.0 --no-build --no-restore --blame-hang-timeout 120s
Result: 154 passed, 0 failed, 0 skipped.
```

Total target-specific executions: 925 passed, 0 failed, 0 skipped.

Additional bounded host check after Release build:

```text
MyDmsVn.Desktop.App.exe --smoke-test with an explicitly selected missing runtime config file
Result: exit 2 within 10 seconds; no modal/hang.

MyDmsVn.Desktop.AppCore.exe --smoke-test with an explicitly selected missing runtime config file
Result: exit 2 within 10 seconds; no modal/hang.
```

The initial pre-change baseline had one `net48` catalog messenger test fail once with 181 passes; the focused test passed 3/3 immediately afterward. The final desktop suite passed 186/186 on each target, including the new overprivileged-runtime startup guard, so no unrelated flaky-test fix was included.

During the final SQL verification, the first `net48` run had one pre-migration SQL timeout; the affected test passed alone and the complete rerun passed 154/154. The first `net8.0` run had the same transient timeout plus one timing-ratio sample at `0.589` against a `0.600` lower bound; the affected filter passed 4/4 and the complete rerun passed 154/154. No production change was made for these unrelated transient failures.

## Evidence files

- `docs/CONFIGURATION_AND_SQL_SECURITY.md`
- `deployment/sql/Provision-MyDmsVnPrincipals.sql`
- `docs/SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md`
- configuration, desktop and SQL tests under `tests/`

## Open operational evidence and risks

- No clean packaged Windows VM was used. T01/T02 do not prove installer ACLs, packaged-file secret scanning or actual installed-host connectivity.
- Runtime least privilege was verified with SQL Server `EXECUTE AS USER` in disposable databases, not a separately provisioned production-style login from each workstation.
- Credential rotation/restart and certificate/firewall behavior were documented but not exercised against a clean configured machine.
- No prior production client release exists, so an actual two-client-version mixed rollout was not run. The current policy blocks every unapproved old/new combination and requires downtime.
- The interrupted-upgrade test applies the known 001–006 prefix, executes a deliberately failing transaction-per-script migration, confirms its artifact rolled back and the journal stayed at 6, blocks the client, then performs an explicit forward migration to 007. Process crash/kill and backup/restore drills remain P6-T06.
- Local Mode remains two-tier. A user or compromised client able to use runtime SQL credentials can bypass Application rules within those SQL grants.
- P5 authenticated interactive walkthroughs remain pending as already recorded; this work does not convert them to Pass.

Next detailed roadmap task: P6-T03 structured logs, correlation and production security audit. P6-T06 recovery evidence remains mandatory before deployment exercises or release sign-off.

