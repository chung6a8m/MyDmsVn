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

## 4. Permission cache

P5 can read from SQL without premature complex caches. If caching is added: scope keys by user + permissions version; invalidate on direct-permission change, role assignment, role-permission change, deactivation and user deletion. Never copy every permission into huge authentication claims. Evaluate dynamic permissions server-side.

## 5. Local and remote risk model

- v1: Desktop SQL access cannot provide the same trust boundary as a separate server. Guard database account privileges, SQL network exposure, backups and deployment package integrity. Never embed sa/admin passwords.
- v2: Only Server.Api knows SQL credentials; desktop uses bearer credentials; HTTPS, rate limits, authorization and structured audit operate at the host boundary.
- No business logic or authorization bypass flag conditional on local mode.

## 6. Test matrix

Direct user grant / deny; deny-over-role; union of multiple roles; unknown key; disabled user; permission removal/role change; invalid credentials; legacy verification and rehash; weak/default passwords rejected; concurrent permission updates; no password material in logs; unauthorized posting cannot mutate inventory.

Legacy migration requires approved fixtures before implementation. If no representative legacy hash format is supplied, implement the abstraction and mark migration behavior blocked rather than inventing a verifier.
