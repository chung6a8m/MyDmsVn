# Local release matrix

**Current source:** `v1a`, 2026-10-18. [Publish-LocalRelease.ps1](Publish-LocalRelease.ps1) emits these two packages:

| Directory | Executable | Target / architecture | Deployment |
|---|---|---|---|
| `desktop-net48-win-x86` | `MyDmsVn.Desktop.App.exe` | `net48` / x86 | Publish directory |
| `admin-migrator-net48-win-x86` | `MyDmsVn.Server.DbMigrator.exe` | `net48` / x86 | Administrator-only publish directory |

`MyDmsVn.Desktop.AppCore` / `net8.0-windows` is parked and must be reported `NOT RUN / NOT SHIPPED`, never PASS. `Publish-LocalRelease.ps1` has no modern package path while the pause is active. The .NET 8 SDK is a build dependency, not a shipped product target.

[global.json](../../global.json) declares SDK `8.0.400` with `rollForward: latestFeature` and no prerelease SDKs. Normal publishing requires a clean committed tree, a new/empty output root, locked restore and the Windows net48 targeting prerequisites. Record the SDK actually selected. Both target packages require .NET Framework 4.8; no .NET 8 product runtime package is currently emitted. There is no MSI, Onova updater or auto-update flow yet.

## Open release metadata blocker

The script reads schemaSupport minimum/maximum and ordered requiredScripts from **SchemaCompatibilityPolicy.CurrentScripts in the shipped admin Infrastructure.dll**, currently schema9 / scripts001–009. A hidden bounded x86 Windows PowerShell helper handles the net48/x86 DLL even from a 64-bit publisher; missing/empty/unreadable policy rejects manifest generation. Focused x86 packaging tests pass; clean-commit artifact reconciliation is pending in [L2DB verification](../../docs/L2DB_VERIFICATION.md#l2db-t02-startup-follow-up--runtime-identity-and-schema-manifest) before RELEASE-MANIFEST-01 closes. Package metadata does not prove runtime startup or release acceptance.

The manifest also records an empty validated SQL-version list and unsigned-release status. These are unfinished release decisions/evidence, not implicit support or sign-off. `-SkipPublish` regenerates metadata for existing package directories; it is not a clean build or release-acceptance substitute.

## Operator and acceptance references

Use [LOCAL_RELEASE_PACKAGING](../../docs/LOCAL_RELEASE_PACKAGING.md) for the publish command, prerequisites, artifact handling and install procedure. Run the admin package from its own directory as `& .\MyDmsVn.Server.DbMigrator.exe`; use the [migration runbook](../../docs/SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) and [security administration runbook](../../docs/SECURITY_ADMINISTRATION_RUNBOOK.md) for protected configuration and maintenance commands.

Verification follows the sole [TEST_POLICY](../../docs/TEST_POLICY.md) and the explicitly required release scope in [DEFERRED_WORK](../../docs/DEFERRED_WORK.md). SEC-REBUILD acceptance is recorded in [HISTORY](../../docs/HISTORY.md); pending P5 interactive, P6 clean-install/upgrade/recovery and release sign-off remain open. A documentation refresh or successful publish does not satisfy them. Restore modern packages only through the reviewed [net8 reactivation procedure](../../docs/REACTIVATE_NET8.md).
