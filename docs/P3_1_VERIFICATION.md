# P3.1 verification — Initial security provisioning and permission synchronization

Date: 2026-10-14
Scope: P3.1 only. This evidence does not claim P4.1 interactive WinForms Login/session, authenticated P5 walkthroughs or remaining P6 VM/operational gates are complete.

## Verified behavior

- A versioned, module-owned permission registry validates exact keys, ownership, lifecycle, catalog activation and independent Administrator grant policy.
- Additive schema `008_Permission_Provisioning.sql` preserves historical assignments, records them as non-authorizing drift and adds catalog, approval, managed-grant, audit and durable bootstrap state.
- The privileged sync CLI produces a deterministic target/state-bound plan, requires the reviewed plan ID on apply, serializes competing operators and rejects stale state without partial writes.
- The system-managed `Administrator` role receives only active, eligible and independently approved grants. Manual grants stay manual; direct user deny wins; revoked managed grants retain tombstones and require an explicit audited restore.
- First-administrator enrollment uses the existing password policy and BCrypt hasher, reads the password twice without echo, rejects secret-bearing arguments and redirected interactive input, and commits user, membership and durable completion marker atomically.
- Fresh-install, preexisting-P3 upgrade, v1→v2→v3 permission lifecycle, historical adoption, collision, drift, concurrency, rollback, repeat/no-op, marker persistence and Application Login/authorization paths run against guarded disposable SQL databases.
- Release packaging declares exact schema 8 and keeps migration/provisioning binaries and SQL scripts out of both desktop packages.

## Commands and results

From the isolated P3.1 worktree:

```text
dotnet restore MyDmsVn.sln --locked-mode --nologo -p:RestoreDisableParallel=true
Result: succeeded; all projects up to date.

dotnet build MyDmsVn.sln -c Release --no-restore
Result: succeeded; 0 warnings, 0 errors.

dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --no-build --no-restore
Result: 130 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net48 --no-build --no-restore
Result: 184 passed, 0 failed, 0 skipped; 7m51s.

dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore
Result: 200 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net8.0 --no-build --no-restore
Result: 5 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net8.0 --no-build --no-restore
Result: 130 passed, 0 failed, 0 skipped.

dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net8.0 --no-build --no-restore
Result: 184 passed, 0 failed, 0 skipped; 6m49s.

dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net8.0-windows --no-build --no-restore
Result: 200 passed, 0 failed, 0 skipped.

.\deployment\release\Publish-LocalRelease.ps1 -Version 1.0.0-p31 -OutputRoot <disposable-output>
Result: succeeded; manifest minimum/maximum schema 8; net48 desktop 141 files, net8 desktop 177 files, admin migrator 76 files; neither desktop package contains DbMigrator or SQL scripts.
```

The final full suites ran sequentially by TFM. SQL integration test collections are also serialized because each collection creates, migrates and removes isolated databases; parallel execution overloaded the local SQL Server and produced unrelated command timeouts. The configured `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` was used only through the guarded disposable-database harness.

## Review findings closed

- Expanded the sync fingerprint to all catalog, approval, role, assignment, managed-grant and drift state that can affect a plan.
- Added actual CLI routing for sync, Administrator revoke/restore and bootstrap rather than documenting internal APIs only.
- Fixed non-authorizing legacy adoption so the matching drift record becomes `Adopted` only after reviewed activation.
- Preserved a durable revoked state when an out-of-band assignment deletion is detected, and rejected out-of-band resurrection.
- Added bootstrap fault injection, concurrent enrollment and subprocess no-echo/no-hang coverage.
- Corrected release-package compatibility metadata from schema 7 to schema 8.
- Serialized SQL integration collections after full-suite review exposed local SQL overload under parallel database migration.

## Remaining roadmap work

P4.1 must add the shared Login form/session lifecycle and authenticated dual-host STA/package walkthroughs. P5 interactive acceptance and remaining P6 VM/backup/recovery operational evidence stay open; P3.1 does not claim those gates.
