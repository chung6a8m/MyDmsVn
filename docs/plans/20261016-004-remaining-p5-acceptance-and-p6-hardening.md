# P5 acceptance + P6 Local v1 — Remaining work only

**Status:** ACTIVE / NOT COMPLETE (2026-10-16).  
**Execution position:** after [TESTGATE-T01–T03](20261016-001-task-gates-and-modular-tests.md), [temporary .NET 8 target-pause plan](20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md), [TESTGATE-T04/T05](20261016-002-modular-test-projects.md) and [SEC-REBUILD](20261016-003-permission-system-rebuild.md); before [P7 HTTP API](20261008-008-phase-7-remote-api.md).  
**Historical source of truth:** [original P5 plan](archive/20261008-001-sales-inventory-foundation.md) and [original P6 detailed plan](archive/20261008-007-phase-6-local-hardening.md), with completed tasks/evidence preserved unchanged. This file carries **all outstanding** P5/P6 requirements; moving the original plans to archive is not acceptance of pending tests.

## 1. Carry-forward scope and evidence

P5.1/P5.4 implementation and automated acceptance were completed previously; the following two interactive GUI acceptance checks remain unchecked. P4.1 automated L3 and packaged Login/manual evidence are already available, and the project owner accepted P4.1 as complete on 2026-10-16 despite the unobserved full idle-expiry/relogin interval. **That owner exception is only for P4.1**, not an exemption from unrelated P5/P6 GUI, SQL, recovery or release gates.

The original detailed P6-T01/T02 implementation and automated SQL tests were completed on 2026-10-12; T03/T04 implementation and developer-workstation automation/package gates were completed on 2026-10-13. Their **environmental/operational checks remain open**, including runtime-principal/rotation on clean deployments, verified client/schema compatibility, installed WinForms GUI, full-disk/audit, two-version upgrade, backup recovery. **Detailed P6-T05–T08 remain entirely pending.**

The new permission rebuild will intentionally replace the old P3.1 sync/Administrator flow. For post-SEC-REBUILD P6 release validation, follow the **actually implemented new bootstrap, catalog/role policies and administrator UI**; retain old P3.1 evidence as history, not as proof the replacement works. Approval of a SQL test instance (`MYDMSVN_SQL_TEST_APPROVAL_FILE`) remains mandatory and unrelated to permission feature removal.

## 2. Executable order and outstanding checkboxes

### REMAIN-P5-GUI-01 — Catalog & lookup interactive acceptance (old P5.1 gate)

- [ ] From the **actual packaged, Login-first** desktop host(s), manually exercise Products, Warehouses, Employees and Customers CRUD, active state, permissions and errors. Record rapid debounced free-text filtering, canceled/stale result suppression, immediate explicit Refresh and correct page reset.
- [ ] Keep a Goods Receipt open while updating/deactivating/reactivating a Product, Warehouse or Employee in a catalog; verify same-process `CatalogChangedMessage` causes lookup invalidation/reload, preserves historical selections and restricts new selections to active entities. Reopen screens; verify current data. Do **not** claim messenger delivery across processes.
- [ ] Record environment, actual TFM, build/package version, user/permission scenario, observation/screenshots (redacted), pass/fail and known gaps. Old STA/headless automated evidence is useful but does not itself complete this manual check.

### REMAIN-P5-GUI-02 — End-to-end Inventory interactive acceptance (old P5.4 gate)

- [ ] From the packaged authenticated host: create catalog records → Goods Receipt Draft/save/reload → Post → Stock Balance and Stock Card show the movement → duplicate Post is rejected → ledger/balance reconciliation remains correct.
- [ ] Exercise restricted/direct-denied account; stale rowversion/draft conflict; UI validation; logout/another user and restart. Avoid any blind retry after an uncertain Post result.
- [ ] Record UI evidence distinct from existing SQL/Application regression; do not claim that the partial P4.1 packaged Post/Stock observation covered all P5.1 filters or duplicate-Post cases.

### REMAIN-P6-OPS-01 — Finish P6-T01–T04 operational validation

- [ ] Prove clean-machine installer/publish integrity, manifest and source/version fingerprints, each active supported host/runtime, runtime SQL principal least privilege, secret handling, explicit migration administrator boundaries and permission provisioning under the *new* SEC-REBUILD model. Parked TFM packages must be labeled **not produced** rather than PASS.
- [ ] Exercise SQL principal/password rotation, absent/misconfigured credentials, SQL offline, bad/mismatched schema, full-disk/denied log path, file access and active audit sink; inspect sanitized diagnostic/log behavior.
- [ ] Verify supported client/schema compatibility with **two client versions**, staged/mixed client rollout, migration journal/preflight including unknown/partial/future schemas; do not infer support from a build alone.
- [ ] Validate the safe updater/installer decision and actual install/upgrade/reinstall on clean Windows test machines; no DbUp/first-admin bootstrap during ordinary desktop startup.

### REMAIN-P6-T05 — Packaged end-to-end GUI smoke

- [ ] Execute original [P6-T05 checklist](archive/20261008-007-phase-6-local-hardening.md#p6-t05--end-to-end-functional-and-windows-gui-smoke) including a real Login, business permissions, all catalogs, receipt Draft/Post, reconciliation, lookup/debounce/messenger, logout/relogin, disabled user/direct deny and deterministic no-modal handling.
- [ ] Exercise GUI on **each currently approved/shipped TFM**. If `net8.0-windows` is parked by NET8-PAUSE, do not demand an impossible package and do not state the old two-host requirement was tested: capture explicit owner-authorized temporary release-scope reduction. After reactivation, run the modern host's complete compatibility gate before claiming dual-host support.
- [ ] Record GUI and backend evidence separately; interactive user observation never replaces authorization or SQL invariants.

### REMAIN-P6-T06 — Backup, failure recovery, deployment operations

- [ ] Preserve the full [P6-T06 implementation and verification checklist](archive/20261008-007-phase-6-local-hardening.md#p6-t06--backup-failed-deployment-recovery-and-operations): SQL versions/topology/principals support matrix, verified pre-upgrade backup and **separate-DB restore drill**, corruption/failure/partial migration, lock and concurrent migrator, partially upgraded clients, controlled stop/drain, Post commit uncertainty and stock reconciliation.
- [ ] Document observed recovery time and data loss window; choose forward-fix versus explicit restore safely. Do not edit DbUp journal manually or restore production data over later writes.
- [ ] Provide runbooks, transcripts and operator decision tree without connection strings, credentials or raw SQL ownership tokens.

### REMAIN-P6-T07 — Nonfunctional/regression and target matrix

- [ ] Validate inventory concurrency/posting/rowversion/rollback, large grids/filter/paging/cancel, timeouts/reconnect/no modal hangs, P1 resources/migration, P2 contracts and *current* P3 security semantics; test with approved SQL instance and bounded case selection during task work.
- [ ] For a frozen release commit, use manifest-owned Final gate for all actual active tests/projects/TFMs, strict **unfiltered** SQL with approval/inventory/discovery/TRX/watchdog/ownership/cleanup, no mandatory skips; do not repeat full SQL L3 after each small edit.
- [ ] Record Release matrix of supported TFMs. Until NET8-PAUSE is executed, existing dual-TFM requirements still apply. If it is completed, active net48/parked net8 scope must be verified as a conscious release decision, **not** an inferred pass.

### REMAIN-P6-T08 — Release review, legacy identity and sign-off

- [ ] Decide supported legacy password migration with real fixture evidence, or obtain an explicit documented exclusion/reset strategy and owner approval; never guess legacy hash behavior.
- [ ] Verify current SEC-REBUILD authentication/permission administration/bootstrap, operator CLI runbook, actual migration separation, installed diagnostics/audit, security limitations of client-side direct SQL and recovery support.
- [ ] Produce the full release evidence matrix and owner sign-off for gates **G1 deployment/schema, G2 SQL/data safety, G3 security/diagnostics, G4 GUI/regression and G5 identity/release decision**. Update `docs/DEVELOPMENT_PLAN.md` only upon verified exits. Without that evidence, v1 is **not ready for production**.

## 3. L1/L2 task gates and final L3 boundary

| Work item | Fast task gate (L1/L2) | SQL only if affected | Manual/L4 obligation |
|---|---|---|---|
| P5-GUI-01 | Scoped Desktop ViewModel + STA control tests, actual active TFM, bounded watchdog | Focused `FastSQL` only for changed persistence behavior | Real Catalog/lookup/debounce/messenger walkthrough |
| P5-GUI-02 | Scoped posting/client/STA tests | Affected `FastSQL`/`SqlConcurrency` selected by behavior | Actual Draft/Post/balance/card/duplicate Post |
| P6-OPS-01 | Migrator/config/packager, secure principal/diag tests | `MigrationSchema`/`SecurityProvisioning` for real SQL changes | Clean-machine principal/rotation/upgrade/full disk |
| P6-T05 | Focused GUI/auth/lookup tests | Affected category only | Packaged UI, permissions, no modal hangs |
| P6-T06 | Backup/compatibility script inspection and restoration harness checks | Dedicated approved restore/drill environment, never production | Executed backup/restore/migration-failure runbook |
| P6-T07 | Focused UI, contract, inventory invariants | Reviewed affected categories; full SQL only at Final | Nonfunctional/target regression |
| P6-T08 | Evidence/audit/readiness review | **No repeated SQL when frozen Final evidence already matches** | Owner-reviewed release decision |

Before every execution resolve **the actual checked-in project × TFM matrix**, selectors and manifest-owned Task/Final implementation. Retain outer GUI watchdog, serial real-SQL approval/owned databases and strict cleanup. Never run speculative project paths or a now-empty selector. Run one planned Release L3 only after task gates pass and gate inputs are frozen; after a gate-affecting edit, invalidate/repeat the affected mandatory Final gate. Re-run SQLPERF benchmarking only with a separate approved performance objective.

## 4. Remaining P6 release evidence matrix

| Gate | Required proof | Exit |
|---|---|---|
| G1 — Deploy/schema | Package(s) install, baseline/upgrade/no-op, migration ownership/preflight and failed-upgrade handling | Open |
| G2 — SQL/data | Least privilege, verified separate-DB backup restore, concurrency/idempotency/ledger reconciliation | Open |
| G3 — Security/diagnostics | Non-NoOp audit, redacted correlated logs and current permission model | Open |
| G4 — Desktop/regression | Both pending P5 GUI checks plus P6 T05 and T07 for every **shipped** target | Open |
| G5 — Identity/release | New bootstrap + Login, legacy decision, lifecycle/temporarily parked TFM sign-off and full matrix | Open |

Release evidence rows should include artifact/version, active TFM/OS/architecture, SQL version/test principal class, method, actual command, elapsed time, PASS/FAIL/BLOCKED/NOT RUN, sanitized evidence location, tester and date. Raw approval files, connection strings, TRX, secrets and SQL owner journals never enter the PR.

## 5. Exit and no-regression rule

- [ ] Both **remaining P5 GUI** acceptance items pass with real packaged Login on every released target.
- [ ] All carry-forward P6 operational checks and detailed T05–T08 are verified (or explicit owner-approved *scope exclusions*, where legitimate, documented without claiming a failed/unchecked mandatory safety gate passed).
- [ ] All G1–G5 have current reviewed evidence; published runtime promises match built/shipped targets and the permission implementation actually present.
- [ ] TFM restoration before P7 is tracked; the modern host cannot be advertised as supported merely because historical dual-TFM tests once passed.
- [ ] No production database is reset or mutated during tests; release sign-off is never inferred from archiving old plans.

Once done, archive this remainder plan with its actual evidence. Preserve [the historical original P6](archive/20261008-007-phase-6-local-hardening.md) for its complete operational checklist and source mapping.
