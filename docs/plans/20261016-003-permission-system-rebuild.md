# SEC-REBUILD — Permission System Replacement Implementation Plan

> **For agentic workers:** Use `superpowers:executing-plans` or `superpowers:subagent-driven-development` to implement this plan task by task. Checkboxes represent work still required, not evidence that it exists.

**Status:** Approved scope/order; NOT IMPLEMENTED; blocked on prerequisite implementation gates.  
**Date:** 2026-10-16  
**Goal:** Replace the unused MVP permission subsystem completely with a single configured superuser, code-defined permission catalog and deliberate operator-managed RBAC through UI/import.  
**Architecture:** One authorization path over current authenticated identity and a code registry. Database stores accounts, role/user assignments, the protected singleton superuser binding and audit, not a synchronized permission catalog. The obsolete subsystem is physically removed.  
**Tech stack:** Active product/test target `net48`; historical product `net8.0`/`net8.0-windows` parked; runner tool-only `net8.0`; WinForms, current Application/Contracts/Local adapters, SQL Server, DbUp, explicit UnitOfWork, RepoDb/Dapper and xUnit. No library/framework replacement.
**Spec:** [ADR 0007 — Code-defined permissions and a single configured superuser](../adr/0007-code-defined-permissions-and-single-superuser.md).

**Roadmap/TFM prerequisite (2026-10-16):** SEC-REBUILD comes after accepted NET8-PAUSE and implemented TESTGATE T04/T05. NET8-PAUSE PR #32 merged at `8e310778`; current `main` uses active SQL/WinForms `net48`, historical .NET 8 is PARKED/NOT RUN, and the runner is tool-only `net8.0`. T05 runtime/build/package/GUI/restoration gates pass at `6b0ce337`, with reviewer fixes re-verified at `6b2ff49`; the owner waived the duration comparison at `09d00bfa`, so it remains unmeasured. At SEC-REBUILD T00, bind to the then accepted manifest rather than old dual-target examples. Do not delete historical logical identities or treat parked results as PASS; P7 requires [reviewed reactivation](../REACTIVATE_NET8.md).

## 1. Execution order, authority and workspace boundary

The required order is:

```text
20261016-001-task-gates-and-modular-tests.md  (TESTGATE T01–T03, verified)
  -> 20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md (NET8-PAUSE)
  -> 20261016-002-modular-test-projects.md   (TESTGATE T04–T05)
  -> 20261016-003-permission-system-rebuild.md (SEC-REBUILD T00–T06)
```

Read [AGENTS](../../AGENTS.md), the spec, [TESTGATE T01–T03](20261016-001-task-gates-and-modular-tests.md), [TESTGATE T04–T05](20261016-002-modular-test-projects.md), [test policy](../CODEX_TEST_EXECUTION_POLICY.md), [gate matrix](../TEST_GATE_MATRIX.md) and [test strategy](../TEST_STRATEGY.md).

TESTGATE T01–T03 is verified; NET8-PAUSE T01–T05 runtime gates pass at `6b0ce337` and PR #32 merged at `8e310778`; its duration comparison was waived, not measured, at `09d00bfa`. Modular T04/T05 remain an unsatisfied prerequisite. This plan does not implement the split/security code. Do not move product tests before the prerequisite evidence is accepted.

The documents may merge before the prerequisite code. Product implementation starts only after T00 verifies the committed/reviewed prerequisite results. Rebase the implementation branch onto that revision, then bind this plan to the actual project/TFM/runner manifest. Missing prerequisites mean **BLOCKED**, not permission to return to the old monolithic test path.

Explicit owner decisions and this plan/spec supersede conflicting requirements to preserve/synchronize the old permission system. Scope the override narrowly: do not weaken session/authentication, password security, UnitOfWork, SQL safety or test gates. Update conflicting active guidance in T01 and finish the documentation sweep in T06. Do not falsify old test evidence or prerequisite completion.

## 2. Global constraints and acceptance model

- This is a **breaking replacement of an unused MVP**, not an upgrade adapter. Remove obsolete code, SQL objects, old commands, configuration and tests; no legacy/fallback/dual-path implementation, compatibility flag, old-data migration or retained SQL permission catalog.
- Reset disposable developer/test databases explicitly; no automatic drop/reset in Login, startup, normal migration or this docs PR. Refuse unsupported nonempty databases before any new baseline DDL. Never alter an unapproved database or erase the test harness ownership/approval controls.
- Keep the **actually declared, approved active** targets (today dual-target; after NET8-PAUSE only the verified scoped matrix) and preserve reactivation. Keep RepoDb `1.16.0`, RepoDb.SqlServer `1.16.1`, RepoDb.SqlServer.BulkOperations `1.16.2`, DbUp, Dapper and explicit UnitOfWork. Follow `.editorconfig`/`.gitattributes` (Markdown UTF-8; CRLF working-tree policy).
- The independent `PasswordHash`/`PasswordSalt` compatibility requirement in ADR 0003 is not permission-sync legacy and is not removed by this plan. No plaintext/default passwords, secrets in CLI arguments or privileged credentials in Desktop configuration.
- A valid current session and active account are always required. Superuser bypass concerns registered Active application permissions only; never validation, business invariants, data boundaries, SQL privilege or session expiry.
- One username-selected, database-bound superuser per application database; `Administrator` has no magic behavior. No user/role auto-grants, catalog activation, startup seeding or synchronization after adding a key.
- All role/user changes are explicit, authorized and audited. Future catalog-only releases on the **new** baseline leave assignment rows unchanged; no old-system migration fixture is required.
- Use local L1/L2 focused tests; one planned L3 acceptance invocation per code-bearing PR after edits freeze. No automatic full SQL/solution execution after every edit/task and no new CI rollout. Failure or later gate-affecting edits require a fresh applicable complete gate.
- Keep real SQL, **every approved active TFM** (currently both, after an accepted NET8-PAUSE scoped to its active matrix), approval, process-tree watchdog, independently reconciled source/discovery/TRX, zero mandatory skips, bounded execution and verified cleanup. Application permission approval removal does **not** remove `MYDMSVN_SQL_TEST_APPROVAL_FILE`.

### Review focus

1. A normal administrator changes the reserved account's password, activity, username or binding and becomes superuser: reject in every UI/import/Application/SQL path; maintenance-only lifecycle (T01/T03/T04).
2. A stale session, account rename/disable, config mismatch or concurrent bootstrap creates unintended bypass: revalidate the current subject, fail closed and preserve singleton/transaction guarantees (T01).
3. A new module/key is missing from the actual composed catalog even though a duplicate hand-written list passes: independent declaration/use-case/module coverage tests and offline export parity (T02).
4. A preview becomes stale, conflicts with direct Deny or silently deletes grants: atomic revalidation, revision/digest check and additive-only import (T03/T05).
5. Removing sync accidentally relaxes SQL principal safety or removes unrelated test coverage: actual least-privilege tests, removal-to-replacement case ledger and all-project final reconciliation (T00/T01/T06).

## 3. Source map and physical removal inventory

These are anchors verified at the remote planning revision, not a claim about uncommitted local paths. Re-resolve moved tests using T04/T05's mapping; source folder names below stay subject to actual prerequisite changes.

| Area | Existing anchors / required treatment |
|---|---|
| Registry | `src/MyDmsVn.Server.Application/Security/PermissionRegistry.cs`, `PermissionRegistryComposition.cs`, `PermissionKeys.cs`; simplify definitions, remove release/Administrator-grant metadata and divergent aggregate key list; preserve module definitions and canonical validation |
| Authorization | `src/MyDmsVn.Server.Application/Security/PermissionAuthorizationService.cs`, `AuthorizationAbstractions.cs`, `AuthorizationBehavior.cs`, `CheckPermissionQueryHandler.cs`; unify evaluation and subject validation; remove catalog-active snapshot field and checks |
| Identity/session | `src/MyDmsVn.Server.Infrastructure/Identity/SqlIdentityStore.cs`, Application Identity, `src/MyDmsVn.Desktop.Infrastructure.Local`; retain current Login/expiry/dispatcher safeguards, replace only obsolete authorization dependencies |
| Removed provisioning | `src/MyDmsVn.Server.DbMigrator/SecurityProvisioning/`; inventory and delete old synchronization engine, approval/plan/fingerprint DTOs, commands, Administrator bootstrap and grant policy implementation; shared secret input/password utilities may be extracted if still needed, not kept through an old compatibility facade |
| CLI dispatch | `src/MyDmsVn.Server.DbMigrator/Program.cs`; delete `permissions sync`, `permissions administrator-grant`, `security bootstrap-admin`; implement only the new documented command surface |
| SQL baseline | `src/MyDmsVn.Server.DbMigrator/Scripts/002_Identity.sql` and `008_Permission_Provisioning.sql`; remove SQL catalog and obsolete provisioning objects; delete the old 008 script, adjust the fresh security baseline/resources/journal expectations atomically; leave unrelated catalog/inventory SQL behavior intact |
| Runtime/startup | `src/MyDmsVn.Server.Infrastructure/Operations/SqlSchemaCompatibilityChecker.cs`, `DesktopDatabaseStartupCoordinator.cs`, `SqlRuntimePrincipalValidator.cs`; remove permission-release/sync gates; preserve schema, principal and startup protections |
| Deployment | `deployment/sql/Provision-MyDmsVnPrincipals.sql`, `deployment/release/Publish-LocalRelease.ps1`; replace old object/command references and package only new security assets; no broad identity DML or owner rights |
| Current documentation | `AGENTS.md`, `docs/SECURITY.md`, `docs/DATA_MODEL.md`, `docs/CONTRACTS.md`, `docs/CONFIGURATION_AND_SQL_SECURITY.md`, `docs/DEVELOPMENT_PLAN.md`, security runbook and affected release/startup guidance; replace obsolete instructions without claiming implementation passed before evidence |
| Tests/tooling | Actual post-T04/T05 Security/Migrations, Application/Architecture, Desktop pure/GUI projects and checked-in gate/source manifests; remove only obsolete behavior, retain independent safety tests and update classifications/ownership mappings |

**Mandatory removed concepts:** `PermissionSynchronizationEngine`, `PermissionSyncCommand`, `AdministratorGrantCommand`, old initial Administrator provisioning, `AdministratorGrantPolicy`, `PendingActivation`, activation `ReleaseId`/release manifest, `PermissionSnapshot.IsCatalogActive`, `Security.PermissionSynchronizationRequired`, managed-role ownership, approval/restore/revoke-drift machinery and all sole-use helpers/resources. Remove old CLI spellings rather than translating them to new behavior.

**Mandatory absent schema:** `dbo.Permissions`, `PermissionGrantApprovals`, `ManagedRolePermissionGrants`, `SystemManagedRoles`, `PermissionSynchronizationState`, and any additional sole-use objects found in the old 008 script. New fresh-schema tests must inspect actual `sys.objects` and grants; a grep alone is not enough. Do not keep 008 as a no-op or create old objects merely to drop them in a later migration.

**Proposed new homes:** `Server.Application/Security/SuperuserIdentityPolicy.cs`, `PermissionCatalogQueryHandler.cs`, `SecurityAdministration/`; `Server.Infrastructure/Security/SqlSecurityAdministrationStore.cs`; `Server.DbMigrator/SecurityAdministration/`; `Contracts/Security/`; `Desktop.Application/Security/`; `Desktop.WinForms/Features/Security/`, all under `src/MyDmsVn.*` using the existing project naming/layer conventions. These are new planned files/folders, not existing APIs. T00 records exact physical paths in the implementation manifest.

## 4. Binding contracts to implement

The complete behavior is in ADR 0007. The following names and boundaries connect tasks; fit DTO serialization to existing `ApiResult` conventions rather than introducing a new result framework.

| Contract | Required shape / responsibility | Owner |
|---|---|---|
| Superuser option | Required `Security:SuperuserUsername`; existing normalizer; no list, wildcard, fallback name or boolean bypass supplied by the client | T01 |
| Superuser binding | `dbo.SystemSecuritySettings`: singleton `Id=1`, `SuperuserUserId`, `BoundNormalizedUsername`; noncascading FK, maintenance-only writes; config + binding + current account must agree | T01 |
| Registry | `IPermissionRegistry.Definitions`, `TryGet(key, out definition)`, referenced-key validation; Key/Module/Description/Active-or-Deprecated; no required manual release ID | T01/T02 |
| Authorization | Existing `IPermissionAuthorizationService` entry points converge on current authenticated subject validation and one decision evaluator; noncurrent-user inspection is explicitly authorized, never implicit impersonation | T01 |
| Catalog query | `ListPermissionCatalogQuery` -> `PermissionCatalogDto { ApplicationVersion, CatalogDigest, Items }`; items contain canonical Key/Module/Description/Status; no grant/SQL dependency for assembling the catalog | T02 |
| Administration | Typed commands for ordinary users/roles, membership, role grants, and direct `Allow / Deny / Inherit`; actor always from session, targets explicitly identified; expected assignment revision on mutations | T03 |
| Assignment import | `PermissionAssignmentImportRequest { SchemaVersion=1, TargetType, TargetId, Entries }`; role entries Allow only; user entries Allow/Deny; same target-specific authorization as UI | T05 |
| Preview/apply | `PreviewPermissionAssignmentImportQuery` and `ApplyPermissionAssignmentImportCommand`; preview returns computed delta, target revision, catalog digest; apply recomputes under transaction and never trusts client delta | T05 |
| Audit | `dbo.SecurityAuditEvents`; transactional actor/target/operation/before-after/UTC/correlation, no secrets/raw payload; user/role aggregate assignment revision changes with writes | T03 |

New CLI names, implemented and tested before documented as executable: `security bootstrap-superuser`, `security recover-superuser`, `security rename-superuser`, and `permissions list --format json`. Bootstrap/recovery read passwords safely and use maintenance authority; list/export is offline and requires no database. Unknown old command names fail with a sanitized usage error and do not connect or mutate anything.

## 5. Tasks and review gates

### SEC-REBUILD-T00 — Verify prerequisites and bind real task gates

**Deliverable:** an implementation revision and reviewed gate manifest that can execute this plan without guessing project names or runner switches. This is a blocking planning/infrastructure binding step, not product implementation.

**Files:** actual gate manifest produced by TESTGATE-T03; post-T04/T05 project/logical-case mapping; create `docs/SEC_REBUILD_VERIFICATION.md` with all implementation gates initially NOT RUN.

- [ ] Verify T01–T03 and T04–T05 are committed/reviewed and their required local evidence is available. Record exact commits, real Task/Final entry points and the verified Desktop watchdog. Do not treat merged plan text or uncommitted local claims as completed gates.
- [ ] Resolve actual `.csproj` files, declared TFMs, test categories, module labels and source/discovery/TRX identity version. Bind aliases A/X/S/M/C/I/H/R/D/W below to real paths and actual runnable commands.
- [ ] Inventory every old security test by durable logical-case key per TFM. Classify `retain`, `replace` or `delete-obsolete-behavior`; record replacement/assertion rationale. Preserve old source/discovery evidence locally without maintaining old product behavior in a test compatibility project.
- [ ] Add SEC-REBUILD T01–T06 selectors and planned new test classes to the checked-in manifest. Expected-but-not-yet-written tests are NOT RUN; no empty-selection pass. Verify manifest parser rejects missing project/TFM/selector and duplicated ownership using focused existing runner tests.
- [ ] Publish sanitized prerequisite/path/command binding and commit this task's manifest/evidence changes. No unrelated full SQL or GUI baseline is rerun just to produce this mapping; existing evidence is usable only under the prerequisite exact-revision rules.

**Exit:** exact runnable per-task commands and bounded guards exist at the implementation revision; otherwise every dependent task remains BLOCKED.

### SEC-REBUILD-T01 — Atomic replacement of registry, authorization, bootstrap and schema

**Deliverable:** existing Login can use the one bound superuser on a fresh database; an ordinary user is governed by explicit RBAC; none of the removed runtime paths/tables is required or retained.

**Files:** source/removal anchors in section 3; new `SuperuserIdentityPolicy.cs`, `SecurityAdministration/BootstrapSuperuserCommand.cs`, recovery/rename commands and binding store; new/reworked fresh security SQL; current startup/DI/principal checks; Application `SuperuserAuthorizationTests`, `PermissionDecisionTests`; mapped SQL `SuperuserBootstrapTests`, `SecurityBaselineTests`, `SecurityPrincipalTests`; mapped pure session `SuperuserSessionTests`.

**Consumes:** T00 executable gates and actual test ownership. **Produces:** registry/identity/authorization/CLI contracts in section 4 and the new baseline used by all following tasks.

- [ ] Write failing tests: superuser with no grants can use every registered Active key; ordinary `Administrator` gets no new grant; direct Deny/Allow/inherit and inactive roles remain correct; unknown/deprecated key fails for everyone. Test each authorization entry point, missing/disabled/deleted accounts and fake DTO username/flag.
- [ ] Add failing identity tests: config missing/blank/normalization collision; mismatch among config/binding/current account; reserved-name creation/rename; same-name recreation; disabled superuser; normal administrator resetting/reactivating the protected account; concurrent bootstrap, rollback on partial failure, recovery/rename session invalidation and stale Login result. Noncurrent-user inspection without authority is denied.
- [ ] Add fresh SQL tests proving old tables are absent, singleton/FK/unique username constraints work, bootstrap creates no role/catalog/grants, runtime cannot alter binding, and migration/startup never seed or reset. An unsupported nonempty database is refused before modification; no repair/upgrade adapter.
- [ ] Run the reviewed focused A/S/M/D selectors to observe intended failures. Keep DB approval, watchdog and cleanup enabled even for expected test failure. Build the affected layers on their actual targets.
- [ ] Implement the new evaluator/subject lookup and bootstrap/recovery/rename; remove old sync code/DTOs/commands/config/resources. Remove both Application and SQL `IsCatalogActive` gates and startup release-sync requirement. Account/activity/binding reads must be current and consistent; no superuser cache from Login.
- [ ] Rework the fresh baseline instead of appending create-then-drop compatibility scripts; revise migration resources/journal/preflight/schema validation atomically. Reserve maintenance authority for bootstrap/recovery and forbid normal CRUD/import from changing **any protected-account credential, identity, activity or binding**. Ordinary account disable must still be enforced.
- [ ] Update current architecture/security/startup/bootstrap guidance and AGENTS' conflicting permission instructions. Keep normal Login/session/expiry semantics and all unrelated fixed decisions. Old binaries/databases are not supported at cutover; document explicit disposable reset, never silently perform it.
- [ ] Run T01 L2, check independent inventory/actual SQL principals, commit the coherent replacement. No partial new/old mode is a releasable intermediate state.

**Exit:** new superuser and ordinary RBAC flow are usable without sync on a genuinely fresh baseline; old implementation and schema are absent; focused security/migration/session gates pass.

### SEC-REBUILD-T02 — Complete permission catalog, offline export and coverage checks

**Deliverable:** every permission supported by the running application is discoverable regardless of assignments or SQL catalog state, with no developer-maintained release-sync step.

**Files:** registry composition/module definitions; new `PermissionCatalogQueryHandler.cs`, `src/MyDmsVn.Contracts/Security/PermissionCatalogDto.cs`; actual Desktop abstraction/Local adapter permission client; CLI list/export; A `PermissionCatalogTests`, X `PermissionRegistryCoverageTests`, D `PermissionCatalogClientTests`.

**Consumes:** T01 registry. **Produces:** catalog DTO/query/export and `Security.Permissions.Read` definition for UI and import validation.

- [ ] Write failing completeness tests independently enumerating module definitions, declared permission constants and permission-protected use cases; a newly added unregistered module/key or undeclared authorization literal must fail. Test case-only duplicates, trailing whitespace, stable ordering and canonical equality.
- [ ] Write query/export tests: no DB is opened to compose/export; every registered key is returned even with zero grants; endpoint/query authorization rejects unauthorized callers; offline CLI output is equal to actual runtime composition. Runtime options/version metadata do not become a second catalog.
- [ ] Run the focused tests, implement query/DTO/Local adapter and offline JSON export. Compute digest deterministically from definitions; no manual `ReleaseId` edit, fingerprint gate or persisted activation status.
- [ ] Support optional comparison with a prior export: Added/Removed/Changed are derived only from the supplied snapshots; Unassigned is separately derived from assignments. With no prior export, display the complete current catalog without inventing a New label. Export never includes credentials or user permission data.
- [ ] Run T02 L2 and commit. Ordinary users remain default-deny for a new key until explicit assignment; a test that adds a key to the code composition must demonstrate immediate superuser access without DB writes.

**Exit:** independent completeness tests catch missing definitions/composition, and catalog listing/export needs no synchronization.

### SEC-REBUILD-T03 — Deliberate administration and a narrow SQL write boundary

**Deliverable:** Application use cases can safely manage ordinary accounts, roles, memberships and permission assignments with transactional audit; no maintenance SQL credential is available to the Desktop runtime.

**Files:** new `Server.Application/Security/SecurityAdministration/`, `Server.Infrastructure/Security/SqlSecurityAdministrationStore.cs`, Contracts Security DTOs; fresh security SQL procedures/revision/audit objects; `deployment/sql/Provision-MyDmsVnPrincipals.sql`, `SqlRuntimePrincipalValidator.cs`; A `SecurityAdministrationTests`; S `SecurityAdministrationSqlTests`, `SecurityAdministrationPrincipalTests`; conditional SQL concurrency tests.

**Consumes:** T01 identity/authorization, T02 registry/catalog. **Produces:** typed read/mutation contracts, expected assignment revision, audit and reusable mutation validation for T04/T05.

- [ ] Write failing tests for role/user management permission enforcement, actor spoofing, missing/disabled targets, reserved account protection, canonical/unknown/deprecated keys, duplicate grants/memberships and direct Allow/Deny/Inherit precedence. A non-superuser with management permission must still be unable to reset/rename/reactivate the bound account.
- [ ] Write real SQL tests for allowlisted procedure execution, denied general DML/DDL/binding/maintenance access, valid business SQL access, transactional audit failure rollback, stale revision conflict and concurrent conflicting writes. Use runtime and maintenance principals separately; an owner-connection-only pass is insufficient.
- [ ] Run failing focused A/S tests. Implement ordinary user/role create/edit/active status, safe ordinary-user password set/reset, memberships, role Allow add/remove and direct Allow/Deny/Inherit. Inherit explicitly deletes that user's direct decision; removing a role grant does not modify direct-user decisions.
- [ ] Implement narrow parameterized procedures and Infrastructure calls through the explicit transaction/UoW boundary. Update affected aggregate revision and audit atomically, with expected-revision checks. Preserve canonical key comparison independent of SQL default collation. Recheck authorization at command execution, not only when opening a form.
- [ ] Keep generic runtime identity/RBAC writes denied and update principal provisioning/validation together. No blanket EXECUTE, db_owner, DDL or credential impersonation. Document Local's trusted-process limitation: actor parameters are not protection against a malicious holder of runtime SQL credentials; do not claim a server boundary that does not exist.
- [ ] Run T03 L2, including actual least-privilege positive/negative paths, and commit. Permission/activity changes affect the next protected request; add no persistent permission-list claim/cache.

**Exit:** all ordinary RBAC writes are explicit, authorized, revision-controlled and audited; protected superuser lifecycle remains maintenance-only.

### SEC-REBUILD-T04 — Usable Desktop permission administration

**Deliverable:** a user can manage ordinary users/roles/memberships and assign permissions through real WinForms UI; no direct Infrastructure/database dependencies enter ViewModels/forms.

**Files:** new `src/MyDmsVn.Desktop.Application/Security/` ViewModels, `src/MyDmsVn.Desktop.WinForms/Features/Security/` forms; actual Desktop abstractions/Local client/composition/navigation; D `SecurityAdministrationViewModelTests`, W `SecurityAdministrationFormTests`.

**Consumes:** T02 catalog and T03 administration contracts. **Produces:** catalog viewer and usable editing surfaces reused by import.

- [ ] Write failing pure tests for full catalog load/filter, target selection, expected-revision conflict, direct Allow/Deny/Inherit display, inactive role/account indicators, and effective-permission explanation distinguishing role grant/direct decision/superuser. Never infer superuser from role names.
- [ ] Write bounded serialized STA tests for navigation, form validation, safe password controls, cancellation/disposal, DataError/unhandled exception capture and no modal failure dialog. Add logout/expiry/relogin races: results from the previous session cannot populate or submit the new session's form.
- [ ] Run D/W through their reviewed gates; implement compact forms using existing UI patterns. Provide role/user create/edit/activation, memberships and assignments; protected account editing is disabled in UI and still rejected by Application. Show new-key comparison only with an actual prior snapshot; keep Unassigned distinct.
- [ ] Refresh relevant UI permission state after changes without treating UI gating as authorization. Every save uses Application and current session, shows conflict without silently overwriting and clears sensitive password input after use.
- [ ] Run T04 L2 and a focused interactive walkthrough in the supported Windows environment. Record actual GUI evidence or BLOCKED; no SQL suite is required for pure form changes beyond the final mandatory boundary.

**Exit:** fresh setup -> superuser Login -> create ordinary user/role -> assign -> ordinary Login is navigable; pure/GUI automated results and remaining interactive checks are explicit.

### SEC-REBUILD-T05 — Versioned JSON import with preview, conflict protection and audit

**Deliverable:** the operator can explicitly add assignments from a bounded JSON file through the same authorization/mutation boundary as UI; import is not renamed permission synchronization.

**Files:** Contracts import DTOs; new Application preview/apply handlers and bounded JSON parser; Local client; Desktop preview/import dialog; A `PermissionImportValidationTests`, S `PermissionImportSqlTests`, D/W import presentation tests.

**Consumes:** T02 registry/catalog digest, T03 authorized mutation/revisions/audit and T04 target selection/UI. **Produces:** ADR import contracts; no separate approval/state engine or background process.

- [ ] Write failing parser tests for unsupported/missing schema version/fields, extra actor fields, wrong target type/ID, unknown/deprecated/case-mismatched/whitespace keys, duplicate entries and conflicting decisions. Enforce 1 MiB UTF-8 and 5,000 entries before unbounded processing. Role decisions are Allow only; user decisions Allow/Deny; missing entries mean unchanged.
- [ ] Write preview/apply tests for unauthorized preview/apply, permission revoked between them, changed catalog digest, stale revision, removed/disabled target, existing direct Deny conflict, repeat import/no-op, partial write/audit failure rollback and malformed payload producing no writes. Apply never accepts a client-provided authoritative delta.
- [ ] Run focused failing A/S/D tests. Implement deterministic preview and atomic apply: reauthorize, revalidate original input, lock/read target revision, recompute delta, compare digest/revision, add only missing entries and audit. Identical decisions are no-ops; conflicting existing decisions reject the entire import. No implicit revoke, replace-all or role/permission auto-creation.
- [ ] Add a usable UI file selector/preview/errors/apply result. Actor comes from current session; target ID/type is resolved in the current database and prominently confirmed. Use cancellation/stale-session handling from T04. Publish a synthetic schema/example, not real unit assignment data.
- [ ] Run T05 L2 and commit; export/preview must leave assignment rows unchanged, and apply must prove only the reviewed additive delta changed.

**Exit:** both role and direct-user additive imports work; no synchronization workflow, data rewrite during upgrades or hidden privileged route has reappeared.

### SEC-REBUILD-T06 — Complete removal, current guidance and final acceptance

**Deliverable:** one supported design in source, fresh schema, operational guidance and executable test manifests; all final results refer to the final gate-affecting revision.

**Files:** source/removal map and any remaining callers/resources; `docs/SEC_REBUILD_VERIFICATION.md`; new `docs/SECURITY_ADMINISTRATION_RUNBOOK.md`; current architecture/security/contracts/data/release docs; affected test inventories and manifest.

- [ ] Reconcile the removal inventory against production source, SQL objects, DI, CLI help, settings, packaging and tests. Delete old sync plan/runbook instructions from active navigation and replace current guidance; do not keep operational fallback docs or executable dead branches. Preserve historical evidence honestly through explicit historical references/Git history, not assertions that obsolete tests validate new code.
- [ ] Replace obsolete tests with new invariant tests and complete the per-key `retain/replace/delete-obsolete-behavior` ledger. Keep ordinary authorization, session, SQL privilege, transaction, ownership and migration-safety coverage. Test removals may reduce counts, but require explicit independent review; never weaken expected inventory just to make the gate pass.
- [ ] Add the catalog-only release scenario using the **new baseline**: add a registered Active key to the composed application, prove it appears in catalog/export and is usable by superuser, deny an unassigned ordinary user, compare complete assignments before/after startup/export/migration, then deliberately grant and verify access. No old-system upgrade support is introduced.
- [ ] Verify current runbook covers explicit disposable reset, new bootstrap, Login, manual assignment, JSON import, protected-account recovery/rename, Local trust limitation and sanitized audit. Update roadmap status only to the level proven by evidence; do not alter prerequisite task evidence/status.
- [ ] Run missing focused L2 checks, freeze all gate-affecting inputs, then execute the **actual verified Final command** from T00 once as the planned L3 boundary. Include all required non-SQL/GUI Release gates and strict unfiltered **all SQL projects**, sequentially on **all actual approved active SQL TFMs** (currently `net48`/`net8.0`; after a verified NET8-PAUSE, `net48` only, until restored). No bare `dotnet test` substitute for the strict SQL gate.
- [ ] Perform and record the packaged interactive walkthrough: bootstrap and Login; full catalog; ordinary user/role creation and direct Deny; additive import; logout/expiry/relogin; disabled protected account and controlled recovery. Required manual checks not executed remain open/BLOCKED, not implied by unit tests.
- [ ] Independently reconcile source -> expanded discovery -> TRX by project and TFM; mandatory SQL skips = 0; check exits, watchdog, approval, ownership and cleanup. A failure or later gate-affecting edit invalidates affected L3 evidence; fix with focused tests and rerun the applicable complete gate. Publish only sanitized aggregates and actual command/selection rationale, not raw TRX, credentials, approval or ownership journals.

**Exit:** acceptance checklist in section 8 is supported by actual evidence; new guidance no longer instructs developers/operators to synchronize permissions.

## 6. Per-task L2 gate matrix

### Project/TFM bindings

Aliases are responsibilities, **not guessed new project names**. T00 must bind them to the real completed T04/T05 layout and commit the exact commands before product work.

| Alias | Real responsibility and required targets |
|---|---|
| A | Application unit/contract tests; current anchor `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj`; use every **actually declared active TFM** (`net48` after accepted pause; add `net8.0` only after restoration) |
| X | Architecture/registry completeness; current anchor `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj`; read actual `.csproj` targets, do not invent an unsupported net48 target |
| S / M | Post-T05 Security / Migration SQL projects; **active `net48`** while paused, approved dedicated/fresh DBs as behavior requires; restored `net8.0` runs only after explicit reactivation |
| C / I | Post-T05 Catalog / Inventory SQL projects for affected cross-module authorization/persistence regressions; **active `net48`** during pause, expanded only on verified restoration |
| H / R | SQL harness / runner tests when gate inputs change: H on **actual active targets** (normally `net48`); R on tool-only `net8.0` if compatible. Runner-only `net8.0` never counts as a second SQL test pass |
| D / W | Post-T04 pure Desktop session/VM vs WinForms projects; D/W **active `net48`** while paused (W with tested serialized whole-tree GUI watchdog); restore pure `net8.0` / WinForms `net8.0-windows` only after reviewed reactivation |

| Task | Focused L2 selection and affected dependencies | Real SQL / adversarial obligations | Runnable command binding and evidence |
|---|---|---|---|
| T00 | R manifest/parser/identity selection tests; H only if harness changes; static prerequisite/source/project map | None by default; no duplicate full SQL/GUI baseline | G0 + actual focused R/H command; record prerequisite SHAs and all command bindings; unavailable tools = BLOCKED |
| T01 | A `SuperuserAuthorizationTests`, `PermissionDecisionTests`; D `SuperuserSessionTests`; S `SuperuserBootstrapTests`, `SecurityPrincipalTests`; M `SecurityBaselineTests`; affected C/I authorization cases | `SecurityProvisioning`, `MigrationSchema`; `SqlConcurrency` only for real concurrent bootstrap; `FastSQL` for actual C/I changes. Identity spoof/disable/config mismatch, rollback, no old objects/grants, refusal before DDL, ordinary Deny, real principal checks | U + guarded Q separately for each reviewed module/category + D task gate; every actual TFM, nonempty case inventory, expected failures then PASS, watchdog/cleanup |
| T02 | A `PermissionCatalogTests`, X `PermissionRegistryCoverageTests`, D `PermissionCatalogClientTests` | None for pure catalog/composition/export; T01/T06 own real account/grant proof | U + X/D task commands; declared-key/use-case/module completeness checked independently; no SQL connection for export |
| T03 | A `SecurityAdministrationTests`; S `SecurityAdministrationSqlTests`, `SecurityAdministrationPrincipalTests`; targeted C/I only if shared stores/transactions affected | `SecurityProvisioning` and applicable `FastSQL`; `SqlConcurrency` for conflicting real writes; protected-account credential mutation, least privilege, stale revision, audit rollback | U + guarded Q; actual runtime/maintenance principal evidence, case reconciliation, no unexplained widening of DML |
| T04 | D `SecurityAdministrationViewModelTests`; W `SecurityAdministrationFormTests`; actual adapter/contract dependencies | None for pure UI; SQL only if adapter change exercises a new persistence path, then reviewed S category | D/W verified Task commands, actual TFM matrix, STA serialization, bounded tree termination/TRX, no modal failures; interactive check separately |
| T05 | A `PermissionImportValidationTests`; S `PermissionImportSqlTests`; D/W import presentation cases | `SecurityProvisioning`, `SqlConcurrency` when real preview/apply race is exercised; additivity, Deny conflict, revocation, stale digest/revision, size limits, rollback/no-op | U + guarded Q + D/W; rejected payloads write zero rows, accepted delta/audit reconciled on every **active SQL TFM** |
| T06 | Focused residual caller/schema/docs/removal/new-release regression tests; R/H if inventories/harness code actually changes | Actual applicable categories for missing L2 assertions only; global full SQL is L3, not another L2 loop | G0 + resolved targeted commands; then one planned verified Final invocation at frozen revision; independent all-project/TFM reconciliation |

### Executable command catalog and the unknown-local-tool boundary

Run from the repo root. The following existing static/unit commands do not pretend that the future module runner already exists.

**G0 — Documentation/source preflight (safe, no SQL):**

```powershell
git status --short
git rev-parse HEAD
git diff --check
git diff --name-only
Get-ChildItem tests -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName
```

**U — Current Application unit-test command pattern (verify the named new tests exist before running):**

```powershell
$project = 'tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj'
# T01 example; replace only with the task's source-reviewed exact class names.
$selector = 'FullyQualifiedName~SuperuserAuthorizationTests|FullyQualifiedName~PermissionDecisionTests'
$activeApplicationTfms = @('net48') # T00: resolve from reviewed actual project/TFM manifest after NET8-PAUSE; do not guess.
foreach ($tfm in $activeApplicationTfms) {
  dotnet test $project -c Release -f $tfm --filter $selector
  if ($LASTEXITCODE -ne 0) { throw "Application tests failed on $tfm" }
}
```

Nonzero exit, zero/unexpected cases, missing new tests/TFM or incomplete evidence is FAIL/BLOCKED. The T00 manifest must wrap execution in the actual verified timeout/evidence contract, not rely on a filter/exit alone. Build as needed; do not use `--no-build` on stale output. X/H/R use their existing `.csproj` and actual supported targets with reviewed selectors, recorded verbatim in T00.

**Q — Guarded SQL pattern, usable only after T00 verifies the post-T05 wrapper contract:**

```powershell
# Values come from T00's checked-in, source-reviewed task manifest, not user guesses.
# $sqlProject: the actual mapped module .csproj.
# $category: one category required by the matrix row.
if (-not (Test-Path $sqlProject)) { throw 'Resolved SQL project is missing.' }
if (-not $env:MYDMSVN_SQL_TEST_APPROVAL_FILE) { throw 'SQL approval is required.' }
$artifactRoot = '.sqlperf/sec-rebuild-l2-' + [guid]::NewGuid().ToString('N')
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Runner build failed.' }
dotnet build $sqlProject -c Release
if ($LASTEXITCODE -ne 0) { throw 'SQL test build failed.' }
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location) `
  -TestProject $sqlProject `
  -ArtifactRoot $artifactRoot `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category $category `
  -Frameworks @('net48') # After NET8-PAUSE only; T00 must bind actual approved active SQL targets
```

The wrapper/path/parameters above exist at the planning revision for the original project; **post-T05 multi-project ownership/source-inventory compatibility must be verified, not assumed**. When the completed T03/T05 orchestration supplies a different verified Task entry point, T00 records and uses its real invocation instead. Without that binding, Q is BLOCKED; do not point the old runner at new assemblies blindly or fall back to bare SQL `dotnet test`. Run categories/projects/TFMs sequentially with fresh artifact roots and the verified cross-process coordinator.

D/W and Final commands cannot truthfully be fixed from uncommitted local work. T00 must extract the real CLI from implemented source/help and commit exact invocations, manifest paths, watchdog budgets and expected logical-case selectors before their use. Until then they are **BLOCKED / NOT YET BOUND**, not fictional `Invoke-TestGate.ps1` switches. This is an explicit prerequisite, not deferred discretion to lower the gate.

For every invocation record task/revision, changed dependencies, actual project/TFM/selector/category, command, expected/discovered/executed/passed/failed/skipped cases, timeout/exit, approval and cleanup result, sanitized artifact summary and PASS/FAIL/BLOCKED/NOT RUN. Never publish raw credentials, test approval files, ownership journals or raw TRX. L3 includes all required SQL modules and all **actual approved active TFMs** without filters; benchmark warm-ups/repetitions are not a routine feature gate.

## 7. Suggested implementation PR boundaries

This PR is **documentation only**. Subsequent code PRs remain dependency ordered:

1. T00 + T01: coherent core/baseline/bootstrap replacement, no dual-mode cutover.
2. T02 + T03: complete catalog and deliberate administration contracts/SQL.
3. T04 + T05 + T06: usable Desktop management/import and final full-scope removal/acceptance.

Each code-bearing PR has focused L2 and its own applicable frozen-revision L3; do not postpone a required gate until PR 3. T06 adds whole-feature acceptance, not an exemption for earlier merges. Smaller reviewable commits inside each PR are encouraged. No old architecture remains as a fallback between PRs; absent later UI/import is explicitly unfinished functionality, not permission to retain old commands.

## 8. Final acceptance checklist

- [ ] Completed and verified prerequisite implementations precede SEC-REBUILD; local uncommitted work was not overwritten or treated as evidence.
- [ ] All sole-use synchronization/approval/managed Administrator source, old command names, settings and schema are removed, not disabled or retained as legacy.
- [ ] Fresh DbUp baseline creates only the new supported schema; explicit disposable reset documented; unsupported nonempty DB refused before mutation; no automatic destruction/seed.
- [ ] Exactly one configured username-bound superuser can bypass registered Active permission grants without any role/grant row; invalid/disabled/expired/spoofed subject never bypasses.
- [ ] Ordinary Administrator and all regular accounts remain explicit RBAC, direct Deny/Allow precedence and default-deny; no catalog-only release changes assignments.
- [ ] Independent completeness tests cover declared/referenced/module-composed keys; catalog/API/CLI export lists them without SQL synchronization.
- [ ] Ordinary users/roles/memberships and direct/role grants are manageable through UI; protected account lifecycle is inaccessible to normal managers.
- [ ] JSON preview/apply is bounded, additive, reauthorized, revision/digest checked, idempotent and atomic with audit; no silent grant removal or direct Deny overwrite.
- [ ] Runtime SQL authority is narrow, effective principal tests pass and Local's trusted-process limitation is documented; no maintenance credentials in Desktop.
- [ ] Current instructions/runbooks/packaging/test manifests describe only the new supported security design; removed tests have reviewed replacement/removal rationale.
- [ ] All relevant L2 and actual final all-project/**active-TFM** L3 gates pass at the final gate-affecting revision, with zero mandatory SQL skips and verified ownership/cleanup.
- [ ] Required packaged Windows interactive checks are recorded honestly; unresolved tests/manual work remain open rather than being declared complete.

**Evidence for this documentation PR:** source anchors and remote planning revision inspected; design/plan consistency and document links are checked separately before opening the PR. No implementation, database reset, build, SQL or GUI pass is claimed by creating these documents.
