# P3.1 — Initial security provisioning and continuous permission synchronization

Status: **Not started** (2026-10-13). Follow-up to completed P3, **not** retroactive completion evidence for P3. Depends on P1–P3 identity, password, schema and authorization foundations. Must be verified **before P4.1 authenticated desktop login walkthroughs, P5 interactive acceptance or P6 release**.

Read: `AGENTS.md`, `docs/SECURITY.md`, `docs/DEVELOPMENT_PLAN.md`, `docs/CONFIGURATION_AND_SQL_SECURITY.md`, `docs/P3_VERIFICATION.md`, `docs/TEST_STRATEGY.md`, `docs/plans/20261008-007-phase-6-local-hardening.md`.

## Objective

On a fresh application database, provide a **controlled, auditable, one-time first-administrator enrollment** and a **repeatable per-release permission synchronization mechanism**. Every new feature/module declares its permission keys in source code; deployment synchronizes the authoritative catalog and eligible Administrator grants without hardcoded passwords, permission bypasses or manual per-module seed scripts.

P3 created `Users`, `Roles`, `UserRoles`, `RolePermissions` and `UserPermissions` and the Login/BCrypt infrastructure; the existing `002_Identity.sql` does **not** provision an application Administrator. A SQL/DBA migration principal is **not** the same as an application user assigned the Administrator role.

## Non-negotiable security and architecture rules

- Keep existing authentication/authorization: authenticated active user, direct `UserPermissions` decision first (explicit deny still wins), otherwise active role union, unknown permission **deny**. No `IsAdministrator` / role-name shortcut, blanket bypass, wildcard permission or automatic direct-user grants.
- SQL **runtime principal remains least-privileged** and unable to INSERT/UPDATE `Users`, `Roles`, `UserRoles`, `RolePermissions` or the new registry table. Use a separate **operator-invoked privileged provisioning/sync command** (or an explicitly scoped maintenance principal), never desktop startup, Login, LocalApiClient, HTTP request or ordinary installer.
- DbUp numbered migrations remain **immutable**; introduce a new additive migration after `007_Inventory_Stock.sql` if needed. Do not rewrite `002_Identity.sql`, journal history or existing user data.
- No default administrator username/password, shipped BCrypt hash, secret in source, config, command-line arguments, environment-variable dumps, CI output, screenshots or logs. Do not expose SQL admin credentials to the desktop.
- Do not seed production catalog/business data here. All SQL tests use guarded disposable databases; no unattended production reset.

## Task list and dependency order

### P3.1-T01 — Permission catalog architecture and stable declarations

- [ ] Define an explicit version-controlled `PermissionDefinition`/`IPermissionRegistry` model (at minimum exact `Key`, owning module, description, lifecycle status and administrator auto-grant eligibility/sensitivity). Existing P5 `Catalog.*`, `Inventory.*` and `Security.*` keys must be registered without renaming public keys.
- [ ] Each module declares its own constants/definitions and registers them via an explicit deterministic composition entry point in the shared Application/Domain-appropriate layer; **no UI/SQL-dependent scan, reflection-order reliance or hidden duplicate definitions**. Adding a feature includes its declaration, registration, authorization checks, tests and deployment manifest changes in the same PR.
- [ ] Validate duplicate keys, invalid/blank keys, casing/ordinal equality, collisions between modules, excessive length (current SQL key limit: 100 characters), descriptions and lifecycle metadata. Fail release validation on omissions; a permission referenced by a handler but absent from the registry must not silently work via a preexisting SQL grant.
- [ ] Build a deployable **versioned registry snapshot/manifest** from the deployed application modules, not a mutable ad-hoc list typed by an operator. Support old/new client compatibility review when the shared DB is upgraded.

### P3.1-T02 — Additive SQL registry migration and compatibility

- [ ] Propose/document a `dbo.Permissions` catalog (exact `PermissionKey` unique with binary/ordinal semantics consistent with existing `RolePermissions`/`UserPermissions`, module, description, lifecycle/active state, audit fields, and optional introduced/deprecated release metadata). Specify authoritative schema and check it against P1/P6 schema preflight/manifest.
- [ ] Create a new ordered DbUp migration; **backfill distinct keys from existing `RolePermissions` and `UserPermissions`** without removing/regranting existing assignments. Unknown legacy keys must be surfaced as **unregistered drift**, not accepted into the authoritative code registry.
- [ ] If introducing foreign keys from assignment tables to `Permissions`, justify rollout/upgrade order and SQL compatibility first; never break existing installations or silently turn known historical permissions into grants. A no-FK staged design is acceptable if migration safety requires it.
- [ ] Update controlled migration/provisioning SQL privilege allowlists and version-compatibility documents/tests. Desktop runtime needs only the necessary read permissions, not registry mutation rights.

### P3.1-T03 — Repeatable sync engine: dry-run, apply and auditing

- [ ] Implement a privileged `permissions sync --dry-run` and `permissions sync --apply` (names illustrative; document final CLI). Produce a deterministic, reviewable plan: added definitions, changed descriptions/status, new proposed Administrator grants, deprecated/unknown keys, and conflicts.
- [ ] **Additive/idempotent** reconciliation with bounded transaction, unique constraints and serialization/application lock: multiple runs and competing operators cannot create duplicate rows or partially grant permissions. On failure, roll back all planned changes.
- [ ] Never automatically delete/revoke assignments or physically delete definitions when a key disappears. Mark an explicitly reviewed key deprecated and retain historical assignments; accidental registry omissions/rename are errors, not permission migration. Key rename requires reviewed mapping/ADR and safe migration.
- [ ] Never rewrite manually maintained role assignments or direct-user allow/deny rows. Do not turn a deliberately removed Administrator role permission back on during a routine sync. Record managed-grant origin/policy or an equivalent explicit reviewable ownership model.
- [ ] Produce redacted, structured operator audit with release/manifest identifier, actor/principal, DB identity, UTC time, changes, outcome and reason; no password/hash/salt/connection string. Fail safely on unsupported DB, unexpected schema or incompatible registry.

### P3.1-T04 — Administrator role policy for new permissions

- [ ] Create `Administrator` role deterministically via normalized unique role name; distinguish **system-managed default role** from user-created roles with collision/ownership checks. Never impersonate an existing unrelated role.
- [ ] Initial enrollment may grant all **approved, registered** baseline permissions to Administrator. On later releases, **only newly registered eligible keys** are auto-proposed for that role; other roles and user overrides remain untouched.
- [ ] Safe policy for sensitive keys (for example future security administration, destructive operations, export): **explicit release/operator approval** is required before Administrator auto-grant. Unapproved keys remain denied and sync records a pending approval. Review feature flags/sensitivity by key, not just module names.
- [ ] If Administrator was manually denied via `UserPermissions`, that deny stays authoritative. If a managed Administrator role grant was explicitly revoked by an operator, routine sync must **not re-create it** without a separate approved restore action.
- [ ] Document whether auto-grant is enabled by default for each permission definition, who approves policy changes and how to verify least-privilege impact across existing users.

### P3.1-T05 — Safe first-administrator enrollment CLI

- [ ] Add a deployment/DBA-only bootstrap command after schema migrations and registry sync. Accept database/target **through existing secure migration/admin configuration mechanism**; show sanitized target identity and require explicit confirmation/intent before mutation.
- [ ] Capture initial account details and a policy-compliant password from an **interactive masked/no-echo prompt** or an approved secret-input channel without process arguments or persisted plaintext; no password echo or sensitive exception payload. Reject noninteractive invocation requiring a prompt promptly (avoid CI hangs); support explicit `--non-interactive`/`--dry-run` for validation without creating a user.
- [ ] Use existing `IPasswordHasher`/BCrypt algorithm marker, password policy and username normalization. Preserve `PasswordSalt` compatibility sentinel; insert user, Administrator role/membership and required grants **atomically**; require active account on success.
- [ ] **One-time guard** against an already initialized database; repeat invocation is a safe no-op/report, not a password reset, account takeover or role reassignment. If some identity data already exists but no administrator, fail and require a separately approved recovery/runbook; never silently elevate the first arbitrary user.
- [ ] Provide a separate, access-controlled lost-admin recovery procedure with operator authorization and audit (not ordinary bootstrap flags, not a bypass built into Login). Concurrency tests must prove only one enrollment wins and failure leaves no half-initialized administrator.

### P3.1-T06 — Deployment workflow and future-module integration

- [ ] Document deployment ordering: schema preflight/backup -> immutable DbUp migration -> validate registry manifest -> permission sync dry-run/review -> approved apply -> first-admin enrollment **only on new installation** -> post-check -> desktop login smoke. For upgrade: skip first-admin enrollment; sync known new definitions and grants under approved policy.
- [ ] Keep sync/provisioning **out of normal desktop startup, Login and background client processes**. Retain separate DBA/admin principal from restricted runtime SQL principal in `docs/CONFIGURATION_AND_SQL_SECURITY.md`; no auto elevation or installer-embedded credentials.
- [ ] Provide a **feature PR checklist**: add `PermissionDefinition`, register it, require permission in Application, update fixtures/snapshot, test new key denial/approved Administrator access, document sync/rollout. A new key that is used but not registered fails validation.
- [ ] Address mixed-version clients explicitly (P6): old clients must not accidentally gain capability; new clients must deny undeployed/unknown keys; report version skew and rollback restrictions.

### P3.1-T07 — Verification and acceptance gates

- [ ] Fresh SQL database: migrate -> sync -> enroll first Administrator -> Login with provided credentials -> all **approved** baseline permissions succeed; inactive/unauthenticated accounts fail. No business Catalog seed required.
- [ ] Re-run provisioning/sync against populated DB: no duplicate role/user/key/membership/grant, no password reset, and no changed custom/direct permissions. Concurrent invocations serialize; injected failure rolls back.
- [ ] Release v2 adds `Sales.Orders.Read`, `Sales.Orders.Write` and a sensitive `Sales.Orders.Approve`: dry-run accurately reports additions; after approval/apply, Administrator gets only eligible/approved grants; normal roles get no new grants; sensitive key stays denied until approved. Repeat is a no-op.
- [ ] Test operator-revoked Administrator grant remains revoked, explicit direct deny overrides Administrator, unknown/unregistered/renamed key fails closed, deprecated keys are not erased and registry drift is visible.
- [ ] Test a preexisting P3 database with permission assignments, malformed manifests, mismatched schema, failed/competing syncs and unsupported/older clients. Preserve historical assignments/data and log sanitized outcomes.
- [ ] Integration tests use `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` guarded disposable DB, not production; test command and actual pass/fail/blocked evidence required. Add CLI subprocess tests that assert no echo/no secrets/no modal prompts/no hangs.

## Acceptance / exit gate

- [ ] Document exact CLI syntax, preconditions, principal privilege split, dry-run/apply outputs, recoverability and administrator emergency recovery in a runbook.
- [ ] Permission registry and new SQL schema are additive and versioned, with proved backfill, deterministic sync and explicit Administrator grant approval policy.
- [ ] Fresh DB first admin can sign in through existing Application Login; no default password is present anywhere; repeated provisioning never changes that account.
- [ ] The **new feature permission lifecycle** (declaration -> registration -> review -> migration/sync -> authorization -> tests) is automated as far as safe and part of the development/release checklist.
- [ ] Evidence recorded; update `docs/SECURITY.md`, deployment docs/permission allowlists, `docs/DEVELOPMENT_PLAN.md` and this plan status only after verification.

## Avoid

Production seeding during desktop login; wildcard Administrator bypass; unconditional regrant of revoked privileges; destructive sync; guessing legacy hashes; SQL admin privileges in desktop packages; treating application Administrator as SQL `sysadmin`; marking SQL/Windows tests passed without execution.
