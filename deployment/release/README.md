# Local release matrix

**Current source:** `v1a-pro`, updated 2026-10-21. [Publish-LocalRelease.ps1](Publish-LocalRelease.ps1) emits these two active net48/x86 packages. RESP, L2DB and GUI-P5 are complete in their approved scopes. [BCF](../../docs/plans/20261021-001-business-compact-lookup-and-visual-parity.md) is COMPLETE / OWNER ACCEPTED in its [final bounded UI scope](../../docs/evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21); [production release prerequisites](../../docs/DEFERRED_WORK.md#release-prerequisites) remain **UNACCEPTED / UNSCHEDULED**.

| Directory | Executable | Target / architecture | Deployment |
|---|---|---|---|
| `desktop-net48-win-x86` | `MyDmsVn.Desktop.App.exe` | `net48` / x86 | Publish directory |
| `admin-migrator-net48-win-x86` | `MyDmsVn.Server.DbMigrator.exe` | `net48` / x86 | Administrator-only publish directory |

`MyDmsVn.Desktop.AppCore` / `net8.0-windows` is parked and must be reported `NOT RUN / NOT SHIPPED`, never PASS. `Publish-LocalRelease.ps1` has no modern package path while the pause is active. The .NET 8 SDK is a build dependency, not a shipped product target.

[global.json](../../global.json) declares SDK `8.0.400` with `rollForward: latestFeature` and no prerelease SDKs. Normal publishing requires a clean committed tree, a new/empty output root, locked restore and the Windows net48 targeting prerequisites. Record the SDK actually selected. Both target packages require .NET Framework 4.8; no .NET 8 product runtime package is currently emitted. There is no MSI, Onova updater or auto-update flow yet.

## Release metadata status and limitations

The script reads schemaSupport minimum/maximum and ordered requiredScripts from **SchemaCompatibilityPolicy.CurrentScripts in the shipped admin Infrastructure.dll**, currently schema9 / scripts001–009. A hidden bounded x86 Windows PowerShell helper handles the net48/x86 DLL even from a 64-bit publisher; missing/empty/unreadable policy rejects manifest generation. Four focused x86 packaging tests and clean-commit d8013030 artifact reconciliation passed: schema9, ordered001–009 and all224 file hashes. [L2DB verification](../../docs/L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed) closes RELEASE-MANIFEST-01 narrowly. Package metadata does not prove runtime startup or release acceptance.

The manifest also records an empty validated SQL-version list and unsigned-release status. These are unfinished release decisions/evidence, not implicit support or sign-off. `-SkipPublish` regenerates metadata for existing package directories; it is not a clean build or release-acceptance substitute.

## Operator and acceptance references

Use [LOCAL_RELEASE_PACKAGING](../../docs/LOCAL_RELEASE_PACKAGING.md) for the publish command, prerequisites, artifact handling and install procedure. Run the admin package from its own directory as `& .\MyDmsVn.Server.DbMigrator.exe`; use the [migration runbook](../../docs/SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) and [security administration runbook](../../docs/SECURITY_ADMINISTRATION_RUNBOOK.md) for protected configuration and maintenance commands.

Verification follows the sole [TEST_POLICY](../../docs/TEST_POLICY.md). [GUI-P5](../../docs/plans/20261019-001-full-winforms-gui-p5-acceptance.md) retains its completed, candidate-specific acceptance and explicit T05 Down NOT TESTED exception; BCI and BCF retain their separate candidate-specific acceptance. [Final BCF evidence](../../docs/evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) records the accepted UI scope and exceptions. Clean install, upgrade/recovery and production sign-off remain [unaccepted, unscheduled release prerequisites](../../docs/DEFERRED_WORK.md#release-prerequisites); a documentation refresh or successful publish does not satisfy them. Modern product targets remain [out of scope](../../docs/DEFERRED_WORK.md#out-of-scope-directions).

