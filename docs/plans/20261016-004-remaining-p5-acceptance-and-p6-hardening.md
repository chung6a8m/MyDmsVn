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

## 3. Task-scoped L1/L2 gate contract (bind actual post-T04/T05/SEC-REBUILD projects)

**Authority:** Every `REMAIN-...` task must have a reviewed **project × actual TFM × source-discovered selector** and nonempty expected Fact/Theory identities before the implementer claims Task PASS. The current source still has the anchors below, but **after TESTGATE-T04/T05 extraction and SEC-REBUILD** their physical paths, methods and categories may change: bind those from the *committed implementation revision* to the versioned manifest; do not run a deleted original test project or copy fictional module selectors.

**Historical/active target distinction:** After successful NET8-PAUSE, pure Application/SQL/WinForms tests use `net48`. The SQL runner (and any retained compatible architecture checker) may still run on tool-only `net8.0`. Desktop.AppCore/`net8.0-windows` and SQL `net8.0` are PARKED/NOT RUN until a separate restoration is verified; if NET8-PAUSE has not actually passed, current dual-TFM gates still apply. Never infer that runner-only net8 tests prove net8 application behavior.

**Binding manifest:** Proposed stable `planId=20261016-004-remaining-p5-acceptance-and-p6-hardening`; `taskId` must equal the subsection ID verbatim (for example `REMAIN-P5-GUI-01`). Before any task runs, register and review unique `gateId`s for **non-GUI, non-SQL Task cases** with exact project, framework list, `NonSql` category, reviewed selector, build prerequisites, source/discovery/TRX identity checks and bounded timeout. Current Task mode rejects SQL/GUI resources; **do not mislabel WinForms or SQL as `NonSql` to bypass it**. Bind affected SQL project + category to the verified guarded T05 runner with approval/owner/cleanup, and use a separately verified whole-tree GUI D1/D2 launcher. T05 must supply a reviewed multi-project strict Final; do not reuse T03's single-project Final for modularized tests.

### Per-task L2 matrix: current source anchors, mandatory selection and evidence

| Task | Non-SQL project / actual TFM and **reviewed current-source class anchors** | SQL category and actual project owner | GUI/operational and mandatory evidence |
|---|---|---|---|
| **REMAIN-P5-GUI-01** | Current `Server.Application.Tests` × `net48`: `CatalogApplicationTests`; Desktop VM anchor `CatalogViewModelTests`, `CatalogLookupViewModelTests`, `DebouncedAsyncActionTests` × **active GUI/VM project's declared TFM** after T04 | `FastSQL` for changed CRUD/lookup storage only; current `CatalogBackendIntegrationTests` / `CatalogMappingIntegrationTests` in original SQL project until moved; actual T05 module owner thereafter | Reviewed bounded GUI D1 for `CatalogControlTests`/`CatalogLookupViewModelTests`, explicit cancellation/messenger/lookup cases; manual packaged all four catalogs, receipt-open lookup reload, latest filter/Refresh, same-process not cross-process; screenshots/environment |
| **REMAIN-P5-GUI-02** | Current `Server.Application.Tests` × `net48`: `GoodsReceiptApplicationTests`, `InventoryQueryApplicationTests`; Desktop VM `GoodsReceiptEditorViewModelTests`, `InventoryViewModelTests` on actual post-T04 project | `FastSQL` for posting/stock mapping; `SqlConcurrency` if changed Post race/idempotency; original `GoodsReceiptCommandIntegrationTests`, `InventoryQueryIntegrationTests` until T05 module move | GUI D1 `GoodsReceiptControlTests`/`InventoryControlTests`; packaged Draft→Post→StockCard/Balance→duplicate Post denied, reauth/restricted user and stale rowversion; ledger/balance reconciliation evidence |
| **REMAIN-P6-OPS-01** | Current `Server.Application.Tests` `AuthenticationTests`/`PermissionAuthorizationTests` × `net48` if still valid **after SEC-REBUILD**; runner/tool boundary `TestProjectBuildContractTests` on its actual tool TFM; active WinForms package smoke as separate GUI gate | `MigrationSchema` for schema compatibility, `SecurityProvisioning` for principal/bootstrap; source anchor `SchemaCompatibilityIntegrationTests`/`SqlPrincipalProvisioningTests` until split | Controlled clean-machine install/publish, runtime/admin principal boundaries, rotation, full-disk, log redaction/audit, dual-version schema rollout; record artifact SHA, OS/SQL, user/tester, PASS/FAIL/BLOCKED |
| **REMAIN-P6-T05** | `CatalogApplicationTests`, `GoodsReceiptApplicationTests`, `InventoryQueryApplicationTests` × active `net48` in their real post-T04/T05 assembly; Desktop `DesktopHostRunnerTests`, `LoginFormTests`/`ProductionDiagnosticsTests` as reviewed | Only affected `FastSQL`/`SecurityProvisioning`/`SqlConcurrency` L2; new SEC-REBUILD permission implementation/tests must be used, not obsolete registry tests | Bounded GUI D1/full D2 at release for packaged Login, all catalogs, Draft/Post/stock/card, messenger, error/modal/disabled user/permission and logout; retain manual checklist separately |
| **REMAIN-P6-T06** | Current architecture/compatibility validation `SchemaCompatibilityPolicyTests` (original SQL assembly, reviewed classification) and `TestProjectBuildContractTests` (tool-only), as actually moved/retargeted. A SQL-backed class is **not** eligible for pure Task merely by its name | Dedicated approved `MigrationSchema` plus actual SQL failure/recovery categories if code changed. **Restore drills** require isolated approved backup/restore environment, not automatic dev database mutation | Separate-DB verified backup restoration, partial migration/concurrent migrator, safe forward-fix/restore decision, disk/network/commit uncertainty, real recovery timings/runbooks; no production DDL |
| **REMAIN-P6-T07** | Current `Server.Application.Tests`: `ApplicationPipelineTests`, `ContractEnvelopeTests`, `GoodsReceiptApplicationTests`; Desktop `InventoryViewModelTests`, `UiDispatcherTests`; architecture `ArchitectureBoundaryTests` on its **actual compatible target**, not invented net48 | Reviewed actual `FastSQL`, `SqlConcurrency`, `MigrationSchema`, `SecurityProvisioning` coverage as affected; post-T05 global case/owner bijection | Large grid/paging/cancel/offline, business invariant and Post races; bounded GUI/STA, supported Release matrix; full strict SQL belongs to frozen L3 **not after each edit** |
| **REMAIN-P6-T08** | Static operator runbook/release manifest and explicit `ArchitectureBoundaryTests`/`ProductionDiagnosticsTests` only when changed; source-reviewed project/TFM | **None** for pure sign-off/docs audit; rely on fresh matching frozen Final evidence, trigger focused SQL only for a code/SQL change | G1–G5 release matrix, legal legacy hash decision, SEC-REBUILD administration/bootstrap, evidence owners/dates, explicit temporary net8 exclusion; cannot sign off blocked gates |

**Per-row L2 exit:** record the task, commit SHA, actual project×TFM, selector and *expected discovered/tested count*, source/discovery/TRX reconciliation, build status, case pass/fail/skip, nonzero exit/timeout, SQL approval/preflight/owned DB cleanup where applicable, bounded GUI state and separately identified manual evidence. Never use a missing/new test name as an empty green selection. If T04/T05 moved or SEC-REBUILD deleted a named class, replace the selector only after a reviewer records the exact renamed/replacement case identities; label unresolved selectors `NOT YET BOUND / BLOCKED`.

### Existing runnable focused *non-GUI* command examples (before project extraction)

These use **currently existing** source class names. They are valid **only while the original project still owns the cases**, on a committed, restored build whose `.csproj` declares the requested TFM. After T04/T05, use its manifest-owned `Task` for the real replacement project; do not treat ad hoc commands as evidence of manifest Task selection.

```powershell
# Select verified current Application non-SQL cases; no SQL approval needed.
$app = 'tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj'
dotnet test $app -c Release -f net48 --filter 'FullyQualifiedName~CatalogApplicationTests'
dotnet test $app -c Release -f net48 --filter 'FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~InventoryQueryApplicationTests'
```

**After module split (proposed manifest-owned Task; NOT runnable until registered and reviewed):**

```powershell
$remainingPlan = '20261016-004-remaining-p5-acceptance-and-p6-hardening'
$artifactRoot = '.sqlperf/p6-task-' + [guid]::NewGuid().ToString('N').Substring(0,12)
& ./tests/tools/Invoke-TestGate.ps1 -Mode Task -PlanId $remainingPlan `
  -TaskId 'REMAIN-P5-GUI-01' -ArtifactRoot $artifactRoot
```

**SQL L2:** resolve **actual post-T05 module `.csproj` path**, category and complete case selector from its *implemented* gated test runner; supply explicit `-Frameworks @('net48')` if it is supported by that verified wrapper and use valid `MYDMSVN_SQL_TEST_APPROVAL_FILE` on an explicitly approved non-production instance. Preserve global cross-process SQL lease, per-project source/discovery/TRX, zero mandatory skips, whole-tree watchdog, owned DB reconciliation and cleanup. `Invoke-FocusedSqlIntegrationTests.ps1` is currently a **single-original-project / one-category** helper; do not claim it supports unverified moved module+category selection. If unavailable, record SQL L2 **BLOCKED**, never fall back to a raw SQL `dotnet test`.

**GUI L2 and release D2:** NET8-PAUSE-T03 provides the initial `Invoke-GuiTestAcceptance.ps1` whole-tree launcher for the original active-net48 Desktop project. NET8-PAUSE-T05 GUI D2 passed on the earlier candidate but must be repeated with the fresh envelope after runner authorization fix `5b1813c`; modular T04-A must revalidate/extend it after project splitting. Direct GUI-bearing `dotnet test` / `--list-tests` still cannot substitute for D2.

### Bounded budgets and Final acceptance

| Execution | Upper-bound contract (adjust only after source-reviewed evidence) | Required result |
|---|---|---|
| Pure L1/L2/manifest Task | **300s per test process; 900s aggregate Task**, nonempty selector | Exact pass/fail/skip count + revision; no incomplete TRX |
| Guarded SQL L2 / L3 | **1200s per SQL project/TFM invocation; 7200s overall Final** while current case count applies; refuse unbounded SQL executions | Approved instance, full discovery/theory rows, watchdog, ownership/cleanup |
| GUI D1/D2 | **900s per GUI project/active TFM whole tree**, or a reviewed stricter threshold derived from baseline | No modal hang, STA/serial, forced cleanup and separate TRX |
| Manual package/restore/VM L4 | Operator-controlled bounded scenario with explicit deadline, safe approved non-production environment and recovery rollback | Screen/runbook/tester/time/result; a partial/manual stop is **NOT VERIFIED** |

**Final acceptance is an evidence envelope, not one loose `dotnet test`:** after all task gates are green and inputs frozen, use **verified post-T05 manifest-owned Final** with **unfiltered all active SQL project(s)** and required non-SQL modules/Architecture (tool-only target where necessary); separately execute complete GUI D2 and required clean-machine/backup/restore L4, all bound to the same release revision/artifact manifest where applicable. SQL retains approval/inventory/TRX/ownership/watchdog/cleanup and zero mandatory skips. No completed P5/P6 stage may rely on obsolete old permission behavior from before SEC-REBUILD. A later gate-affecting edit invalidates and repeats the *affected complete mandatory gate*, not the full suite after every small edit.

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
