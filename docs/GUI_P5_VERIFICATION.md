# GUI-P5 verification

This is task-scoped evidence, not full P5/P6 acceptance. [Coverage matrix](GUI_P5_COVERAGE_MATRIX.md), [plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md) and [TEST_POLICY](TEST_POLICY.md) govern. T03–T06, both attended P5 IDs and REL G1–G5 remain OPEN. T01 and T02 task-scoped evidence follows.

## GUI-P5-T01 — Bounded GUI reliability foundation

**Date/tester:** 2026-10-19 (Asia/Bangkok), Codex. **Base:** `6fdcbfe`. **Implementation:** `ab41cd8fb2e25b27e88a23e28593cbbef4057482`, followed by bounded probe compilation at **`32ea40cb90d9f2c2525821d016c96cd33e10321b`**. **Branch/worktree:** `codex/gui-p5-t00`, managed `gui-p5-t00/CleanArchitecture`. Documentation follows the tested executable source; no release package was produced.

**T01 COMPLETE in scope:** actual bounded failure probes, full current Desktop regression and Session regression passed on the declared developer desktop. This does not complete real packaged Login, independent SQL E2E, attended walkthroughs, DPI/theme acceptance or production certification.

### Changes and boundaries

- [GuiOwnedProcess](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcess.cs) creates a root suspended with `CreateProcess`, assigns it to a Windows Job Object before resuming, and restricts inherited handles to its stdin/stdout/stderr allowlist. The job denies breakaway and kills members on close. Cleanup terminates the exact job and verifies both root exit and zero active job members, rather than killing by executable name/PID prefix. Parent exit with surviving descendants is a reported failure even when forced cleanup succeeds.
- A finite caller budget covers launch/startup and execution, with cancellation checks while observing the root; cleanup has a separate five-second budget independent of caller cancellation. Packaging uses 60 seconds; probe compilation uses 30; fatal/hang/modal probes use their explicit small budgets. No parameterless `WaitForExit()` remains in packaging tests. Native creation is a synchronous local Windows call; this helper is not a general remote/network executable launcher or suite watchdog.
- Both redirected streams drain concurrently to EOF; each retains at most 32,768 characters while continuing to consume excess data. A synchronized capture snapshot survives pipe failure or incomplete cleanup. Failure reports preserve the original timeout/cancellation/observation exception, cleanup exception when present, observed exit code and available output. Unexpected cleanup poisons further `StaTest`, guard and owned-child admission in this testhost; fixture artifacts are retained rather than deleted on uncertain process cleanup. The latch is testhost-local, not a global lease or replacement test runner.
- [StaTest](../tests/MyDmsVn.Desktop.Tests/StaTest.cs) retains serialized STA execution and five-second cooperative cleanup. Cancellation callbacks run off the test thread and share that cleanup budget; an action cleanup exception remains attached to the timeout. Noncooperative STA/callback cleanup poisons GUI admission; it cannot forcibly stop managed threads. Such destructive/hang probes belong in the owned subprocess, as implemented here. [WinFormsTestGuard](../tests/MyDmsVn.Desktop.Tests/WinFormsTestGuard.cs) still captures ThreadException/DataError and suppresses fatal error dialogs on its guarded thread.
- The embedded [probe source](../tests/MyDmsVn.Desktop.Tests/GuiProcessProbe.cs) is excluded from testhost compilation. Its fixture invokes the installed .NET Framework x86 compiler through the same bounded job and creates a temporary x86 executable. Modes exercise output flooding, noncooperative STA, a real MessageBox, child/grandchild lifetime, root exit with descendants, and a synthetic fatal crash. This is a GUI-owned test fixture in the existing project; no new test project, external automation dependency, SQL framework, product switch or authentication shortcut is introduced. The compiler is a test-machine prerequisite, not a shipped product dependency.
- [LoginViewModel](../src/MyDmsVn.Desktop.Application/LoginViewModel.cs) keeps the caller's UI synchronization context for completion, rejects canceled results and suppresses late state/error/notification publication after disposal. Form-owner-thread tests establish ordinary WinForms publication/disposal serialization; this does not authorize cross-thread control disposal or change backend session/admitted-command semantics. Reporter failure still yields safe inline error/support ID.
- A newly reproduced race in [GoodsReceiptEditorViewModelTests](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs) could leave a save waiting on a second deferred notification: the fake dispatcher released an asynchronous continuation before switching to immediate mode. The test now publishes immediate mode before releasing the callback and bounds its wait. This changes test scheduling only; Inventory production code/SQL is unchanged.

### Environment and prerequisites

The approved [Minimal Disposable Test Topology](GUI_P5_COVERAGE_MATRIX.md#6-disposable-topology-credentials-and-cleanup-contract) remains authoritative: Windows 11 x64 interactive, `net48`/x86 application, 1920×1080 at 100%, supplemental 125%, Light/Dark. T01 used the observed Windows 11 Home Single Language 25H2 build `26200.9457`, SDK `8.0.425`, installed v4 registry Version `4.8.09221` / Release `533509`, primary display 1920×1080 at 100%. No display settings were changed; the full 125%/theme acceptance matrix remains NOT TESTED.

VSTest console reports x64, while the final Desktop TRX's `StaTest_runs_action_on_sta_thread` output independently records **observed testhost bitness 32**. The output-flood probe also asserts **BITNESS=32** from the actual child. Vendor pins, product target frameworks and package dependencies are unchanged. No test SQL connection, disposable database, SQL identity, runtime credential, migration or protected account was created or used. Existing SQL/shared-instance/principal prerequisites remain as documented by T00.

### Commands and accepted results

Run direct projects sequentially from the worktree root. Both projects were restored using `--locked-mode` before testing. The final source was built before `--no-build` discovery. Keep matching build/testhost bitness; when switching to AnyCPU/x64 in the same output directory, rebuild the affected graph or use separate output paths to avoid reusing x86 dependencies.

```powershell
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet restore tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj --locked-mode

dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=t01-bounded-compiler-desktop-x86.trx' --results-directory TestResults/gui-p5-t01 --blame-hang --blame-hang-timeout 45s --blame-hang-dump-type none -- RunConfiguration.TargetPlatform=x86

dotnet test tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --logger 'trx;LogFileName=t01-final-session-login-x86.trx' --results-directory TestResults/gui-p5-t01 -- RunConfiguration.TargetPlatform=x86

dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --no-build --list-tests -- RunConfiguration.TargetPlatform=x86
```

Built-in VSTest blame-hang was used for diagnosis of the reproduced test race; it is not the subprocess ownership mechanism. All tests finished in the final run, so it generated no incomplete sequence or hang dump. Raw TRX/logs remain local and are not committed.

| Owner/scope | Source/discovered/selected/executed | PASS / FAIL / skipped / timeout / aborted | Final evidence |
|---|---|---|---|
| Desktop Release/net48/x86, unfiltered | 149 methods: 146 Facts + 3 Theories (7 rows); 153 discovered / selected / executed | **153 / 0 / 0 / 0 / 0**, ResultSummary Completed | `TestResults/gui-p5-t01/t01-bounded-compiler-desktop-x86.trx`, about 34 seconds total command time |
| Desktop.Session Release/net48/x86, unfiltered | 108 selected / executed cases | **108 / 0 / 0 / 0 / 0**, ResultSummary Completed | `TestResults/gui-p5-t01/t01-final-session-login-x86.trx`; product source identical to final code, subsequent change only owns compiler fixture in Desktop.Tests |

Desktop source method/row names and discovery were reconciled with the final TRX (including all Theory rows). **14 new invocations** relative to T00: GuiOwnedProcess has 6 new Facts + 3 rows of one new timeout Theory (9 invocations); LoginForm has 2 new Facts + 2 rows of one new Theory (4); STA cleanup has 1 new Fact. The exact additions are listed in the coverage matrix; existing 139 invocations remain present.

Final Desktop test assembly SHA-256 (test artifact, not a release package): `2200BA2CBF3EBE3C07796DBF1535F17AD55FE489AF6434C0572A4642B2F77491`.

### Development failures, cleanup and review

Development runs are not folded into the accepted pass totals:

| Observation | Outcome / resolution |
|---|---|
| Initial process API stub | Output-drain regression RED with NotImplementedException, then native implementation GREEN |
| Initial probe bootstrap | Wrong DLL for `WerSetFlags` caused CLR exit before READY; corrected against the [Windows API declaration](https://learn.microsoft.com/en-us/windows/win32/api/werapi/nf-werapi-wersetflags). Child execution/drain remained bounded |
| Initial Login test waits | Blocking the STA prevented its captured continuation; tests now pump messages within a finite bound |
| Actual late Login results | Two rows RED: late success authenticated disposed VM, late failure published an error; fixed disposal/cancellation suppression and observed GREEN |
| STA cleanup exception | RED: timeout lost its cleanup exception; fixed preservation and observed GREEN |
| Review P2 diagnostics | Fake pipe RED: consumed READY text was lost after read error; capture now survives failed/incomplete drains and regression passes |
| Full development trial hung | Manually stopped only the positively identified worktree testhost after verifying its parent command/test source/results path. 110 completed cases, run **ABORTED**, not accepted despite VSTest's misleading partial “Passed” line. No probe/PowerShell child remained at inspection. Controlled deferred-dispatch interleaving then reproduced the missing-consumer race with a bounded RED test; order/visibility/wait fix verified in final unfiltered run |
| Login owner-thread regression | RED: background completion published IsAuthenticated from a worker; removing ConfigureAwait(false) restored the form context, observed GREEN |
| Session mixed-bitness trial | 9 failures with BadImageFormatException after x86 dependency outputs were reused by default x64 testhost; final matching x86 build/testhost run passed all 108. No product/API fix or dependency change was needed |

Owned job tests verify zero active members/root exit and drained pipes on normal, timed-out, canceled and descendant-survival paths; PID absence assertions are additional observations, not cleanup authority. Successful fixture teardown deletes only its own generated directory. Final narrow inspection found **0 probe processes, 0 probe fixture directories and 0 release fixture directories** under these test roots. No unresolved native job/GUI/PowerShell child, quarantine or SQL allocation was reported in accepted runs. Tests intentionally exercising process timeout/crash pass by asserting the expected result and verified teardown; these are not VSTest timeout/abort outcomes. The pipe-reader fault regression uses a controlled reader and does not leave a live child or poison the real session.

Independent read-only review identified and then rechecked the retained-output P2, owner-thread behavior and fake-dispatch race; final review found no blocker. A subsequent review also accepted moving compiler creation into the bounded job. At the T01 exit, coverage gaps and packaged/attended proof remained T02–T06; T02 closure follows below. This developer-host result is not a release support/sign-off matrix.

## GUI-P5-T02 — Deterministic control and ViewModel coverage

**Date/tester:** 2026-10-19 (Asia/Bangkok), Codex. **Base:** `7941124`. **Tested executable revision:** `3a0621c65a2b584d2dfbe99b01febe915769bb44`. **Isolation:** retained managed worktree `gui-p5-t00/CleanArchitecture`, branch `codex/gui-p5-t00`; original checkout untouched. No release package was created.

**T02 COMPLETE in scope.** [Coverage matrix Appendix C](GUI_P5_COVERAGE_MATRIX.md#appendix-c--gui-p5-t02-deterministic-control-closure) maps every L1–U1 control obligation to named new/existing Desktop and Session tests, with explicit visual/runtime limits. T03–T06, real packaged Login, independent SQL E2E, both attended P5 IDs and REL G1–G5 remain OPEN.

### Implemented behavior and boundaries

- Added seven acceptance classes in the existing Desktop.Tests project: **59 methods / 129 new invocations**. Actual controls exercise all four catalog adapters/fields/CRUD/active states, controlled debounce and cancellation/stale results, immediate Refresh and page contracts, fresh/historical lookups, receipt dirty/conflict/Post/duplicate/authorization responses, both Stock controls, security management/import and native tab/focus/read-only/theme/layout behavior. Existing pure Session tests remain the oracle for control-independent state machines; no new framework, dependency, runner or product target was introduced.
- Login now has distinct Username/Password accessibility names and returns native focus to the first invalid input or password for retry after busy inputs re-enable. Focus is deferred through the UI queue, with disposed/busy guards. Tests verify native child focus, safe response messages (including inactive/expired-session response binding), masking, Enter duplicate suppression, and Escape's Cancel result followed by form closure/drain. This modeless control evidence does not claim actual modal packaged Login or backend account enforcement.
- Closing a shell disposes/cancels its FoundationViewModel and suppresses old status/error publication. A canceled permission request cannot enable an action even if its client ignores cancellation. Four integrated Receipt/shell rows prove direct user switch or sign-out cancels/disposes a pending Save; late success/failure cannot affect the next user's workspace, input, status or shared notifications.
- An ambiguous Post exception, cancellation after dispatch, or InternalServerError/Unspecified response locks receipt mutation and requires explicit successful read reload of that same receipt before another mutation. A denied/failed reload retains the lock and owned receipt identity while clearing sensitive details; recovered Read can resolve Draft versus Posted. There is no automatic mutating retry. These are desktop workflow safeguards; SQL atomicity, exactly-once posting and persisted outcome remain independent backend/T03 assertions.
- Security grids preserve selected identities while rebinding. Programmatic membership updates cannot dispatch commands; protected/deprecated explanation text is displayed. Import uses injectable JSON/confirmation delegates for deterministic control tests; the default constructor retains native file/confirmation dialogs. Selection generation rejects late previews after target changes, including User/Role mode changes. Moving columns within the same row preserves unsaved fields/password and valid preview.
- Stock filters expose explicit Warehouse ID/Product ID, balance search and UTC date accessibility names. Vendor pins, contracts, schema001–009, SQL permissions/identities, server authorization/posting and package composition are unchanged.

### Actual environment, execution and reconciliation

Reverified developer host: Windows 11 Home Single Language 25H2 build **26200.9457**, x64 interactive desktop/session 38, primary **1920×1080** at the previously observed **100%** baseline; SDK 8.0.425; installed v4 Version4.8.09221 / Release533509. No display settings changed. Light/Dark structural render/tab/focus assertions ran on this desktop; full visual acceptance and supplementary 125% remain **NOT TESTED**. No supported-OS certification is inferred from this host.

Project references, package dependencies and locked restore inputs are unchanged from T01. Direct projects were built/tested sequentially with explicit net48/x86; no SQL project or milestone release matrix was run. Final commands:

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=t02-final-desktop-x86.trx' --results-directory TestResults/gui-p5-t02 --blame-hang --blame-hang-timeout 45s --blame-hang-dump-type none -- RunConfiguration.TargetPlatform=x86

dotnet test tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --logger 'trx;LogFileName=t02-final-session-x86.trx' --results-directory TestResults/gui-p5-t02 -- RunConfiguration.TargetPlatform=x86

dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --no-build --list-tests -- RunConfiguration.TargetPlatform=x86

dotnet test tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj -c Release -f net48 --no-restore --no-build --list-tests -- RunConfiguration.TargetPlatform=x86
```

| Scope | Source / discovered / selected / executed | PASS / FAIL / skipped / unexpected timeout / aborted |
|---|---|---|
| Desktop Release/net48/x86, unfiltered | 208 methods: 177 Facts + 31 Theories / 105 rows; **282 / 282 / 282** invocations | **282 /0 /0 /0 /0**, TRX Completed; about 63s command time |
| Desktop.Session Release/net48/x86, unfiltered | Existing 108 discovered/selected/executed invocations; no new source cases | **108 /0 /0 /0 /0**, TRX Completed |

All 282 Desktop and 108 Session discovered display names match their final TRX exactly (zero differences), all 59 new methods match their 129 source rows, and all 153 earlier T01 display names remain present. Actual `StaTest_runs_action_on_sta_thread` TRX output records **observed testhost bitness 32**; existing owned-process tests assert x86 probe bitness and verified cleanup. Final Desktop test assembly SHA-256: `4D1549BBA7B86AB3C8F935D543785ED0C85871B694DA2FDC5ACA0A7570F31C1B` (test artifact, not a release package).

Raw TRX remain local under `TestResults/gui-p5-t02/`; discovery/build/run logs are local `gui-p5-t02-*.log` files in the operator's temporary directory. Raw SQL IDs, credentials and screenshots are not committed. There is no package/hash/real-Login/SQL-pass claim for T02.

### Development failures, cleanup and review

Development trials are retained separately; filenames containing “green”, “fixpass” or “final” do not override their actual failed TRX outcome:

| Local development TRX | Executed | PASS | FAIL |
|---|---:|---:|---:|
| `t02-controls-final.trx` | 129 | 125 | 4 |
| `t02-controls-fixpass.trx` | 121 | 107 | 14 |
| `t02-controls-green.trx` | 121 | 118 | 3 |
| `t02-controls-red.trx` | 103 | 72 | 31 |
| `t02-login-deferred-focus.trx` | 15 | 14 | 1 |
| `t02-login-diagnosis.trx` | 4 | 0 | 4 |
| `t02-login-focus.trx` | 15 | 10 | 5 |
| `t02-login-native-focus.trx` | 15 | 11 | 4 |
| `t02-receipt-session.trx` | 4 | 4 | 0 |
| `t02-review-red.trx` | 17 | 10 | 7 |

Initial control RED exposed real missing focus/names, old-shell callback publication, canceled binder grant, ambiguous-Post retry and security selection/binding/import defects. Later trials isolated composite textbox wrapper versus native child focus, asynchronous retry-focus restoration, modeless Escape semantics and unshown-shell test setup. Those test assumptions were corrected without weakening the control contract. Two independent review findings were reproduced RED: horizontal same-row navigation reset unsaved values; User/Role target change allowed an old preview confirmation. Both now have named passing regressions. Initial compile/analyzer issues in new test code were fixed before execution; no build failure was counted as passing coverage.

All real-control cases ran through serialized STA/WinFormsTestGuard and finite operation waits. Pending fake requests and old-session work drain before teardown. Existing T01 job-owned process/PowerShell/compiler/fatal/modal/hang probes passed cleanup assertions in the unfiltered run; follow-up process inspection found **zero remaining owned GUI probes/testhosts**. No SQL connection, disposable database, principal, runtime credential or protected account was allocated, so no DB cleanup was attempted. Ambiguous database ownership rules and SQL provision steps remain unchanged.

Independent Codex code review found the two security issues above; both were fixed after RED proof, then re-reviewed with no further important findings. Review is not runtime proof; the accepted evidence is the final candidate's 282 Desktop and 108 Session passes. Backend SQL/package/DPI/attended prerequisites remain explicitly separate in the matrix and acceptance plan.

## GUI-P5-T03 — Packaged real-login E2E candidate (acceptance OPEN)

**Implementation:** `30171015f46fa553d9524329038b1b9afd95e727`; branch `codex/gui-p5-t00`, managed worktree `gui-p5-t00/CleanArchitecture`. **Date/tester:** 2026-10-19 (Asia/Bangkok), Codex. The frozen candidate and100% regression results are recorded below; independent review is complete. Actual125% Light/Dark execution is still pending, so no T03/P5/P6 completion is asserted here.

### Approved topology and actual provision

The Project Owner approved the private, owner-managed non-production SQL instance for T03. Shared-instance DBA approval is therefore not a missing prerequisite for this instance. Windows 11 x64 interactive, net48/x86, 1920×1080/100% plus supplemental 125%, and Bootstrap Light/Dark remain the approved matrix; 150% is outside the mandatory matrix. The owner agreed to switch the desktop to 125% after the 100% runs stop and drain. The owner subsequently confirmed the125% transition; the run and its initial failure are recorded below.

The existing SQL Server is **2017 Developer, version 14.0.2130.4 (64-bit)**, verified through server metadata. SQL Server 2022 Developer remains the target only if a new instance is provisioned. No new SQL instance, Docker/Testcontainers, test project, runner/framework or net8 product was introduced. The T01/T02 host observations above remain the baseline; final candidate metadata must be recorded again with the matrix. These observations establish a tested developer topology, not general OS/SQL production support.

[GuiSqlEnvironment](../tests/MyDmsVn.Desktop.Tests/GuiSqlEnvironment.cs) reuses the existing positively owned SQL helper through an explicit GUI allocation factory. Each SQL-backed case creates a separate `MyDmsVn_GuiP5_<GUID>` database and three temporary SQL logins: migration/maintenance, limited runtime and read-only oracle. The existing provisioning identity uses effective `master` only for infrastructure. Packaged DbUp/migration and administrative protected-user bootstrap run before Desktop; current scripts001–009 are required. Only the migration identity has `db_owner` in that disposable database. Runtime receives the existing `MyDmsVnRuntimeRole` grants and no DDL, `db_owner` or server administration. Oracle receives schema SELECT only. Server role/grant, SID/create-date, schema, protected binding and database ownership checks run against SQL, not a test session substitute.

Compiler and probe launches exclude inherited SQL/security configuration. Runtime configuration is explicitly supplied to the UIA probe; the actual Desktop child receives the limited application database connection and configured protected username, while probe-only passwords/barriers are removed. Passwords remain in memory/environment, never command arguments, committed files or logs. The unavailable-endpoint case uses a nonexistent SQL credential against loopback port1; it does not send the Windows provisioning identity.

Cleanup verifies live instance/database identity, creation metadata and ownership marker, then verifies and disables only the exact captured temporary principals, waits for owned SQL sessions to drain, drops the positively owned database and verifies exact login removal. GUI job/root/output drain precedes SQL cleanup. Uncertain ownership or cleanup retains/quarantines resources. No deletion is authorized by a database-name prefix. A poisoned GUI testhost is rejected before SQL dependency/configuration/allocation work; its admission self-test runs in an isolated child.

### Executable behavior ledger

All methods below belong to [PackagedGuiAcceptanceTests](../tests/MyDmsVn.Desktop.Tests/PackagedGuiAcceptanceTests.cs). The source defines **18 expected invocations per configuration**: sixteen SQL-backed native UIA rows, one SQL-only topology fact and one standalone unavailable-endpoint native UIA fact. Discovery/execution totals must be reconciled against the final TRX before acceptance.

| Method | Expected rows | Independent observation/oracle |
|---|---:|---|
| `Disposable_gui_topology_separates_provisioning_migration_runtime_and_readonly_oracle` | 1 | Actual principals, permissions, schema9, binding, live ownership and engine metadata |
| `Actual_packaged_login_uses_native_uia_and_authenticates_against_the_owned_database` | 2 | Real Login, masking, shell identity, logout/relogin and process restart; actual runtime isolation |
| `Catalog_rows_support_semantic_uia_selection_and_persist_create_edit_active_changes` | 2 | Product/Warehouse/Employee/Customer create/edit/deactivate/reactivate; read-only SQL final fields |
| `Packaged_receipt_draft_reload_edit_post_and_stock_reconcile_with_readonly_sql` | 2 | Draft/save/reload/edit/Post; second actual client's stale-Draft Post rejected; readonly Balance/Card rows and independent receipt/ledger/balance reconciliation |
| `Two_actual_clients_reject_stale_receipt_save_and_preserve_losing_input` | 2 | Concurrent winner persists, losing input remains visible, one Draft and no stock mutation |
| `Disabled_login_and_server_direct_deny_reject_a_previously_enabled_write_without_mutation` | 2 | Disabled real login denied; initially enabled Write is revoked by direct Deny before an actual server request; role/grant/deny plus no-mutation SQL |
| `Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh` | 2 | Original offscreen Product/Warehouse/Employee editors change before any receipt-tab activation; historical selections survive inactivity; new selection rejected; real process reopen fetches fresh names |
| `Real_sql_post_timeout_locks_mutation_until_explicit_reload_and_checked_retry` | 2 | Owned runtime WAITFOR observed in SQL; uncertain Post locks mutation; Draft/zero-stock checked before and after explicit UI reload, then one explicit retry; fault removed and startup revalidated |
| `Invalid_receipt_fields_preserve_input_and_create_no_inventory` | 2 | Zero quantity/missing references rejected, entered note retained, no receipt/line/ledger/balance created |
| `Unavailable_sql_startup_is_safe_owned_and_drains_under_native_uia` | 1 | Actual safe startup dialog, no connection/SQL/credential details, expected exit and process drain |

Light/Dark are application Bootstrap modes, read back from the actual theme action. The Dark denied-login path first authenticates, changes theme and signs out so the anonymous failure is exercised in Dark too. The first launch uses the application's default Login theme. Attended visual/keyboard acceptance remains a separate T04/T05 obligation.

[GuiPackageProbe](../tests/MyDmsVn.Desktop.Tests/GuiPackageProbe.cs) uses installed native UIAutomationClient/UIAutomationTypes/WindowsBase and the existing bounded x86 compiler/owned subprocess. Actual Desktop starts from a clean published package with no `--smoke-test`, session injection or business API shortcut. Inputs/actions use semantic names and unique results. Lookup confirmation sends Enter only to a verified focused HWND belonging to the exact owned process; no screen coordinates/global SendKeys are used. Predicate waits are finite (normally15s, Login30s, SQL-fault outcome45s), app close5s, compiler30s and GUI roots90/120/180s. The supervisor now allows a500ms natural tree drain inside the caller budget before rejecting persistent descendants; forced cleanup still has its independent5s budget and fail-stop latch.

Required accessibility additions stay in Desktop.WinForms and leave vendor files pinned: semantic readonly row/open actions, column Help, an HWND-backed theme button/current mode, numeric value adapters with range/precision/disabled guards, lookup committed/pending status and stock result names/ID filters. Native E2E also reproduced a receipt defect: asynchronous lookup result binding erased pending user text. Binding now preserves pending text for an unchanged selection, while explicit New/receipt selection/line changes cancel that pending input. Focused RED/GREEN regression covers preservation and explicit discard.

The real SQL timeout is an owned-database-only temporary trigger installed **after** compatible startup and authenticated draft preparation. The strict production startup contract is preserved. The test verifies trigger identity/definition and database ownership before removing it, checks persisted state before releasing any mutating retry, then restarts the actual app after removal. No instance shutdown, global session kill or recovery database is involved.

### Development checkpoints, not the frozen acceptance result

- `t03-stock-fault-lookup-native.trx`: **3/3 PASS**, Light/100%, using clean product package `gui-p5-t03-stock10` at source `121409b`, with evolving test sources. These cover the Post/Stock/duplicate, real SQL timeout/reload/retry, and same-process lookup/history/reopen paths.
- `t03-stock-stale-timeout-native.trx`: stale two-client case **PASS**; two other rows **FAIL** in that development run. Fault placement was moved after startup; finite Login waits and safe outcome classification were refined. Failed rows are not counted as acceptance passes.
- Earlier development TRX separately establish real Login lifecycle, all four catalog flows, disabled/direct-Deny and invalid-input cases. They span intermediate revisions and do not form a combined frozen-candidate pass.
- Focused receipt/lookup control regression: **53/53 PASS**; stock accessibility/control regression: **18/18 PASS**. Final process/environment/poison admission plus safe startup probe: **13/13 PASS**. The topology/context follow-up: **2/2 PASS**. Raw TRX, package outputs, allocation identities and diagnostics remain local/ignored.

### Frozen candidate — 100% complete; 125% pending

The clean candidate was published with the existing `deployment/release/Publish-LocalRelease.ps1 -Version 1.0.0-guip5-t03` under the bounded supervisor. Manifest source is **`e412ebc41cce554953585d5d1ec4f3839a301e42`**, `sourceTreeDirty=false`, schema maximum9, exactly the Desktop and admin migrator net48/win-x86 packages. Local output is `artifacts/release/gui-p5-t03-candidate1/`. The test sources at that same revision were built Release/net48/x86; package rows require the matching `MYDMSVN_GUI_P5_TEST_REVISION` and log that exact revision. Documentation-only follow-up does not replace these tested executable bytes.

| Artifact | SHA-256 |
|---|---|
| `release-manifest.json` | `17173D89D9C76B56F8885C5DD3A021E735F9FC35AE13B9CB5440AF99A4E299F1` |
| Desktop `MyDmsVn.Desktop.App.exe` | `7D8EDF0417E6C52ECF1914BC053AD2C0F364B479E29DF9DE7DA8C12F9C48F029` |
| Desktop `MyDmsVn.Desktop.WinForms.dll` | `70B5DEFE437A6FBB7015D9AEFA7E65FD1E49620E1CADCE828021707F71772956` |
| Admin `MyDmsVn.Server.DbMigrator.exe` | `3F1729310A266FFC451727E3005FC4C3AA2DA414D9DC7E19411E373E1DAEF5F4` |
| Test `MyDmsVn.Desktop.Tests.dll` | `89B9D0C1D1EB8DA80BD8FA75E71F484CDEEF24D6E0CF9431F0D8574D88DA8E1C` |

All manifest-listed package file lengths/hashes and x86 executable headers were checked by the executable tests. Actual host recheck: CIM caption **Microsoft Windows 11 Home Single Language**, version10.0.26200, build **26200.9457**, DisplayVersion25H2, OS64-bit, interactive session38; installed v4 Version **4.8.09221**, Release **533509**, SDK8.0.425. The registry ProductName still says Windows10 Home Single Language; the CIM caption and exact build are recorded rather than inferring a support policy. SQL topology TRX records **14.0.2130.4 / Developer Edition (64-bit)**.

| Scope / actual TRX | Discovered / selected / executed | PASS / FAIL / skipped |
|---|---:|---:|
| Desktop 100%, unfiltered, `t03-final100.trx` | **311 /311 /311** | **311 /0 /0** |
| Desktop.Session, `t03-MyDmsVn.Desktop.Session.Tests.trx` | 108 /108 /108 | **108 /0 /0** |
| Architecture, `t03-MyDmsVn.Architecture.Tests.trx` | 12 /12 /12 | **12 /0 /0** |
| SQL Foundation | 61 /61 /61 | **61 /0 /0** |
| SQL Migrations | 65 /65 /65 | **65 /0 /0** |
| SQL Integration | 59 /59 /59 | **59 /0 /0** |
| SQL Catalog | 19 /19 /19 | **19 /0 /0** |
| SQL Inventory | 42 /42 /42 | **42 /0 /0** |
| SQL Security | 60 /60 /60 | **60 /0 /0** |
| Desktop 125%, first frozen attempt, `t03-final125.trx` | 311 /311 /311 | **294 /17 /0**; NOT ACCEPTED |

SQL project TRX are `TestResults/gui-p5-t03/sql-regression/t03-<project-name>.trx`; the six SQL projects ran sequentially and passed **306/306**. Their normal fixture reset/seed/ownership/lifecycle checks remain separate from GUI E2E. Server.Application tests were not rerun because this task changes neither that layer nor its contracts; this is an affected-scope regression, not the T06/P6 full release gate.

Desktop source contains **226 methods:186 Facts and40 Theories /125 InlineData rows =311 invocations**, up from T02's208 methods/282 rows. The18 added methods contribute29 rows: the10 packaged methods/18 rows above, Catalog semantic selection4 rows, receipt pending-input discard1, numeric/lookup/receipt-grid accessibility3, and owned-process natural drain/environment isolation/poison admission3. Current discovery and final100 TRX have311 unique identical display names, zero omissions or duplicates; all282 previous T02 display names remain represented. The100 run completed **2026-10-19 12:51:41–13:10:30 +07:00**,1128.28s TRX wall time, with no unexpected timeout/abort/error. Deliberate subprocess timeout/fatal probes and the real SQL command timeout are passing expected-error tests, not unexpected suite timeouts.

All18 packaged rows passed at100%:16 Light/Dark UIA scenario rows, one SQL topology row and one unavailable-startup row. Their TRX record32-bit testhost18 times, administrative SQL environment exclusion17 times, and actual monitor scale100/window DPI96 in61 window observations. Light/Dark mode is read back from the application; the OS theme does not substitute. Safe startup, real Login/logout/relogin/restart, four catalogs, Draft/Post/Stock/duplicate, stale write, direct Deny/disabled login, invalid input, changed lookup/history/reopen and real SQL uncertain-Post/reload/retry all passed their independent assertions.

Commands use ordinary direct project testing, with both `-p:PlatformTarget=x86` and `-- RunConfiguration.TargetPlatform=x86`. After sequential SQL, Session and Architecture runs, the frozen GUI invocation was:

```powershell
$env:MYDMSVN_GUI_P5_PACKAGE = '<worktree>/artifacts/release/gui-p5-t03-candidate1'
$env:MYDMSVN_GUI_P5_TEST_REVISION = 'e412ebc41cce554953585d5d1ec4f3839a301e42'
$env:MYDMSVN_GUI_P5_EXPECTED_SCALE = '100'
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --no-build --list-tests -- RunConfiguration.TargetPlatform=x86
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --logger 'trx;LogFileName=t03-final100.trx' --logger 'console;verbosity=normal' --results-directory TestResults/gui-p5-t03 -- RunConfiguration.TargetPlatform=x86
```

The100 run's17 GUI SQL allocations completed positive ownership/principal checks and cleanup; together with47 development allocations, all64 local resource ledgers record `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`. A subsequent read-only query of each exact captured database/principal name found **zero remaining databases and zero principals**; process inspection found **zero owned GUI probe/package processes**. No prefix cleanup was attempted. Raw allocation identities, TRX and TEMP logs remain local/ignored; no credentials, SQL IDs or screenshots are committed.

One fresh independent whole-branch static review covered `4c557f2..e412ebc`, including product accessibility/pending binding, real-login UIA, process ownership and SQL lifecycle/fault oracles. It found no reproducible blocking P1/P2 issue. The reviewer ran no tests and review is not counted as runtime proof.

### DPI125 probe correction — final reruns pending

After the100% run stopped/drained, the owner switched the actual desktop to125%, retaining1920×1080. Independent native32-bit and64-bit metadata reads confirmed125%. The initial frozen125% run (`t03-final125.trx`) completed **294 PASS /17 FAIL /0 skipped**: the16 scenario rows and unavailable-startup failed before business interactions, while the SQL-only topology and293 control/infrastructure rows passed. Cleanup was verified; this failed run is retained and is not an acceptance result.

Temporary diagnostic startup probes (`t03-scale125-diagnostic`, `t03-scale125-monitor-diagnostic`, `t03-scale125-aware`, `t03-scale125-thread-diagnostic`, `t03-scale125-effective-dpi`) each failed1/1 and showed that this host's compiled UIA client returned monitor scale100 from `GetScaleFactorForMonitor` even after explicit process/thread per-monitor awareness2; the same monitor's effective DPI was120,120. Independent PowerShell processes returned scale125. No cause beyond these observations is asserted, and the Desktop window's observed DPI96 is not relabeled120.

The probe now declares and verifies PerMonitorV2 awareness before any UIA call and measures the actual monitor's effective DPI using `GetDpiForMonitor(MDT_EFFECTIVE_DPI)`, checking equal axes and successful HRESULT. It compares measured DPI/96 against the declared100/125 scale and logs observations before rejection. This changes only the temporary test client; shipped application DPI settings/manifest are unchanged. Microsoft describes [UIA client scaling](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-screenscaling) and the awareness-dependent [effective monitor DPI return values](https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor). Existing native startup assertions now require the declared UIA context. Focused startup and real-Login Light passed **2/2** at actual125% (`t03-scale125-effective-green.trx`, working-tree trial); this does not substitute for the full matrix.

**Outstanding before T03 exit:** freeze the corrected probe and clean publication, rerun full125% Light/Dark, then100% on the same candidate, reconcile discovery/TRX and verify final resource cleanup. T03 checkboxes stay OPEN. T04–T06, both attended P5 IDs and REL G1–G5 remain OPEN independently.
