# Architecture Decision Records (ADRs)

Accepted decisions supersede conflicting example snippets in historical `project-idea.md`. An ADR should record context, decision, consequences and affected docs. Changes require user approval; do not silently revise decisions.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-local-and-remote-adapters.md) | Local v1 / HTTP v2 with shared desktop API seam | Accepted |
| [0002](0002-explicit-unit-of-work.md) | Explicit rather than ambient UnitOfWork | Accepted |
| [0003](0003-compatibility-and-legacy-passwords.md) | Dual-runtime, RepoDb versions, legacy PasswordSalt | Accepted |
| [0004](0004-p5-ledger-and-posting.md) | P5 transactionally posted stock ledger and balances | Accepted / proposed schema details |

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
