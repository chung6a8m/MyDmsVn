# P3 — Identity and authorization foundation

Status: Not started. Depends on P2. Read `docs/SECURITY.md` and `docs/TEST_STRATEGY.md`.

## Objective

Provide reusable user/role/permission checks and safe compatibility with imported legacy user credentials, without inventing a legacy hashing algorithm.

## Task list

- [ ] **P3-T01 / Migrations:** DbUp Users, Roles, UserRoles, RolePermissions, UserPermissions according to source `project-idea.md` and `docs/SECURITY.md`; preserve PasswordHash and PasswordSalt; FK/index/unique constraints; normalized user names.
- [ ] **P3-T02 / Identity abstractions:** CurrentUser identity, password hashing/verifier interfaces, user store, login/current-user client contracts; default deny for unauthenticated/inactive accounts.
- [ ] **P3-T03 / New hashes:** BCrypt for newly set credentials, secure comparison; algorithm marker/format strategy; do not store plaintext or reversible secret.
- [ ] **P3-T04 / Legacy path:** implement only from verified legacy algorithm spec with test fixtures; if missing, keep interface + clearly blocked implementation, never fallback to accepting unknown hashes; keep PasswordSalt field.
- [ ] **P3-T05 / Authorization:** explicit UserPermissions record overrides Roles (Granted false denies, true allows), role union otherwise; unknown key denies. Enforce in Application pipeline or use-case guard.
- [ ] **P3-T06 / Permission tests:** combinations for direct grant/deny, role union, no role, unknown permission, disabled user, revoked role, unauthorized write.
- [ ] **P3-T07 / Audit and logging:** user IDs + UTC timestamps for privileged changes; no passwords, salts, hashes, tokens in logs.
- [ ] **P3-T08 / Integration:** SQL tests for uniqueness, permission lookup and persistence, legacy rehash only if fixtures approved.

## Exit evidence

Permission behavior is deterministic and tested; test-only use case rejects unauthorized mutation; database constraints proven; no unapproved assumptions about old password hashing.

## Open prerequisite

Legacy import verifier requires algorithm description or sanitized sample fixture from the legacy system. Do not equate missing fixture with permission to guess. Document migration path as pending if needed.
