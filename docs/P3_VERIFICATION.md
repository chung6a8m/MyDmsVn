# P3 verification — Identity and authorization foundation

Date: 2026-10-09  
Scope: P3 only. No P4 WinForms shell, P5 business feature, HTTP bearer host, or production structured-log sink was added.

## Implemented

- DbUp identity schema for `Users`, `Roles`, `UserRoles`, `RolePermissions`, and `UserPermissions`, preserving `PasswordHash` and `PasswordSalt` while adding an explicit `PasswordAlgorithm` marker, normalized-name uniqueness, foreign keys, relationship uniqueness, activity flags, UTC audit fields, and row versions where admin edits need optimistic concurrency.
- Application identity abstractions for current user, user persistence, current/legacy password verification, login and current-user queries. Missing, anonymous, inactive, wrong-password, and unsupported-algorithm paths fail closed without account-enumerating public messages.
- BCrypt.Net-Next 4.2.0 hashing for new/current credentials with work-factor rehash detection. Password replacement is a single conditional SQL update matching the prior hash and algorithm; BCrypt's embedded salt is used and the retained legacy `PasswordSalt` column receives the documented empty sentinel.
- A deliberately unsupported legacy verifier. No legacy hash algorithm was guessed. The orchestration can rehash only after a separately supplied verifier validates an approved algorithm fixture.
- Exact, declared permission keys; direct user grant/deny precedence; role union only when no direct row exists; unknown keys, anonymous users, inactive users, inactive roles, and missing grants deny. MediatR authorization runs before validation and handlers, so a denied test mutation cannot write.
- A typed security-audit contract containing only action, outcome, user ID, and a validated UTC timestamp. Login emits success/failure/disabled/rehash-failure events without passing request payloads or password material to the sink. P6 still owns the production structured-log sink; future privileged mutation handlers must emit their success event only after commit.
- Dapper SQL stores for user lookup, atomic password replacement, direct permission lookup, and active-role permission lookup. No permission cache was introduced, so revocation/deactivation is visible on the next check.

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
Result: 239 passed, 0 failed, 0 skipped across all eligible target frameworks.
```

Breakdown:

- Architecture: 5 passed (`net8.0`).
- Server.Application: 61 passed on `net48`; 61 passed on `net8.0`.
- Desktop: 14 passed on `net48`; 14 passed on `net8.0-windows`.
- Server.Infrastructure integration: 42 passed on `net48`; 42 passed on `net8.0`.

The configured `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` targeted the guarded disposable-database harness. Identity migrations, uniqueness, permission persistence/revocation, compare-and-swap password updates, and BCrypt rehash all ran against isolated databases that were removed after each test.

## Blocked prerequisite and remaining risks

- Legacy password verification and legacy-to-BCrypt integration evidence remain blocked until the owner supplies an approved algorithm description or sanitized fixture. Unknown legacy markers are rejected; this phase contains no compatibility guess or permissive fallback.
- The default security-audit sink is intentionally no-op. P6 owns structured logging/persistence and must preserve the typed redaction boundary.
- Local Mode remains a two-tier deployment whose SQL credentials can bypass application checks; least-privilege SQL configuration and trusted installation controls remain operational requirements.
- P4 must supply the desktop session/current-user accessor and Local identity client implementation. P7 owns bearer tokens and the remote enforcement boundary.

Next roadmap task: P4 desktop foundation, starting with `docs/plans/20261008-006-phase-4-desktop.md`.
