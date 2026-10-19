# GUI-P5 verification

This is task-scoped evidence, not full P5/P6 acceptance. [Coverage matrix](GUI_P5_COVERAGE_MATRIX.md), [plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md) and [TEST_POLICY](TEST_POLICY.md) govern. T02–T06, both attended P5 IDs and REL G1–G5 remain OPEN.

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

Independent read-only review identified and then rechecked the retained-output P2, owner-thread behavior and fake-dispatch race; final review found no blocker. A subsequent review also accepted moving compiler creation into the bounded job. P5 automated coverage gaps and packaged/attended proof remain T02–T06; this developer-host result is not a release support/sign-off matrix.
