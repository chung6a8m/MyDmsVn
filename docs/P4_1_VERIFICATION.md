# P4.1 verification evidence — Desktop Login/session

Status: **Automated L3 verified; packaged/manual L4 remains open** (2026-10-15).

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

## Test Execution Report

- Baseline: locked restore succeeded; Desktop.Tests Release `net48` 200/200 and `net8.0-windows` 200/200, zero skipped.
- L1 RED→GREEN: login arbitration, active-account validation, disabled revalidation/session clearing, Login ViewModel, LoginForm, Login-only smoke and idle boundary/background-poll behavior.
- Final L1 regression additions: explicit reauthentication notice, late A-request after B-login protection, stale Login validation protection, primary credential-lookup SQL outage mapping and scoped UI graph disposal.
- Final Desktop Release: `net48` 214/214 and `net8.0-windows` 214/214; zero skipped.
- Final Server.Application Release: `net48` 134/134 and `net8.0` 134/134; Architecture `net8.0` 5/5; zero skipped.
- Both production host projects build Release with zero warnings/errors (`Desktop.App` `net48`; `Desktop.AppCore` `net8.0-windows`).
- SQL environment prerequisites and approval preflight are present. The reviewed non-SQL classification manifest was mechanically refreshed with repo inventory fingerprints after production dependency changes and its dual-TFM classification test passed 2/2. Focused `SecurityProvisioning` completed on both TFMs: each selected/discovered/executed 36 cases, skipped 0, with watchdog and ownership reconciliation complete. The first `net48` attempt used an overlong artifact path and failed before DB ownership journaling; the accepted rerun used a short artifact root. This focused result is L2 only, not L3 acceptance.
- Fresh-context whole-range review found no Critical issues and three Important issues (cross-generation invalidation, root-resolved disposable UI graphs and primary Login SQL-outage mapping); all three were fixed with regression tests before the final gates. Theme-specific Bootstrap visual verification remains a review follow-up.
- L3 final gate-affecting revision `8e3b6d3641032523cb42e4d2c61c5c17148f2b92`: Release solution build succeeded with 0 warnings/0 errors. Strict unfiltered SQL baseline completed with runner exit 0, watchdog/approval/ownership guards and eight independent TRX files (one warm-up plus three measured passes per TFM). Every TRX discovered/executed/passed 222/222, failed 0, skipped 0, for both `net48` and `net8.0`.
- L4 packaged real-user walkthroughs remain **not run**: no operator-supplied disposable Administrator/second-user credentials or attended screenshot session was provided. Headless Login smoke is intentionally not counted as interactive authentication evidence.

Readiness: **AUTOMATED L3 VERIFIED; NOT RELEASE-COMPLETE**. Keep P4.1 and dependent P5/P6 interactive acceptance open until packaged, attended L4 Login/logout/expiry/permission walkthroughs have current evidence.
