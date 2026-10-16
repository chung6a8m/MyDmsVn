# ADR 0007 — Code-defined permissions and a single configured superuser

**Date:** 2026-10-16  
**Status:** Approved direction; implementation NOT STARTED. Detailed design is reviewed with the accompanying plan.  
**Implementation:** [SEC-REBUILD](../plans/20261016-003-permission-system-rebuild.md)

## Context and authority

The owner explicitly confirmed that MyDmsVn is an unused MVP and requested a complete replacement of permission synchronization, not a backward-compatible migration. The previous suggestion to retain synchronization tables, legacy paths, existing assignments or fallback behavior is withdrawn for this replacement.

Implement only after [TESTGATE T01–T03](../plans/20261016-001-task-gates-and-modular-tests.md) and then [TESTGATE T04–T05](../plans/20261016-002-modular-test-projects.md) have completed their required gates. The owner reports T01–T03 implementation is in an uncommitted local workspace. A remote documentation review cannot inspect or certify that workspace. This documentation PR does not modify either prerequisite plan, test tooling or application code.

This ADR and SEC-REBUILD supersede conflicting **permission provisioning and synchronization** requirements in P3/P3.1, AGENTS, security/runbook/schema and startup documentation when implementing SEC-REBUILD. They do not supersede authentication/session safeguards, explicit UnitOfWork, SQL safety, test isolation or the independent password compatibility decision in ADR 0003. Update current guidance and remove superseded operational instructions during implementation; Git history, not a maintained compatibility subsystem, retains the old design.

## Decision

Use **code-defined Permission Registry + operator-managed RBAC assignments + one configured System Superuser**. Application upgrades never synchronize permission data or automatically grant/revoke permissions. There is one authorization implementation, one bootstrap flow and one supported security baseline after cutover.

### 1. A clean replacement, not a transition layer

Delete synchronization engines/commands, release manifests used for activation, approval artifacts, managed Administrator grants, SQL catalog activation gates, obsolete schema objects, tests asserting the removed product behavior and old runbook steps. Do not retain disabled classes, obsolete CLI aliases, compatibility flags, dual-read/write, fallback roles or create-then-drop migrations for obsolete tables.

Rework the unreleased security SQL baseline and reset disposable developer/test databases explicitly. Do not migrate or preserve the old MVP data. The new supported baseline must never create `dbo.Permissions`, `PermissionGrantApprovals`, `ManagedRolePermissionGrants`, `SystemManagedRoles` or `PermissionSynchronizationState`, nor any other object used solely by the removed subsystem. Inventory the actual migration before deleting its objects; this list is not permission to overlook unnamed dependencies.

A reset is an operator action on an identified disposable database, not something Login, startup, schema checking or normal DbUp execution does. Keep approved-instance checks, ownership verification and safe teardown. Refuse unsupported nonempty databases before applying the new baseline; report that an explicit development reset is required. Such refusal is not an upgrade adapter. No reset or SQL execution is authorized by this docs PR itself.

### 2. Code owns the catalog; the operator owns assignments

`IPermissionRegistry.Definitions` is the complete, immutable catalog of the running composition. A definition contains canonical `Key`, `Module`, `Description` and `Active`/`Deprecated` status. Remove `PendingActivation`, `AdministratorGrantPolicy`, manually maintained release IDs and activation fingerprints. A computed content digest may identify an export or detect stale previews; it is never a prerequisite for deployment or authorization.

Keep canonical, ordinal key matching and reject duplicate keys including case-only collisions. SQL permission-key columns/comparisons must have equivalent case-sensitive, trailing-space-safe semantics; validate canonical keys before writing, including length and whitespace. Unknown and deprecated keys are denied, including for superuser. Do not create wildcard or implicit prefix grants.

Keep a single catalog composition over explicitly registered modules. Remove independent maintained lists such as a duplicate `PermissionKeys.All` when they can diverge. Build/architecture tests independently enumerate declared keys, protected use cases and module registration, then verify coverage by the actual composition. Do not test only one hand-written list against another. Forbid undeclared permission string literals in production authorization calls, except explicit negative test data.

Retain/rework `Users`, `Roles`, `UserRoles`, `RolePermissions` and `UserPermissions` for RBAC; no FK from grants to a SQL permission catalog. A regular user receives permissions only from explicit assignments. Direct user Deny/Allow overrides active-role grants; absence of a direct decision and active-role grant means Deny. Disabled/missing accounts are denied first. `Administrator`/`ADMINISTRATOR` is an ordinary role name with no special treatment, automatic creation, ownership record or update behavior.

### 3. One superuser identity, selected by username and bound to an account

Use the required operator-controlled setting `Security:SuperuserUsername`, normalized by the existing `UsernameNormalizer`. It selects the superuser, but a client-supplied name, DTO flag, role or permission claim is not proof of identity.

Bootstrap creates the account and a singleton `dbo.SystemSecuritySettings` row atomically. The row has `Id = 1` enforced by PK/CHECK, `SuperuserUserId` with a noncascading FK to `Users`, and `BoundNormalizedUsername`. Resolve privileges only when the configured normalized name, bound name and current database account identity all agree. The existing unique normalized-username constraint remains mandatory. Do not replace username selection with an arbitrary configurable user-ID bypass.

Only the explicit maintenance bootstrap/recovery/rename paths may establish or change this binding. Normal user/role administration cannot rename/delete the bound account, create/rename another account to the reserved username, transfer the binding or edit the operator setting. Do not auto-adopt an existing similarly named account. Concurrent bootstrap attempts must result in one account/binding and no partial assignments.

Malformed/missing configuration, absent binding or a binding/config/account mismatch produces an explicit configuration/bootstrap error and no fallback account. A disabled bound account cannot authenticate or exercise bypass; it does not become a reason to elevate another account. Other valid users retain normal RBAC when only that account is disabled. Recovery resets/reactivates the **same** bound account with expected-user-ID confirmation and a safely entered password; rename is a separate explicit operator transaction on that identity, coordinated with configuration and host restart. Neither creates a second superuser. Passwords are never CLI arguments, committed defaults, console output or audit payloads. Invalidate affected sessions on credential/identity recovery.

Both bootstrap and recovery use separately supplied maintenance SQL authority, not runtime database-owner credentials. Bootstrap does not create a role, seed catalog rows or grant all permissions. Runtime accounts cannot modify the singleton binding or execute maintenance-only commands.

### 4. Authorization evaluates a trusted, current subject

Preserve the authenticated request/session boundary. A client cannot supply the authorization subject or an `IsSuperuser` flag. Resolve the user by the trusted session user ID and verify the current database account/activity and binding; do not trust a stale display username cached at Login.

Evaluation order is: valid authenticated subject and active account; registered Active key; bound superuser match; otherwise direct decision; otherwise active-role grant; otherwise Deny. Superuser skips grant lookup/decisions, including direct Deny, but not authentication, session expiry, account revocation, validation, organization/data boundaries or business invariants. It is not SQL Server `sysadmin` or `db_owner`.

Unify all authorization overloads on this evaluator. Any administrative inspection of another user's effective permissions requires the appropriate management permission and is not impersonation. Entitlement/permission changes take effect on the next protected request; UI state is advisory. Do not add an unbounded permission cache or enumerate all grants into authentication claims. Reuse existing session cancellation/stale-result protection and test logout/relogin/recovery races.

### 5. List All Permission Keys and deliberate assignment

Expose a read-only application query and Desktop API contract backed by the actual registry, plus an offline CLI JSON export from the same composition. Include canonical key, module, description, status, application version and computed catalog digest. Offline export requires no database connection, seed, superuser or synchronization.

Authenticated catalog UI access requires the new `Security.Permissions.Read` key. Role assignment changes use existing `Security.Roles.Manage`; direct-user assignments use existing `Security.Users.Manage`. Import uses the same target-specific permission checks as editing; it is not a separate privileged backdoor. These management permissions intentionally permit RBAC administration, potentially including broad ordinary-account grants, but never superuser binding changes.

The viewer shows all current definitions even when no assignment rows exist. It can compare an optional prior JSON export to show Added/Removed/Changed keys; show Unassigned separately. No prior export means no claim about which keys are new. No SQL review-state table, approval workflow or forced acknowledgment is required. Catalog completeness is a code/test guarantee; it cannot guarantee that an operator will grant every newly added key.

Provide a minimal usable role/user administration path: create/edit/activate ordinary roles and users, safely set/reset ordinary-user passwords, membership editing, role grants and direct Allow/Deny/Inherit. Guard the reserved identity in every path. Validate writes in Application, execute atomically through Infrastructure, and update bounded UI state after successful changes. Reads and catalog listing never write assignments.

### 6. Import and audit without rebuilding synchronization

Support one versioned JSON assignment format initially; no arbitrary SQL import and no mandatory CSV/XLSX framework. Each request targets exactly one existing role ID or user ID in the current database. The file contains `schemaVersion = 1`, `targetType`, `targetId` and `entries` (`permissionKey`, `decision`). Role imports accept Allow only; user imports accept Allow or Deny. Inherit/removal is an explicit UI operation, not an omitted file row.

Default import is additive: missing rows are preserved; identical existing decisions are no-ops; a conflicting existing direct decision fails the entire import rather than being overwritten. Reject unknown/deprecated/noncanonical keys, duplicate entries, invalid targets and unsupported fields/versions before writing. Maximum payload: 1 MiB UTF-8 and 5,000 entries; enforce limits before unbounded deserialization. Never take actor identity from a file.

Preview returns the complete proposed delta, target assignment revision and catalog digest. Apply reauthorizes the current actor, reparses/revalidates the original request, recomputes the delta under a transaction and rejects stale target revision/digest or changed decisions. Do not trust a client-supplied approved delta. No persisted sync plan or approval record is introduced. Repeating an identical import is a no-op with no duplicate grants.

Use a monotonic assignment revision on each user/role aggregate, advanced atomically with membership/grant changes. Record actor, target, operation, before/after decisions, UTC time and correlation ID in `dbo.SecurityAuditEvents` in the same transaction; audit failure rolls back mutation. Log privileged administration and recovery without passwords, raw credentials or raw import payloads. Future upgrades never modify assignments solely because the catalog changed; verify this against the new baseline, not a legacy upgrade fixture.

### 7. SQL and Local trust boundary

Keep generic runtime DML on identity/RBAC tables denied. Add an explicit allowlist of narrowly scoped administration procedures, invoked only after Application authorization; procedures validate targets/constraints, update revisions and audit in the transaction. Runtime cannot execute bootstrap/recovery/binding procedures or obtain DDL, impersonation, owner or blanket `EXECUTE` authority. Update provisioning and effective-permission validation together, preserving unrelated business SQL capabilities. Test using actual least-privileged principals, not only owner connections.

The v1 Local adapter is a trusted in-process client. An actor ID passed to a SQL procedure by that process is **not** cryptographic proof against a malicious client that controls runtime SQL credentials. Document that limitation explicitly; procedure permission narrowing does not make Local tamper-proof. Do not place maintenance credentials in Desktop configuration. A hostile-client security boundary belongs in the separately planned trusted HTTP host; this plan preserves reusable Application/Contracts without silently adding the P7 server project.

## Consequences and scope limits

Adding a registered Active key makes it immediately usable by the bound superuser with no database grant/copy/activation step. Ordinary users remain default-deny until the operator deliberately assigns it. Removing a supported key cannot silently repurpose an older grant; never reuse a key for different semantics.

This is an intentional breaking MVP reset. There is no old-data preservation, compatibility mode or rollback through old binaries against the new database. Operational recovery is an explicit fresh disposable setup or recovery of the current supported design, not fallback to removed authorization code.

Keep current target frameworks and libraries, DbUp, explicit UnitOfWork, authentication/password protections and test-harness safety. Removal of the **application permission approval** workflow does not remove `MYDMSVN_SQL_TEST_APPROVAL_FILE`, SQL instance approval or test ownership journals. Do not conflate the two.

The accompanying plan owns implementation tasks, source removal inventory, per-task gates and final acceptance. A merged documentation PR changes the approved direction, not the measured implementation status.
