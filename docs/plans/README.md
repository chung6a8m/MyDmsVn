# Execution roadmap — v1a

**Updated:** 2026-10-19. This index owns execution order and plan state. **RESP and L2DB are completed foundations**; the next acceptance work is GUI-P5, followed by P6 release acceptance (REL). Adding plans does not mark deferred release requirements passed.

| Order | Plan | Status / source of truth | Next action |
|---:|---|---|---|
| 1 | [RESP — Respawn fixture reuse](20261018-002-respawn-fixture-reuse.md) | **COMPLETE / accepted**: T00–T05; frozen fresh/reuse SQL comparison and ownership evidence in [RESP verification](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up); PR #36 merge `1e24df4` | Preserve accepted fixture/reset/isolation behavior |
| 2 | [L2DB — net48 LINQ to DB migration](20261018-001-linq2db-net48-persistence-migration.md) | **COMPLETE / accepted in plan scope**: T00–T07; native cutover and clean package/limited real Local smoke in [L2DB verification](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke); PR #37 merge `42d94ba` | Preserve SQL/Application contracts; do not repeat migration work |
| 3 | [GUI-P5 — full WinForms testing and P5 interactive acceptance](20261019-001-full-winforms-gui-p5-acceptance.md) | **IN PROGRESS / OPEN**: T00–T01 COMPLETE ([inventory/topology](../GUI_P5_COVERAGE_MATRIX.md), [reliability evidence](../GUI_P5_VERIFICATION.md)); T02–T06 and P5 acceptance OPEN | T02 deterministic control/VM coverage; prepare T03 resources, then packaged/attended evidence |
| 4 | [REL — P6 Local v1 release acceptance](20261019-002-local-v1-p6-release-acceptance.md) | **NEW / OPEN (planned)**: T00–T06 and G1–G5; NOT READY FOR PRODUCTION | Define supported OS/SQL/legacy/signing/recovery choices; run operational evidence and frozen release gate |

Filename prefixes follow creation time. **Follow this table, not filename sorting or historical roadmap instructions.** GUI-P5 completes P5 interaction and full GUI proof. REL-T00–T04 preparatory work may proceed alongside GUI-P5 on separate approved resources; final REL-T05/T06 and G4 need accepted GUI-P5 evidence. Neither new plan is a new framework/runtime/persistence migration.

## Baseline and exact remaining gap

- Product and all ten active tests target `net48`; shipped release host is Windows x86 `MyDmsVn.Desktop.App.exe` plus separate net48 x86 admin migrator. AppCore/net8 remains PARKED.
- The current persistence is **LINQ to DB + typed native SqlClient**; **Respawn 4** is implemented in test-only owned fixtures. DbUp scripts/journal and strict runtime schema `001–009` remain. Do not reintroduce RepoDb/Dapper or the deleted generic SQL runner/test-gates.
- In L2DB-T07 the nine non-GUI owners had 581 passed invocations; 139 `Desktop.Tests` GUI invocations were conditionally **NOT RUN**. T07's limited packaged Local smoke does not complete full GUI/P5 acceptance or P6 operations.
- The release-manifest schema8/9 metadata discrepancy is **closed narrowly** by L2DB-T02/clean-commit evidence. Actual install/upgrade, backup/restore, legacy policy, supported environment and G1–G5 are **still OPEN**. Never reopen fixed metadata solely because an older checklist called it open.

## Execution and policy

1. Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), this index, the particular task and the sole [TEST_POLICY](../TEST_POLICY.md).
2. Read [TEST_STRATEGY](../TEST_STRATEGY.md) for required behaviors and [tests/README](../../tests/README.md) for real direct commands. Pick source-verified task-scoped test cases, not automatic full SQL/GUI for every edit; acceptance tasks explicitly require broader matrix.
3. For GUI test safety use interactive Windows/serialized STA, bounded processes and no modal hangs. For real SQL use an operator-designated test instance, positive database allocation ownership, correct worker disposal and direct sequential project tests. Missing resources/skips are BLOCKED / NOT RUN.
4. For completed work record revision/package, observed cases and Theory rows, SQL/temp resource cleanup, safe logs/screenshots and tester/date. **Plans only** do not assert PASS; each checkbox needs actual evidence.

## Outside this queue

[P7 HTTP/remote, AppCore/net8 reactivation and future modules](../DEFERRED_WORK.md) remain unapproved/deferred. Historical removed framework proposals, permission synchronization and old host dual-TFM checklists are available via [History](../HISTORY.md), not executable instructions. The owner has not authorized an MSI/auto-update feature or schema upgrade from rejected 001–008 prefixes.

## Handoff

State the current task, exact revision/target, changes, verification and counts, remaining risks/resources and next task. Report explicit **GO / NO-GO** only at the milestone for which corresponding evidence exists. GUI-P5 success by itself is not a production release.
