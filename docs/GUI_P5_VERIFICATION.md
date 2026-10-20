# GUI-P5 verification

**Current status: GUI-P5-T00–T06 COMPLETE in the approved scope.** Both attended IDs are Owner-signed; T05 retains the accepted Down NOT TESTED exception. [Final T06 acceptance](#final-t06-acceptance--2026-10-20) records the frozen329-row100/125 gates, separate18-row packaged suite, Session108, review correction and final cleanup. REL/P6 G1–G5 and production readiness remain OPEN. Earlier checkpoints below are historical; their original candidate, failures and scope are retained. [Coverage matrix](GUI_P5_COVERAGE_MATRIX.md), [plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md) and [TEST_POLICY](TEST_POLICY.md) govern.

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

## GUI-P5-T03 — Packaged real-login E2E (COMPLETE in task scope)

**Implementation:** `30171015f46fa553d9524329038b1b9afd95e727`; branch `codex/gui-p5-t00`, managed worktree `gui-p5-t00/CleanArchitecture`. **Date/tester:** 2026-10-19 (Asia/Bangkok), Codex. Implementation checkpoints and failed trials are preserved below. Final candidate02eff22 has passed all312 unique obligations at100% and125%, with the125% full-run failure and focused automated rerun explicitly reconciled below. Independent review and follow-ups are complete. T03 closes only its automated scenario/SQL/cleanup scope; attended P5 and T06/P6 remain OPEN.

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

### First frozen candidate — 100% PASS; 125% FAIL

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

### DPI125 probe correction — development checkpoint

After the100% run stopped/drained, the owner switched the actual desktop to125%, retaining1920×1080. Independent native32-bit and64-bit metadata reads confirmed125%. The initial frozen125% run (`t03-final125.trx`) completed **294 PASS /17 FAIL /0 skipped**: the16 scenario rows and unavailable-startup failed before business interactions, while the SQL-only topology and293 control/infrastructure rows passed. Cleanup was verified; this failed run is retained and is not an acceptance result.

Temporary diagnostic startup probes (`t03-scale125-diagnostic`, `t03-scale125-monitor-diagnostic`, `t03-scale125-aware`, `t03-scale125-thread-diagnostic`, `t03-scale125-effective-dpi`) each failed1/1 and showed that this host's compiled UIA client returned monitor scale100 from `GetScaleFactorForMonitor` even after explicit process/thread per-monitor awareness2; the same monitor's effective DPI was120,120. Independent PowerShell processes returned scale125. No cause beyond these observations is asserted, and the Desktop window's observed DPI96 is not relabeled120.

The probe now declares and verifies PerMonitorV2 awareness before any UIA call and measures the actual monitor's effective DPI using `GetDpiForMonitor(MDT_EFFECTIVE_DPI)`, checking equal axes and successful HRESULT. It compares measured DPI/96 against the declared100/125 scale and logs observations before rejection. This changes only the temporary test client; shipped application DPI settings/manifest are unchanged. Microsoft describes [UIA client scaling](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-screenscaling) and the awareness-dependent [effective monitor DPI return values](https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor). Existing native startup assertions now require the declared UIA context. Focused startup and real-Login Light passed **2/2** at actual125% (`t03-scale125-effective-green.trx`, working-tree trial); this does not substitute for the full matrix.

**Outstanding at that checkpoint (superseded below):** freeze the corrected probe and clean publication, rerun full125% Light/Dark, then100% on the same candidate, reconcile discovery/TRX and verify final resource cleanup. T03 checkboxes stay OPEN. T04–T06, both attended P5 IDs and REL G1–G5 remain OPEN independently.

### Second frozen candidate — GUI-P5-T03

**Source/package/test revision:** `45c7b230334c550677379109b48a38d34566b3a1`; clean `artifacts/release/gui-p5-t03-candidate2/`, same two net48/win-x86 packages and schema9. Only the UIA probe DPI measurement/context and its existing startup assertion changed after the earlier candidate. Product source, SQL helper, package tooling and dependency graph are unchanged. A follow-up review by the same independent reviewer found no issue in the DPI delta; no second fresh review agent or concurrent test executor was introduced.

| Artifact | Final candidate SHA-256 |
|---|---|
| `release-manifest.json` | `90A2F50ECBE559306B2ED9D071B374AE1723D4021369031671A716D46D214CA0` |
| Desktop `MyDmsVn.Desktop.App.exe` | `1EDF0EE3EFE6AB898032B7589CE34668CDF922B3351BB50255F61D299200589B` |
| Desktop `MyDmsVn.Desktop.WinForms.dll` | `A406D03A0BE30EC01F1FD33FB7CF296B3065A009B161FAAC90395482A69F753D` |
| Admin `MyDmsVn.Server.DbMigrator.exe` | `009E908E6341CEE1CDA8C14A44D755E355231D7E43CF4700AA92E3B9FFF0B9A4` |
| Test `MyDmsVn.Desktop.Tests.dll` | `F8E9529286FBD19A6B01510EA53E3C5E98F818FF4B67273B830A3EC036AF84A6` |

| Final configuration / local TRX | Source / discovery / selected / executed | PASS / FAIL / skipped / unexpected timeout / aborted |
|---|---|---|
| Actual125%, Light/Dark, `t03-candidate2-final125.trx` | 226 methods /311 /311 /311 | **311 /0 /0 /0 /0** |
| Actual100%, Light/Dark, `t03-candidate2-final100.trx` | 226 methods /311 /311 /311 | **310 /1 /0 /0 /0**; NOT ACCEPTED |

The125% run completed2026-10-19 **13:24:15–13:43:36 +07:00**,1160.34s TRX wall time. All311 discovered names exactly match unique TRX results, including18/18 packaged rows and all282 original T02 display names. The17 UIA processes verified `PerMonitorV2`;61 window observations recorded **monitor scale125 / effective monitor DPI120 / Desktop window DPI96**. Native32-bit and64-bit independent metadata also observed125%, with active1920×1080. The application's virtualized96 window DPI is recorded literally; no per-monitor-aware product, pixel-perfect rendering or supported-OS certification is asserted.

All17 SQL GUI allocations in this125% run completed verified drain and exact ownership cleanup. Including prior development, first100% and failed125% trials, all99 local GUI resource ledgers record `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`. Read-only queries against every exact captured database/principal name found zero remaining; process inspection found zero owned package/probe processes. The owner was then notified to restore100% for the final same-candidate run. Cleanup ambiguity was not observed and no prefix deletion was used. Four early development ledgers (before the expanded snapshot existed) omit the additional instance/database-ID/create-date/marker JSON object; their captured three-principal identities and successful guarded helper cleanup remain recorded. They are not used as final-matrix ownership evidence. All17 final125% allocations include the full ownership snapshot and three principal identities.

**Second-candidate exit: OPEN.** Its100% run completed310 PASS/1 FAIL; the failed receipt/Post/Stock Light row is retained separately. The next corrected driver candidate must pass both configurations before T03 closes.

### Lookup opening race — development checkpoint

The owner restored the desktop to100%, confirmed by the native API. The second candidate's unfiltered100% run (`t03-candidate2-final100.trx`) completed **310 PASS /1 FAIL /0 skipped**. Receipt/Post/Stock Light timed out at `receipt-draft`, waiting for the unique Product lookup result; the probe/application tree cleanup was verified. The other17 packaged rows passed. This run is not accepted as a complete matrix pass.

A source-level timing boundary explained the driver defect: typing can auto-open lookup results, while the named dropdown affordance toggles an already-open popup closed. The driver had unconditionally invoked that toggle after typing. A controlled real-control fact now verifies that typing opens results before the action, the old affordance activation closes them, and F4 opens from the closed state and remains open from the open state. Its first setup attempt could not locate the affordance through local accessibility enumeration (`t03-lookup-open-order-proof`,0/1); using the actual child control activation produced1/1 PASS (`t03-lookup-open-order-confirmed`). The monotonic-wait follow-up also passed1/1.

The temporary UIA driver now sends F4 to the verified focused HWND belonging to the owned application, then waits for exactly one native result and sends Enter through the same ownership/focus guard. The inactive-new-choice path uses the same idempotent open action. No vendor/control/business source, retry, wait budget or SQL behavior changed. The previously failing native receipt/Post/Stock Light plus the new control fact passed **2/2** (`t03-idempotent-open-positive.trx`, working-tree trial). The existing reviewer checked this small delta and found no blocking issue.

**At that development checkpoint T03 remained OPEN.** A new clean publication and complete100%/125% matrix will record the final source and hashes. The added control fact increases current source to **227 methods:187 Facts and40 Theories /125 InlineData rows =312 expected invocations**; final discovery/TRX must confirm that count. All117 GUI resource ledgers after the focused native run record verified drain/drop; final ownership evidence will be recorded separately from these development trials.
### Final frozen candidate — GUI-P5-T03

**Source, package and test revision:** `02eff225d41ff564769bedbb0ca4420d27233a67`, published from a clean tree to `artifacts/release/gui-p5-t03-candidate3/`. The manifest contains exactly two net48/win-x86 packages and current schema9. The test-only F4 change leaves product/vendor/business source and SQL behavior unchanged. Git informational revision metadata produces new artifact hashes, so both DPI configurations use these same frozen bytes. The existing independent reviewer accepted the F4 delta after reviewing ownership, focus and selection ordering.

| Artifact | Final SHA-256 |
|---|---|
| `release-manifest.json` | `4C28486221AFB81243F94DDBCC1A8482BAAE43E6E92BFD6ABAA4E11096BD9F51` |
| Desktop `MyDmsVn.Desktop.App.exe` | `92D400CD583689D82C99B85EDBB7D4B969E1B68804BC938AA3B6F8C9B91C38D6` |
| Desktop `MyDmsVn.Desktop.WinForms.dll` | `A6D1531867B26E51B18137277EBD2445FBD9BCD00D1BAD16878FF6C7DE2548AB` |
| Admin `MyDmsVn.Server.DbMigrator.exe` | `163DAD57DED228234DDF47D11DEC48EBAF98E9F0D406831B9B5987B6B21D2E90` |
| Test `MyDmsVn.Desktop.Tests.dll` | `4A517B2D30EA07CBDE00143F97ACA0C89A869121F7A0216C6F6B6B6DAD6A6A4F` |

| Configuration / local TRX | Methods / discovered / selected / executed | PASS / FAIL / skipped / unexpected timeout / aborted |
|---|---|---|
| Actual100%, Light/Dark, `t03-candidate3-final100.trx` | **227 /312 /312 /312** | **312 /0 /0 /0 /0** |
| Actual125%, Light/Dark, `t03-candidate3-final125.trx` | **227 /312 /312 /312** | **311 /1 /0 /0 /0**; full-run failure retained |
| Actual125%, exclusive desktop, `t03-candidate3-lookup125-exclusive.trx` | Same frozen binary /1 selected /1 executed | **1 /0 /0 /0 /0**; exact failed Light lookup row |

The 100% run completed on 2026-10-19 at **14:29:56–14:48:24 +07:00**, with1107.68s TRX wall time. Its312 unique discovered names exactly match312 final TRX results. All18 packaged rows passed, including the previously failing receipt/Post/Stock Light. The new real-control popup-order fact also passed. Source now contains227 methods:187 Facts plus40 Theories with125 InlineData rows; the19 T03 additions contribute30 invocations beyond T02's282.

The17 UIA processes verified PerMonitorV2;61 window observations record **monitor scale100 / effective monitor DPI96 / Desktop window DPI96**. Source logs identify the exact package/test revision above; each configured package case verifies manifest hashes and runtime identity boundaries. The earlier six SQL projects306/306, Session108/108 and Architecture12/12 remain the affected-scope regression evidence at `e412ebc`; only the temporary probe/control regression changed afterward. Those broader project results are not relabeled as executions at the final revision, and Server.Application/full P6 regression is not claimed.

All17 GUI SQL allocations from the final100% run have complete instance/database-ID/create-date/ownership-marker snapshots and three captured principal identities. They completed verified process/worker/connection drain and exact ownership cleanup. All134 local GUI resource ledgers (including earlier trials) record successful guarded drop; read-only queries of every exact captured database/principal name found zero remaining. Process inspection found zero owned GUI probes/package processes. Four early development ledgers lack the expanded database snapshot, as noted above; none belongs to this final matrix. No prefix deletion or uncertain ownership cleanup was performed.

The owner confirmed the actual desktop transition to125%, retaining1920×1080. The full125% run completed **2026-10-19 14:50:50–15:09:00 +07:00**,1090.07s TRX wall time: **311 PASS /1 FAIL /0 skipped**. Only `Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh(theme: "Light")` failed at `inactive-new-selection-Product`, waiting for visible lookup result status. All three same-process refresh and historical-selection checks had already passed; the Dark row and every other case passed. TRX timeout/abort counters are zero; this assertion failure is retained separately from deliberate timeout tests.

The owner confirmed using/switching windows during that run. The existing vendor lookup controller closes its popup on application deactivation; this makes foreground interference consistent with the observed missing popup, although no event trace proves the exact instant of deactivation. After requesting an exclusive desktop interval, the **same package, test DLL, assertions and budgets**, without rebuilding or source changes, ran only that exact Light row on a new owned disposable database. `t03-candidate3-lookup125-exclusive.trx` passed **1/1**, **15:12:00–15:14:10 +07:00**,129.85s wall time. It exercised the complete lookup/history/inactive-new-choice/restart scenario and SQL oracles. This was a separate test execution after verified cleanup, not an unchecked retry of a business mutation.

Full100% and full125% TRX contain the same312 unique display names; the focused row exactly equals the sole failed125% name. Thus **312 unique automated obligations have passing results at each scale on these frozen binaries**. The125% evidence is explicitly **311/1 full run plus1/0 focused rerun**, not a claim that an unfiltered125% run passed312/312. All18 packaged obligations have automated passing results in both configurations. The125% full run recorded59 window observations and the focused rerun4: **monitor scale125 / effective monitor DPI120 / Desktop window DPI96**. This does not establish pixel-perfect rendering or attended keyboard/visual acceptance. Future GUI runs require an exclusive foreground desktop; T06 retains the obligation for a fresh frozen full-suite acceptance run.

The final100%, full125% and focused125% executions allocated **17 +17 +1** GUI databases respectively. Every one of these35 allocations has the full instance/database-ID/create-date/ownership-marker snapshot and three captured principal ID/SID/create-date identities, with no uncertain principal. All **152** local GUI resource ledgers, including earlier trials, record `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`. Final read-only queries against every exact captured name found **zero databases and zero principals**; process inspection found **zero owned GUI probes/package processes**. Four early development ledgers lacking the expanded snapshot remain excluded from final-matrix ownership evidence. No prefix deletion was used. Manifest, Desktop, WinForms, migrator and test DLL hashes were rechecked after the focused run and remain exactly as listed above. Raw TRX/resource identities/logs remain local and ignored.

Final ordinary project commands used the frozen DLL, with only expected scale and TRX filename changed between full configurations:

```powershell
$env:MYDMSVN_GUI_P5_PACKAGE = '<worktree>/artifacts/release/gui-p5-t03-candidate3'
$env:MYDMSVN_GUI_P5_TEST_REVISION = '02eff225d41ff564769bedbb0ca4420d27233a67'
$env:MYDMSVN_GUI_P5_EXPECTED_SCALE = '125' # '100' for final100
# Full run: omit --filter; filenames t03-candidate3-final100.trx / t03-candidate3-final125.trx.
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --no-build -p:PlatformTarget=x86 --filter '(FullyQualifiedName~Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh&DisplayName~Light)' --logger 'trx;LogFileName=t03-candidate3-lookup125-exclusive.trx' --logger 'console;verbosity=normal' --results-directory TestResults/gui-p5-t03 -- RunConfiguration.TargetPlatform=x86
```

**Current T03 exit: COMPLETE in task scope**, with the foreground-resource incident and focused-rerun qualification above. Date/tester:2026-10-19, Asia/Bangkok, Codex. This documentation-only follow-up does not replace executable source02eff22 or its frozen bytes. T04/T05 attended walkthroughs, T06 frozen full acceptance, both P5 sign-offs and REL/P6 G1–G5 remain OPEN; production readiness is not asserted.

## GUI-P5-T04 — Attended Catalog/Lookup (COMPLETE / OWNER ACCEPTED)

The preparation and intermediate checkpoints below are historical; final Owner acceptance is recorded at the end of this section.

**Prepared:**2026-10-19, Asia/Bangkok, Codex; documentation base `f33a959`. The [T04 worksheet](GUI_P5_T04_WALKTHROUGH.md) is the per-step attended ledger. T03 package/test/executable source02eff22 is unchanged. All133 Desktop manifest files were copied to a new private per-user versioned installation and every length/SHA-256 verified; the inherited DACL was replaced with current-account/SYSTEM/Administrators access, and no admin migrator was copied. No existing installation or shared runtime configuration/shortcut was changed. This is preparation on the developer desktop, not P6 clean-install or limited-user evidence.

Fresh metadata observed Windows11 Home Single Language25H2/build26200.9457/x64/session38; installed v4 Version4.8.09221/Release533509;1920×1080 with native x86 primary scale100/HRESULT0. Actual scale is re-measured per attended session; the final T03 run's125% is not assumed to persist. Computer-use runtime initialized and returned no existing MyDmsVn target window; no application authentication or UI input was performed.

**Selected/executed attended cases:0/0; all worksheet cells NOT RUN.** No T04 SQL database/principal was allocated, no Desktop process launched, no screenshot/oracle/cleanup execution recorded and no human signature received. No new xUnit run, runner, framework or product/test behavior change is claimed. Under TEST_POLICY this preparation uses actual file/hash/ACL/environment verification and documentation checks; it does not rerun the GUI/SQL suite for Markdown. T04 remains OPEN pending attended execution, screenshot/oracle evidence and verified teardown. T05/T06, both P5 sign-offs and REL/P6 remain OPEN.


**T04 attended continuation (2026-10-19):** Project Owner confirmed ready and attended. The worksheet now records28 confirmed four-catalog baseline case rows at100% Light, inspected application-only screenshots linked and hashed in the worksheet, guarded restricted-oracle snapshots and normal chapter process drain. Actual T04 SQL is14.0.2130.4 Developer64-bit with current schema and separated principals. Setup failures and a revoked temporary application-credential exposure are explicitly retained in the worksheet; the corrected handoff uses a masked user-chosen password through an ACL-restricted in-memory named pipe, with no credential file. Product bytes remain unchanged; the explicitly separate attended supervisor helper was subsequently rebuilt and verified16/16 at d89d80d. All four allocations/12 principals are now dropped with exact census zero; final cleanup, remaining cases/configurations and final human sign-off are pending. Earlier zero-execution statements above describe preparation only, not the current attended state. T04 is not COMPLETE.

T04 checkpoint: Owner selected full100% Light functional baseline plus supplemental layout/keyboard/errors/popup UX at100% Dark and125% Light/Dark. Customers chapter5 exceeded its5-minute supervisor before complete artifacts; the later30-minute supervised replay now has complete C-C01–04 evidence and Owner confirmation. Test-only RunAttendedAsync adds an explicit30-minute cap using existing cleanup; automated RunAsync retains5minutes. Focused owned-process typed tests passed16/16; product/package02eff22 remains unchanged. T04 and P5 remain OPEN.

T04 layout checkpoint16:40: 28 attended baseline PASS remain historical02eff22 evidence, but U-02 is FAIL: real Receipt header clipped even maximized at100% Light. Lookup/permission/supplemental work and final sign-off remain OPEN. All4 T04 databases/12 principals and owned processes drained/dropped (exact census0). Minimal Receipt layout repair has focused78/78 PASS and independent code review; new clean package and actual affected GUI/DPI verification are pending. No old hash/result is relabeled as a new-candidate pass.

T04 candidate1 checkpoint17:01: frozen source1b8bc57/package1.0.0-guip5-t04 has filtered100% regression307PASS/3FAIL/0SKIP of310;306non-packagedPASS, ReceiptLightPASS, ReceiptDark popup-readinessFAIL and bothLookuprows owned3-minuteTimeout. All four regression allocations/12principals cleaned, exact DB/principal/process census0 (eight T04 allocations/24principals dropped total). Test-only diagnostic on unchanged product is in progress; no repaired-candidate attended/125%/full-GUI pass or T04 sign-off is claimed. See worksheet for hashes and preserved failures.
## Driver readiness trial — Receipt Dark, 2026-10-19 17:20 +07:00

On unchanged candidate1 product bytes/source1b8bc57, the working-tree test driver now batches fresh non-password UIA result snapshots, caches widget identity only, rejects main-editor matches using the owned popup's native ancestry, and waits for the committed popup to close before proceeding. Main-window HWND is saved/restored together with the owned process when opening the second client. Business assertions, ambiguity rejection,3-minute subprocess limit, permissions and cleanup are unchanged. The verbose trace switch is not forwarded by ConfigureRuntime; this trial reports the always-on completion markers, not foreground/focus diagnostics.

Release/net48/x86 build and existing owned temporary-probe compilation PASS. Exact Receipt Dark row selected/executed1, PASS1/FAIL0/SKIP0, duration2m33s, local t04-receipt-dark-optimized.trx. It explicitly reports TEST_SOURCE=WORKING_TREE_TRIAL. Actual native monitor100%/96DPI; Warehouse, Employee and Product committed-popup-closed markers at60.289/68.371/82.136 seconds. Duplicate Post rejected, receipt/stock SQL oracle and application drain PASS. The fresh database and3 principals are ownership-checked/dropped; exact captured-name census0, owned processes0, all162 historical ledgers Dropped. The first read-only census invocation failed in PowerShell property assignment before opening SQL; corrected connection-string indexer completed successfully and changed no SQL resources.

This passing trial does not erase candidate1's307/310 result or close attended U-02/K-* cases. Both Lookup rows still require driver verification; a new clean test-source/package freeze and affected checks are required before final acceptance. No external desktop interference or product root cause is inferred from timing improvement alone.

### Historical T04 checkpoint21:41 — candidate2 attended matrix (superseded below)

Frozen source d5207b2ce1eae09e9317625b3d7d486c994f09fe/package1.0.0-guip5-t04-c2 is installed and its133Desktop files verified. See the [attended worksheet](GUI_P5_T04_WALKTHROUGH.md) for exact hashes, preserved candidate1 failures, candidate2 automated Lookup100 Light/Dark2/2PASS, real human Login and observations. Baseline48/49PASS comprises28historical unchanged Catalog cases on02eff22 plus20explicit candidate2 observations. Supplemental100%Dark8/8 and125%Light8/8 are Owner-confirmed;125%Dark8observations are pending. The28historical cases are not relabeled as candidate2 reruns; the product diff02eff22..d5207b2 changes only GoodsReceiptControl.cs.

Actual125%/1920x1080 was independently measured before chapter2 at21:32:44; actual theme changed toDark at21:40 on the same owned process. Old165GUI ledgers are Dropped; allocation166 remains positively owned for this chapter. Final U-05, disposal/census and explicit Owner signature remain pending. T04/REMAIN-P5-GUI-01, T05/T06, P5 and P6 remain OPEN; no current full unfiltered GUI or P6 certification result is inferred.

### Historical T04 execution checkpoint21:43 — signature pending at that time

All49baseline and24supplemental UX observations PASS (73applicable,132N/A), with source boundaries preserved in the worksheet. At125%Dark Owner completed both requested groups; actual Dark Receipt/Product popup screenshots and restricted no-inventory-write oracle captured. Chapter2 ended normally21:43:26/exit0/CleanupVerified; final helper cleanup21:43:32 PASS/Disposed. All166GUI ledgers Dropped; exact latest database/principal/process census0. T04's14allocations/42principals all Dropped,0uncertain. Final Owner acceptance of the concrete scope/provenance/qualifications remains pending, so T04/REMAIN-P5-GUI-01 remains OPEN. T05/T06/overallP5/P6 remain OPEN. This documentation-only completion adds no new runtime/test claims or full unfiltered gate result.

### Final T04 Owner sign-off —2026-10-19 (recorded21:52 +07:00)

Project Owner explicitly accepted evidence commit bd59f6f: “Xác nhận nghiệm thu T04 theo biên bản.” GUI-P5-T04 COMPLETE / REMAIN-P5-GUI-01 PASS. The accepted worksheet records49baseline +24supplemental UX =73PASS/132N/A/0unresolved,43linked screenshots with verified hashes, explicit28historical02eff22 plus45candidate2d5207b2 provenance and the documented UI qualifications. Actual100/125Light/Dark observations, runtime/SQL separation and all14T04allocations/42principals disposed remain evidenced; final166/166ledgerDropped/exactlatestdatabase-principal-processcensus0. The final sign-off changes documentation only; no new xUnit/runtime execution is claimed. T05/T06, REMAIN-P5-GUI-02, overallP5, REL G1–G5 and production readiness remain OPEN.

## GUI-P5-T05 — Inventory attended acceptance (IN PROGRESS)

The following setup entries are historical checkpoints; the current evidence status follows below.

The [T05 worksheet](GUI_P5_T05_WALKTHROUGH.md) maps27baseline/18supplemental UX observations, all NOT RUN. Owner approved full100%Light baseline plus UX100%Dark/125%Light/Dark; actual100%/1920x1080 and installed frozen d5207b2 files/hashes verified. First SQL setup failed on identity-collation join before Desktop launch; exact owned cleanup/census0/167Dropped verified. Setup-only correction and fresh retry are recorded in the worksheet. No T05 attended PASS or new automated result is claimed. T04 remains accepted; T05/T06/overallP5/P6 OPEN.

T05 setup retry22:01: corrected seed succeeded,8ordinaryactors/54rolegrants/0protectedmatches; initial7active+1disabled. Fresh restricted oracle0catalogs/receipts/lines/ledger/balances,0reconciliation mismatches; SQL2017Developer14.0.2130.4/current001–009/separatedidentities verified. Chapter1 launched22:01:54, deadline22:31:54; human p5.t05.operator Login pending. Old167ledgersDropped; current168positivelyowned/admitted. No attended case PASS yet. First setup failure preserved; finaldrop pending.

### Current T05 evidence / final teardown —2026-10-20,11:34+07

[T05 worksheet](GUI_P5_T05_WALKTHROUGH.md) is authoritative for exact cases/provenance/qualifications. Current33/45PASS:27baseline including final independentoracle/cleanup, plus6Owner-confirmed100DarkUX. Both125Light/Dark6-cellUXgroups executed on measured125%/1920x1080 with inspected artifacts; explicit Owner observations pending, final signature pending. Source/test/product d5207b2 unchanged; installed133/133files freshly match frozenmanifest. No T05 automatedinvocations added, no T03/T04 result relabeled.

OriginalGUI-createdfunctionalcohort168 and continuation169 are distinct. Original actualSave/Post/Post-only/Write/conflict/DirectDeny evidence preserved. Continuationcatalog/26pagingPostedreceipts are setup-only fixtures; oneactualtimeoutPost rolledback, explicitRefresh/singlejustifiedretryPosted token2031. Currentfinalrestrictedoracle33receipts59lines53ledger27balancekeys/base27,0full-key/line/documentmismatches;15tables unchanged through125UX. OwnedfaultRemoved=true. Disabled/revokedInactive,63ordinaryassignments,0protectedbindings; runtime/admin/oracle separation verified.

Finalchapter4normalclose11:33:51.954/exit0/CleanupVerified; rootDisposed11:33:52.996/exit0/cleanupPASS. Exactinstance-aware census latestdatabase0/principals0/process0;169/169GUIledgersDropped, all3T05allocations/9principalsdisposed.66standardSHA256artifactreferences verified inworksheet; originalsetupfailure/UI/cache/truncation/tooltimingqualifications retained. T05/REMAIN-P5-GUI-02 remainsOPEN pending Owner125UXconfirmation/finalevidencereview/signature; T06/overallP5/P6OPEN, no productionreadinessclaim.

T05 current acceptance checkpoint2026-10-20: Owner explicitly confirmed both125Light/Dark observations. Canonical worksheet44/45PASS (27baseline+17supplemental),1Warehouse125DarkPARTIAL solelyDown NOT TESTED; other observed paths confirmed. Finalreview complete and cleanup evidence unchanged. FinalOwnerT05signature/explicit disposition of narrowDownlimit pending; T05/REMAIN-P5-GUI-02/T06/P5/P6 remainOPEN. Earlier checkpoints are historical.

### Final T05 Owner sign-off — 2026-10-20 11:45 +07:00

Project Owner explicitly accepted evidence commit5ed7bc6: “Nghiệm thu T05 theo biên bản, chấp nhận giới hạn Down đã ghi”. GUI-P5-T05 COMPLETE / REMAIN-P5-GUI-02 PASS within the documented, Owner-accepted scope. Outcome remains44PASS and1PARTIAL observation cell; the Warehouse125DarkDown substep remains NOT TESTED with an explicit Owner-accepted exception, never converted into a testPASS. The other observed paths and both125Light/Dark groups are confirmed.

Frozen d5207b2 candidate/cohort provenance,80publicimages, independent oracle/reconciliation and final owned cleanup remain as evidenced above. All3T05allocations/9principals disposed,169/169GUIledgersDropped, exactlatest database/principal/process0. This signature changes documentation only; no GUI/SQL/test execution or new automatedpass is claimed. T06, overallP5, REL/P6 G1–G5 and productionreadiness remainOPEN. Historical pending checkpoints above are superseded by this signed decision.

## GUI-P5-T06 — Frozen GUI gate (IN PROGRESS,2026-10-20)

Taskbase cb86fbc; existing isolatedworktree codex/gui-p5-t00. T04/T05 Owner-signed decisions and explicit T05Warehouse125DarkDownexception remain unchanged. Mandatory gate: unfilteredSession/Desktop, separate18-row packaged E2E and actual100/125Light/Dark matrix, no missing/skippedmandatoryrows, verifiedcleanup; P6 remainsoutsideacceptance.

Admission2026-10-20 before fullrun12:07:31+07: Owner explicitly agreed exclusive desktop, fresh x86native125/HRESULT0,physical1920x1080,interactiveWindows session; designatedprivateSQL effectiveMaster/Developer14.0.2130.4 validated. BeforeGUI169/169historicalledgersDropped/exactlatestdatabase-principals-process0. Product/test/deploy source diff d5207b2..cb86fbc empty. No behavioralchanges proposed; no Application/SQLbusinessregression added for documentation-onlydifferences.

Lockedrestore/currentprojectSessionRelease/net48/x86 passed108/108/0failed/0skip; raw t06-session.trx local. Session assemblySHA256 23C4C82B1241DC449BBC1DB5F10EA1082FC96F972F2292E3A24E5E5D2C8AB77F; buildmetadata cb86fbc, executablecode-equivalent to frozen d5207b2, explicitly not matching archivedDesktoptesthash. Desktop lockedrestore/build0warnings/errors, discovery324rows/18Packaged. RebuildDesktophash differs because generatedInformationalVersion incorporatesdocumentationcommitcb86fbc. Beforeexecution restored archivedd5207b2 testhost output and verifiedDesktoptestSHA256 DD0BBBFC176D60F778CC3C5BDC7BD433636E0DA8AFC2C68CB803D98752CA41F7. Package1.0.0-guip5-t04-c2/source d5207b2 remainsfrozen; no productbinarychange or relabeling.

Current directproject command: dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --logger trx --results-directory TestResults/gui-p5-t06/desktop125 -- RunConfiguration.TargetPlatform=x86. Environment selects frozenpackage, TEST_REVISION=d5207b2ce1eae09e9317625b3d7d486c994f09fe and EXPECTED_SCALE=125; no connection/password published. ExistingboundedGUIprocess/SQLownershiphelper owns allchildren/resources. Unfiltered125executioninprogress; noresultyet.100%/separateE2E/TRXreconciliation/finalcleanup/review pending; T06/P5/P6OPEN.

T06 sourceinventory:232Desktopmethods (189Facts/43Theories), staticFact+InlineDatarows324 exactlymatchesdiscovery;18Packagedrows are10methods. Duringfull125run, Real_sql_post_timeout_locks_mutation_until_explicit_reload_and_checked_retry(Dark) failedbeforefaultinstallation: receipt-draft Product uniquelookupresult unavailable; Warehouse/Employee committedpopupclosed markers present. Ownedchild exit1/DescendantsOutlivedParent cleanupVerified=true; failure preserved, no rootcauseassigned and no SQLtimeoutboundaryPASS claimed. Suite continues, finalTRXnotyetavailable. ExistingoptionalTRACE_LOOKUPS instrumentation will be enabled for requiredseparate18-rowpackagedrun on unchangedbinaries; no speculativefix or timeoutincrease.

### T06 first full125% result and bounded diagnostic

UnfilteredDesktop125 completed324/324 uniqueinvocations (source/discovery/TRX names differences0):323PASS/1FAIL/0SKIP,15m32s. All306nonpackagedPASS; packaged17PASS/1FAIL. FailedrowReal_sql_post_timeout_locks_mutation_until_explicit_reload_and_checked_retry(Dark) failedpre-fault atProductlookup, so its actualSQLtimeoutpath is notcredited. RawTRXSHA256 B32457675D981C5B5F1BF581FD48AC0F147487B0EA312EA87C85087CAB5376FE. ProbecontextPerMonitorV2/monitorDPI120/scale125/windowDPI96/testhost32. All17runallocations ownership-cleaned;186/186historicalledgersDropped/exactlatestdatabase-principals-process0.

Existingfrozenprobe optionalTRACE_LOOKUPS is stripped by both Start and ConfigureRuntime environment sanitization; merely settingtesthostvariable wouldnot enableit. Ignoredone-offdiagnostic invokes existingGuiSqlEnvironment/GuiProcessProbeFixture/GuiOwnedProcess viareflection, thenexplicitlysetsnonsecrettrace afterConfigureRuntime. NoUIAdrivercode/product/runner/framework added. This is diagnostic inventory-auditDark (samepre-faultDraftpreparation), not an exactfaulttest/TRXrerun. Existing3-minsupervisor unchanged; actualReceiptPost/StockoraclePASScaptured. Warehouse/Employee/Product traces allforegroundowned/focused/enabled/onscreen; codequeries/Enterselection/popupclosed visible, Productcommit~40.8s. No failure reproduced; cause remainsunresolved, nofixclaimed. DiagnosticownedDisposePASS;187/187Dropped/exactlatestdatabase-principals-process0. Requiredseparate18-rowPackaged125suite nowrunning unchangedfrozenbinaries; not yetreportedPASS. Full100matrix/finalreview and current125green gate stillpending; T06/P5/P6OPEN.

### T06 separatePackaged125 suite —18/18PASS

Unchangedfrozend5207b2 directproject/classfilter completed18/18PASS/0FAIL/0SKIP in15m08s, exact18names matchfullrunsubset. BothReal_sql_post_timeout rows Light/Dark recordedRUNTIME_SQL_WAITFOR_OBSERVED=PASS andUNCERTAIN_POST_RELOAD_EXPLICIT_RETRY=PASS, with independentSQLoracles/cleanup. RawTRXSHA256 637110A059033CF2F7FB01D3A5DCF500353944374493AC5690BDB11C5B77362F. All17separatesuiteallocations dropped,204/204historicalledgersDropped/exactlatestdatabase-principals-process0. Firstfullfailure remainsunexplained/notreproduced; nofixedclaim or originalfailureerasure.

Freshenvironment reread: Windows11HomeSingleLanguage25H2/build26200.9457/x64, registryruntime4.8.09221/Release533509; noisolated4.8-onlycertification. Frozenmanifest/testDLLhashes unchanged. Fullunfiltered125rerun started12:43+07 on samebinaries/scale, normalconsolelogging forprogress; nofilter/noincreasedbudgets/noTestframeworkaddition. Resultsfinalpending.100%fullmatrix/finalreview/closurepending; T06/P5/P6OPEN.

### T06 final unfiltered125% —324/324PASS

Secondunfiltered125run on identicalfrozend5207b2 binaries completed324/324PASS/0FAIL/0SKIP in16.1770minutes. Exactdiscovery/TRXnames differences0,324uniqueobligations includingall18PackagedPASS; no absent/skippedmandatoryrow. RawTRXSHA256 3E201CAC56BBFB69F63F892FFA077F861B20DC389380232FC674215CD85E39B2. BothSQLtimeoutLight/Dark and same-processLookupLight/Dark passed in thisfullrun. Initial323PASS/1FAILrun remainsunchanged; intermittentpre-faultProductpopupfailure was not reproduced by diagnostic/separatesuite/fullrerun, cause remainsunknown, no product/driverfixclaimed.

All17rerunallocations disposed;221/221historicalGUIledgersDropped, exactlatestdatabase0/principals0/ownedprocess0. Freshx86probe still125/HRESULT0 afterdrain. Ownerrequested toswitch100%/1920x1080 onlyaftercleanup; do notstart100matrix beforehumanconfirmation/freshprobe. Mandatoryfull100/whole-planreview/handoffremainpending; T06/P5/P6OPEN. ExistingOwnerT04/T05acceptance/Downexception preserved.

T06 100%admission: Owner confirmed100%/1920x1080after125drain; freshx86native100/HRESULT0 andphysical1920x1080verified, frozenDesktoptesthashunchanged. Fullunfiltered100projectrun started~13:00+07,no-build/no-restore/expectedscale100, samepackage and boundedhelpers; resultpending. Sessiondiscovery108names exactlymatches108TRXrows; SessionrawTRXSHA25621D1E63409A5BDE7369285837903F2B5552D10E2D45474ECD2D3260499141C37. T06/P5/P6notyetclosed.

### T06 first full100% —320PASS/4FAIL, gate remainsOPEN

Owner confirmed100%/1920x1080; freshnativeprobe verified100/HRESULT0 beforeexecution. Unfiltered100run executed324/324,320PASS/4FAIL/0SKIP in13.3221minutes. All306nonpackagedPASS; packaged14PASS/4FAIL. Failed: timeoutDark andReceiptLight preparation reportedWarehouse uniquepopupresult unavailable; SameProcessLookupDark/Light preparation reportedownedlookupeditor failed to receivekeyboardfocus. Allfailedbefore theirintendedbusiness/assertionstage; no passcredited forabsentpaths. RawTRXSHA256250D7F477D8BE38187448F6A1F94608D5F025C32C8D6DF455FBE5DB4536BA1AE. All17allocationsdrained/dropped;238/238historicalGUIledgersDropped/exactlatestdatabase-principals-process0.

Ownerasked whetherdesktop waslocked/sleeping/used or anotherwindow appeared; replypending, no externalinteractioncausationassigned. Read-onlysky windowinventory sawoneownedGUIwindow; itclosednormallybeforefollow-uprehydration, so noscreenshot/input/activationoccurred. One-off100Darkdiagnostic reuses frozenboundedhelpers/tracewithcorrectexpectedscale100, no behavioralcodechange; resultpending. Currentfull100gateFAIL, finalreview/P5closureblockedbyunresolvedGUIcoverage; no speculativefix/timeoutincrease/newframework.

### Reproduced100% diagnostic / foreground boundary

One-off100Darkdiagnostic failed on unchangedfrozenprobe: beforeWarehouseopen MS31082, FOREGROUND_OWNED=False; FOCUSED=False; ENABLED=True; OFFSCREEN=False; typedGUI-WHpreserved. SendOwnedLookupKey thenfailedkeyboardfocus beforeF4. This establishes the missingforeground/focus precondition, not which externalwindow/user action causedit. GuiOwnedProcess childexit1/DescendantsOutlivedParent cleanupVerified=true; diagnosticfixtureDisposePASS;239/239historicalledgersDropped/exactlatestdatabase-principals-process0. NoaliveGUIorSQLresourcesretained.

Sourceinspection: SetInput and SendOwnedLookupKey call editor.SetFocus, but driverdoesnotrequest/verifytheownedtop-levelwindowforeground beforeinput; existingtraceonlyreportsitwhenexplicitlyenabled. A boundedtest-driverdesign is presented toOwner: activateonlytheactualownedtargetwindow, verifyforeground+editorfocus beforekeys, failwithdiagnosticifunavailable, norepeatedbusinesscommands/noincreasedtimeouts/noproductchanges. Addcontrolledforeground-lossregression RED→GREEN andrerunaffected/fullgates withnewtestrevision/hash explicitlyseparatefromunchangedproductpackage. Designapprovalpending perbrainstorminghardgate; noimplementationstarted. PriorOwnerdesktopinteractionquestionstillpending; nohumaninterferenceattribution. T06/P5/P6OPEN; T04/T05signatures andDownexceptionunchanged.

### Owner-approved test-driver foreground guard — RED/GREEN

Owner explicitly approved the boundeddriverdesign inchat2026-10-20. No product/vendor/SQL/schema/frameworkchange. Added syntheticchildforms within existing GuiOwnedProcess/GuiProcessProbeFixture: positive backgroundtarget/recoveredF4delivery and negativeeditor thatredirectsGotFocustoanownedfocus sink. FirstharnesscompilefailedCS0136(namecollision), corrected beforebehaviorverification. Positivebackgroundtestpassedoldcode, correctlynotcountedRED. Negativefocusrefusaltest thenfailedoldSetInput in2s: INPUT_PATTERN=Focus target editor:Value was emitted despitefocusrefusal, violating no-writeboundary. Childcleanupverified; originalfocus-refusal-red TRX retained.

Minimaldriverguard nowvalidateseditorhandleandnative-rootprocess againstcurrentownedapplication, requestsactivationonlyforthatroot (restoreonlyifminimized), callsSetFocus andusesexistingfiniteWait toverifyexactforegroundroot+editor.HasKeyboardFocus BEFORE ValuePatternwrite orF4/Enter PostMessage. NoAttachThreadInput/ALTbypass/repeatedactivationloop, nobusinesscommandretry, noexistingtimeoutincrease. Rawtestprogram syntheticmode isnotshipped. GuiOwnedProcessTests focusedRelease/net48/x86passed18/18/0fail/0skip in29s, includingbothnewregressions (existing16+2). NoSQLallocation in thisscope; prior239ledgersremainDropped.

Package/test provenance separated explicitly: legacy MYDMSVN_GUI_P5_TEST_REVISION continuespinningpackage source d5207b2, nowloglabelEXPECTED_PACKAGE_SOURCE; actualtestAssemblyInformationalVersionlogged independently. Productpackagebytes/hashremain unchanged. Newtestsource/hashfreeze and bothfullDPIgates/separatepackagedverificationpending; old324-rowmatrix isnotrelabeledasnewdriverproof. T06/P5/P6OPEN.

### T06 driver freeze —test2da2868 / productd5207b2

Clean test-onlysource2da2868770feff77ea52130f0d4d01e4e9a62ca1 builtRelease/net48/x86 with0warnings/errors. DesktoptestDLLSHA25670478A92F270FF97B0003849EA1D3C764A7D6026B8332B6FB13BF648C9619293; frozen testhost archivedlocally. AssemblyInformationalVersion1.0.0+2da2868770feff77ea52130f0d4d01e4e9a62ca1. Discovery326rows (prior324+2focusregressions), packaged18unchanged. Existingproductpackage/source d5207b2 and manifestSHA256EDB0FE41DAB59FE7F4DEE7F2673815017C056C744E70E9D2E896D50075146101 unchanged; noattendedcaseisrelabeledasnewproductexecution. No product/vendor/sharedfixture/schema/businesschange; broaderApplication/SQLbusinessrerun nottriggered bythisdriver-onlychange.

SeparatePackaged100suite currentlyexecutingdirectproject/classfilter on frozennewtesthost/oldpackage, expectedscale100. New326-rowunfiltered100/125matrixstillpending; olddriver324-rowresults arehistorical separateevidence. New test source logs expectedpackage source andactualtestbuild independently; legacyenvironmentvariable semantics retained forpackagepinning. T06/P5/P6OPEN until currentdrivergate andfinalreview complete.

### Driver2da scope failure and lookup-only correction

Frozen2da2868 SeparatePackaged100 completed6PASS/12FAIL/0SKIPof18 in12.7319minutes. RawTRXSHA256D31913658760084A277773E48A009E771B4F092CD8DA2AC1109ACCE16C81263A. Thiswas an authorintroducedoverbroadtestguard: stricteditor.HasKeyboardFocus was applied toeveryValuePatterninput, includingcompositeNumericUpDown andlogin/restartinputs. Receipttraces showWarehouse/Employee/Productcommittedpopupclosed before subsequentinputguardfailed; not a productInventoryfailure or a validaccepteddriver. All17trialallocationsdisposed;256/256historicalledgersDropped/exactlatestdatabase-principals-process0. Trialandoriginalfailuresretained.

CorrectionnarrowsSetInputactivation/foreground/focusguard to semanticComboBoxlookupinputs only, exactlyapprovedscope; otherValuePatternproviders retainpreviousSetFocus/valuepath, preservingcompositenumericbehavior. F4/Enter alwaysuseownedforeground+focusguard. Regressionfixture now exposes aComboBoxsemanticwrapperwithrealchildTextBox, includingcontrolledGotFocusredirect/no-value-change; positiveregression verifiesbackgroundtarget/actualF4delivery. Failurediagnosticsreport onlyinputname/foregroundowned/rootforeground/editorfocusbooleans, no values/credentials/externalwindowtitlesorIDs. Focus+InventoryAccessibilityscope passed32/32/0fail/0skip in34s (18ownedprocess+14Inventoryaccessibility). Newcleanfreeze andrequiredpackaged/fullmatrix pending; no product/vendor/SQL/sourcechange.

### Lookup-scoped driver clean freeze —fc0851d

Clean testsourcefc0851d7b0f8e5a207beb38e610d72795e25cb60 built0warnings/errors Release/net48/x86; DesktoptestSHA25624C877E35CC86C2686FD4A1943505BC892E2C7A9C27ECF242BE37AB8DB0BF4AC, archivedlocallyasfrozenfc085-testhost. Discovery326rows; product/deployment source diff versusd5207b2 empty, unchangedpackageEDB0manifest. New18-rowSeparatePackaged100run usesexactfc085testhost/oldproduct, legacyexpectedpackagesourced520, expectedscale100; results pending. No currentmatrixPASS inferredfrom2daoroldD520driver. T06/P5/P6OPEN.

### Scoped driverfc085 SeparatePackaged100 —18/18PASS

Frozenfc085 testhost/unchangedd520product completed18/18PASS/0FAIL/0SKIP in15.3032minutes. Exact18discovery/TRXnames differences0. RawTRXSHA256B85CFFBB59DA84D234A896BCF5C4A6EA827F8ED51563D11B964B5B5C163D2DC8. TRX explicitlyrecords EXPECTED_PACKAGE_SOURCE=d5207b2ce1eae09e9317625b3d7d486c994f09fe and TEST_BUILD_INFORMATIONAL_VERSION=1.0.0+fc0851d7b0f8e5a207beb38e610d72795e25cb60; no matching-sourcefiction. BothallLookup/Receipt/timeout/permission/invalid/realLogin/restart/topology/startupcasesPASS; earlierfailedversions retainedseparately.

All17allocationsdisposed;273/273historicalledgersDropped/exactlatestdatabase-principals-process0. Additionalreadonlycensus selected allT06capturedrecords sincefirstfullrun, validatedeachcapturedinstance againstdesignatedmaster, queriedexactcapturednamesonly:104distinctdatabases/312principals, remaining0/0, allrecordsDisposed. Censuswasread-only/no deletion; setupConnectTimeoutkeyword correctedbeforeSQLquery, not acoveragefailure. Newfull326@100begunafterfreshx86scale100/HRESULT0/hash24C877...verification; full100/125andfinalwhole-planreviewpending. T06/P5/P6OPEN.

### Scoped driver fc085 full 100% — 326/326 PASS

The unfiltered Desktop project completed 326/326 PASS, zero fail/skip/timeout/abort, in 16.4584 minutes on the frozen fc085 testhost and unchanged d520 product package. All 326 unique TRX names exactly match frozen discovery (zero differences), including all 18 packaged rows and both new owned lookup focus regressions. Raw TRX SHA256: EF5B60397C08573B8F8262AD8B0F7FD81030EF935B28A239A1975C7E58C24B21. Earlier failed driver/trials remain recorded separately; no external interaction cause is inferred.

After drain, all 290 historical GUI ledgers are Dropped; latest exact database/principal census and owned GUI process count are zero. The read-only all-T06 census verified all 121 captured databases / 363 principals against the designated instance, with zero remaining and all allocations disposed. Owner has been asked to switch to 125% / 1920x1080 after cleanup; fresh scale verification, current-driver full 125% and final whole-plan review remain pending. T06/overall P5/P6 remain OPEN. T04/T05 signatures and the accepted Down limitation are unchanged.
T06 125% admission (2026-10-20): Owner confirmed the drained desktop switched to 125% / 1920x1080. Fresh x86 native probe returned PrimaryScale125 / HResult0; physical resolution1920x1080 and frozen test DLL24C877... were verified before execution. The full unfiltered 326-row current-driver run is in progress, with the same no-build/no-restore command as 100%, EXPECTED_SCALE=125 and result directory scoped-desktop125. Current source inventory is 234 methods (191 Facts including two GuiPackageFacts, 43 Theories including eight GuiPackageTheories); 326 static rows exactly match frozen discovery. No final PASS or T06 completion is claimed before TRX, cleanup and review.
### Scoped driver fc085 full 125% — 326/326 PASS

The current frozen fc085 testhost with unchanged d520 product completed unfiltered Desktop125 at 326/326 PASS, zero fail/skip/timeout/abort, in 16.6029 minutes. All 326 unique names exactly match frozen discovery (zero differences), including all 18 packaged rows and both owned lookup focus regressions. Raw TRX SHA256: A54DD5FC41E2D9011A0BFD169F098AE256776A4289F9D5B7A7A0DF4C51B640C7. Current driver full100 and full125 therefore each cover the complete 326-row set; old driver trial results remain historical, not substituted.

Final drain census: 307/307 historical GUI ledgers Dropped, latest exact database/principal/process counts zero. Read-only all-T06 census verifies all 138 captured databases / 414 principals against the designated instance, remaining zero, all allocations disposed. Rechecked published manifest and all 205 package files match; product/deployment source diff d520..current is empty. Desktop package contains 133 files. Owner's desktop is released after verified process drain.

Gate reconciliation: Session108 PASS (frozen cb86fbc metadata, unchanged Session executable source); Desktop326 PASS at100 and326 PASS at125 (test fc085); separate packaged18 PASS at100 (test fc085). Total 778 passing invocations / 434 distinct obligations, not 778 distinct tests. No missing mandatory rows or skips. Full T06 command is the direct project no-build/no-restore Release/net48/x86 invocation recorded above, with console verbosity normal, unique TRX paths scoped-desktop100 and scoped-desktop125 and scale100/125 respectively. No Application/SQL business implementation changed during T06; no broader business rerun triggered by the test-driver-only correction. Packaged cases still execute real SQL independent oracles.

Final whole-branch review remains pending before T06/P5 closure. Owner-signed T04/T05 and the disclosed T05 Warehouse125Dark Down NOT TESTED exception stand; no substep is relabeled PASS. REL/P6 G1–G5 and production readiness remain OPEN.
### Final review — SQL quarantine fail-stop correction

Fresh whole-branch review of42d94ba..ea2c194 found one Important issue: GuiSqlEnvironment quarantine recorded/threw but did not poison subsequent SQL/process admission. Corrected both ownership-uncertain and cleanup-failure paths to poison before recording. If local quarantine recording also fails, aggregate preserves the original failure and recording error. No product/business/SQL schema or timeout change.

Added a three-row isolated child regression invoking the actual DisposeAsync paths through synthetic constructor-only fixtures: uncertain principal, invalid SQL configuration (fails before SQL I/O), and recording failure. Initial harness CS0619/xUnit2014 compile and dependency-load failures were corrected; those are not behavioral RED. Actual RED then observed3/3 failures on old DisposeAsync with “SQL quarantine must stop the entire GUI session”; corrected implementation GREEN3/3 in962ms. Parent remains unpoisoned; no real SQL allocation, connection or deletion occurs. A preliminary broad probe run had16PASS/5FAIL while dependency/focus harness issues remained, retained separately without credit. New final frozen testhost and full suite are required; previous fc085 results remain valid only for their recorded revision.

Review has no Critical findings. Minor: stale historical status headings in walkthrough/tests documentation; the authoritative final handoff status supersedes these, with historical wording retained as a deferred documentation cleanup. No second reviewer is dispatched: the one correction pass is verified RED→GREEN and by the new full suite. Declined-to-judge rulings: P6 install/recovery/signing/support remains REL OPEN; DPI150 is outside approved matrix; Owner-accepted Warehouse125Dark Down remains NOT TESTED; reviewer only read stored SQL/attended evidence, while executor independently ran final live census. Costs: unverified release environments, DPI150, the isolated Down path and an undetected issue in unobserved live state respectively remain outside claimed proof.
### Corrected fail-stop candidate freeze — 12e6a20

Clean test source12e6a20a7af8e5c0f6bf820f172580f01e44181b built Release/net48/x86 with zero warnings/errors; test DLL SHA256 CD12D38840C630A8F5B5FC950691ACEBAA3050DA4ACA687F46644C14C2510FF8. Archived locally as frozen12e6-testhost. Discovery329 rows (prior326 plus three quarantine regression rows). Product/package d520 remains unchanged. Final census still307/307 historical ledgers Dropped / exact latest database-principal-process zero. New full329-row matrix is pending exclusive desktop confirmation; no current-candidate full PASS is inferred from fc085. T06/P5/P6 remain OPEN.
12e6a20 full125 admission: Owner confirmed exclusive desktop after review correction. Fresh x86 probe125/HResult0 and physical1920x1080, test DLLCD12D388... verified before starting. Running the direct unfiltered Release/net48/x86 project with no-build/no-restore, expected scale125, console normal and unique TRX final-desktop125/t06-final-desktop125.trx. Result pending; no full329-row PASS yet. Standalone packaged/current100 gates remain pending after this run and verified drain.
### Final corrected candidate12e6 full125 — 329/329 PASS

Owner confirmed exclusive125 desktop; fresh x86 native125/HResult0, physical1920x1080 and frozen DLLCD12D388... were verified. Full unfiltered Release/net48/x86 no-build/no-restore completed329/329PASS, zero fail/skip/timeout/abort, in16.5708minutes. All329 unique TRX names exactly match frozen discovery (zero differences), including all18 packaged rows and all3 new SQL-quarantine regressions. Raw TRX SHA256 B6C603BD0D3C9C3D04B8C097898EC0E75847BF6E6949C93D733DD6DF30974C6D. Source inventory235methods/191Facts/44Theories yields329rows; no old candidate PASS substituted.

All17 actualSQL allocations from this run drained/dropped:324/324 historicalGUIledgersDropped, exactlatestdatabase/principal/process counts zero. Read-only all-T06 census155capturedDB/465principals remaining0/0/allDisposed after instance validation. Synthetic quarantine regressions create noSQLallocation. Owner asked to switch100%/1920x1080 onlyafterdrain for final full329-row100 gate and separate18-row current-candidate packaged suite; both pending. Fresh review Important fix is RED3→GREEN3 and now full329GREEN at125; final100/current separatepackaged required before T06/P5closure. No second review; P6 remains OPEN.
12e6 final100 admission: Owner confirmed100%/1920x1080 after verified125 drain and had been asked to reserve the desktop for both sequential final runs. Fresh x86 native100/HResult0, physical1920x1080 and testDLLCD12D388... verified. Full unfiltered329-row directproject Release/net48/x86 no-build/no-restore started with expectedscale100 and unique TRX final-desktop100/t06-final-desktop100.trx. Standalone18-row packaged gate follows only after full-run result and verified cleanup. Both results pending; T06/P5/P6 remain OPEN.
### Final corrected candidate12e6 full100 — 329/329 PASS

Unfiltered current-driver100 completed329/329PASS, zero fail/skip/timeout/abort, in16.4698minutes. All329 unique names exactly match frozen discovery; all18 packaged and3quarantine rows PASS. Raw TRX SHA2563298DEAFF866E5C38D4B3A1C51C50CDE20B05BFED27CD0C4AD6C1A7564486F1F. Both current-candidate full100/125 now pass the same complete329-row set. Full-run allocation cleanup verified341/341historicalGUIledgersDropped, latestexactdatabase/principal/process0; all-T06 exact172DB/516principals remaining0/allDisposed. Standalone current-candidate packaged18-row suite then started at100, same frozen12e6test/oldproductd520, existing classfilter and unique TRX final-packaged100/t06-final-packaged100.trx; resultpending. T06/P5closure waits this suite and finalcensus/handoff; P6OPEN.
## Final T06 acceptance — 2026-10-20

**GUI-P5-T06 COMPLETE; GUI-P5 COMPLETE within the approved, Owner-signed scope.** This final status supersedes earlier pending checkpoints. Both attended IDs remain Owner-accepted; T05 remains44PASS/1PARTIAL, with Warehouse125Dark Down NOT TESTED explicitly accepted by Owner. REL/P6 G1–G5 and production readiness remain OPEN. No new Owner signature is inferred.

### Frozen artifacts and verified environment

- Product/package source: d5207b2ce1eae09e9317625b3d7d486c994f09fe, clean1.0.0-guip5-t04-c2. Manifest SHA256 EDB0FE41DAB59FE7F4DEE7F2673815017C056C744E70E9D2E896D50075146101. All205 published files rehashed successfully, including133 Desktop files. DesktopEXE SHA256 C068D0F10AAD451AA90F2659EA893A3B8CEE6E456DDDF8E748C850FE65B758EF. No product/deployment changes during T06.
- Final Desktop test source:12e6a20a7af8e5c0f6bf820f172580f01e44181b; DLL SHA256 CD12D38840C630A8F5B5FC950691ACEBAA3050DA4ACA687F46644C14C2510FF8. Clean Release/net48/x86 build:0warnings/errors. Expected package source and actual test build are logged independently. No matching-revision fiction.
- Session test build:cb86fbc metadata, DLL23C4C82B1241DC449BBC1DB5F10EA1082FC96F972F2292E3A24E5E5D2C8AB77F. Session source is unchanged between that build, productd520 and final Desktop test source;108 exact discovered names match TRX. No Session result is relabeled as a new build.
- Actual Windows11 Home Single Language25H2/x64 build26200.9457; interactive desktop1920x1080 at measured100% and125%, Light/Dark packaged rows at each scale; net48/x86/32-bit testhost. Registry runtime4.8.09221/Release533509 is the actual installed runtime; no isolated4.8-only or broader Windows support certification. DPI150 is outside approved matrix. Desktop remains100% after final drain.
- Actual private, Owner-authorized non-production SQL2017 Developer14.0.2130.4/64-bit, scripts001–009; SQL2022 Developer remains the new-provision target. Provisioning/master, migration, limited MyDmsVnRuntimeRole runtime and read-only oracle identities remain distinct. No production/recovery database or new runner/framework is used.

### Final gate and exact reconciliation

| Direct project scope | Configuration / test source | Executed / PASS | Missing / non-PASS | Raw TRX SHA256 |
|---|---|---:|---:|---|
| Desktop.Session unfiltered | Release/net48/x86,cb86fbc |108/108|0/0|21D1E63409A5BDE7369285837903F2B5552D10E2D45474ECD2D3260499141C37|
| Desktop unfiltered |100%,12e6a20|329/329|0/0|3298DEAFF866E5C38D4B3A1C51C50CDE20B05BFED27CD0C4AD6C1A7564486F1F|
| Desktop unfiltered |125%,12e6a20|329/329|0/0|B6C603BD0D3C9C3D04B8C097898EC0E75847BF6E6949C93D733DD6DF30974C6D|
| Separate PackagedGuiAcceptanceTests |100%,12e6a20|18/18|0/0|479F47C77063FF0DB7F109CBF68E7F0DD6BE0CCBE0260AED71421A2961EF00C9|

Desktop source235methods (191Facts/44Theories) discovers329rows. Both full TRX sets contain exactly329 unique discovery names; standalone18 exactly matches the packaged discovery subset. No failed, skipped, aborted, unexpected-timeout or absent mandatory row. Total784 passing invocations /437 distinct obligations, without adding historical trials. Full100 took16.4698minutes, full12516.5708minutes and separate packaged15.1768minutes. Packaged actualLogin, SQL timeout/reload/checkedretry, ReceiptPost/Stock, same-process lookup, invalid input, denial, Catalog, topology and startup rows pass independently at both scales within the full suites. The final separate suite also verifies actual test build12e6 and expected productd520 metadata.

Actual final Desktop invocations (each executed sequentially after fresh scale checks; no rebuild during runs):

~~~powershell
$env:MYDMSVN_GUI_P5_PACKAGE = Join-Path (Get-Location) 'artifacts/release/gui-p5-t04-candidate2'
# Legacy variable pins PACKAGE source; actual test build is read from the assembly.
$env:MYDMSVN_GUI_P5_TEST_REVISION = 'd5207b2ce1eae09e9317625b3d7d486c994f09fe'
$env:MYDMSVN_GUI_P5_EXPECTED_SCALE = '125'
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --logger 'trx;LogFileName=t06-final-desktop125.trx' --logger 'console;verbosity=normal' --results-directory TestResults/gui-p5-t06/final-desktop125 -- RunConfiguration.TargetPlatform=x86
# After drain, Owner scale change and fresh native probe:
$env:MYDMSVN_GUI_P5_EXPECTED_SCALE = '100'
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --logger 'trx;LogFileName=t06-final-desktop100.trx' --logger 'console;verbosity=normal' --results-directory TestResults/gui-p5-t06/final-desktop100 -- RunConfiguration.TargetPlatform=x86
# After verified full-run cleanup:
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~PackagedGuiAcceptanceTests' --logger 'trx;LogFileName=t06-final-packaged100.trx' --logger 'console;verbosity=normal' --results-directory TestResults/gui-p5-t06/final-packaged100 -- RunConfiguration.TargetPlatform=x86
~~~

### Review, cleanup and remaining limits

One fresh whole-branch review42d94ba..ea2c194 found no Critical issue and one Important SQL quarantine admission gap. The single fix pass poisoned admission before both quarantine records and preserved original/recording failures. Actual3-row RED→GREEN plus both329-row full gates verify the correction; no second review is claimed. Focus regressions also pass in both final full runs. No T06 business/production/SQL implementation change triggered broader Application/SQL coverage; actual packaged SQL oracles remain independent. Earlier T03/T04 backend and attended evidence retain their own source/scope.

All17 final standalone allocations drained/dropped. Final read-only census:358/358 historical GUI ledgers terminalDropped; all189 captured T06 databases and567 principals checked against the designated instance and exact captured identity names, remaining0/0/allDisposed. Exact latest database/principal counts and owned GUI process count are zero. No prefix-based deletion; synthetic quarantine regression fixtures create no actual SQL allocation. Raw connection/credential/SQL-identity evidence remains ignored. No automatic cleanup of uncertain ownership is permitted. Desktop released only after verified drain.

All failed trials remain above with their actual revisions: old d520 driver first125 failure and full100 failures; rejected2da guard6PASS/12FAIL; correctedfc085 successful gates; regression compile/loader failures and genuine RED. The foreground-loss diagnostic establishes a missing driver precondition, not attribution to a person or external window. Final green does not erase the failures or certify unknown environments.

Deferred minor: historical status headings inside earlier verification/walkthrough/test documentation can still say OPEN/IN PROGRESS; this final section and current roadmap are authoritative. A broad historical wording cleanup is deferred. It does not reopen signed T04/T05 or convert the Down exception to PASS.

### Execution rulings retained for handoff

1. Use the plan checklist and human worksheets instead of fabricating a passing test command for human sign-off. Cost if wrong: sign-off is manually traceable rather than a machine gate.
2. Preparation-only documentation does not trigger full GUI/SQL reruns; verify package/environment evidence and actual source diff. Cost if wrong: an overlooked runtime change could escape that preparation check.
3. Attended supervision may use the explicit30-minute bounded path; automated RunAsync remains5minutes. Cost if wrong: an attended hung chapter can consume up to30minutes before forced cleanup.
4. T05 attended acceptance does not invent TDD or unfiltered reruns for Markdown-only work. Cost if wrong: worksheet mistakes need evidence review rather than an automated assertion.
5. Retain Warehouse125Dark Down NOT TESTED rather than rerun accepted groups; subsequently explicitly accepted by Owner. Cost if wrong: an isolated Down regression could remain undetected.
6. P6 install/recovery/signing/support decisions remain REL OPEN. Cost if wrong: GUI acceptance supplies no production certification.
7. DPI150 remains outside the Owner-approved matrix. Cost if wrong:150% behavior is unverified.
8. Final review preserves the signed Down exception without relabeling it. Cost if wrong: the same isolated125Dark Down regression can remain.
9. Reviewer used stored SQL/attended evidence; executor verified final live census separately. Cost if wrong: review is not a second independent live SQL/attended execution.
Operational handoff note: automatic approval review rejected the combined command containing recursive deletion of this plan's ignored .superpowers scratch directory (reported only 'blocked by policy'). The documentation commit was then executed separately. Scratch is retained; managed worktree and raw ignored TestResults are preserved. This concerns local planning files only; verified SQL/principal/process cleanup above is complete. No integration/push/merge has been performed.
