# Identity, permissions and Local sessions

**Current implementation:** SEC-REBUILD accepted in PR #34; documentation refreshed 2026-10-18. RESP and L2DB preserve these semantics. [ADR 0007](adr/0007-code-defined-permissions-and-single-superuser.md) records the decision; [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md) owns operator commands.

## 1. Identity and password compatibility

Current identity tables include Users, Roles, UserRoles, RolePermissions, UserPermissions, SystemSecuritySettings and SecurityAuditEvents. The checked-in migrations define their constraints/revisions. Preserve `Users.PasswordHash`, `PasswordSalt` and algorithm identity when importing legacy data; a separate salt column is not permission to guess the old algorithm.

New credentials use the configured one-way password hasher. Legacy verification/rehash must use approved specifications and sanitized test vectors, fail closed for unsupported algorithms and preserve valid state on failure. Never log the supplied password, stored hash/salt or raw authentication exceptions. Legacy release support remains an explicit [open decision](DEFERRED_WORK.md).

## 2. Authorization order

The current [PermissionAuthorizationService](../src/MyDmsVn.Server.Application/Security/PermissionAuthorizationService.cs) applies this order:

1. Require an authenticated, active current actor with the matching user ID and an **Active definition in the code registry**. Unknown or deprecated keys deny before any bypass.
2. Read the current SQL account/binding snapshot and require an active account.
3. Allow the configured superuser only when current user ID, normalized account name and singleton database binding match the configured identity policy.
4. For other accounts, use an explicit UserPermissions Grant/Deny when present.
5. Otherwise allow an exact-key grant through an active assigned role; otherwise deny.

Direct user Deny overrides ordinary role grants. The configured superuser's matching identity bypass is distinct from those assignments; the role name Administrator has no intrinsic bypass.

Every Application use case enforces authorization; hiding a control does not enforce it. Mutating operations carry the admitted actor snapshot to their handlers and audit. Provider migration must not introduce stale positive permission caches, claims expansion or a query path that skips these checks.

## 3. Code-owned catalog and deliberate administration

`PermissionRegistryComposition.CreateDefault()` composes module definitions for Catalog, Inventory and Security. Keys, module/description and Active/Deprecated lifecycle come from code. Malformed definitions, collisions or missing referenced keys are rejected. The code catalog is available through a read-only Application query and offline CLI export.

New keys do not create grants or mutate authorization tables. Operators deliberately assign ordinary user/role permissions. There is no persisted catalog synchronization, release activation state, automatic Administrator grants or startup seeding.

Security administration/import uses the current target revision and catalog digest, protected-target rules and SQL procedures. Preview is revalidated on Apply; stale/conflicting decisions fail. Mutation and corresponding security audit are atomic. Preserve exact revision, result and error semantics during L2DB; the [Contracts](CONTRACTS.md) and current store/procedures are the source of truth.

## 4. Configured superuser and SQL principals

The separate administrator package's `security bootstrap-superuser` command requires an empty user store and unbound settings. It creates one active account and its singleton binding in a transaction, with an interactively confirmed target and masked password entry. It creates no role/permission grants and never runs during desktop startup.

`Security__SuperuserUsername` must agree with the bound normalized username and current account. Startup fails closed on a missing/mismatched binding. Ordinary administration cannot modify the protected identity; rename/recovery uses explicit maintenance commands and the current bound user ID. Bootstrap is not a recovery command.

Desktop uses the least-privilege runtime SQL principal. Migrations, bootstrap and protected maintenance use a separate privileged principal outside the desktop package. The runtime's reviewed execute permissions are restricted to named security procedures; it cannot change schema or the binding directly. See [SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md).

## 5. Local session behavior

Login validates current backend account state before publishing the process-local session. Local dispatch captures one immutable actor/generation; protected dispatch revalidates account activity. Logout/expiry clears the session and invalidates old UI state. Already admitted operations retain their actor and follow explicit cancellation; logout is not an implicit rollback of a committed or admitted command.

The default idle timeout is 30 minutes on a monotonic clock. Keyboard/mouse activity renews it; background polling/API traffic does not. Local Mode does not issue bearer tokens. A future trusted HTTP host owns its own authentication/session boundary.

P4.1 was owner-accepted with a specific incomplete packaged idle-expiry observation; [History](HISTORY.md) records that qualification. SEC-REBUILD acceptance is complete. Pending P5/P6 walkthrough/release checks remain in [Deferred work](DEFERRED_WORK.md), not an active permission-rebuild task.

## 6. Trust, audit and tests

Local Mode runs Application code on a client with SQL connectivity. A person controlling that machine/process can act within the runtime credential's SQL privileges; least privilege limits access but does not create a trusted server. Do not claim an actor ID passed from that process is cryptographic proof. The deferred HTTP service is the separate trust boundary.

Use current audit sinks, safe error mapping and redaction. Workstation log files are not immutable centralized audit. Preserve existing post-commit diagnostics semantics; a log failure does not retroactively roll back committed business data.

[TEST_STRATEGY](TEST_STRATEGY.md) covers active-account/registry/binding checks, direct decisions and roles, protected targets, imports/revisions/audit, legacy boundaries, sessions and unauthorized no-mutation behavior. [TEST_POLICY](TEST_POLICY.md) selects execution scope. Respawn must clear/reseed security state in owned test databases without reintroducing the retired production provisioning workflow.
