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

## 2a. Safe first Administrator enrollment and Local desktop session (planned P3.1/P4.1)

- The existing identity tables and Login/BCrypt handler do **not** imply that an initial application Administrator or a production WinForms Login UI already exists. A deployment-only operator command must provision a first Administrator after schema migration and approved permission synchronization, using an operator-provided secret and separate privileged SQL principal. No default password/hash, implicit startup seed or re-enrollment on subsequent invocations.
- The Administrator is an ordinary **application role** with explicit `RolePermissions` assignments, **not SQL sysadmin**, not a wildcard permission and not a bypass of user direct-deny precedence. Recovering a lost administrator requires separately approved/audited maintenance, never an unguarded bootstrap replay.
- Local desktop session belongs to the currently authenticated user in one host process. Login creates that state only after successful backend verification; logout/expiry clears it, cancels/invalidates old UI state and requires reauthentication. Local Mode does not issue or emulate P7 bearer tokens. Backend authorization must fail closed without an authenticated user and remains mandatory regardless of UI state.
- Approved tasks: `docs/plans/20261013-001-initial-security-provisioning-permission-sync.md` and `docs/plans/20261013-002-desktop-login-session.md`. These are **plans, not completed implementation**.

## 3. Authorization algorithm

1. User must be authenticated and active.
2. If `UserPermissions` row exists for (user, permission): `Granted=1` => allow; `Granted=0` => deny.
3. Otherwise allow if any assigned role contains `RolePermissions` for the exact permission key.
4. Otherwise deny.

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

## 3a. Continuously synchronized permission registry (planned P3.1)

- Each feature/module declares stable, exact permission keys in a version-controlled source registry with ownership/description/lifecycle and auto-grant eligibility metadata. Registry validation must reject collisions, undeclared handler keys and unsafe accidental renames. New feature changes include tests and deployment metadata; no manual one-off production seeds.
- A new additive `dbo.Permissions` catalog is **proposed**, not present in current P3 migrations. Backfill existing assignment keys safely; retain unknown historical keys as reviewable drift rather than legitimizing unknown privileges. DbUp migrations remain immutable.
- A separate privileged per-release sync operates through **dry-run -> review/approval -> transactional apply**. New definitions are additive, repeatable and concurrency-safe; deprecated keys and existing role/direct-user assignment history are preserved. No automatic deletion, manual grant overwrite or regrant of an intentionally revoked Administrator permission.
- `Administrator` receives newly introduced **eligible and explicitly approved** permission grants under a documented, auditable policy, but ordinary roles do not. Sensitive new permissions require explicit approval before assignment. A direct `UserPermissions.Granted = false` still denies every role, including Administrator.
- Permission synchronization is **not** executed from desktop startup, Login, business API calls or ordinary client installs. Schema compatibility and authorization default-deny behavior are retained across mixed-version clients.

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
