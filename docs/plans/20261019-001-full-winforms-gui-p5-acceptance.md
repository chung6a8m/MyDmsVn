# GUI-P5 — Full WinForms GUI testing and P5 interactive acceptance

**Created:** 2026-10-19 · **Branch:** `v1a` · **Status:** PLANNED / OPEN. This document does not assert any new GUI acceptance.
**Planning baseline:** merge `42d94baea1655dddbd46f971e806f84401cd1c63`. Follows completed [RESP](20261018-002-respawn-fixture-reuse.md) and [L2DB](20261018-001-linq2db-net48-persistence-migration.md). [REL](20261019-002-local-v1-p6-release-acceptance.md) owns P6 release acceptance.

## 1. Objective, authority and scope

Close `REMAIN-P5-GUI-01` (Catalogs/Lookups) and `REMAIN-P5-GUI-02` (Goods Receipt/Stock), and complete **automated GUI regression plus real packaged-login E2E** for the Local v1 WinForms host. Evidence has three deliberately separate levels: guarded STA/control tests; out-of-process packaged E2E with real authentication and independent SQL assertions; and attended operator walkthroughs. None silently substitutes for the others.

The current source, [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md), [tests/README](../../tests/README.md), [PRD](../PRD.md) and [L2DB-T07 evidence](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) govern. The [2026-10-16 unfinished-work plan](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) is historical traceability, not the execution policy.

At frozen T07, nine test projects supplied **581 passing** invocations; the Desktop GUI project's **139 invocations were NOT RUN (conditional)**. T07 completed limited, approved real packaged Local smoke, **not** all P5 UI scenarios or a P6 release gate. Re-inventory case names and Theory rows against each new candidate; do not turn 139 into a permanent expected count.

## 2. Fixed baseline, dependencies and safety

- **Only shipped desktop:** `MyDmsVn.Desktop.App.exe`, `net48`/x86, directory artifact `desktop-net48-win-x86`; admin migrator is separate `admin-migrator-net48-win-x86`. AppCore `net8.0-windows` stays PARKED / NOT SHIPPED. No fabricated two-host gate.
- **Test owners:** `tests/MyDmsVn.Desktop.Tests` (WinForms/STA/package/GUI-E2E); `tests/MyDmsVn.Desktop.Session.Tests` (pure session and ViewModel); relevant Application/Architecture/six SQL owners for independent business and SQL guarantees. Reuse current projects by default; creating another project needs a documented reason and inventory change.
- **Actual environment:** Windows interactive user desktop, .NET Framework 4.8/targeting packs and actual package architecture. Record OS build, x86 process and testhost bitness, theme/DPI, SQL edition/version, code revision, manifest/hash, account/permission scenario, tester/date. Headless CI and missing interactive desktop are BLOCKED / NOT RUN.
- **Test data:** explicitly designated non-production SQL instance, separately authorized admin setup and limited runtime account, current scripts 001–009 and positively owned disposable application DB. Do not run Respawn/reset on production or operational backup/recovery databases. Never bulk-drop by name prefix, expose credentials, or drop an allocation with un-drained connections.
- **Current identity:** protected single configured superuser and code-defined permission registry with deliberate role/user assignments; direct Deny overrides ordinary grants. No permission synchronization or first-Administrator auto-grant. UI hiding alone does not establish authorization.
- **Execution:** source-verified `dotnet test`/Test Explorer and TRX; one GUI suite/packaged process at a time. Removed SQL runner, `SqlTestHarness`, `tests/test-gates.v1.json`, approval file and global lease do not return.

## 3. Coverage map to reconcile before implementation

| User behavior | Existing source owners/anchors | Evidence that must be closed |
|---|---|---|
| Login / shell / session | `LoginFormTests`, `DesktopHostRunnerTests`, `FoundationShellFormTests`; Desktop.Session tests | Actual Login-first package; masking, keyboard/focus, disabled/failed/restricted login, logout/relogin, idle expiry, delayed result from prior user, restart |
| Catalogs | `CatalogControlTests`, Catalog ViewModel tests | Products, Warehouses, Employees, Customers CRUD/active state; validation/errors/permissions; debounce, canceled/stale results, immediate Refresh, paging |
| Lookups | `GoodsReceiptControlTests` and `CatalogLookupViewModelTests` | Product/Warehouse/Employee change while Receipt open; same-process `CatalogChangedMessage` refresh, active-only new selection, historical selection retained; re-open fetches fresh state |
| Receipt | `GoodsReceiptControlTests`, editor/list ViewModels and Inventory SQL | Draft/new/save/reload/edit, dirty form, rowversion conflict, Post/duplicate Post, Post-only vs Write/Read/direct Deny, uncertain outcome reload without blind retry |
| Stock | `InventoryControlTests` and Inventory SQL | Balance and card filtering/order/paging/date, zero/empty/busy/errors, historical entity, independent ledger/balance reconciliation |
| Security | `SecurityAdministrationFormTests`, `PermissionActionBinderTests`, Security ViewModels/SQL | Protected superuser, role/direct decisions, import preview/apply stale revisions, sign-out/revocation and no-mutation on denial |
| Resilience / UX | `WinFormsTestInfrastructureTests`, `StaTest`, `WinFormsTestGuard`, `ReleasePackagingTests` | Bounded startup and SQL failures, unexpected modal/`DataError`, disposal, error rendering, theme, accessibility, keyboard, supported DPI matrix |

A claimed **full GUI** acceptance needs an explicit behavior-to-test ledger, not a coverage-percentage slogan. Create `docs/GUI_P5_COVERAGE_MATRIX.md` during T00, marking each behavior `existing automated`, `new automated`, `packaged E2E`, `attended`, `backend oracle` and any gap. A manual check cannot turn a missing mandatory automated case into PASS.

## 4. Task list

### GUI-P5-T00 — Reconcile source and baseline

- [ ] Read the current GUI/ViewModel implementations, Test Facts/Theory rows, permission keys, production DI, packaging and available UI Automation selectors. List all `MyDmsVn.Desktop.Tests` methods and separately record selected/discovered/executed/skipped cases. The historical 139 are not evidence that current GUI tests passed.
- [ ] Publish `docs/GUI_P5_COVERAGE_MATRIX.md` with exact source test mapping, uncovered control/packaged paths, owning task, proposed supported Windows/DPI/test SQL configurations and supported native UI Automation approach. Prove any external automation library works with net48/x86 in a small spike before adoption; never require brittle screen coordinates/global sleeps.
- [ ] Pin an approved disposable test topology, least-privilege runtime identity, secure credentials and cleanup procedure. Capture baseline supported/not-tested distinction.

**Exit:** reviewed coverage matrix with executable owners and explicit prerequisites.

### GUI-P5-T01 — Bounded GUI reliability foundation

- [ ] Retain `StaTest` STA/assembly serialization, `WinFormsTestGuard` ThreadException/DataError capture, safe error presentation and deterministic form/VM disposal. Add meaningful regression tests for async callbacks after close, error reporting and no unintended modal hangs.
- [ ] Fix the current `ReleasePackagingTests` subprocess wait without a bound. Add finite startup/execution/cancellation limits, safe stdout/stderr draining, termination/drain of actual owned child/descendant processes and failure reporting.
- [ ] `StaTest` cancels cooperatively then waits five seconds but cannot stop a noncooperative STA thread. Put fatal dialogs/crashes/noncooperative hang probes in a **GUI-owned bounded subprocess** (use Windows process/job ownership as necessary), with timeout/forced cleanup self-tests. This is **not** a resurrected generic SQL runner or cross-process test lease.
- [ ] On failure, stop rather than continue with a leaked process, dialog, data connection or poisoned session; preserve initial and cleanup failure evidence.

**Exit:** repeatable timeout/failure behavior without orphan GUI/PowerShell children or masked exceptions.

### GUI-P5-T02 — Full deterministic control and ViewModel tests

- [ ] Add missing real-control tests for Login/shell/session, all four catalogs, lookup refresh, Goods Receipt Draft/Post interactions, Stock Balance/Card, security administration, permission binder and keyboard/focus/theming/layout.
- [ ] Exercise rapid input, debounce, cancellation/stale completion, explicit Refresh and page reset; active/inactive/historical lookup with same-process messenger; dirty editor, concurrency token, permission revocation, disabled user and cross-session async completion.
- [ ] Use controlled fake clients for timing and pure Desktop.Session tests for behavior without controls. Preserve Application/SQL tests as separate strong authorization/posting oracles; fake calls never prove transaction atomicity.

**Exit:** every in-scope control path in the matrix has a named test or a justified attended-only limitation; focused GUI/Session tests run on net48.

### GUI-P5-T03 — Packaged, real-login UI Automation E2E

- [ ] Build with a clean source revision using [Publish-LocalRelease.ps1](../../deployment/release/Publish-LocalRelease.ps1); launch actual `MyDmsVn.Desktop.App.exe` from published x86 artifacts under the designated runtime principal. Separate admin migration/bootstrap steps run before starting; no `--smoke-test` bypass or injected `IDesktopSession`.
- [ ] Use stable UI Automation names/IDs and bounded event/state waits to drive real Login, four catalog actions, receipt Draft/save/reload/edit/Post, balance/card, duplicate rejection, logout/relogin/restart. Add accessible selectors only where needed without business shortcuts.
- [ ] Exercise critical negative paths: direct Deny/disabled user, stale receipt, invalid field input, changed lookups, SQL offline/timeout and uncertain Post outcome. Correlate UI with independent read-only SQL receipt/ledger/balance/permission/no-mutation assertions. Reload/check persisted state before any mutating retry.
- [ ] Own and bound the full UI process tree, each navigation step, log/exception capture and teardown. Drain GUI and SQL work before positively owned database cleanup. Failed or ambiguous ownership = retain/quarantine and report, not guessed deletion.
- [ ] If a mandatory E2E automation case cannot reliably run, keep it OPEN (while preserving manual evidence separately), not “manual pass implies automated pass”.

**Exit:** real packaged E2E scenarios, independent SQL oracles and cleanup pass on declared Windows configurations.

### GUI-P5-T04 — REMAIN-P5-GUI-01 attended Catalog/Lookup sign-off

- [ ] From the **installed Login-first package**, exercise all four catalog CRUD/active/permission/validation cases, rapid filter/cancel/stale-result behavior, Refresh/page reset, navigation, keyboard and errors.
- [ ] Keep receipt open during Product/Warehouse/Employee update/deactivate/reactivate; verify same-process lookup refresh, historical selected item, active-only new selection and fresh results on reopen/restart. Never claim cross-process messaging.
- [ ] Record per-step PASS/FAIL/BLOCKED/NOT RUN, package hash, OS/DPI/runtime, SQL version, actor/permissions, sanitized screenshots, tester/date. GUI automated evidence is listed separately.

**Exit:** `REMAIN-P5-GUI-01` PASS for every shipped target, else OPEN.

### GUI-P5-T05 — REMAIN-P5-GUI-02 attended Inventory sign-off

- [ ] From real Login, create catalog data → Draft/save/reload/edit → Post → inspect Stock Balance/Card → deny duplicate Post → independently reconcile ledger/balance.
- [ ] Exercise Post-only vs Write/Read, direct Deny, restricted/disabled user, rowversion conflict, invalid input, catalog changes, logout/other account/restart and failure/uncertain Post. Confirm no mutation on denial/failure and no blind Post retry.
- [ ] Record exact steps, safe results, controlled account/permission state, revision/artifacts and operator screenshots independently from backend test evidence. L2DB-T07's limited smoke does **not** close this checklist.

**Exit:** `REMAIN-P5-GUI-02` PASS for every shipped target, else OPEN.

### GUI-P5-T06 — Unfiltered GUI gate and evidence handoff

- [ ] On the **frozen candidate** run unfiltered `MyDmsVn.Desktop.Session.Tests` and `MyDmsVn.Desktop.Tests` on interactive Windows/Release/net48, plus the separate packaged E2E suite and agreed DPI/theme/failure matrix. Reconcile actual TRX Facts/Theory rows and pass/fail/skip/abort with discovered source; skipped/missing mandatory cases are blockers.
- [ ] Where a GUI defect changed business/SQL behavior, rerun affected Application/SQL coverage under TEST_POLICY. Do not replace real SQL concurrency, permissions or rollback with GUI mock assertions.
- [ ] Verify process, temporary identity, connection, disposable database, log and resource cleanup; publish sanitized `docs/GUI_P5_VERIFICATION.md` with commands, case ledger, photos/observations, artifact hashes and all outstanding issues.
- [ ] Check off tasks only with current-revision evidence. P5 completion does not approve the broader P6 release.

**Exit:** both P5 IDs PASS; every mandatory mapped GUI path has appropriate automated + attended proof; unfiltered GUI/Session matrix has no absent/skipped mandatory row and cleanup is verified. Otherwise this plan remains OPEN and P6 G4 remains OPEN.

## 5. Example direct final commands

Execute **sequentially** in an interactive Windows desktop. Use focused source-verified FullyQualifiedName filters during implementation; this unfiltered example belongs to T06, not every edit. Keep raw TRX local.

~~~powershell
$projects = @(
 'tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj',
 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
)
foreach ($p in $projects) {
  dotnet restore $p --locked-mode
  if ($LASTEXITCODE -ne 0) { throw "Restore failed: $p" }
  dotnet test $p -c Release -f net48 --no-restore --logger 'trx;LogFileName=gui.trx' --results-directory ('TestResults/gui-' + [guid]::NewGuid().ToString('N').Substring(0,8))
  if ($LASTEXITCODE -ne 0) { throw "Test failed: $p" }
}
~~~

For packaged GUI automation, use only a reviewed bounded child-process flow from T01/T03; no direct unbounded host launch. Missing Windows/SQL/GUI or a hung subprocess = BLOCKED/FAIL with cleanup details, never PASS. Follow [TEST_POLICY](../TEST_POLICY.md) for all scopes.

## 6. Explicit non-goals

No AppCore/.NET 8 reactivation, P7/HTTP, new business modules, SQL runner/test-gate rebuild, test-login bypass, production DB reset, secret-in-package or implicit migration. The distribution method is presently a versioned publish-directory, **not** an MSI or auto-updater. Signing, SQL/Windows support decisions, backup/restore and final G1–G5 live in [REL](20261019-002-local-v1-p6-release-acceptance.md).
