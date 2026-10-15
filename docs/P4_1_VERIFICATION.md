# P4.1 verification evidence — Desktop Login/session

Status: **In progress / not release-verified** (2026-10-15).

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

## Test Execution Report (partial)

- Baseline: locked restore succeeded; Desktop.Tests Release `net48` 200/200 and `net8.0-windows` 200/200, zero skipped.
- L1 RED→GREEN: login arbitration, active-account validation, disabled revalidation/session clearing, Login ViewModel, LoginForm, Login-only smoke and idle boundary/background-poll behavior.
- L2 Desktop after idle/disabled-account additions: Release `net48` 208/208 and `net8.0-windows` 208/208; Server.Application `net8.0` 134/134; zero skipped.
- Both production host projects build Release with zero warnings/errors (`Desktop.App` `net48`; `Desktop.AppCore` `net8.0-windows`).
- SQL environment prerequisites and approval preflight are present. The reviewed non-SQL classification manifest was mechanically refreshed with repo inventory fingerprints after production dependency changes and its dual-TFM classification test passed 2/2. Focused `SecurityProvisioning` completed on both TFMs: each selected/discovered/executed 36 cases, skipped 0, with watchdog and ownership reconciliation complete. The first `net48` attempt used an overlong artifact path and failed before DB ownership journaling; the accepted rerun used a short artifact root. This focused result is L2 only, not L3 acceptance.
- L3: not run against final revision.
- L4 packaged real-user walkthroughs/concurrency/failure probes: not run.

Readiness: **NOT VERIFIED**. Keep P4.1 and dependent P5/P6 interactive acceptance open until every mandatory gate has current evidence.
