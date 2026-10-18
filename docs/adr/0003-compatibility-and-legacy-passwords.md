# ADR 0003 — Runtime history and legacy password compatibility

Status: **Accepted password compatibility; original runtime/package requirements refined by ADRs 0008/0009** · Date: 2026-10-08

## Context

The original 2026-10-08 decision required .NET Framework and .NET 8 compatibility. ADR 0008 subsequently parked the modern product target, and ADR 0009 selected a future DAL replacement. Imported legacy user records retain distinct PasswordHash and PasswordSalt columns.

## Decision

- Active product/SQL/Desktop uses `net48`; modern product targets are parked under ADR 0008. The original dual-runtime decision is historical, not a current build instruction.
- Do not upgrade to .NET 10 absent further approval.
- Current RepoDb pins remain in code until the approved LINQ to DB migration (ADR 0009) removes their consumers. The central package file is the current version source.
- Retain both `Users.PasswordHash` and `Users.PasswordSalt` for migration compatibility, even though BCrypt hashes normally contain their salt.
- Implement algorithm-aware password verification/rehash through an abstraction; do not invent missing legacy hash semantics.

## Consequences

Package restore/build verification follows the actually active runtime matrix and TEST_POLICY. .NET 8 support lifecycle is a tracked maintenance risk. Legacy verifier behavior requires approved test vectors.

See `docs/TECH_STACK.md`, `docs/SECURITY.md`.
