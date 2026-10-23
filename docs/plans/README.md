# Execution roadmap — v1a-pro

**Updated:** 2026-10-23. This index is the sole execution queue. After PR #41, the Owner selected SAL as the sole active implementation roadmap. Completed plans remain evidence; retired directions remain unscheduled.

## Current roadmap — SAL / ACTIVE

The Owner selected **direct sales receipts and moving weighted average costing** on 2026-10-21, with manual selling prices, duplicate Product lines, gifts, cost tracking, Product filtering, LINQ-first queries and keyset pagination. The source baseline is merged PR #41 at `35ba2e8a3f37fb07095701eb94f4b56f7941c491`.

Read [SAL design](../DIRECT_SALES.md), [costing](../DIRECT_SALES_COSTING.md), [query/UI](../DIRECT_SALES_QUERY_AND_UI.md), [ADR 0011](../adr/0011-direct-sales-and-moving-average-costing.md), [acceptance](../DIRECT_SALES_ACCEPTANCE.md) and the [implementation plan](20261021-002-direct-sales-and-moving-average-costing.md). These documents define one queue; approval is not implementation or runtime acceptance.

| Task | Outcome | Current state |
|---|---|---|
| SAL-T00 | Exact arithmetic, domain/contracts and permission definitions | COMPLETE — [focused evidence](../evidence/sal/20261022-t00-foundation.md) |
| SAL-T01 | Atomic schema10, verified schema9 bootstrap/admission and reset profiles | COMPLETE — [bounded evidence](../evidence/sal/20261022-t01-schema-bootstrap.md) |
| SAL-T02 | Valued receipt posting, shared lock protocol, reconciliation and stock reads | COMPLETE — [bounded evidence](../evidence/sal/20261022-t02-valued-inventory.md) |
| SAL-T03 | Authorized sales Draft lifecycle and version/line identity | COMPLETE — [bounded evidence](../evidence/sal/20261022-t03-sales-draft.md) |
| SAL-T04 | Atomic sale Post, cost allocation and stock concurrency | COMPLETE — [bounded evidence](../evidence/sal/20261022-t04-atomic-sales-post.md) |
| SAL-T05 | LINQ filtering and keyset list | COMPLETE — `f9149a6`, [bounded evidence](../evidence/sal/20261022-t05-sales-keyset-queries.md) |
| SAL-T06 | Local API, ViewModels and session/recovery integration | COMPLETE — `5fbd6f5`, [bounded evidence](../evidence/sal/20261022-t06-local-session-viewmodels.md) |
| SAL-T07 | Compact seven-column UI, native input and accessibility | IN PROGRESS / OPEN — source `879ab7b`, [bounded native100 evidence](../evidence/sal/20261022-t07-native-sales-workspace.md); native125 RESOURCE BLOCKED; R4 source fix approved `f5039cb` |
| SAL-T08 | Frozen integrated SQL/UI/package evidence and Owner disposition | IN PROGRESS — [frozen automated evidence](../evidence/sal/20261021-t08-integrated-acceptance.md) product/package `f5039cb`, GUI tests `4697744`; core/SQL/package/native100 runnable PASS; native125/full T07, attended and final Owner acceptance OPEN |

T00-owned pure, T01-owned schema/bootstrap and T02-owned receipt/reconciliation/stock-read and T03-owned Draft/detail and T04-owned atomic Post/protocol portions have focused evidence; T05 List/cursor/ownership has reviewed complete affected-project evidence. Exact bounded row dispositions are recorded in the [acceptance ledger](../DIRECT_SALES_ACCEPTANCE.md); multi-task rows remain open. The original serial order records technical dependencies; the bounded continuation decision below now permits feasible T08 work. No task checkpoint is deployable until receipt/sale/reconciliation writers and schema/runtime/reset guards are coherent. The historical baseline supports schema9; development source at `81e8aab` requires schema10, with receipt/reconciliation implemented at `47165ded` and Sales Post at `7eeafa4`. Coherent source protocol does not establish end-to-end runtime/package/Owner acceptance. Payments, debt, returns, HTTP and framework reactivation are excluded. Release prerequisites remain UNACCEPTED / UNSCHEDULED.

On 2026-10-23 the Owner replied “Tiếp tục” after the explicit choice between feasible T08 continuation and waiting for native125. The execution controller interprets that reply as authorization to proceed with feasible T08 implementation, SQL, package and native100 work while full T07/native125, attended observations and final dated Owner acceptance remain OPEN. This changes the earlier serial execution order only; it waives no evidence or feature/release acceptance requirement.

## Completed baselines — retain, do not restart

| Plan | Accepted scope | Evidence to preserve |
|---|---|---|
| [BCF — lookup correctness and visual parity](20261021-001-business-compact-lookup-and-visual-parity.md) | T00–T06 COMPLETE / OWNER ACCEPTED in bounded UI scope | [Final frozen evidence and Owner dispositions](../evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) |
| [BCI — Business Compact and inline Goods Receipt](20261020-001-business-compact-inline-goods-receipt.md) | T00–T06 complete in final signed UI scope; PR #40 merged at `6040d7b` | [Final signed acceptance](../BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21), exact candidate/test/package evidence and retained failed trials |
| [RESP — fixture reuse](20261018-002-respawn-fixture-reuse.md) | T00–T05 complete; PR #36 `1e24df4` | [Reset/seed/ownership evidence](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| [L2DB — net48 persistence](20261018-001-linq2db-net48-persistence-migration.md) | T00–T07 complete in plan scope; PR #37 `42d94ba` | [Correctness/package/limited Local smoke](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| [GUI-P5 — GUI acceptance](20261019-001-full-winforms-gui-p5-acceptance.md) | T00–T06 complete in Owner-approved scope; PR #38 `f37038b` | [Final frozen gates](../GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); T05 retains its signed Warehouse 125% Dark Down NOT TESTED exception |

Historical counts, product/test revision distinctions and exceptions stay in their canonical ledgers. Historical baselines do not supply SAL results or its explicit Owner dispositions. Reconcile the actual new discovery and execution sets at each named gate.

## Retired unimplemented plans

The unexecuted REL/P6 plan and deferred .NET 8 reactivation procedure remain deleted. The former P7/net8 checklist remains removed from the deferred-work page. [History](../HISTORY.md#2026-10-20--business-compact-roadmap-reset) preserves the original sources and deletion rationale.

[Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) remain unaccepted, unscheduled constraints. P7/HTTP and runtime reactivation remain outside SAL; only the explicitly approved direct-sales/valuation scope is newly scheduled. Removing their plans neither executes their tasks nor waives production release requirements.

## Execution rules

1. Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), this index, the selected task, its spec and the sole [TEST_POLICY](../TEST_POLICY.md).
2. Keep active product/tests `net48`, Windows x86 packages, pinned vendor references and security/ownership invariants; implement only the explicitly approved SAL business/schema additions. Any proven vendor seam needs narrow compatible implementation and explicit pin provenance.
3. Use focused direct project tests for task work. SAL-T08 is the frozen integrated milestone; BCF-T02/T06 are historical gates. Do not repeat the entire SQL/GUI matrix for each binding or layout edit.
4. Keep interactive Windows, serialized guarded STA, bounded owned processes, explicit non-production SQL and positive resource ownership. No global runner, approval-file or automatic prefix cleanup.
5. Record exact revisions, artifacts, commands actually run, discovered/executed outcomes, environment, resource disposal and limitations. Missing or skipped mandatory evidence is NOT RUN/BLOCKED.
6. Do not infer new UI or production acceptance from the planning commit, previous test counts, or a successful build.
