# SEC-SIMPLE — Single configured superuser and customer-managed permissions

**Status:** Proposed / NOT IMPLEMENTED · **Date:** 2026-10-16 · **Scope:** application security architecture, backward-compatible SQL authorization, source permission catalog, protected administration. **Implementation order:** start **only after verified completion** of [TESTGATE T04/T05 modular test projects](20261016-002-modular-test-projects.md). This document is a plan, not evidence of implementation, acceptance or tests.

## 1. Purpose, authority and scheduling

MyDmsVn currently requires an operator-approved permission sync on every release and can block startup if `PermissionSynchronizationState.LastAppliedReleaseId` is stale. This is burdensome during feature development and risks treating customer-owned role grants as release-owned. Proposed target: **Code-defined permission keys + SQL-owned RBAC grants + exactly one configured application superuser bypass**.

- Read `AGENTS.md`, [proposed ADR 0007](../adr/0007-code-defined-permissions-and-single-superuser.md), [P3.1 historical plan](20261013-001-initial-security-provisioning-permission-sync.md), [security baseline](../SECURITY.md), [provisioning runbook](../SECURITY_PROVISIONING_RUNBOOK.md), [test execution policy](../CODEX_TEST_EXECUTION_POLICY.md), [gate matrix](../TEST_GATE_MATRIX.md), [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md) and [proposed ADR 0006](../adr/0006-task-scoped-test-execution-and-module-projects.md). ADR 0007 must receive explicit review/acceptance before code implementation, following the ADR governance rules.
- **Hard start gate:** TESTGATE-T02/T03 from [20261016-001](20261016-001-task-gates-and-modular-tests.md) AND TESTGATE-T04/T05 from [20261016-002](20261016-002-modular-test-projects.md) must be implemented, independently reviewed and have accepted L2/L3 evidence for the required project×TFM coverage. A merged **plan/document** is not proof of these gates. Before T04/T05 conclude, SEC-SIMPLE tasks remain `BLOCKED`; do not code around unfinished test-project moves or assume proposed paths/scripts exist.
- The user's local in-progress changes to `20261016-001-task-gates-and-modular-tests.md` are not modified by this documentation plan. On implementation kickoff, re-read the actual committed scripts, target frameworks, category ownership and test-gate manifest after T05 and update **only this prospective plan** as needed; never silently resurrect old assembly names.
- No CI rollout; local gates only under TESTGATE policy. Do not rerun completed SQLPERF measurements for routine security feature changes. SQL DDL/tests only on explicitly approved non-production instances with complete resource/watchdog/ownership guarantees.
- **Historical immutability:** retain completed P3.1 tasks/evidence, `002_Identity.sql`, `008_Permission_Provisioning.sql`, existing DBA audit, migrations and current user/role/grant data. New schema changes use ordered, additive DbUp scripts. Do not rename/delete business permissions without a separately approved mapping/ADR.

## 2. Verified baseline/code surfaces (at plan creation; recheck at implementation revision)

| Concern | Existing anchor | Required change |
|---|---|---|
| Source registry | `Server.Application/Security/PermissionRegistry.cs`, `PermissionRegistryComposition.cs`, module `*PermissionDefinitions` | Keep authoritative stable definitions and completeness checks; simplify lifecycle/manifest workflow only with matching tests |
| Runtime decision | `PermissionAuthorizationService.cs`, `AuthorizationBehavior.cs` | Verified single-superuser bypass after authentication and active declared-key check; ordinary direct-deny→role→deny retained |
| DB grants | `Server.Infrastructure/Identity/SqlIdentityStore.cs` | Preserve user/direct/role snapshot and active-role semantics; remove **universal** `dbo.Permissions.CatalogStatus` gate only after dormant legacy risk is quarantined |
| Startup veto | `Operations/SqlSchemaCompatibilityChecker.cs`, `DesktopDatabaseStartupCoordinator.cs` | Preserve schema/privilege preflight, remove per-release `LastAppliedReleaseId` veto without relaxing schema-version checks |
| Existing CLI | `Server.DbMigrator/SecurityProvisioning/*`, `Program.cs` | Replace routine sync/bootstrap dependency with controlled superuser bind/recovery and legacy review; retain audited legacy commands only as explicitly scoped compatibility paths until retired |
| Existing schema | `Scripts/002_Identity.sql`, `008_Permission_Provisioning.sql` | Additive migration for stable binding and dormant-grant quarantine/review, if needed; preserve historical grants/managed tombstones and audit |
| SQL least privilege | `SqlRuntimePrincipalValidator.cs`, `deployment/sql/Provision-MyDmsVnPrincipals.sql` | Never give desktop runtime unrestricted INSERT/UPDATE on RBAC tables or deploy privileged SQL credentials to clients |
| Local identity | `LocalDesktopSession.cs`, Login, application `CurrentUser`, `UsernameNormalizer` | Pin identity to verified UserId/DB; honor request/session revalidation and expiry; client-provided username/flags cannot authorize |
| Test partitioning | [TESTGATE T04/T05](20261016-002-modular-test-projects.md) | Use **actual** post-T05 security/migration SQL projects and pure/GUI Desktop projects with accepted gate manifest; no speculative commands |

Current DB contains `dbo.Permissions`, `PermissionSynchronizationState`, `SystemManagedRoles`, `PermissionGrantApprovals`, `ManagedRolePermissionGrants`, `PermissionLegacyAssignmentDrift`, `SecurityBootstrapState` and `PermissionSecurityAudit`. Those tables are **not** to be dropped during the behavior cutover.

## 3. Architectural contract and invariants

### 3.1. Identity and bypass

- Exactly **one** superuser bypass principal **per installation/application database**, selected by a protected configurable `SuperuserUsername` (example key `Security:SuperuserUsername` is proposed, not existing). It is a *named user* (not an RBAC role, not SQL `sysadmin`, not a client/UI `IsSuperuser` claim). No shipped default username, password, recovery password, wildcard key or environment/command-line password.
- Normalize via existing `UsernameNormalizer` and reconcile with SQL's `NormalizedUsername` uniqueness/collation; reject ambiguity/mismatch, unsafe normalization, nonexistent user, disabled account and username collisions. Prefer operator-controlled **binding** of normalized username to immutable `UserId` and application `DatabaseIdentity` in protected SQL metadata. Both configuration and binding must match at every privileged decision. Binding is created/changed using a separately privileged, audited, database-target-confirmed maintenance operation with protected configuration; do not silently bind a new account just because its username matches.
- Disable self-service rename/delete/recreate of the bound identity; a deleted/recreated same-name user has a different `UserId` and must never inherit bypass. Loss/rotation of identity requires separately authorized recovery with backup/evidence and audit. Multiple hosts sharing one DB cannot each nominate a different principal; mismatched host settings fail closed.
- Validate authenticated, currently **active** account and exact authenticated actor binding on the trusted authorization path. Existing `IsAllowedAsync(int userId,...)` must never apply the caller's superuser status to a different supplied userId; check the target identity explicitly. A mutable `CurrentUser.Username` string or client flag alone must not be an authorization proof. Re-evaluate active/revoked account as required by the live request/session gate, not solely by an old login DTO.
- Sequence: (1) authenticated+active verified identity; (2) permission is declared and `Active` in running `IPermissionRegistry` and not quarantined; (3) if bound superuser, **allow** ignoring direct `UserPermissions` and role grants; (4) otherwise exact-key `UserPermissions` direct allow/deny (deny wins), then active-role grants, else deny. Unknown/deprecated/key typo denies **all**, including superuser. Quarantine denial must precede the superuser bypass for historical dormant keys unless an explicitly reviewed adoption approves them; no superuser exception is allowed to accidentally resurrect a formerly inaccessible sensitive feature.
- Authorization bypass does **not** bypass authentication, business validations, tenant/business boundaries if introduced, optimistic concurrency, posting locks, audit, SQL privilege restrictions, migration/schema checks, or feature deactivation. Audit superuser privileged operations and sensitive identity changes without credentials/connection strings. Do not add an `Administrator`-name or role-membership special-case.

### 3.2. SQL RBAC and dormant legacy grants

- For non-superusers, current user/role/direct-deny data is customer-owned. Normal release upgrades: **no UPDATE/INSERT/DELETE to** `Users`, `Roles`, `UserRoles`, `RolePermissions`, `UserPermissions` as a permission-sync side effect; no auto-grant on new key (including role `Administrator`), no regrant of a revoked managed row. Customer/user-initiated audited UI/import writes are distinct, explicit administration actions.
- **Critical cutover risk:** existing role/direct-allow rows may reference `Unregistered`, `PendingActivation`, `Deprecated` or SQL-missing keys and are currently suppressed by the SQL catalog gate. Removing that gate could turn a dormant legacy assignment into an effective grant as soon as source code introduces an `Active` key with the same name.
- Before removing the SQL gate, introduce a one-time, additive, conservative **legacy compatibility quarantine** for all previously ineffective historic assignment keys (including absent catalog rows) with key, observed reason, audit metadata and review disposition. Keep original assignments byte-for-byte; no silent adoption, deletion or automatic new grant. Auth must deny quarantined keys independently of source-active state until an **explicit, separately authorized, audited** operator adoption/remediation; reject ambiguous or inconsistent state. A key never historically assigned needs no SQL registration or activation and must be available to superuser once declared Active.
- Preserve old managed role ownership/revocation records and avoid turning formerly `Revoked` records into grants during conversion. Inventory all baseline grants, including the previous `Administrator`, before/after. If a safe binding/quarantine/read boundary cannot be demonstrated for an installation, **block cutover/upgrade** rather than opening a privilege gap.
- Retain `dbo.Permissions` and old sync tables as compatibility/history in this change; optionally mark retired in documentation, not destructive DDL. Startup removes **release sync veto only**, not schema validity/SQL least-privilege validation. Analyze old/new binary-client compatibility: no unsupported rolling mixed-version operation; enforce known schema version gates and staged/reversible deployment guidance.

### 3.3. Permission catalog, review and customer grant workflow

- `IPermissionRegistry.Definitions` from running code is the sole supported-key source. Keep module and description, source lifecycle `Active/Deprecated` (keep pending only if an explicit product need remains), case-sensitive exact keys, duplicate detection, reference validation, rename/deprecation reviews and tests. Simplify release ID/fingerprint: no manual per-key sync release workflow; if producing a manifest fingerprint, compute it reproducibly and use it for **display/diff**, not SQL sync gating.
- `List All Permission Keys` returns every declared definition, including non-active/deprecated entries, independent of SQL `dbo.Permissions`. A protected API/CLI export supports JSON/CSV (safe deterministic order and escaping); UI must expose exact Key, Module, Description, Lifecycle and practical grants summary. Read-only viewing must never itself change grants or legacy review status.
- Differentiate `Registered`, `Unassigned`, `Deprecated`, `Unknown/Legacy-in-DB`, `Quarantined`, and `New-since-reviewed-manifest`. The `New` badge is only reliable when an explicit recorded prior manifest snapshot exists; otherwise show `Comparison unavailable` rather than mislabeling every key as new. The reviewed manifest snapshot/acknowledgment is **informational only**: never enables a permission or grants a role.
- Import exact permission keys for a chosen user or role via reviewed preview/confirm, idempotent re-import, row-level validation/error reporting, optimistic concurrency/recheck at apply, transaction, actor/time/source audit and **additive-only by default**; removal/deny is an explicit separate operation. Unknown/deprecated/quarantined keys cannot be granted into active use without the appropriate security review, and no direct deny may be silently overwritten by a role import.
- **Trusted administration boundary:** Local v1 desktop connects directly to SQL; app-level UI permission checks alone cannot protect privileged SQL writes from a modified client. No broad `db_owner`/`db_datawriter` or general runtime DML on RBAC tables. Prefer a trusted admin API/service; until one is actually available and security-reviewed, ship catalog UI/preview/export **read-only** and an operator-only CLI/import using protected maintenance credentials. Making direct client write UI work by loosening SQL permissions is forbidden. For sensitive grants (`Security.*`, superuser binding), require stronger operator-only controls; ordinary role administrators must not self-escalate by changing their own effective grants.
- Catalog changes and user/role changes must invalidate affected UI lookup/cache state and permission checks (including desktop session cache) on the proper trusted boundary; no granting merely by hiding/enabling a button.

## 4. Tasks and reviewable implementation PR slices

### SEC-SIMPLE-T01 — Upgrade inventory, compatibility guard and reversible cutover design

- [ ] After T05, record actual source/test project×TFM inventory, privilege model, schema versions and existing grant/sync/bootstrap state from a **sanitized disposable representative** database. Capture before/after equality expectations without publishing customer records, identifiers or secrets.
- [ ] Add tests and an additive DbUp migration/guard for dormant assignments, including old `Unregistered`, `PendingActivation`, `Deprecated` and absent catalog rows; preserve all user/role/direct decision rows, manual Administrator grants, managed tombstones and audit. Define the quarantine's explicit reviewed-adoption operation and protected SQL read/maintenance privileges, including rollback/recovery after partial failure.
- [ ] Demonstrate legacy grant key later declared `Active` still denies until approved, and ordinary keys not formerly assigned require **zero** release SQL sync. Reconcile SQL schema preflight/permission checks for both clean and already-P3.1-upgraded databases; do not modify immutable `008`.

**Exit:** proved no dormant-to-active privilege escalation and no customer assignment mutation; blocked installation states fail closed. **No SQL gate removal yet.**

### SEC-SIMPLE-T02 — Configured immutable superuser identity and audit

- [ ] Define protected deployment configuration and SQL binding schema/abstraction: normalized `UserName` to stable `UserId` plus DB identity, single binding, no implicit fallback. Safely bootstrap/bind an **existing** account with operator-supplied secret or a separately reviewed account enrollment; never seed a known password. Preserve historic `InitialAdministrator` bootstrap marker and allow safe upgrade without requiring another Administrator.
- [ ] Centralize bypass in Application `IPermissionAuthorizationService` (or an equivalent single enforcement point), with a testable identity-resolver port and Infrastructure verification; no special role-name checking and no insecure client-provided flags. Ensure all authorization entry points (current-user and userId overloads, pipeline/UI permission checks, Local dispatcher and future HTTP adapter) use consistent actor semantics.
- [ ] Test active/disabled/revoked user, logout/re-login/session expiry, stale actor, userId mismatch, rename/delete/recreate, duplicate/colliding normalized username, swapped/missing config, wrong DB, multiple hosts/conflicting config, unknown/deprecated/quarantined key, direct deny, and audit/recovery. Test no change to ordinary user's allow/deny/role precedence.

**Exit:** exactly one verified bound identity can bypass grants for supported unquarantined Active keys; ordinary `Administrator` membership never qualifies.

### SEC-SIMPLE-T03 — Stop per-release sync dependency without breaking installed databases

- [ ] Refactor SQL snapshot to rely on source-key/lifecycle plus conservative compatibility quarantine rather than **universal** `dbo.Permissions.CatalogStatus='Active'`; preserve atomic direct and role decisions, `IsActive` requirements and exact-key casing semantics.
- [ ] Remove `SqlSchemaCompatibilityChecker` startup check of `LastAppliedReleaseId` while preserving schema-version, artifact, privilege and supported-client compatibility checks. Prove no hidden `permissions sync` requirement remains in application startup, Login, packaging or deployment gates.
- [ ] Retire routine `permissions sync` and `permissions administrator-grant` commands from the **new** deployment path (retain controlled historical tooling/data until decommission reviewed). Rework first-enrollment runbook for superuser binding without role auto-grants; prohibit auto-granting new keys to `Administrator` or any user. Protect existing revoked/tombstoned managed rows from repair/regrant.
- [ ] Execute additive upgrade tests on P3.1-populated data, unsynced/new source registry, fresh install and deliberately malformed catalog/grant state; validate no auto-mutation of role/user grants or bootstrap marker, no surprise authorization, correct recovery and fail-closed unsupported client/version scenarios.

**Exit:** new source permission immediately available to bound superuser after update without permission sync; ordinary users get only their unchanged explicit grants. Drift/adoption rules remain enforced.

### SEC-SIMPLE-T04 — List All Permission Keys, manifest compare and export

- [ ] Expose a read-only Application query for complete deterministic source registry, with safe filtering/pagination if needed, and a shared DTO for local/remote adapters; do not enumerate only SQL catalog rows or silently hide deprecated keys.
- [ ] Add explicit reviewed-manifest baseline snapshot/diff, `Unassigned`, `New`, `Deprecated`, `Unknown/Legacy`, `Quarantined` categories and safe unavailable/unknown behavior; no key synchronization, no auto-grant, no per-release approval artifact.
- [ ] Add authenticated WinForms catalog UI (compact desktop layout) and JSON/CSV export, with suitable access control, no leaking secrets or raw database identifiers; unit/contract plus relevant UI tests verify counts, exact keys, pagination/filtering, escaping, empty/no-baseline case, category precedence and no writes.

**Exit:** after upgrade, administrators can inspect **all** keys supported by that build and compare what needs explicit assignment; viewing/reviewing never changes entitlement.

### SEC-SIMPLE-T05 — Explicit permission assignment via trusted UI/service or protected CLI import

- [ ] Select and document an enforceable trusted write boundary **before** implementation. If trusted service is unavailable in Local v1, keep GUI read-only and provide reviewed operator-only CLI import; do not block safe viewer/export waiting for insecure direct SQL UI writes. Track true UI editing as a separate pending sub-gate until trust boundary exists; **do not mark full UI-write acceptance as done**.
- [ ] Implement preview/diff, explicit actor/target/DB confirmation, additive-only default role/user grant changes, version/optimistic check at commit, validated active keys and quarantine checks, transaction isolation and durable audit, safe errors and idempotent re-import. Sensitive keys and changes to the bound account require explicit higher-trust review.
- [ ] Add focused failure/concurrency tests: duplicate import, unknown/deprecated/quarantined key, stale preview, partial invalid input, direct-deny precedence, concurrent grant/revoke, unauthorized impersonation/self-escalation, disabled operator, rollback/audit failure and no other role affected.
- [ ] Demonstrate UI or protected CLI can provision newly introduced permissions for ordinary user/role without changing any unrelated grants; after import, session/permission UI refresh is deterministic.

**Exit:** secure explicit customer grant path exists; release update never performs this operation automatically. State whether **trusted UI-write** is implemented versus deferred, with its own unresolved acceptance gate.

### SEC-SIMPLE-T06 — Rollout, recovery, documentation and final security acceptance

- [ ] Update the **current** `docs/SECURITY.md`, `SECURITY_PROVISIONING_RUNBOOK.md`, `CONFIGURATION_AND_SQL_SECURITY.md`, `SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md`, packaging/operational guidance and Codex instructions to describe implemented commands, security model, one-time quarantine/review, superuser recovery, clean install and upgrade. Keep historical P3.1 evidence and immutable migrations untouched.
- [ ] Run accepted modular security/migrations/desktop gates for all declared TFMs; mandatory strict unfiltered all-SQL two-TFM L3 at the **final frozen code-bearing PR revision**, with actual source/discovery/theory-case/TRX/ownership reconciliation and zero mandatory skips.
- [ ] Review full before/after account and grant parity, old Administrator role behavior, explicit deny, legacy dormant grant reactivation prevention, wrong database/configuration, upgrade/restore rehearsal, runtime SQL least privilege and cross-host Local deployment restrictions. Add manual-only recovery/packaged GUI acceptance at L4 as applicable.
- [ ] Publish only sanitized evidence and report unsupported/unrun required gates as `BLOCKED / NOT VERIFIED`. Do not mark rollout ready if secure administration, lost-superuser recovery or migration quarantine is unverified.

**Exit:** installation upgrade is repeatable and customer grants unchanged by release, and superuser/new-key/catalog/manual-import workflows are evidenced; any deliberately deferred trusted UI editor is clearly documented as not implemented.

## 5. L1/L2/L3/L4 test gate contract for future code PRs

**Do not guess T05 test project names, selectors or commands.** Resolve exact registered module/category, actual project×TFM and task-gate manifest at the implementation commit, after TESTGATE-T05 verifies its moves. Runtime SQL remains sequential, operator-approved, watchdog-bound and ownership-reconciled. Mandatory SQL skips=0. `L3` uses one planned strict *unfiltered* all-project execution per required SQL TFM at the frozen final revision; if changes invalidate evidence, rerun it. No additional CI gate or routine L4 benchmark.

| Task | L1 focused check | Required L2 projects/TFMs and SQL category | Negative/failure coverage | Final boundary |
|---|---|---|---|---|
| T01 | Pure quarantine classification/schema contract tests | Actual migration/security SQL projects on `net48` and `net8.0`; `MigrationSchema` + `SecurityProvisioning`; relevant harness/runner checks | Old denied grants remain denied, no DB mutations, wrong/stale schema, failed migration rollback and cleanup | L3 of T01 code PR |
| T02 | Pure username/config/binding/actor authorization tests | Application tests on declared TFMs, security real-SQL on both SQL TFMs; Desktop session tests on actual pure/GUI TFMs when affected; `SecurityProvisioning` | Recreated account, disabled, wrong DB, caller-userId mismatch, session race, unknown key, direct deny | L3 of T02 code PR |
| T03 | Focused authorization and startup-compatibility tests | App/Infrastructure + relevant Desktop startup tests, security and migration SQL on both TFMs; `SecurityProvisioning` + `MigrationSchema` | Stale release accepted but bad schema denied; dormant grants quarantined; no managed regrant/data drift | L3 of T03 code PR |
| T04 | Registry/list/diff serialization/unit tests | Application/Contract tests, actual Desktop pure + GUI WinForms TFMs as applicable; focused real SQL `SecurityProvisioning` when SQL assignment summary/baseline is exercised | Unregistered/unknown, empty baseline, duplicate keys, malformed CSV, no write side effects | L3 of T04 code PR |
| T05 | Preview/import parser/policy/unit tests | Trusted admin boundary tests; real SQL `SecurityProvisioning` on both TFMs, plus `SqlConcurrency` where actual multi-session race is tested; GUI on affected Desktop TFMs | Privilege escalation, stale preview, unauthorized writes, partial failure, direct deny, duplicate import | L3 of T05 code PR |
| T06 | Targeted doc/packaging/link checks | All affected migration/security/session/GUI projects×TFMs and L4 packaged/recovery walkthroughs where required | Upgrade/recovery smoke, least privilege, mixed-version rejection | Final release evidence |

**L1/L2 accounting for every slice:** revision, task ID, actual projects/TFMs, exact reviewed filter/case identity count, module/category, SQL approval/watchdog/cleanup, discovered/executed/pass/fail/skip, test evidence, blockers. Verify source/discovery/TRX logical-case mapping; do not mistake empty filter for passing tests. Do not run real destructive SQL against unapproved databases; do not publish raw TRX/approval/ownership journals/credentials/customer data. If proposed TESTGATE automation is absent or fails, report `BLOCKED`, never replace it silently with a bare `dotnet test`.

**Documentation-only PR containing this plan/ADR:** static link/consistency review only; runtime tests and SQL migration tests **NOT RUN** because code and schema remain unchanged. Later code-bearing PRs must satisfy all L3 obligations.

## 6. Acceptance matrix for clean install and upgrade

| Scenario | Required evidence |
|---|---|
| Fresh database, new app, no configured superuser | No implicit default account or bypass, secure bootstrap required, ordinary permission denied until explicitly assigned |
| Existing P3.1 database with approved Administrator grants | Upgrading makes no unrequested changes to `UserRoles`, `RolePermissions`, `UserPermissions`, direct denies or managed tombstones |
| New `Catalog.*` or `Inventory.*` key in source but absent from SQL catalog | Bound superuser may use declared Active key immediately; other users must be explicitly granted; no sync/startup veto |
| Previously `Unregistered` / pending / deprecated / missing-catalog key had old direct/role grants | No formerly denied grant activates by upgrading; quarantine/review must be explicit and audited; old assignment remains intact |
| Normal user with role grant and explicit direct deny | Deny still wins, including a role named `Administrator`; deny does not overrule only the uniquely verified superuser bypass for non-quarantined Active key |
| Two candidate accounts, username rename/delete-recreate, config/DB mismatch | No second superuser or silent transfer; tamper/mismatch rejected; audited recovery only |
| Superuser inactive or stale session | Bypass denied; protected operations rejected on current trusted active-state check |
| Unknown/deprecated permission | Denied to ordinary users and superuser; remains visible in catalog as appropriate |
| Repeated catalog export/manifest review and additive import | Read-only actions never change grants; repeat import is idempotent and audited |
| Local direct SQL client attempts privilege mutation outside trusted admin boundary | Rejected by SQL principal privileges; do not depend on client-controlled check alone |
| Old client/new schema or unsupported rolling deployment | Fail compatibility preflight per documented support matrix; no implicit downgrade or mixed-version success claim |
| SQL error/timeout/crash during migration/import | Atomic rollback/quarantine preservation, durable audit where possible, no leaked disposable test DB or unreviewed grant mutation |

## 7. Rollout/rollback and explicit exclusions

1. **Before rollout:** snapshot permission and grant state, verify backup/restore and target DB identity, validate trusted operator principal, inventory dormant legacy keys, approve the migration/quarantine strategy and configure one bound superuser identity. No production data or credential included in PR examples.
2. **Introduce compatibility first:** additive quarantine/binding metadata and tests, while existing runtime denial behavior is retained. Only then switch authorization and stop per-release sync. Stage rollout without mixed binary versions unless explicitly supported and verified; keep prior installer/runbook available for incident review.
3. **Rollback:** reverting app binaries **alone** may be insufficient because older builds still enforce `LastAppliedReleaseId` and SQL catalog status. Document tested forward-fix or coordinated restore procedure. Never silently restore auto-grants or delete a legacy grant to make downgrade work.
4. **Out of scope:** production permission auto-sync, replacing DbUp/RepoDb/Dapper, broader Identity redesign, new HTTP API product rollout, full CI migration, performance A/B reruns, removing historic audit/managed-role tables, relaxing SQL approval or parallelism controls.

## 8. Codex task handoff and evidence

- [ ] ADR 0007 explicitly reviewed/accepted.
- [ ] TESTGATE T02/T03/T04/T05 verified; capture commit, gate manifest and actual post-split project×TFM paths.
- [ ] For each SEC-SIMPLE task, create a **separate focused implementation PR or reviewable slice** and cite acceptance matrix rows addressed. Never collapse migration, bypass, SQL principal elevation and UI import into one unreviewable diff.
- [ ] Mark a checkbox only after its security invariants and real gates pass. Publish scope, safe diff summary, commands actually run, sanitized test inventory and outstanding `BLOCKED` cases.
- [ ] On completion, revise current operational docs and ADR decision status by explicit review, without altering already completed historical plan records.
