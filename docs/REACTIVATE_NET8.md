# Reactivate .NET 8 product targets

**Status:** required reviewed procedure; not authorized or executed by NET8-PAUSE. The current candidate keeps .NET 8 product/GUI coverage `PARKED / NOT RUN`. This is not a one-line TFM toggle.

## Entry conditions

- A product owner approves the release/runtime scope and confirms whether .NET 8 remains the intended P7/modern-desktop baseline or a separate framework plan supersedes it.
- Start from an accepted, clean revision with the current active `net48` Final evidence, retained historical logical-case IDs/rows, and all required Windows/.NET Framework/.NET 8/SQL resources available.
- Freeze the reactivation project matrix before editing. Do not infer product coverage from the tool-only `net8.0` runner.

## Restore the graph atomically

1. Restore `net8.0` to SharedKernel, Contracts, Domain, Application, Infrastructure, DbMigrator, Local adapter and applicable non-GUI tests; restore `net8.0-windows` to WinForms/Desktop tests.
2. Re-add `src/MyDmsVn.Desktop.AppCore/MyDmsVn.Desktop.AppCore.csproj` to `MyDmsVn.sln`, repair its references, and restore the modern desktop release package. Keep the legacy `net48` desktop and administrator-only migrator packages.
3. Regenerate lock files through a normal unlocked restore, review dependency differences, then prove `--locked-mode` restore. Do not remove the `net48` graph.
4. Change the reviewed manifest profile's active SQL and GUI arrays only after every declared project supports the restored TFM. Preserve historical logical IDs, theory-row locators and fingerprints; inventory growth requires reviewed additions, while unexplained shrinkage blocks the change.
5. Keep `-Frameworks` / `--frameworks` explicit. The approved order is `net48`, then `net8.0`; GUI uses `net48`, then `net8.0-windows`. Keep the global SQL and GUI leases and all approval/watchdog/ownership controls.

## Progressive verification

- Run focused unit/contract/build checks per changed project and framework. Include adapter parity, serialization/contracts, architecture boundaries, AppCore composition and release-manifest assertions.
- Run the bounded GUI launcher independently for the complete `net48` Desktop inventory and the restored modern GUI inventory. Require STA/global serialization, expanded discovery-to-TRX bijection, zero skips, modal/unhandled/DataError capture and clean process-tree shutdown.
- From one clean frozen revision, run the manifest-owned strict Final with one unfiltered sequential SQL pass on `net48` and `net8.0`, approved non-production SQL, source/discovery/TRX reconciliation, zero mandatory skips and owned-database cleanup.
- Build/package every restored application project/TFM and inspect both desktop packages plus administrator migrator output. Run the plan-specific P7/HTTP or modern-host acceptance that motivated reactivation.

## Acceptance and rollback

Record the exact revision, declared/active/historical matrix, commands, counts, package manifests and owner decision. Any missing targeting pack, SQL/GUI resource, stale inventory, failed restored TFM, cleanup ambiguity or package mismatch is `BLOCKED`, not a waiver. Until all restored gates pass, retain the pause release scope and do not advertise the modern host or P7 as supported. If reactivation is abandoned, revert the whole reviewed matrix/solution/package/manifest change together; never leave a partially restored graph.
