# P6 — Local v1 production hardening (detailed implementation plan)

Status: **Not started**. Depends on verified P5 acceptance; do not release P6 while mandatory P5 SQL/concurrency/authorization/Windows GUI gates are blocked. Scope is **Local Mode v1**, not the P7 HTTP service.

Read first: `AGENTS.md`, `docs/DEVELOPMENT_PLAN.md`, `docs/PRD.md`, `docs/ARCHITECTURE.md`, `docs/TECH_STACK.md`, `docs/SECURITY.md`, `docs/PERSISTENCE.md`, `docs/TEST_STRATEGY.md`, `docs/CONTRACTS.md`, `docs/P3_VERIFICATION.md`, and `docs/plans/20261008-001-sales-inventory-foundation.md`. Existing migrator entry point: `src/MyDmsVn.Server.DbMigrator`.

## Objective and boundaries

Make the P5 Local Mode vertical slice installable, diagnosable, maintainable and recoverable on supported Windows/SQL Server deployments, through **verified artifacts** for both `net48` and `net8.0-windows`.

- Preserve the v1 two-tier model: desktop hosts load LocalApiClient + Server.Application/Infrastructure and connect to SQL Server. Neither local API permissions nor client-side protection establish an independently trusted server boundary.
- Preserve Explicit UnitOfWork, RepoDb/Dapper, DbUp, SQL posting invariants, transport-neutral `ApiResponse<T>` and both desktop targets. No migration on desktop login or normal startup.
- No silent change to approved frameworks/packages, automatic self-update, new business modules, P7 HTTP implementation, destructive schema reset, or default credentials. Any architectural change requires a separate approved ADR.
- Plan and test the **shared-database, multi-workstation** topology: applications may be installed on different client versions during a rollout. Any mixed-version operation must be explicitly supported by the compatibility matrix; otherwise block it safely.
- All SQL/deployment exercises must use isolated disposable test databases or explicitly approved restored copies; never run destructive drills against arbitrary developer/production databases.

## Implementation order and phase traceability

Complete T01/T02/T06 safety contracts before using packaged clients against a migrated DB; T03/T04 can be developed in parallel with controlled integration points; T05/T07 validate the resulting binaries; T08 is the final sign-off gate.

The summary roadmap in `docs/DEVELOPMENT_PLAN.md` has five P6 summary items; this document has eight execution tasks. **Do not infer identical IDs across documents.** Mapping:

| Roadmap P6 summary | Detailed tasks providing evidence |
|---|---|
| P6-T01: migration and client/schema compatibility | P6-T02, P6-T06 |
| P6-T02: installer/deployment, configuration/updater | P6-T01, P6-T04 |
| P6-T03: logs, correlation, audit redaction | P6-T03 |
| P6-T04: dual-host smoke/install/upgrade/recovery | P6-T02, P6-T05, P6-T06, P6-T07 |
| P6-T05: checklist/known limits | P6-T08 |

At phase completion, update both the detailed checklist and the roadmap with matching evidence; do not mark the summary complete merely because an individual detailed task passes.

## P6-T01 — Secure configuration, credentials and SQL privileges

### Implementation requirements

- [ ] Define documented configuration precedence/locations for both desktop hosts and the **separate admin migrator** (installation-specific values, runtime environment overrides if approved, validation on startup). Fail closed on missing/invalid values with actionable localized diagnostics; never print the raw connection string.
- [ ] Define and provision **separate SQL principals**: (a) runtime principal with only demonstrably necessary object/operation permissions, (b) migration/admin principal with schema-change privileges available only during controlled deployment. Runtime must not be `sa`, `db_owner`, `db_ddladmin`, or inherit equivalent schema-management powers. Record the necessary read/write/execute grants per P3/P5 object and review them whenever migrations add objects.
- [ ] Select a deployment-specific credential strategy: prefer constrained Windows authentication where supported; when SQL authentication is required, provision outside source/installer defaults and protect local configuration using suitable Windows access controls and, if applicable, DPAPI. Document machine/user binding, rotation, recovery and deployment to multiple workstations. **DPAPI/ACLs do not prevent the authorized desktop process or a compromised client from using its SQL privilege.**
- [ ] Exclude passwords, `PasswordHash`, `PasswordSalt`, bearer/auth artifacts, decrypted secrets and full SQL connection strings from logs, error dialogs, diagnostics exports, crash data and audit payloads. Check configuration files, environment-variable diagnostics, installer scripts and sample documentation for exposure.
- [ ] Document SQL network constraints (authorized hosts/firewall, encryption/server-certificate validation policy, supported authentication and connection timeout defaults) without disabling TLS verification as an undocumented workaround. Verify client access under the least-privileged identity.
- [ ] State the residual **two-tier trust limitation** in administrator guidance: users able to extract/use runtime SQL credentials may bypass Application authorization and business workflows; least privilege reduces blast radius but does not create a trusted server. P7 is the architectural remedy.

### Verification and acceptance

- [ ] On a clean configured machine, both hosts connect as the intended runtime principal; configuration errors have stable safe error codes/messages; credential rotation and restart succeed.
- [ ] Integration tests/permission probes prove the runtime principal can execute each required P3/P5 workflow, but **cannot** ALTER/DROP objects, change DbUp journal/schema, grant itself privileges, or query objects not expressly granted. Failed permissions cause no business-table mutation.
- [ ] Scan packaged files and controlled diagnostic output for accidental credentials; redact exception chains; no production secret or default admin password appears in repo or distributable.
- [ ] Evidence: versioned configuration guide, SQL GRANT/provisioning script or approved DBA runbook, principal/permission matrix, test results and remaining risk statement.

## P6-T02 — Controlled migrations and schema compatibility

### Implementation requirements

- [ ] Keep `Server.DbMigrator` independent of both client executables: admin/infrastructure installation path only; requires explicit target database and a migration principal. Retain system-database rejection, immutable numbered DbUp scripts and `dbo.SchemaVersions` journal compatibility from P1. Never apply migrations implicitly on client login/startup.
- [ ] Define a **client-to-schema compatibility contract** (explicit schema version/manifest plus minimum and maximum supported client/database combinations, or an equivalent documented range). Version detection must not assume that merely finding the latest known journal entry proves compatibility; detect unknown/future scripts, missing expected history and partially applied releases. If hashes/manifest integrity are introduced, preserve existing journal compatibility and specify an approved baseline for previously applied scripts.
- [ ] Both desktop hosts perform a **read-only schema preflight before any business data access or mutation**. Unsupported old/new/unknown schema fails closed with a specific, localized, non-secret error and upgrade instructions; no background auto-migration. Only allow mixed client versions against the same DB when compatibility has been explicitly verified.
- [ ] Specify deterministic migration ordering and preflight: environment/principal validation, target identity, supported SQL version, available capacity/permissions, current journal and manifest, verified backup/recovery point, active-client quiescence/maintenance window and target release. Enforce a single migration owner with an explicit deployment lock/protocol; concurrent migrators must not both proceed. Ensure the lock spans the actual migration operation, not merely an unrelated preflight connection.
- [ ] Respect the existing DbUp `WithTransactionPerScript()` model: individual scripts may commit even if a later script fails. Declare migration scripts immutable and additive where feasible; never claim that multi-script upgrades are automatically all-or-nothing. Define a safe failure state that stops clients until an operator completes a documented recovery decision.
- [ ] Produce a schema-version decision table covering empty DB, supported prior DB, current DB (no-op), DB too old/new, unknown/forked history, failed partial migration and concurrent migration attempts. Document the exact recovery action for each.
- [ ] Establish an upgrade compatibility policy: expand/contract changes and client rollout order where viable; otherwise require downtime until both DB and clients are upgraded. Post-migration launch must pass schema preflight before writes are enabled.

### Verification and acceptance

- [ ] Automated SQL integration tests on isolated databases: first install, upgrade from **each supported release baseline**, repeat/no-op, rejected invalid/system DB, old/new/unknown/partial history, two competing migration invocations, and unchanged production data on failed preflight.
- [ ] Fault injection after at least one successful migration script but before a later script succeeds; verify journal/schema reality is detected, the old/incompatible client does not mutate data, and recovery/forward-fix proceeds only via an explicit admin step.
- [ ] Two-client-version tests demonstrate blocked unsafe combinations and any expressly supported mixed-version combination; both `net48` and `net8.0-windows` check the same compatibility policy.
- [ ] Evidence: schema compatibility matrix, migration operator runbook, automated SQL results, failure-injection report and applicable version/manifest references.

## P6-T03 — Structured logs, security audit and diagnostics

### Implementation requirements

- [ ] Configure production **Serilog sinks**, enrichment, levels and retention for both desktop hosts; include application version, host/runtime, operation name, correlation ID and safe error code. Each LocalApiClient request/command must carry a correlation context across Application pipeline, handler and persistence diagnostics without relying on leaked cross-request state.
- [ ] Replace the P3 production **NoOp security audit sink** with an actual registered sink in **both** desktop composition roots; keep typed security audit contract/redaction boundary. Cover login success/failure/disabled/rehash errors and privileged P5 actions (especially successful/denied Post); define minimum action/outcome/actor/timestamp/document identifier fields without sensitive request bodies.
- [ ] Emit audit **success only after durable business commit**; failed validation/authorization/conflict/cancellation must not be mislabeled successful. Explicitly decide how to surface and retry/report audit-write failures after a committed operation; do not tell users the transaction rolled back if only the post-commit sink failed. If stronger durability is required, specify a transactionally persisted/outbox design as a separate approved change.
- [ ] Define log folder and writable ACLs without requiring ordinary users to be administrators; rotation, retention, disk-full behavior, support export, access and cleanup. Local workstation file logs are **not** inherently tamper-proof centralized audit evidence.
- [ ] Use stable, localized user-facing errors mapped from `ApiError.Code`; attach correlation ID for support. Logs may contain controlled technical detail, but UI/exports must not expose SQL command text with parameters, connection strings, stack traces or credentials.
- [ ] Provide a troubleshooting/runbook path for config failure, SQL offline, migration incompatibility, authorization failure, slow queries, failed posting, logging failure and crash recovery.

### Verification and acceptance

- [ ] Tests assert both hosts resolve a non-NoOp audit implementation in production DI, login/security events reach the configured sink, privileged Post success is emitted after commit and denied/failed/canceled work creates no false success event.
- [ ] Automated redaction fixtures cover password/hash/salt, auth tokens, connection strings, SQL errors and nested exceptions; correlation IDs remain consistent through Local adapter and pipeline on errors and cancellation.
- [ ] Exercise sink permission denial/full disk and startup logging failures without uncontrolled modal dialogs or misleading business commit status; record supported fallback/fail policy.
- [ ] Evidence: logging/audit schema and retention guide, redaction results, DI/test evidence and troubleshooting examples.

## P6-T04 — Reproducible packaging for both Windows hosts

### Implementation requirements

- [ ] Choose and document **actual release artifacts** for `net48` and `net8.0-windows` (installer and/or documented publish-directory deployment). Specify per-host version, build/publish commands, prerequisites, supported Windows editions, architecture `x86`/`x64` and native/transitive dependencies. Do not mistake successful `dotnet build` for deployment verification.
- [ ] For `net48`, document installed .NET Framework 4.8/runtime requirements; for `net8.0-windows`, decide framework-dependent vs self-contained, bitness and .NET Desktop Runtime handling. Test the **built artifacts**, not only the development environment.
- [ ] Define installation and upgrade paths, user vs machine installation permissions, config preservation, shortcuts, writable log/data folders, clean uninstall behavior and whether uninstall leaves shared SQL data intact (default: **never remove DB**).
- [ ] Keep migrations in a separate admin package/step with separate credentials. An ordinary user installer/update must not silently invoke DbUp, alter SQL principal grants or embed privileged secrets.
- [ ] Do not adopt Onova or auto-update without approval; document manual/safe updater decision and version skew/maintenance policy. If signed binaries are required by deployment policy, verify signature/trust chain; otherwise record explicit release acceptance.
- [ ] Build a release manifest with product/runtime version, schema support range, package dependencies, artifact hashes (e.g. SHA-256), SQL compatibility and optional signing information.

### Verification and acceptance

- [ ] Clean-install each host from its release artifact on appropriate Windows VM(s) without Visual Studio, developer SDK or undeclared dependencies; test correct architecture and supported runtime installation.
- [ ] Upgrade over a prior installed version preserves configuration and shared SQL data; restart, repair/uninstall and limited-user execution behave as documented.
- [ ] Verify release artifact contents contain no admin/migrator connection strings, migration automation or unexpected elevated privileges; verify hashes/signature where applicable.
- [ ] Evidence: deterministic package/build scripts, installation guide, artifact manifest/hashes and VM install/upgrade test records.

## P6-T05 — End-to-end functional and Windows GUI smoke

### Implementation requirements

- [ ] Execute the P5 acceptance walkthrough **from packaged binaries** for both hosts: login, permissions, all four catalog CRUD/active-state workflows, Goods Receipt Draft create/edit/reload, Post, StockBalances, StockCard, duplicate Post rejection, logout/relogin and restart.
- [ ] Validate Catalog UI free-text filtering debounce, cancellation and request-generation stale-response discard; explicit Refresh remains immediate and paging resets correctly. Keep large-grid tests in T07.
- [ ] With Goods Receipt open, update/deactivate/reactivate Product/Warehouse/Employee in catalog screens: `CatalogChangedMessage` causes same-process lookup invalidation/reload, maintains historical selection and respects active-only new selections. Reopening screens fetches current data; **do not claim cross-process messenger delivery**.
- [ ] Smoke stale receipt concurrency tokens and Post-only versus Write/Read permissions; unauthorized operations and failed Post must not change ledger or balances.
- [ ] Distinguish test layers: deterministic ViewModel/SQL integration tests carry strong assertions; STA GUI automation/manual smoke exercises actual controls and captures results/screenshots, not merely handler calls.
- [ ] Suppress unintended WinForms `DataError`, `ThreadException`, unhandled-exception and default modal dialogs in unattended test runs; bounded startup/login/DB-failure smoke must terminate with deterministic diagnostics.

### Verification and acceptance

- [ ] Repeat a release smoke checklist on `net48` and `net8.0-windows` **separately**, recording package build, OS, architecture, runtime, SQL version, tester, time, Pass/Fail/Blocked and evidence.
- [ ] Confirm stock reconciliation after the happy-path Post and failure-path retry; no duplicates or silent lookup selection changes.
- [ ] Evidence: automated test logs, bounded STA results and separately labeled manual GUI observations/screenshots for each host.

## P6-T06 — Backup, failed deployment recovery and operations

### Implementation requirements

- [ ] Define SQL Server support/compatibility matrix by version/edition/topology with verified test coverage; distinguish supported versions from untested configurations. Include Windows and SQL authentication/network assumptions for local vs intranet DB.
- [ ] Define pre-upgrade backup ownership, sufficient storage, consistency/integrity verification, secure location, RPO/RTO expectations and a **tested restore** to a separate safe database before any destructive recovery rehearsal. A backup file existing is not proof that it is restorable.
- [ ] Create deployment runbook: notify users, stop/drain clients, ensure no active posting transaction, confirm backups, run migration under admin principal with a lock, verify history/schema, deploy matching clients, smoke/reconcile and reopen access. Explain partial rollout and downgrade restrictions.
- [ ] Define operator decision paths for migration failure (resume/forward-fix vs restore from a verified point). Document that database **restore can lose writes made after the recovery point**; do not instruct unqualified automatic rollback of schema or data. Never rewrite DbUp history manually as a routine fix.
- [ ] Specify how desktop reacts to SQL offline, network interruption, login failure, connection timeout and incompatible schema: bounded retry/backoff for safe reads, cancellation, a clear reconnect/exit UX and **no blind automatic retry of mutating commands** with uncertain commit outcome. For Post, reload server state and use existing idempotency/status checks before deciding on a retry.
- [ ] Record basic operational checks: connectivity/principal, free disk, migration journal version, stock ledger/balance reconciliation, log/audit health and diagnostic bundle extraction.

### Verification and acceptance

- [ ] Restore drill from pre-upgrade backup onto a **separate test database**; exercise successful reinstall/reconnect and document observed recovery time, any expected data loss window and exact operator steps.
- [ ] Simulate migration failure after an earlier script committed, crashed/killed migrator, concurrent launch and client left open during upgrade; verify safe blocking, clear status and tested forward-recovery or explicit restore path.
- [ ] Exercise DB offline/timeout during read and Post including commit-result uncertainty; verify UI remains responsive, connection/UoW resources are disposed, no duplicate movement and reconciliation succeeds after reconnect.
- [ ] Evidence: backup/restore runbook with drill transcript, compatibility matrix, rollback/forward-fix decision tree, downtime/RPO/RTO record and operational checklist.

## P6-T07 — Nonfunctional and cross-target regression

### Implementation requirements

- [ ] Re-run P5 SQL invariant suite using isolated database: double/concurrent Post, concurrent different receipts to one balance key, UpdateDraft-vs-Post, optimistic `rowversion`, master-data deactivation race, unique constraints, fault-injection rollback and `FULL OUTER JOIN` ledger/balance reconciliation including missing/extra zero balance rows.
- [ ] Exercise large catalog/receipt/stock-card paging, deterministic ordering, rapid filters with debounce and old-result protection, quick navigation/dispose and lookup refresh during background loading. Define reproducible test volumes and environment metrics; do not assert arbitrary performance claims without baseline.
- [ ] Validate nonblocking WinForms behavior on slow/offline SQL, cancellation and command retry boundaries, progress/busy/empty/error states, field-level validation and localized error codes. No unhandled modal dialogue or frozen unattended process.
- [ ] Re-run P3 identity/permission tests (direct deny override, inactive user, revoked role, password rehash, confidential-data redaction), P2 contract serialization and P1 resource ownership/migration tests. Verify production DI includes secure config and audit sink for both hosts.
- [ ] Run supported target-framework build/test matrix in Release with locked restore, record actual commands/environment and any disabled/skipped/blocked tests. Windows GUI results cannot be inferred from Linux-only CI.

### Verification and acceptance

- [ ] Compare results for both desktop hosts; preserve identical business semantics and presentation contract. Do not suppress failures or change test expectations solely to release.
- [ ] Any target or SQL environment unavailable is **Blocked/Not run**, never Pass; release sign-off requires actual evidence for every mandatory gate.
- [ ] Evidence: regression matrix with duration/test volumes where relevant, output logs, coverage of concurrency/reconciliation and recorded deficiencies.

## P6-T08 — Release review, legacy identity decision and sign-off

### Implementation requirements

- [ ] Resolve the **P3 legacy-password prerequisite** explicitly: **(A)** owner supplies approved actual algorithm specification/sanitized fixtures, a legacy verifier is implemented and tested end-to-end (successful rehash and failure/retry paths), **or (B)** owner explicitly accepts a v1 release **without legacy account login/migration support** and approves a safe account conversion/reset procedure. Unsupported algorithms must remain fail-closed. Never imply legacy migration passed using only fresh BCrypt accounts.
- [ ] State known v1 limits: Local Mode direct SQL/two-tier trust, same-process-only catalog messenger, absent P5 outbound sales/order/accounting/valuation/returns, no offline sync, no P7 HTTP boundary and any untested SQL/Windows topology.
- [ ] Evaluate .NET 8 Desktop support/lifecycle and dependency/vendor support for the **actual release date**; document risk, security update policy and owner decision. Do **not** silently retarget to .NET 10 or another framework.
- [ ] Assemble release manifest: versions/commit, binary package hashes, schema manifest/range, database/OS matrix, approved deployment and backup steps, optional signature verification, change notes, known issues and post-release support path.
- [ ] Perform security/operations sign-off: SQL runtime privilege test, absence of embedded privileged credentials, active production audit sink, redaction, reviewed schema compatibility and proven backup/restore. Release sign-off must identify owner/date and nonnegotiable blockers.
- [ ] Reconcile detailed task checkboxes with P6's five roadmap summary items and update release/status documentation. Keep unsupported or unexecuted gates visible instead of marking P6 complete.

### Verification and acceptance

- [ ] Produce a **release verification matrix** with rows for clean install, upgrade, repeat migrator, older/newer/partial DB, recovery, auth/legacy decision, permission denial, catalog lookup propagation, concurrency/reconciliation, slow/offline SQL, logging/audit and packaged GUI smoke. Columns: artifact/version, target TFM/OS/arch, SQL version, test method/command, Pass/Fail/Blocked, evidence URL/path, owner and date.
- [ ] Sign off only with **all mandatory P5 and P6 gates passed**, or record that v1 is *not ready for production*. Option B for legacy accounts is not a silent test waiver: documented reduced support scope, migration/reset procedure and explicit product-owner approval are required.
- [ ] Evidence: signed or owner-approved release checklist, accepted exceptions (if any), artifact manifest, matrix, support runbooks and updated roadmap.

## Mandatory release gates

| Gate | Required evidence | Release blocker if missing |
|---|---|---|
| **G1 — Deployment/schema** | Two-host packaged clean install; supported baseline upgrade and no-op; schema preflight old/new/unknown/partial; single-migrator guard; recoverable failed migration | Yes |
| **G2 — SQL/data safety** | Least-privilege runtime vs admin principals; no privileged embedded secrets; verified backup restore; Post concurrency, rollback and ledger reconciliation | Yes |
| **G3 — Security/diagnostics** | Non-NoOp audit sink wired in both production hosts; correct post-commit semantics; logs redact secrets and correlate errors | Yes |
| **G4 — Desktop/regression** | P5 acceptance and WinForms/STA smoke on **both** installed release artifacts; debounce/messenger/lookup, cancel/offline/timeout cases; no modal hangs | Yes |
| **G5 — Identity/release decision** | Approved real legacy verifier tests **or** approved exclusion/reset strategy; .NET 8 lifecycle risk disposition; complete release matrix and owner sign-off | Yes |

**Evidence discipline:** Every task must record changed files, actual Release build/test commands and results, Windows/SQL environment, manual vs automated evidence, outstanding findings and next task. A skipped SQL/Windows test is not equivalent to a passed gate; environmental inability to execute mandatory verification leaves P6 blocked. Use `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` only with the guarded disposable test database harness. Production data must never be reset to make tests pass.

## Exit evidence / Definition of Done

- [ ] Both `net48` and `net8.0-windows` artifacts install and operate on documented clean Windows environments; P5 features and critical regressions pass with per-host evidence.
- [ ] Installed clients enforce explicit schema compatibility, with controlled separate DbUp migration and verified failed-upgrade/recovery drills; a shared database cannot be silently changed by ordinary clients.
- [ ] Runtime SQL privileges and credential handling meet documented constraints, with residual direct-SQL risk disclosed; production security audit sink and privacy-safe correlated diagnostics are verified.
- [ ] SQL backup has been **restored successfully** in a safe test environment; operational runbooks cover failures and commit uncertainty without duplicate inventory movements.
- [ ] Legacy credentials have an explicitly approved supported migration or exclusion/reset path; no unfounded compatibility claim.
- [ ] Release matrix, known limits, lifecycle decision, artifact manifest and owner sign-off are complete; `docs/DEVELOPMENT_PLAN.md` is updated only after all relevant gates have evidence.

## Avoid

- Claiming Local Mode is a trusted independent middle tier or that SQL ACL/DPAPI prevents a privileged/compromised local client from bypassing business logic.
- Shipping `sa`, `db_owner` or migration credentials to every desktop; running DbUp on login, normal launch or automatic update.
- Treating DbUp per-script transactions as a single all-or-nothing release; blindly restoring a backup over new production writes; silently editing journal history.
- Allowing clients to continue against unknown/incompatible schema, or blindly retrying a mutating command after the connection drops with unknown commit result.
- Shipping a production NoOp security audit sink, logging password material or claiming that workstation-local logs are inherently tamper-proof.
- Labeling developer-machine `dotnet build`, headless unit tests or unexecuted SQL/Windows GUI tests as proof that release installers work.
- Marking legacy password migration as supported without approved fixtures, changing target frameworks without approval, or treating unresolved mandatory release gates as passed.
