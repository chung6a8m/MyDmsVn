# P4.1 — Desktop Login, authenticated session and logout

Status: **Not started** (2026-10-13). Follow-up to completed P4 shell/UI foundations, **not** evidence that P4 already shipped interactive Login. Depends on existing P3 Login/current-user contracts, P4 host/LocalApiClient wiring and **P3.1 verified first-administrator enrollment**. Must precede P5/P6 real-user interactive acceptance. See `docs/plans/20261013-001-initial-security-provisioning-permission-sync.md`.

Read: `AGENTS.md`, `docs/CONTRACTS.md`, `docs/SECURITY.md`, `docs/ARCHITECTURE.md`, `docs/TEST_STRATEGY.md`, `docs/P3_VERIFICATION.md`, `docs/plans/20261008-006-phase-4-desktop.md` and `docs/plans/20261008-007-phase-6-local-hardening.md`.

## Goal and boundaries

Provide one shared, keyboard-friendly and responsive WinForms `LoginForm` + `LoginViewModel` and a deterministic per-host desktop session lifecycle, reused by both `net48` and `net8.0-windows`. Login through the **existing `IIdentityApiClient`/Application Login** path; do not introduce a parallel password/SQL implementation or require P7 HTTP/token infrastructure for Local Mode.

P3 already contains `LoginCommandHandler`/BCrypt. P4 hosts currently render a shell for test/smoke; that is **not** an authenticated production session. Session state and privilege checks must fail closed from first launch.

## Task list

### P4.1-T01 — Audit contracts and explicit session state machine

- [ ] Inspect `IIdentityApiClient`, `LoginAsync`/`GetCurrentUserAsync`, `CurrentUserDto`, `ICurrentUserAccessor` and both host DI composition roots. Document actual gaps before changes; reuse and extend existing contracts minimally without changing P7 bearer semantics prematurely.
- [ ] Define a shared desktop session coordinator/accessor with states such as `Unauthenticated -> Authenticating -> Authenticated -> Expired/LoggingOut -> Unauthenticated` and explicit transitions. Session owns immutable authenticated identity (at minimum user ID) and generation/epoch, never the password; lifetime is a **host application session**, not a Windows machine-wide singleton.
- [ ] Wire Application `ICurrentUserAccessor` and the Local adapter to the session snapshot for each host. An absent/expired/disabled identity must deny **all** privileged use cases. Preserve P3 rules: authorization captures the actor for an already-running operation; UI logout cannot retroactively reattribute it.
- [ ] Decide and document a **configurable Local Mode idle/expiry policy** and controlled revalidation of current-user activity/revocation as required by the existing backend. Do not silently claim a server-enforced token TTL or automatic HTTP refresh before P7.

### P4.1-T02 — Shared compact WinForms Login UI / ViewModel

- [ ] Reusable `LoginForm` with username, masked password, Show/Hide password, submit/cancel, validation/error summary, busy indicator and accessible keyboard order (Enter submits, Esc behaves predictably); use existing Bootstrap5WinFormUI compact styling/theme, DPI/localization patterns, with no new UI framework.
- [ ] `LoginViewModel` using CommunityToolkit.Mvvm async commands and cancellation: prevent double submit, never block UI thread, bound/clear password lifecycle, dispose/cancel work on close. Input validation should mirror public contract without duplicating backend password policy.
- [ ] Provide safe Vietnamese user-facing messages for invalid credentials, disabled/expired session, SQL offline/timeout, unsupported schema and unexpected failures; backend may intentionally map unknown/disabled users to the **same public credential error** to prevent account enumeration. Do not reveal SQL details/stack traces.
- [ ] No password persistence, prefilled administrator credentials, automatic privileged login or credentials in debug logs, telemetry, exception dialogs or screenshots. Never use test/`--smoke-test` switches to bypass authentication in real business flows.

### P4.1-T03 — Host startup, authentication gate and DI scope

- [ ] Both `MyDmsVn.Desktop.App` and `MyDmsVn.Desktop.AppCore` use the same shared startup coordinator. Verify runtime configuration/schema with bounded, localized error handling, then show Login **before allowing access to authenticated shell/navigation**. Do not run DbUp, permission sync or first-user enrollment from either host.
- [ ] On success set session identity atomically, refresh current user as needed, then construct/activate `FoundationShellForm`/real workspace with current-user label and permission-based UI actions. On failure, remain unauthenticated, no shell/business API execution.
- [ ] Distinguish headless `--smoke-test` of UI wiring from actual production auth: tests can inject a fake session in test-specific DI only, while production bootstrap always requires valid credentials. Avoid global mutable/static identity shared between forms/tests.
- [ ] Treat changing logins on the same desktop process as a new session: reset/recreate user-scoped ViewModels, query caches, lookups and navigation state so prior user's authorized data or permissions never leak.

### P4.1-T04 — Logout, expiry and concurrency safety

- [ ] Logout explicitly clears session and user-scoped state, cancels pending UI reads/mutations when possible, closes/disables privileged workspace and returns to Login; show success/error UI as appropriate and do not leave hidden authorized screens usable.
- [ ] Expired/disabled/revoked session: a 401/`Auth.Unauthorized` (or authenticated status recheck) returns to Login with an explicit reauthentication notice, **not** a silent retry that could repeat a mutation. A 403/`Auth.Forbidden` retains the session but denies the specific action. Centralize API error mapping for Local and future HTTP modes.
- [ ] Generation/epoch-check async responses so a slow result from user A cannot update user B's screens after logout/relogin; cancel and dispose obsolete operations. Already-authorized in-flight server writes may have committed; on re-login reload server state rather than assuming cancellation rolled them back.
- [ ] Avoid password/session material in messenger payloads and logs; clearing UI state must not issue privilege-grant or database-changing commands.

### P4.1-T05 — Dual-runtime automated and manual verification

- [ ] Deterministic ViewModel tests for success, invalid credentials, disabled user public error, validation, cancellation, duplicate submit, network/SQL timeout, busy state, error localization and password clearing.
- [ ] Real SQL (isolated disposable DB) integration: provision first Administrator via P3.1; Login with correct/incorrect credentials; verify `CurrentUser`, role/direct deny, anonymous deny, logout/relogin as another user; direct deny must override Administrator role.
- [ ] Controlled fake scheduler tests for idle expiry, login/logout/relogin races, two Login attempts, revocation/activity checks, stale UI responses and failure/cancel during startup. Prove backend denies unauthenticated calls even when UI guards are bypassed.
- [ ] Safe STA tests for `LoginForm` and shell on **both `net48` and `net8.0-windows`**: control handles, DPI, compact layout, keyboard access, theme, disposal and no `DataError`/`ThreadException`/UnhandledException default modal dialog. Tests must be bounded; never open an unhandled Login prompt in headless CI.
- [ ] Manual smoke from packaged binaries: clean DB -> P3.1 CLI provisioning -> login -> catalog/receipt/stock permission checks -> logout -> another user -> expiry/relogin -> restart. Record environment, target TFM, pass/fail/blocked and screenshots without credentials.

## Exit / acceptance criteria

- [ ] Both production hosts display the actual Login UI and **cannot** reach business features before authenticated session creation; invalid credentials cannot create a session.
- [ ] Shared Login and session components are reusable across both TFMs; LocalApiClient calls Application, not direct SQL from UI. No privileges depend on a UI-only check.
- [ ] Logout and session expiry are explicit; no cross-user cache/UI leaks or stale response reuse; no silent repeat of mutating requests.
- [ ] With P3.1 Administrator fixture, authenticated P5.1/P5.4 walkthroughs and P6 packaged GUI smoke can run without a test-only production bypass.
- [ ] Update `docs/CONTRACTS.md`/`docs/SECURITY.md` if contract/policy changes, `docs/DEVELOPMENT_PLAN.md` and evidence files after **actual** tests; do not reopen or relabel historical P4 validation as if it covered Login.

## Out of scope

New user/role management screens, password recovery email, "remember me", legacy hash guessing, HTTP Bearer token issuance/refresh/revocation (P7), automated database migrations/seeding on desktop startup, and new business modules.
