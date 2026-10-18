# Active roadmap — v1a

**Owner decision:** 2026-10-18. Exactly two plans are active. Their direction is approved; implementation checkboxes start open. A documentation commit is not runtime or acceptance evidence.

| Execution order | Plan / ID | Entry condition | Current status |
|---:|---|---|---|
| 1 | [20261018-002-respawn-fixture-reuse.md](20261018-002-respawn-fixture-reuse.md) / `RESP` | Current net48 checkout and approved test resources for implementation | NOT IMPLEMENTED; start at RESP-T00 |
| 2 | [20261018-001-linq2db-net48-persistence-migration.md](20261018-001-linq2db-net48-persistence-migration.md) / `L2DB` | RESP acceptance on current RepoDb/Dapper baseline | NOT IMPLEMENTED; do not start DAL cutover before RESP acceptance |

Filename sequence reflects creation time. Follow the table, not filename sorting. RESP must produce a reusable-fixture baseline with verified reset/seed, ownership and case coverage. L2DB consumes that baseline and freezes fixture behavior while replacing DAL internals.

## Execution contract

Read [AGENTS](../../AGENTS.md), [current context](../../AI_CONTEXT.md), the task and [TEST_POLICY](../TEST_POLICY.md). Use [tests/README](../../tests/README.md) for actual commands. New Markdown task IDs are not automatically registered in `tests/test-gates.v1.json`; register and validate them in the owning implementation task before using manifest-owned commands.

The active target is `net48` for product, SQL and Desktop. `netstandard2.0` is a shared harness library; `net8.0` is a tool host. Neither plan reactivates product .NET 8, changes permissions, replaces DbUp, enables test parallelism or starts HTTP work.

## Outside this queue

- Completed foundation, TESTGATE, NET8-PAUSE, modularization and SEC-REBUILD work is summarized in [History](../HISTORY.md). Do not reimplement it.
- P5 GUI acceptance, P6 hardening/release, P7 HTTP, runtime reactivation and optional manifest-v2 simplification are [deferred](../DEFERRED_WORK.md). They are not active prerequisites except a narrowly identified issue that a current plan explicitly owns.
- The earlier rejection of fixture reuse is superseded by the owner's Respawn decision and [ADR 0010](../adr/0010-respawn-fixture-reuse-and-test-policy.md). Its old benchmark results remain historical observations.

## Handoff

Report the active task, changed files/behavior, verification commands and actual outcomes, missing requirements, accepted implementation revision and next task. Move to the next plan only after the previous acceptance criteria have evidence. Do not turn a parked target, historical result, deferred gate or owner's unrelated waiver into a new PASS.
