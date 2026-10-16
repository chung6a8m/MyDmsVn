# Architecture Decision Records (ADRs)

Accepted decisions supersede conflicting example snippets in historical `project-idea.md`. An ADR should record context, decision, consequences and affected docs. Changes require user approval; do not silently revise decisions.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-local-and-remote-adapters.md) | Local v1 / HTTP v2 with shared desktop API seam | Accepted |
| [0002](0002-explicit-unit-of-work.md) | Explicit rather than ambient UnitOfWork | Accepted |
| [0003](0003-compatibility-and-legacy-passwords.md) | Dual-runtime, RepoDb versions, legacy PasswordSalt | Accepted |
| [0004](0004-p5-ledger-and-posting.md) | P5 transactionally posted stock ledger and balances | Accepted / proposed schema details |
| [0005](0005-test-isolation-and-bounded-parallelism.md) | Test resource isolation and bounded SQL parallelism; keep GUI-interacting Desktop tests serialized | Accepted |
| [0006](0006-task-scoped-test-execution-and-module-projects.md) | Task-level selection, final acceptance boundaries, and module-scoped test project target | **Proposed** |
| [0007](0007-code-defined-permissions-and-single-superuser.md) | Code-defined permissions, deliberate RBAC administration, single configured superuser (SEC-REBUILD) | **Approved direction; implementation NOT STARTED** |

## New ADR template

```text
# NNNN — Title
Status: Proposed | Accepted | Superseded
Date: YYYY-MM-DD
Context:
Decision:
Alternatives considered:
Consequences / tradeoffs:
Required tests:
Affected documents:
```

Do not mark a superseding ADR as accepted without clear approval.
