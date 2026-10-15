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
- L2 Desktop after first vertical slice: Release `net48` 206/206 and `net8.0-windows` 206/206, zero skipped. After idle/disabled-account additions, `net8.0-windows` is 208/208 and Server.Application `net8.0` is 134/134, zero skipped; the final `net48` Desktop rerun is still required.
- Both production host projects build Release with zero warnings/errors (`Desktop.App` `net48`; `Desktop.AppCore` `net8.0-windows`).
- SQL environment prerequisites and approval preflight are present. The focused `SecurityProvisioning` runner is currently blocked before discovery because the required reviewed non-SQL classification manifest is stale after the production dependency changes (`MissingSqlCategory, InvalidOrStaleExemption`). This was not bypassed and is not a SQL pass.
- L3: not run against final revision.
- L4 packaged real-user walkthroughs/concurrency/failure probes: not run.

Readiness: **NOT VERIFIED**. Keep P4.1 and dependent P5/P6 interactive acceptance open until every mandatory gate has current evidence.
