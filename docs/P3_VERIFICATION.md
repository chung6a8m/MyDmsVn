# P3 verification — Identity and authorization foundation

Date: 2026-10-09  
Scope: P3 only. No P4 WinForms shell, P5 business feature, HTTP bearer host, or production structured-log sink was added.

## Implemented

- DbUp identity schema for `Users`, `Roles`, `UserRoles`, `RolePermissions`, and `UserPermissions`, preserving `PasswordHash` and `PasswordSalt` while adding an explicit `PasswordAlgorithm` marker, normalized-name uniqueness, foreign keys, relationship uniqueness, activity flags, UTC audit fields, and row versions where admin edits need optimistic concurrency.
- Application identity abstractions for current user, user persistence, current/legacy password verification, login and current-user queries. Missing, anonymous, inactive, wrong-password, and unsupported-algorithm paths fail closed without account-enumerating public messages.
- BCrypt.Net-Next 4.2.0 hashing for new/current credentials with work-factor rehash detection. Password input is capped at BCrypt's 72-byte UTF-8 boundary in both policy and hashing/verification paths so distinct suffixes cannot authenticate through truncation. Password replacement is a single conditional SQL update matching the prior hash and algorithm; BCrypt's embedded salt is used and the retained legacy `PasswordSalt` column receives the documented empty sentinel.
- A deliberately unsupported legacy verifier. No legacy hash algorithm was guessed. The orchestration can rehash only after a separately supplied verifier validates an approved algorithm fixture.
- Exact, declared permission keys; direct user grant/deny precedence; role union only when no direct row exists; unknown keys, anonymous users, inactive users, inactive roles, and missing grants deny. MediatR authorization runs before validation and handlers, so a denied test mutation cannot write.
- A typed security-audit contract containing only action, outcome, user ID, and a validated UTC timestamp. Login emits success/failure/disabled/rehash-failure events without passing request payloads or password material to the sink. P6 still owns the production structured-log sink; future privileged mutation handlers must emit their success event only after commit.
- Dapper SQL stores for user lookup, atomic password replacement, and permission lookup. Direct overrides and active-role grants are returned by one serializable snapshot so concurrent administration cannot produce an impossible allow decision. No permission cache was introduced, so revocation/deactivation is visible on the next check.

## Commands and results

From the repository root in the P3 worktree:

```text
dotnet restore MyDmsVn.sln --force-evaluate
Result: succeeded; downstream lock files refreshed after adding BCrypt.Net-Next to Server.Infrastructure.

dotnet restore MyDmsVn.sln --locked-mode
Result: succeeded.

dotnet build MyDmsVn.sln -c Release --no-restore
Result: succeeded; 0 warnings, 0 errors.

dotnet test MyDmsVn.sln -c Release --no-build --no-restore
Result: 247 passed, 0 failed, 0 skipped across all eligible target frameworks.
```

Breakdown:

- Architecture: 5 passed (`net8.0`).
- Server.Application: 62 passed on `net48`; 62 passed on `net8.0`.
- Desktop: 14 passed on `net48`; 14 passed on `net8.0-windows`.
- Server.Infrastructure integration: 45 passed on `net48`; 45 passed on `net8.0`.

The configured `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` targeted the guarded disposable-database harness. Identity migrations, uniqueness, permission persistence/revocation, a concurrent permission-snapshot regression, compare-and-swap password updates, and BCrypt rehash all ran against isolated databases that were removed after each test.

## Blocked prerequisite and remaining risks

- Legacy password verification and legacy-to-BCrypt integration evidence remain blocked until the owner supplies an approved algorithm description or sanitized fixture. Unknown legacy markers are rejected; this phase contains no compatibility guess or permissive fallback.
- The default security-audit sink is intentionally no-op. P6 owns structured logging/persistence and must preserve the typed redaction boundary.
- Local Mode remains a two-tier deployment whose SQL credentials can bypass application checks; least-privilege SQL configuration and trusted installation controls remain operational requirements.
- P4 must supply the desktop session/current-user accessor and Local identity client implementation. P7 owns bearer tokens and the remote enforcement boundary.
- The password compare-and-swap command currently uses Dapper inside `SqlIdentityStore`; aligning this command-side write with the repository's RepoDb convention is deferred as a review follow-up because changing persistence style is not required to close the P3 security defects above.

Next roadmap task: P4 desktop foundation, starting with `docs/plans/20261008-006-phase-4-desktop.md`.
