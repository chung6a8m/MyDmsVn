# Identity, authentication and authorization

## 1. Database baseline and backward compatibility

Preserve the conceptual tables from `project-idea.md`:
`Users` (`UserId`, `Username`, `DisplayName`, `Email`, `Source`, **`PasswordHash` + `PasswordSalt`**, activity and audit fields), `Roles`, `UserRoles`, `RolePermissions`, `UserPermissions`.

During migration, respect the legacy source schema and width where historical values must be imported. New schema may add safe optional columns for `PasswordAlgorithm`/version if justified by test fixtures. Do not rename/remove `PasswordSalt` or assume it can be dropped because BCrypt embeds a salt.

Add:
- case/collation-aware unique index on normalized `Username` (and `RoleName`);
- unique (`UserId`, `RoleId`), (`RoleId`, `PermissionKey`), (`UserId`, `PermissionKey`);
- FK and appropriate indexes; optional `rowversion` for admin edits;
- clear constraints for statuses/sources and audit UTC timestamps.

## 2. Authentication / migration

- Introduce `IPasswordHasher` / `ILegacyPasswordVerifier` with an explicit algorithm/version identification strategy.
- Verify legacy credentials using **known real algorithm specifications and sanitized test vectors**, not a guess about `PasswordSalt` semantics.
- New/rehash credentials should use a current one-way password hasher such as BCrypt; where legacy columns remain NOT NULL, store an explicitly documented harmless sentinel for an unused separate salt (e.g. empty value), not a security secret.
- On successful legacy login, rehash/migrate atomically to the new format and update algorithm marker, with safe failure/rollback behavior. Never log supplied password, hash or salt.
- Distinguish disabled user from wrong credentials internally without disclosing account enumeration through public messages.
- Local Mode executes authentication in-process. Its SQL credentials are a security limitation; use least-privilege database principal and trusted installation controls.
- Remote Mode adds HTTP auth (Bearer tokens), token expiry handling and transport security; cookies not required for desktop bearer scenario. Revocation/session model finalized in P7.

## 2a. Configured superuser enrollment and Local desktop session

- The current SEC-REBUILD implementation replaces the historical first-Administrator/synchronization workflow. A single configured superuser is bound to one database user through the protected singleton `dbo.SystemSecuritySettings` row. The operator command `security bootstrap-superuser` is maintenance-only, accepts only an empty user store and unbound settings table, and creates no role or permission grants. There is no default password/hash, desktop-startup seed or automatic re-enrollment.
- Runtime configuration `Security__SuperuserUsername` must match both the bound normalized username and the current database account name. Startup rejects a missing/mismatched binding. Each authorization check reads current account activity and the singleton binding from SQL; a disabled account is denied. The configured superuser bypasses application role/direct-user decisions only while that database identity is active and matches the binding. `Administrator` is an ordinary role name with no bypass.
- Local desktop session belongs to the currently authenticated user in one host process. Login creates that state only after successful backend verification; logout/expiry clears it, cancels/invalidates old UI state and requires reauthentication. Local Mode does not issue or emulate P7 bearer tokens. Backend authorization must fail closed without an authenticated user and remains mandatory regardless of UI state.
- P3.1 T01–T07 implementation and automated acceptance evidence are complete; see `docs/P3_1_VERIFICATION.md`. **P4.1 is owner-accepted complete (2026-10-16)** using automated L3 and attended packaged UI, with an explicit incomplete idle-expiry/relogin observation accepted as an exception, not a passed manual test. See `docs/P4_1_VERIFICATION.md`. Current behavior: Local dispatch captures one immutable actor/generation per request, production Login validates current SQL account state before publishing the session, and protected dispatch revalidates active-account status. The default Local idle timeout is 30 minutes, measured with a monotonic clock; keyboard/mouse input resets it, while API/background polling does not. Other P5/P6 GUI/operational gates remain open in `docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md`. The [SEC-REBUILD plan](plans/20261016-003-permission-system-rebuild.md) proposes replacing this *current* permission system, but has not been implemented.

## 3. Authorization algorithm

1. User must be authenticated and active in the current session and current database account.
2. The configured superuser is allowed only when the current database user id and normalized username match the protected singleton binding and the account is active.
3. For every other user, if a `UserPermissions` row exists for (user, permission), `Granted=1` allows and `Granted=0` denies.
4. Otherwise allow if any assigned active role contains `RolePermissions` for the exact permission key.
5. Otherwise deny.

Unknown or undeclared keys default deny. Permission names are stable, namespaced keys, not UI captions. Explicit user deny always wins over all role grants.

Proposed keys for P5:
- `Catalog.Products.Read`, `Catalog.Products.Write`;
- `Catalog.Warehouses.Read`, `Catalog.Warehouses.Write`;
- `Catalog.Employees.Read`, `Catalog.Employees.Write`;
- `Catalog.Customers.Read`, `Catalog.Customers.Write`;
- `Inventory.GoodsReceipts.Read`, `Inventory.GoodsReceipts.Write`, `Inventory.GoodsReceipts.Post`;
- `Inventory.Balances.Read`, `Inventory.StockCard.Read`;
- `Security.Users.Manage`, `Security.Roles.Manage`.

Check authorization at each command/query use case in Application. The UI may hide/disable actions for usability but must **not** be the enforcement point. HTTP endpoints also require authentication/authorization middleware as appropriate.

Authorization snapshots the authenticated user identifier before its first asynchronous permission lookup and evaluates permissions for that exact user. Mutating requests carry the authorized actor snapshot into their handlers for audit fields. A later Local desktop sign-out or sign-in does not cancel or reattribute an already-authorized in-flight operation; explicit request cancellation remains the cancellation mechanism.

## 3a. Code-defined permission registry and deliberate RBAC

- `IPermissionRegistry` is composed deterministically from module-owned key definitions. A definition contains key, module, description and Active/Deprecated lifecycle. It rejects malformed keys and collisions and verifies that every referenced use-case key is composed. There is no synchronized SQL permission catalog, release manifest, `PendingActivation` state or automatic grant policy.
- Migration `002_Identity.sql` creates `dbo.SystemSecuritySettings` with a singleton key, a noncascading FK to `Users`, a normalized bound username and a unique user binding. Runtime may read this row but cannot modify it. The maintenance-only bootstrap command creates exactly one active user and this binding in one transaction after validating an empty store; it stores only a one-way password hash and no role/grant rows.
- Every ordinary user receives permissions only from explicit user or active-role assignments. New permission keys do not grant access until an operator assigns them through a reviewed security-administration workflow. Direct user Deny still overrides all role grants; unknown, deprecated and unassigned keys deny.
- The historical P3.1 provisioning and permission-sync implementation remains documented as historical evidence in `docs/P3_1_VERIFICATION.md`; its old commands, SQL artifacts and execution path have been removed from the current application. Current operator steps are in [SECURITY_PROVISIONING_RUNBOOK.md](SECURITY_PROVISIONING_RUNBOOK.md).

## 4. Permission cache

P5 can read from SQL without premature complex caches. If caching is added: scope keys by user + permissions version; invalidate on direct-permission change, role assignment, role-permission change, deactivation and user deletion. Never copy every permission into huge authentication claims. Evaluate dynamic permissions server-side.

## 5. Local and remote risk model

- v1: Desktop SQL access cannot provide the same trust boundary as a separate server. Guard database account privileges, SQL network exposure, backups and deployment package integrity. Never embed sa/admin passwords.
- v2: Only Server.Api knows SQL credentials; desktop uses bearer credentials; HTTPS, rate limits, authorization and structured audit operate at the host boundary.
- No business logic or authorization bypass flag conditional on local mode.

## 6. Test matrix

Direct user grant / deny; deny-over-role; union of multiple roles; unknown key; disabled user; permission removal/role change; invalid credentials; legacy verification and rehash; weak/default passwords rejected; concurrent permission updates; no password material in logs; unauthorized posting cannot mutate inventory.

Legacy migration requires approved fixtures before implementation. If no representative legacy hash format is supplied, implement the abstraction and mark migration behavior blocked rather than inventing a verifier.

Additional P3.1/P4.1 verification: clean database first-admin enrollment, re-run without reset, new feature permission registry/sync/review, direct deny and administrator-revoked grant preservation, SQL runtime principal unable to provision, Login-first startup, logout/expiry, stale-session response isolation and bounded dual-host STA GUI tests.
