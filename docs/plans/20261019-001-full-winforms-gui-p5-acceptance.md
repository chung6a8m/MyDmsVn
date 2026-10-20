# GUI-P5 — Full WinForms GUI testing and P5 interactive acceptance

> **Historical completed plan:** GUI-P5-T00–T06 are complete in the approved scope. Preserve all candidate-specific results, failed trials, attended sign-offs and the explicit T05 Down NOT TESTED exception. This file is not an active execution queue; use the [current plan index](README.md) for the sole active BCI roadmap. Open-status statements in dated checkpoints below retain their historical meaning.

**Created:** 2026-10-19 · **Branch:** `v1a` · **Status:** COMPLETE in approved GUI-P5 scope — T00–T06 complete; both attended IDs Owner-accepted, with the explicit T05 Down NOT TESTED exception retained. [Production release prerequisites](../DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.
**Planning baseline:** merge `42d94baea1655dddbd46f971e806f84401cd1c63`. Follows completed [RESP](20261018-002-respawn-fixture-reuse.md) and [L2DB](20261018-001-linq2db-net48-persistence-migration.md). The [former REL plan](https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/docs/plans/20261019-002-local-v1-p6-release-acceptance.md) is linked only as immutable historical provenance; it has been removed from the active roadmap.

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

- [x] Read the current GUI/ViewModel implementations, Test Facts/Theory rows, permission keys, production DI, packaging and available UI Automation selectors. List all `MyDmsVn.Desktop.Tests` methods and separately record selected/discovered/executed/skipped cases. The historical 139 are not evidence that current GUI tests passed.
- [x] Publish `docs/GUI_P5_COVERAGE_MATRIX.md` with exact source test mapping, uncovered control/packaged paths, owning task, proposed supported Windows/DPI/test SQL configurations and supported native UI Automation approach. Prove any external automation library works with net48/x86 in a small spike before adoption; never require brittle screen coordinates/global sleeps.
- [x] Pin an approved disposable test topology, least-privilege runtime identity, secure credentials and cleanup procedure. Capture baseline supported/not-tested distinction.

**T00 execution (2026-10-19):** [Coverage matrix and complete source inventory](../GUI_P5_COVERAGE_MATRIX.md) at source `f275183c8b39dc2b712dc37be7f0f4ac71396357`: 138 methods / 139 discovered invocations; focused Login/infrastructure baseline 10 passed, 0 failed/skipped, remaining 129 NOT RUN. Native UIA is proposed; no external library adopted or packaged E2E claimed. Project Owner approved the **Minimal Disposable Test Topology** on 2026-10-19: Windows 11 x64 interactive, net48/x86, 1920×1080 at 100% plus 125%, Light/Dark; suitable existing non-production SQL or newly provisioned SQL Server 2022 Developer; separate positively owned `MyDmsVn_GuiP5_<GUID>` database/schema001–009 and provisioning/migration/runtime identities. Section 6 of the matrix pins secure credential delivery, cleanup, actual metadata and remaining provision steps. Shared SQL additionally needs DBA/instance-owner confirmation. **T00 COMPLETE:** reviewed inventory, approved design and explicit prerequisites; actual SQL/principal provisioning, 125%/theme matrix and packaged E2E are BLOCKED / NOT TESTED, not PASS. T01/T02 completion is recorded below; T03–T06/P5/P6 remained OPEN at that T00 checkpoint; current follow-up is recorded below.

**Exit:** reviewed coverage matrix with executable owners and explicit prerequisites.

### GUI-P5-T01 — Bounded GUI reliability foundation

- [x] Retain `StaTest` STA/assembly serialization, `WinFormsTestGuard` ThreadException/DataError capture, safe error presentation and deterministic form/VM disposal. Add meaningful regression tests for async callbacks after close, error reporting and no unintended modal hangs.
- [x] Fix the current `ReleasePackagingTests` subprocess wait without a bound. Add finite startup/execution/cancellation limits, safe stdout/stderr draining, termination/drain of actual owned child/descendant processes and failure reporting.
- [x] `StaTest` cancels cooperatively then waits five seconds but cannot stop a noncooperative STA thread. Put fatal dialogs/crashes/noncooperative hang probes in a **GUI-owned bounded subprocess** (use Windows process/job ownership as necessary), with timeout/forced cleanup self-tests. This is **not** a resurrected generic SQL runner or cross-process test lease.
- [x] On failure, stop rather than continue with a leaked process, dialog, data connection or poisoned session; preserve initial and cleanup failure evidence.

**T01 execution (2026-10-19): COMPLETE in scope.** [Verification](../GUI_P5_VERIFICATION.md#gui-p5-t01--bounded-gui-reliability-foundation) records candidate `32ea40c`, suspended/job-owned children and compiler, bounded execution/drain/cleanup, preserved diagnostics, late Login/UI-thread fixes and the reproduced deferred-dispatch test race. Final unfiltered Desktop: 149 methods / 153 invocations, **153 passed / 0 failed/skipped/timeout/aborted**, observed testhost32; Session x86: **108/108 passed**. Failed/aborted development trials are recorded separately. No package/SQL/P5/P6 sign-off is claimed.

**Exit:** repeatable timeout/failure behavior without orphan GUI/PowerShell children or masked exceptions.

### GUI-P5-T02 — Full deterministic control and ViewModel tests

- [x] Add missing real-control tests for Login/shell/session, all four catalogs, lookup refresh, Goods Receipt Draft/Post interactions, Stock Balance/Card, security administration, permission binder and keyboard/focus/theming/layout.
- [x] Exercise rapid input, debounce, cancellation/stale completion, explicit Refresh and page reset; active/inactive/historical lookup with same-process messenger; dirty editor, concurrency token, permission revocation, disabled user and cross-session async completion.
- [x] Use controlled fake clients for timing and pure Desktop.Session tests for behavior without controls. Preserve Application/SQL tests as separate strong authorization/posting oracles; fake calls never prove transaction atomicity.

**T02 execution (2026-10-19): COMPLETE in scope.** [Verification](../GUI_P5_VERIFICATION.md#gui-p5-t02--deterministic-control-and-viewmodel-coverage) records executable `3a0621c`: 59 new methods/129 invocations, named mapping for every L1–U1 control obligation and explicit visual/runtime limitations. Final unfiltered Desktop: 208 methods/282 discovered and executed rows, **282 passed /0 failed/skipped/unexpected timeout/aborted**; Session **108/108 passed**, Release/net48/x86. Fixes cover native Login retry focus, disposed shell callbacks, canceled permission grant, uncertain Post requiring explicit read reload, security selection/membership/import target changes and accessible Stock names. Independent review findings were reproduced and fixed. No actual SQL/package/DPI125/attended sign-off is claimed; T03–T06 and P5/P6 remained OPEN at that T02 checkpoint; T03 follow-up is recorded below.

**Exit:** every in-scope control path in the matrix has a named test or a justified attended-only limitation; focused GUI/Session tests run on net48.

### GUI-P5-T03 — Packaged, real-login UI Automation E2E

- [x] Build with a clean source revision using [Publish-LocalRelease.ps1](../../deployment/release/Publish-LocalRelease.ps1); launch actual `MyDmsVn.Desktop.App.exe` from published x86 artifacts under the designated runtime principal. Separate admin migration/bootstrap steps run before starting; no `--smoke-test` bypass or injected `IDesktopSession`.
- [x] Use stable UI Automation names/IDs and bounded event/state waits to drive real Login, four catalog actions, receipt Draft/save/reload/edit/Post, balance/card, duplicate rejection, logout/relogin/restart. Add accessible selectors only where needed without business shortcuts.
- [x] Exercise critical negative paths: direct Deny/disabled user, stale receipt, invalid field input, changed lookups, SQL offline/timeout and uncertain Post outcome. Correlate UI with independent read-only SQL receipt/ledger/balance/permission/no-mutation assertions. Reload/check persisted state before any mutating retry.
- [x] Own and bound the full UI process tree, each navigation step, log/exception capture and teardown. Drain GUI and SQL work before positively owned database cleanup. Failed or ambiguous ownership = retain/quarantine and report, not guessed deletion.
- [x] If a mandatory E2E automation case cannot reliably run, keep it OPEN (while preserving manual evidence separately), not “manual pass implies automated pass”.

**Exit:** real packaged E2E scenarios, independent SQL oracles and cleanup pass on declared Windows configurations.

**T03 execution (2026-10-19): COMPLETE in task scope.** [Final evidence, exact hashes and retained failures](../GUI_P5_VERIFICATION.md#final-frozen-candidate--gui-p5-t03) records clean package/test source `02eff225d41ff564769bedbb0ca4420d27233a67`, net48/x86, actual Windows11 build26200.9457 and SQL2017 Developer14.0.2130.4. Actual100% full suite passed312/312. Actual125% full suite recorded311 PASS/1 FAIL; the exact Light lookup row passed1/1 on unchanged binaries after requesting an exclusive desktop, with owner-confirmed window switching during the full run. All312 unique obligations have automated passing results at both scales; no unfiltered125%312/312 pass is claimed. Light/Dark packaged scenarios, independent SQL oracles and all35 final allocations' ownership/drain/cleanup passed; final exact database/principal/process census is zero. The foreground-resource incident and full-run failure remain explicit. T04/T05 attended sign-off, T06 full frozen acceptance and P5/P6 remain OPEN.

### GUI-P5-T04 — REMAIN-P5-GUI-01 attended Catalog/Lookup sign-off

- [x] From the **installed Login-first package**, exercise all four catalog CRUD/active/permission/validation cases, rapid filter/cancel/stale-result behavior, Refresh/page reset, navigation, keyboard and errors.
- [x] Keep receipt open during Product/Warehouse/Employee update/deactivate/reactivate; verify same-process lookup refresh, historical selected item, active-only new selection and fresh results on reopen/restart. Never claim cross-process messaging.
- [x] Record per-step PASS/FAIL/BLOCKED/NOT RUN, package hash, OS/DPI/runtime, SQL version, actor/permissions, sanitized screenshots, tester/date. GUI automated evidence is listed separately.

**T04 execution (2026-10-19): COMPLETE / OWNER ACCEPTED.** The [operator worksheet](../GUI_P5_T04_WALKTHROUGH.md) records49/49baselinePASS and24/24supplemental UX PASS (73applicable,132N/A by Owner scope). Historical28Catalog functional rows use02eff22; remaining21baseline/all24supplemental rows use candidate2d5207b2. Both100/125Light/Dark UX contexts are Owner-confirmed; artifacts/oracles/final U-05 verified. Both latest chapters closed normally/exit0/CleanupVerified;166/166GUI ledgers Dropped, exact latest database/principal/process census0. Project Owner explicitly accepted evidence commit bd59f6f: “Xác nhận nghiệm thu T04 theo biên bản.” Signature recorded21:52 +07:00; T04 COMPLETE / REMAIN-P5-GUI-01 PASS within the documented scope.

Actual old Receipt clipping was repaired in product source1b8bc57. Its filtered candidate1 regression307PASS/3FAIL remains recorded. Frozen candidate2 product/test source d5207b2 passed both100% Lookup Light/Dark rows2/2 within original3-minute subprocess budgets; earlier ReceiptDark working-tree trial passed1/1 on unchanged candidate1 product. Candidate2 attended same-process names, inactive history, active re-selection/real restart and ordinary direct-Deny/no-Read cases have Owner+oracle evidence. Dirty unsaved-Draft navigation is an existing guard; the operator instruction was corrected to close/restart without adding a Save, preserving original Draft2015. No full324-row or125% automated/full-suite candidate2 pass is claimed; attended125%UX observations are recorded separately.

The historical18:42 checkpoint had13T04 allocations/39principals disposed. The subsequent UX cohort also completed positive ownership cleanup: final14T04 allocations/42principals disposed,166/166GUI ledgers Dropped and exact latest database/principal/process census0. The worksheet preserves all failures, exact hashes/artifacts, credential-handling correction and accepted UI qualifications. T04 is Owner-accepted; overallP5/P6 and T05/T06 remain OPEN.
**Exit:** `REMAIN-P5-GUI-01` PASS for every shipped target, else OPEN.

### GUI-P5-T05 — REMAIN-P5-GUI-02 attended Inventory sign-off

- [x] From real Login, create catalog data → Draft/save/reload/edit → Post → inspect Stock Balance/Card → deny duplicate Post → independently reconcile ledger/balance.
- [x] Exercise Post-only vs Write/Read, direct Deny, restricted/disabled user, rowversion conflict, invalid input, catalog changes, logout/other account/restart and failure/uncertain Post. Confirm no mutation on denial/failure and no blind Post retry.
- [x] Record exact steps, safe results, controlled account/permission state, revision/artifacts and operator screenshots independently from backend test evidence. L2DB-T07's limited smoke does **not** close this checklist.

**T05 execution (2026-10-20): COMPLETE / OWNER ACCEPTED.** Owner explicitly accepted evidence commit5ed7bc6: “Nghiệm thu T05 theo biên bản, chấp nhận giới hạn Down đã ghi”. [Inventory worksheet](../GUI_P5_T05_WALKTHROUGH.md) records27baselinePASS and17supplementalPASS, plus1PARTIAL Warehouse125Dark cell with explicit Owner-accepted Down NOT TESTED exception. Other observed keyboard paths and both125Light/Dark groups confirmed; no untested substep is relabeledPASS. Frozen d5207b2/net48/x86, actualWindows11build26200.9457/SQL2017Developer14.0.2130.4/schema001–009;133installedfiles verified. Distinct originalGUI-createdfunctional and setup-onlycontinuation/page fixtures preserved. Finalrestrictedoracle33receipts59lines53ledger27balancekeys/base27/0full-key/line/documentmismatches. Finalchapterexit0/CleanupVerified, rootDisposed,169/169GUIledgersDropped/exactlatestdatabase-principals-process0; all3T05allocations/9principalsdisposed. Independentreview completed;80publicimages/hash/provenance retained. No new automatedpass or product/testbinarychange. T06/overallP5/P6 remainOPEN.

**Exit:** `REMAIN-P5-GUI-02` PASS within Owner-signed scope and documented exception.
### GUI-P5-T06 — Unfiltered GUI gate and evidence handoff

- [x] On the **frozen candidate** run unfiltered `MyDmsVn.Desktop.Session.Tests` and `MyDmsVn.Desktop.Tests` on interactive Windows/Release/net48, plus the separate packaged E2E suite and agreed DPI/theme/failure matrix. Reconcile actual TRX Facts/Theory rows and pass/fail/skip/abort with discovered source; skipped/missing mandatory cases are blockers.
- [x] Where a GUI defect changed business/SQL behavior, rerun affected Application/SQL coverage under TEST_POLICY. Do not replace real SQL concurrency, permissions or rollback with GUI mock assertions.
- [x] Verify process, temporary identity, connection, disposable database, log and resource cleanup; publish sanitized `docs/GUI_P5_VERIFICATION.md` with commands, case ledger, photos/observations, artifact hashes and all outstanding issues.
- [x] Check off tasks only with current-revision evidence. P5 completion does not approve the broader P6 release.

**T06 execution (2026-10-20): COMPLETE.** [Final frozen evidence](../GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20): productd5207b2 / test12e6a20, net48/x86; Session108/108, unfilteredDesktop100%329/329 and125%329/329, separatepackaged18/18, zero missing/skipped/non-PASS rows. Total784PASS invocations /437distinct obligations. One fresh whole-branch review found a SQL-quarantine fail-stop gap, fixed with actual3-rowRED→GREEN and both full gates; no second review claimed. Final exact all-T06 census189DB/567principals absent,358/358historicalledgersDropped/ownedprocess0. T04/T05 signatures and Down exception preserved; historical failed trials retained. T06 changed only test infrastructure, so no additional business/SQL implementation rerun was triggered. P5 is complete within the approved scope; REL/P6 G1–G5 remain OPEN.

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

No AppCore/.NET 8 reactivation, P7/HTTP, new business modules, SQL runner/test-gate rebuild, test-login bypass, production DB reset, secret-in-package or implicit migration. The distribution method is presently a versioned publish-directory, **not** an MSI or auto-updater. Signing, SQL/Windows support decisions, backup/restore and final G1–G5 remain [unaccepted, unscheduled release prerequisites](../DEFERRED_WORK.md#release-prerequisites). The [former REL plan](https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/docs/plans/20261019-002-local-v1-p6-release-acceptance.md) is historical provenance only.

T04 checkpoint: Owner selected full100% Light functional baseline plus supplemental layout/keyboard/errors/popup UX at100% Dark and125% Light/Dark. Customers chapter5 exceeded its5-minute supervisor before complete artifacts; the later30-minute supervised replay now has complete C-C01–04 evidence and Owner confirmation. Test-only RunAttendedAsync adds an explicit30-minute cap using existing cleanup; automated RunAsync retains5minutes. Focused owned-process typed tests passed16/16; product/package02eff22 remains unchanged. T04 and P5 remain OPEN.

T04 layout checkpoint16:40: 28 attended baseline PASS remain historical02eff22 evidence, but U-02 is FAIL: real Receipt header clipped even maximized at100% Light. Lookup/permission/supplemental work and final sign-off remain OPEN. All4 T04 databases/12 principals and owned processes drained/dropped (exact census0). Minimal Receipt layout repair has focused78/78 PASS and independent code review; new clean package and actual affected GUI/DPI verification are pending. No old hash/result is relabeled as a new-candidate pass.

T04 candidate1 checkpoint17:01: frozen source1b8bc57/package1.0.0-guip5-t04 has filtered100% regression307PASS/3FAIL/0SKIP of310;306non-packagedPASS, ReceiptLightPASS, ReceiptDark popup-readinessFAIL and bothLookuprows owned3-minuteTimeout. All four regression allocations/12principals cleaned, exact DB/principal/process census0 (eight T04 allocations/24principals dropped total). Test-only diagnostic on unchanged product is in progress; no repaired-candidate attended/125%/full-GUI pass or T04 sign-off is claimed. See worksheet for hashes and preserved failures.

T05 current acceptance checkpoint2026-10-20: Owner explicitly confirmed both125Light/Dark observations. Canonical worksheet44/45PASS (27baseline+17supplemental),1Warehouse125DarkPARTIAL solelyDown NOT TESTED; other observed paths confirmed. Finalreview complete and cleanup evidence unchanged. FinalOwnerT05signature/explicit disposition of narrowDownlimit pending; T05/REMAIN-P5-GUI-02/T06/P5/P6 remainOPEN. Earlier checkpoints are historical.

### Signed T05 status supersedes prior checkpoints — 2026-10-20 11:45 +07:00

Project Owner explicitly accepted evidence commit5ed7bc6: “Nghiệm thu T05 theo biên bản, chấp nhận giới hạn Down đã ghi”. GUI-P5-T05 COMPLETE / REMAIN-P5-GUI-02 PASS within the documented, Owner-accepted scope. Outcome remains44PASS and1PARTIAL observation cell; the Warehouse125DarkDown substep remains NOT TESTED with an explicit Owner-accepted exception, never converted into a testPASS. The other observed paths and both125Light/Dark groups are confirmed.

Frozen d5207b2 candidate/cohort provenance,80publicimages, independent oracle/reconciliation and final owned cleanup remain as evidenced above. All3T05allocations/9principals disposed,169/169GUIledgersDropped, exactlatest database/principal/process0. This signature changes documentation only; no GUI/SQL/test execution or new automatedpass is claimed. T06, overallP5, REL/P6 G1–G5 and productionreadiness remainOPEN. Historical pending checkpoints above are superseded by this signed decision.

