# Active roadmap — v1a

**Owner decision:** 2026-10-18. Exactly two plans are active. Their direction is approved; task evidence below records progress. The documentation reset and removal of the former SQL test framework do not implement either plan or establish their acceptance.

| Execution order | Plan / ID | Entry condition | Current status |
|---:|---|---|---|
| 1 | [20261018-002-respawn-fixture-reuse.md](20261018-002-respawn-fixture-reuse.md) / `RESP` | Current net48 checkout and approved test resources for implementation | RESP-T00–T05 accepted on exact local working-tree candidate; full fresh/reuse 265/265 each, cleanup and observed suite improvement; see [evidence](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| 2 | [20261018-001-linq2db-net48-persistence-migration.md](20261018-001-linq2db-net48-persistence-migration.md) / `L2DB` | RESP acceptance on current RepoDb/Dapper baseline | T00–T01 complete: accepted RESP bound to PR #36 merge `1e24df4`; net48 session/package ownership verified ([evidence](../L2DB_VERIFICATION.md#l2db-t01--net48-package-and-resource-ownership)). T02 final **GO**: strict error parity, schema9/x86 package and least-privileged startup passed; temporary identity/database cleaned up ([decision](../L2DB_VERIFICATION.md#l2db-t02-final-go--approved-runtime-startup)). T03 Catalog conversion complete,125 scoped test invocations passed ([evidence](../L2DB_VERIFICATION.md#l2db-t03--catalog-conversion)). T04 Inventory conversion complete,134 scoped invocations passed ([evidence](../L2DB_VERIFICATION.md#l2db-t04--inventory-conversion)). T05 Identity/Security/operations conversion complete,200 scoped invocations passed ([evidence](../L2DB_VERIFICATION.md#l2db-t05--identity-security-and-operations-conversion)). T06 transitional dependency/test cleanup delivered ([evidence](../L2DB_VERIFICATION.md#l2db-t06--transitional-provider-and-test-graph-cleanup)); T07 complete:581 correctness invocations, clean205-file package checks and approved restricted-runtime packaged Local smoke passed; temporary login/database cleanup verified ([evidence](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke)); L2DB plan-specific acceptance complete, deferred release gates open |

Filename sequence reflects creation time. Follow the table, not filename sorting. RESP must produce a reusable-fixture baseline with verified reset/seed, ownership and case coverage. L2DB consumes that baseline and freezes fixture behavior while replacing DAL internals.

## Execution contract

Read [AGENTS](../../AGENTS.md), [current context](../../AI_CONTEXT.md), the task and [TEST_POLICY](../TEST_POLICY.md). Use [tests/README](../../tests/README.md) for actual commands. Execute actual test projects directly using source-verified category/method filters and ordinary TRX outcomes. Plan/task IDs label work and evidence; there is no executable gate registry or runner bootstrap.

The active target is `net48` for product, SQL and Desktop. All ten active test projects also target net48; the .NET 8 SDK remains a build tool. Neither plan reactivates product .NET 8, changes permissions, replaces DbUp, enables test parallelism or starts HTTP work.

## Outside this queue

- Completed foundation, NET8-PAUSE, modularization and SEC-REBUILD work, plus the now-removed TESTGATE framework, is summarized in [History](../HISTORY.md). Do not reimplement it.
- P5 GUI acceptance, P6 hardening/release, P7 HTTP and runtime reactivation are [deferred](../DEFERRED_WORK.md). They are not active prerequisites except a narrowly identified issue that a current plan explicitly owns. The old framework's unimplemented manifest-v2 redesign is cancelled by its complete removal.
- The earlier rejection of fixture reuse is superseded by the owner's Respawn decision and [ADR 0010](../adr/0010-respawn-fixture-reuse-and-test-policy.md). Its old benchmark results remain historical observations.

## Handoff

Report the active task, changed files/behavior, verification commands and actual outcomes, missing requirements, accepted implementation revision and next task. Move to the next plan only after the previous acceptance criteria have evidence. Do not turn a parked target, historical result, deferred gate or owner's unrelated waiver into a new PASS.
