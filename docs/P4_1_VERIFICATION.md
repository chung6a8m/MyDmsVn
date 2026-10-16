# P4.1 verification evidence — Desktop Login/session

Status: **P4.1 CLOSED by explicit product-owner acceptance (2026-10-16)**. Automated L3 was verified and attended packaged/manual UI exercised real Login/logout and business flow; 30-minute idle-expiry/relogin was **not completely observed** and was accepted as an explicit evidence exception, **not** a passed test. P5/P6 release gates remain open.

## Audited starting gaps

- `LocalDesktopSession` was both a process-wide mutable session and `ICurrentUserAccessor`; request authorization could observe a later identity after an asynchronous boundary.
- Login replaced an authenticated identity without an explicit logout and published the cached DTO without authoritative SQL account validation.
- `GetCurrentUserQueryHandler` echoed cached `IsActive = true`; disabled/deleted users could remain visibly signed in while permission storage only produced a deny.
- Both production hosts opened `FoundationShellForm` directly; `--smoke-test` rendered the shell and was not evidence of real authentication.
- No production `LoginForm`/`LoginViewModel`, idle-expiry clock, input activity policy or Login-first coordinator existed.

## Implemented evidence so far

- Session state/arbitration, immutable request actor scope, SQL account validator, disabled-account logout, fail-closed protected dispatch and stable already-authorized actor behavior.
- Shared Login ViewModel/Form with cancellation, duplicate-submit guard, masked/show password, Enter/Esc behavior, safe Vietnamese errors and password clearing.
- Both hosts route through Login before shell; headless smoke renders Login only and does not authenticate or create the shell.
- Configurable monotonic idle expiry (30-minute default); keyboard/mouse activity resets it and background dispatch does not.
- Generation-conditional invalidation prevents a late user-A request or Login validation from clearing user B's newer session; failed/canceled Login attempts leave `Authenticating` deterministically.
- Each Login and shell lifecycle has its own DI scope, so disposable ViewModels/forms and user-scoped graphs are released on logout/relogin.
- Account invalidation returns to Login with an explicit Vietnamese reauthentication notice; SQL failure during credential lookup or account validation is fail-closed as `Database.Unavailable`.
- Bootstrap-styled Login controls have bounded busy/error layouts under both supported runtimes; long localized errors with Support IDs remain visible after theme changes.
- Dispatch rechecks the captured generation after asynchronous account validation, so logout/relogin during validation cannot admit a stale request under the new user.

## Test Execution Report

- Baseline: locked restore succeeded; Desktop.Tests Release `net48` 200/200 and `net8.0-windows` 200/200, zero skipped.
- L1 RED→GREEN: login arbitration, active-account validation, disabled revalidation/session clearing, Login ViewModel, LoginForm, Login-only smoke and idle boundary/background-poll behavior.
- Final L1 regression additions: explicit reauthentication notice, late A-request after B-login protection, stale Login validation protection, primary credential-lookup SQL outage mapping and scoped UI graph disposal.
- Final Desktop Release: `net48` 231/231 and `net8.0-windows` 231/231; zero skipped.
- Final Server.Application Release: `net48` 135/135 and `net8.0` 135/135; Architecture `net8.0` 5/5; zero skipped.
- Both production host projects build Release with zero warnings/errors (`Desktop.App` `net48`; `Desktop.AppCore` `net8.0-windows`).
- SQL environment prerequisites and approval preflight are present. The reviewed non-SQL classification manifest was mechanically refreshed with repo inventory fingerprints after production dependency changes and its dual-TFM classification test passed 2/2. Focused `SecurityProvisioning` completed on both TFMs: each selected/discovered/executed 37 cases, skipped 0, with watchdog and ownership reconciliation complete. The first `net48` attempt used an overlong artifact path and failed before DB ownership journaling; the accepted rerun used a short artifact root. This focused result is L2 only, not L3 acceptance.
- Fresh-context whole-range review found no remaining Critical or Important issues after fixes. Continuation review identified clipped Bootstrap controls/error text, unbounded test signals and a SQL fixture setup-cleanup gap; all were fixed with dual-runtime regression coverage before the final gates.
- L3 final gate-affecting revision `b9c5e181508c9b7c3459616a42050f116398ad71`: Release solution build succeeded with 0 warnings/0 errors. Strict unfiltered SQL baseline at local artifact root `C:\MyData\2026\p41-l3-final-b9c5e18` completed with the guarded multi-pass runner, watchdog/approval/ownership reconciliation and eight independent TRX files (one warm-up plus three measured passes per TFM). Every TRX discovered/executed/passed 223/223, failed 0 and not-executed/skipped 0, for both `net48` and `net8.0` (1,784/1,784 aggregate). A fresh post-fix whole-range review found no remaining Critical or Important findings.
- Package preparation from clean source revision `b9c5e181508c9b7c3459616a42050f116398ad71` succeeded for `net48` win-x86, `net8.0-windows` win-x64 and the separate net8.0 admin migrator. The `0.4.1-p41.2` manifest reports schema `8..8`, three packages and a clean source tree; all 394 packaged file lengths and SHA-256 values were independently verified.
- Attended L4 used approved disposable database `MyDmsVn_P41_Acceptance`, separate migration/runtime SQL identities and P3.1-provisioned Administrator credentials without exposing passwords. Both packaged desktop hosts displayed the real Login and authenticated shell. The net8 host posted `GR0000000001`; the net48 host independently reopened it as `Posted`/read-only with total `125.00`. Both hosts showed stock balance `100.0000` and stock-card movement `GR0000000001`/`100.0000`; explicit logout returned each host to `MyDmsVn — Đăng nhập`.
- A second active local user, `p41_restricted`, with zero roles/direct permissions authenticated in the packaged net8 host. Opening Products produced the server-side status `The operation is not permitted` and no catalog rows. The operator confirmed the same visible status. The 30-minute idle-expiry observation was stopped at approximately 13.5 minutes to create this PR, so packaged expiry/relogin is **not verified** and the full L4 checkbox remains open. No credentials were captured in screenshots, logs or repository files.

## Owner acceptance decision (2026-10-16)

The project owner explicitly accepts **P4.1 as complete on the basis of automated L3 and the available attended manual UI evidence**. The original 30-minute packaged expiry/relogin observation remains unverified (stopped at approximately 13.5 minutes), with no invented result. This single owner-approved P4.1 evidence exception does **not** satisfy or waive P5.1/P5.4 UI acceptance, P6 security/operations/release drills, or future changes to expiry behavior. Their remaining tasks are tracked in [the active remainder plan](plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md).

Readiness: **P4.1 OWNER-ACCEPTED COMPLETE; AUTOMATED L3 VERIFIED; PACKAGED L4 PARTIAL. LOCAL V1 RELEASE STILL BLOCKED BY P5/P6 REMAINING GATES.**
