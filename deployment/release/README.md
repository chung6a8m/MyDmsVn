# Local release matrix

NET8-PAUSE candidate output is intentionally limited to:

- `MyDmsVn.Desktop.App` — `net48`, `win-x86`, publish-directory package.
- `MyDmsVn.Server.DbMigrator` — `net48`, `win-x86`, administrator-only publish-directory package.

`MyDmsVn.Desktop.AppCore` / `net8.0-windows` is parked and must be reported `NOT RUN / NOT SHIPPED`, never PASS. `Publish-LocalRelease.ps1` has no modern package path while the pause is active. The tool-only `net8.0` test runner is not a release artifact.

Before publishing, run locked restore and explicit Release builds for the active matrix, inspect the generated manifest, and complete the plan's frozen Final plus bounded GUI D2. Missing SQL/GUI/packaging evidence blocks release. Restore the modern package only as part of the reviewed [net8 reactivation procedure](../../docs/REACTIVATE_NET8.md), including dual-target Final and modern GUI/package acceptance.
