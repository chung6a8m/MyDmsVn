# Architecture decision records

Current decisions and explicit owner instructions supersede older examples. Implementation status is separate from approved direction. Use the [execution index](../plans/README.md) for current work; [UI-NEXT](../ui-next/README.md) retains historical candidate records. The [latest archive](../archive/20261027-main-roadmap-reset/README.md) retires Sidebar and Security bounded execution plans; [older archives](../archive/README.md) and unfinished acceptance remain preserved. [TEST_POLICY](../TEST_POLICY.md) remains the sole test-execution policy.

| ADR | Decision | Current status |
|---|---|---|
| [0001](0001-local-and-remote-adapters.md) | Local v1 and future HTTP v2 share contracts/handlers | Accepted; HTTP implementation deferred |
| [0002](0002-explicit-unit-of-work.md) | Explicit connection/transaction ownership and UoW | Accepted; library-independent principle preserved by L2DB |
| [0003](0003-compatibility-and-legacy-passwords.md) | Runtime/package history and legacy PasswordSalt | Password compatibility binding; runtime refined by 0008, DAL by 0009 |
| [0004](0004-p5-ledger-and-posting.md) | Atomic idempotent posting, immutable ledger and derived balances | Accepted and implemented; preserve business semantics |
| [0005](0005-test-isolation-and-bounded-parallelism.md) | Ownership/resource isolation | Accepted principle, fixture direction refined by 0010 |
| [0006](0006-task-scoped-test-execution-and-module-projects.md) | Former gate policy and modular test extraction | Superseded policy; framework removed, modular test projects retained |
| [0007](0007-code-defined-permissions-and-single-superuser.md) | Code registry, explicit assignments and configured superuser | Implemented and accepted in PR #34 |
| [0008](0008-temporary-net8-target-pause.md) | Active net48 product with parked modern targets | Implemented and accepted in PR #32 |
| [0009](0009-linq2db-local-net48-persistence.md) | LINQ to DB with Local v1, net48, DbUp and explicit UoW | Implemented and accepted in L2DB-T00–T07 scope; historical design rationale retained |
| [0010](0010-respawn-fixture-reuse-and-test-policy.md) | Respawn fixtures and rebuilt test policy | Implemented and accepted in RESP-T00–T05 scope; fixture ownership and replacement policy remain binding |
| [0011](0011-direct-sales-and-moving-average-costing.md) | Direct sales, moving weighted average, coherent value inventory and LINQ/keyset | Accepted and source implemented; SAL queue archived, native125/attended/Owner acceptance OPEN |

Historical approvals and measurements are indexed in [History](../HISTORY.md). Do not describe a documentation decision as delivered code, restore an obsolete policy from Git, or reopen a completed plan from its old task ID.

A new decision record should state date, owner decision/status, context, scope, consequences, verification and superseded documents. Record a real changed decision explicitly; routine implementation choices within an approved plan do not create a new approval gate.
