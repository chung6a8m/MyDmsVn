# SEC-REBUILD verification and gate bindings

**Status (2026-10-17):** T00 and T01 accepted. T01 L2 gates pass: non-SQL Task 29/29; guarded SQL SecurityProvisioning 18/18, SqlConcurrency 2/2, MigrationSchema 60/60, all zero skips with cleanup evidence. T01 was code-reviewed and committed. T02 is next; T02–T06 product gates NOT RUN. The earlier abandoned-lease block was cleared by the user-provided fresh approved disposable instance; the preserved prior lease was not deleted or bypassed.

## Prerequisite evidence (reviewed in this workspace)

- TESTGATE T01–T03: verified context; NET8-PAUSE T01–T05 merged at `8e310778`; the duration comparison remains waived, not measured.
- TESTGATE T04: accepted at `7a1e0a397dd3eb24f5997dffee72d6c7bd367330`. Packaged GUI D2 at that revision reconciled 139 source methods and 140 executed cases, all passed, zero skips; watchdog and GUI lease clean. The strict SQL Final at the same frozen revision reconciled 223/223 across all six SQL projects with zero failures/skips and completed ownership cleanup.
- TESTGATE T05: accepted at `973109fe42ae1255ef0197c296a1cd0ae6abdec9`. Strict all-project Final reported 223/223 source/discovery/execution, all passed, zero failures/skips, watchdog and ownership clean. Runner adversarial suite passed 236/236 on tool-only `net8.0`. Sanitized report files and raw local evidence are available under the documented local temp artifact root `mdv-t05d-final-8a1efea3`; raw evidence remains local. SEC-REBUILD starts from the descendant code revision `c9d09e23cd08f54ab94fea73200fa80f49d5e2a9`; intervening commits are documentation-only.

These prerequisite results are historical evidence at their listed frozen revisions; no prerequisite suite was rerun for this mapping task. Current active targets are product/test `net48`, tool runner and runner tests `net8.0`. Application/GUI `net8.0` targets remain PARKED / NOT RUN.

A broader attempt including `TestGateRepositoryContractTests` failed in this local run: `Security.Tests|net48|FingerprintMismatch,IncompleteFingerprint` (52 discovered cases). The same failure reproduces with the unchanged accepted manifest from `973109f`, so it is not caused by SEC-REBUILD gate additions. It is not counted as T00 parser evidence and remains an open runner/source-inventory discrepancy to resolve before final L3; no pass is claimed for it.

## SEC-REBUILD implementation evidence

- T00 `Invoke-TestGate.ps1 -Mode Task -PlanId '20261016-003-permission-system-rebuild' -TaskId 'SEC-REBUILD-T00'`: 33 source/discovery/execution cases, 33 passed, zero failed/skipped, watchdog/ownership clean. The focused runner parser/identity selector also passed 33/33 on tool-only `net8.0` after the logical-case inventory update.
- T01 Task `Invoke-TestGate.ps1 -Mode Task -PlanId '20261016-003-permission-system-rebuild' -TaskId 'SEC-REBUILD-T01'`: Application 17/17, Security configuration/startup 6/6, maintenance command 2/2, Desktop session 4/4; zero failed/skipped, no timeout, clean watchdog/ownership. Current method inventory reconciles Application 107/107, Security 36/36, Migrations 70/70, and Desktop Session 92/92.
- Guarded SQL Task categories, sequentially on active `net48`: Security `SecurityProvisioning` 18/18, Security `SqlConcurrency` 2/2, Migrations `MigrationSchema` 60/60; all passed with zero skips and owned-database cleanup. The final MigrationSchema run includes refusal of the unsupported interrupted baseline before mutation, unknown-history classification for a missing middle journal entry, seven-script baseline expectations, and loss of the migration-lock session during schema preflight.
- During the lock-loss test, SQL `KILL` closed the migration connection. `DatabaseMigrationLock.Dispose()` then attempted `sp_releaseapplock` on that closed connection and masked the original SQL failure. The cleanup now releases only on an open connection; the guarded regression passed. Earlier MigrationSchema attempts (6 failures, then 1 failure) are repair evidence, not PASS; only the final 60/60 run counts.
- Focused builds succeeded for Migrations.Tests `net48` and SQL runner `net8.0` with zero warnings/errors. Other T01 layer builds are recorded in the implementation task artifacts. T02–T06, packaged GUI acceptance, and final unfiltered SQL/GUI L3 remain NOT RUN.

## Project and category map

| Alias | Project | TFM | Scope |
|---|---|---|---|
| A | `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj` | `net48` | Application authorization, registry, catalog and import rules |
| X | `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj` | `net48` | Registry/module/use-case completeness |
| S | `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` | `net48` | Security SQL; `SecurityProvisioning`, plus `SqlConcurrency` only for concurrent identity/admin mutations |
| M | `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj` | `net48` | Baseline/schema; `MigrationSchema` |
| C | `tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj` | `net48` | Catalog authorization regressions; `FastSQL` when a shared persistence path changes |
| I | `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj` | `net48` | Inventory authorization regressions; `FastSQL` when a shared persistence path changes |
| H | `tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj` | `net48` | Harness only if implementation changes it |
| R | `tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj` | `net8.0` | Manifest/source/discovery/TRX contracts; tool-only, not product or SQL coverage |
| D | `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` | `net48` | Pure ViewModel/session tests |
| W | `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` | `net48` | Serialized STA WinForms tests, guarded by the GUI watchdog for acceptance |

All SQL task runs use the existing `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` with one reviewed category, explicit active `-Frameworks @('net48')`, unique artifact root, current approval, process watchdog and owned-database cleanup. It does not support module+category selection; source-reviewed category inventory and zero mandatory skips remain required. No SQL gate is run for pure catalog composition/export.

## Task-to-gate bindings

| SEC task | Manifest-owned non-SQL Task gate | Guarded SQL category/project when required | GUI boundary |
|---|---|---|---|
| T00 | Runner `TestGateManifestTests`, `LogicalCaseIdentityTests` | None | None |
| T01 | A `SuperuserAuthorizationTests`, `PermissionAuthorizationTests`, `PermissionRegistryTests`; S `SuperuserConfigurationTests`, `DesktopDatabaseStartupCoordinatorTests`; M `SuperuserMaintenanceCommandTests`; D `DesktopSessionExpiryTests` | S `SecurityProvisioning`; M `MigrationSchema`; add S `SqlConcurrency` for concurrent bootstrap | None |
| T02 | A `PermissionCatalogTests`; X `PermissionRegistryCoverageTests`; D `PermissionCatalogClientTests` | None for code composition/export | None |
| T03 | A `SecurityAdministrationTests` | S `SecurityProvisioning`; `SqlConcurrency` for contested writes; C/I `FastSQL` only when their persistence boundary changes | None |
| T04 | D `SecurityAdministrationViewModelTests` | None for pure UI | W runs through the existing unfiltered GUI D2 watchdog once at T04 boundary; no focused GUI selector is supported by the current CLI |
| T05 | A `PermissionImportValidationTests`; D `PermissionImportViewModelTests` | S `SecurityProvisioning`; `SqlConcurrency` for preview/apply races | W import tests are included in the T04/T06 D2 watchdog run |
| T06 | Updated affected project gates and R `TestGateManifestTests` / `LogicalCaseIdentityTests` | Only missing focused categories; all SQL projects run unfiltered under Final | Packaged D2 walkthrough under GUI watchdog |

SQL categories are selected one at a time with `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1`; the current wrapper intentionally has no module-plus-category selector. Do not widen a task to the full SQL suite. The exact project paths and active TFMs are in the project map above.

## Exact entry points

- Application / architecture / pure Desktop task gates: `tests/tools/Invoke-TestGate.ps1 -Mode Task -PlanId '20261016-003-permission-system-rebuild' -TaskId 'SEC-REBUILD-T0N' -ArtifactRoot '<unique-local-artifact-root>'`. Exact selectors are in `tests/test-gates.v1.json`. No selector may pass with zero discovered source cases; planned test classes remain NOT RUN until added and executed.
- Focused SQL: build `tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj` and the selected SQL project in Release, then invoke `Invoke-FocusedSqlIntegrationTests.ps1` with the mapped project/category, approval file and `@('net48')`. Resource preflight denial is BLOCKED, never PASS.
- Packaged GUI acceptance: build runner and Desktop tests in Release, then `tests/tools/Invoke-GuiTestAcceptance.ps1 -RepositoryRoot '<repo>' -TestProject 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj' -ArtifactRoot '<unique-local-artifact-root>' -Revision '<exact-clean-40-hex-HEAD>' -TimeoutSeconds 900`. This is unfiltered D2, serialized, and only run at a planned GUI boundary; do not run it after each edit.
- Planned final L3: `Invoke-TestGate.ps1 -Mode Final -PlanId '20261016-003-permission-system-rebuild' -TaskId 'SEC-REBUILD-T06' -ArtifactRoot '<unique-local-artifact-root>' -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE`. It is not executable as an acceptance gate until T06 adds all-project Final bindings and all implementation test inventories are complete. Do not substitute bare `dotnet test`.

## Old security test inventory policy

The pre-cutover security suite is owned by Application, Security SQL and Migrations projects, with additional authorization callers in Catalog/Inventory. The reviewed source/expanded-row inventory is captured in [the test replacement ledger](SEC_REBUILD_TEST_REPLACEMENT_LEDGER.csv): 152 TFM-specific rows across retained, replaced, and obsolete behavior, including parked SQL `net8.0` identities. The decision is behavior-based:

- `retain`: login/session/account-active checks, password hashing and legacy password verification, current-subject authorization, direct Allow/Deny/default-deny precedence, SQL principal least-privilege and unrelated business SQL access.
- `replace`: registry lifecycle/coverage and authorization tests that currently assert catalog activation; administrator provisioning tests; sync/import-style approval and grant management tests; schema tests that assert obsolete permissions/catalog objects.
- `delete-obsolete-behavior`: sync release approvals, managed Administrator grant ownership/repair, synchronization state, automatic Administrator bootstrap, and old catalog grant workflows. Each deletion must have a linked replacement assertion or a stated superseded behavior key before T06 completes.

Logical identity is `(active TFM, normalized fully qualified method signature, stable Fact/Theory row key, reviewed classification)`. The active source is `net48`; no parked TFM result is inferred. Expanded Theory rows and code fingerprints are recorded only from actual discovery/source evidence, never guessed from display names.

## Implementation gates

The T00 manifest gate ran `TestGateManifestTests` and `LogicalCaseIdentityTests`: source/discovery/execution 33/33, 33 passed, 0 failed/skipped, no timeout, watchdog and ownership clean. The focused direct runner selection also passed 33/33. T01's manifest Task gate was rerun on 2026-10-17 and reconciled 29/29 source/discovery/execution cases across Application (17), security configuration/startup (6), maintenance command (2) and session (4); all passed, zero failed/skipped, timeout false, watchdog/ownership clean. The superuser bypass assertion iterates every Active definition in the composed runtime registry. Sanitized reports are local under `%TEMP%\\sec-rebuild-resume-t01-task`. Inventory reconciliation removed 16 retired Migrations test methods and their 13 associated fingerprint exemptions from the active manifest; historical behaviors remain in the 152-row replacement ledger. The former approved instance's abandoned `legacy/focused-run/SecurityProvisioning` lease was not removed or bypassed. After the owner supplied a fresh approved disposable instance, the guarded SQL categories completed on `net48`: SecurityProvisioning 18/18, SqlConcurrency 2/2, and MigrationSchema 60/60, each with zero skips and owned-database cleanup. The session was isolated through the approved wrapper and the former lease was left untouched. Planned test class selectors are manifest-owned and intentionally do not authorize an empty-selection pass. For each code task, update logical-case ownership from actual test source and discovered Fact/Theory rows before running the task gate. SQL and GUI evidence remain separate from non-SQL Task gates.
