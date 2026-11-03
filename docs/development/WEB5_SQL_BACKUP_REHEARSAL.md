# WEB5 disposable SQL backup rehearsal

This **test-only, opt-in** fixture rehearses COPY_ONLY backup and restoration of a fresh owned schema13 database on a local, explicitly selected nonproduction SQL instance. It does not deploy a host, restore a production database, rehearse application recovery or establish service RPO/RTO. The [dated evidence](../evidence/web5/20261103-b05-iis-express-wip.md) records the actual bounded result and remaining gates. Run only after coordinating SQL ownership; never run concurrently with the attended or browser fixture.

## Setup and execution

Use the locked backend test build and a nonproduction master connection in process-local `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`. Set `MYDMSVN_WEB5_BACKUP_TEST_ENABLE=1` and set `MYDMSVN_WEB5_BACKUP_TEST_DIRECTORY` to an existing absolute local parent directory. Its ACL must be protected from inheritance, owned by the current Windows user, and allow only that user and the SQL Server service identity; no ancestor may be a reparse point. The SQL service must be able to traverse and write this location. The fixture verifies that SQL runs on this machine and discovers its service account; insufficient metadata access, filesystem permissions or unknown identity fails closed. Do not log or commit the connection string.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Web5BackupRestoreTests.Copy_only_backup_restores_owned_exact13_clone --logger 'trx;LogFileName=web5-backup.trx' --results-directory TestResults/web5-backup
```

Without explicit opt-in and the required settings the selector is skipped; a skipped/zero execution is NOT RUN. Verify TRX counters and the numeric `WEB5_BACKUP_RESTORE` record, not only the command exit code. No credentials or detailed private diagnostics should be copied into public evidence.

## Ownership, verification and quarantine

The supervisor creates a private randomly named leaf, creates a new empty backup device exclusively, and rechecks its identity and ACL before `BACKUP ... FORMAT, INIT, COPY_ONLY, CHECKSUM`. It creates a fresh ownership-tracked source via `SqlTestDatabase` and migrates it to exact13. It verifies the backup checksum, permits exactly one data and one log file, and restores to a new randomized clone and new paths without `REPLACE`. It checks the restored schema fingerprint, original epoch and test marker. The timings cover backup and SQL restore/read verification only.

A private quarantine manifest records the known source, clone, server identity and file paths as the run advances; it contains no passwords or connection strings. On success the supervisor verifies the clone instance, database identity, marker and absence of sessions before drop, uses the guarded source disposal, rechecks database absence, and deletes the exact backup only after rechecking its identity and bytes. It enumerates the file directories to check the owned backup, data and log paths are absent. A changed identity, incomplete operation, inaccessible evidence or uncertain drain must fail and retain resources rather than bulk-delete by prefix. If cleanup and manifest writing both fail, the local error contains exact source/clone/manifest identifiers for an operator; keep that diagnostic private and inspect the SQL instance before acting. Do not force-drop or delete resources from a name prefix.

An independent source review identified no additional P1/P2 after corrections, but did not execute the fixture or issue a formal approval. The observed run and SQL-free tests are reported separately in evidence. The fixture is not a substitute for backup retention, monitoring, restore-to-service, workload measurements or Owner-approved production thresholds.
