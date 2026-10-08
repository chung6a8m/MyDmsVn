# ADR 0003 — Dual runtime, RepoDb pins, legacy password compatibility

Status: **Accepted** · Date: 2026-10-08

## Context
Existing libraries and development laptops require .NET Framework and .NET 8 compatibility. Imported legacy user records have distinct PasswordHash and PasswordSalt columns.

## Decision
- Keep `net48` and `net8.0` libraries/hosts; modern WinForms TFMs use `net8.0-windows`.
- Do not upgrade to .NET 10 absent further approval.
- Pin `RepoDb=1.16.0`; `RepoDb.SqlServer=1.16.1`; `RepoDb.SqlServer.BulkOperations=1.16.2`.
- Retain both `Users.PasswordHash` and `Users.PasswordSalt` for migration compatibility, even though BCrypt hashes normally contain their salt.
- Implement algorithm-aware password verification/rehash through an abstraction; do not invent missing legacy hash semantics.

## Consequences
Package restore and dual-target builds are mandatory acceptance checks. .NET 8 support lifecycle is a tracked maintenance risk. Legacy verifier behavior requires approved test vectors.

See `docs/TECH_STACK.md`, `docs/SECURITY.md`.
