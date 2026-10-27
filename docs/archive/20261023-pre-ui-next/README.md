# Pre-UI-Next plans — archive manifest

**Archived:** 2026-10-23
**Snapshot:** `v1a-pro-ui-next@7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`
**Disposition:** HISTORICAL / NOT AN EXECUTION QUEUE
**Successor:** [UI Next active roadmap](../../ui-next/20261023-active-roadmap.md)

The Owner requested a new active roadmap for the UI foundation proposals, with old plans archived and their unfinished work excluded from the new roadmap. This archive preserves those plans and their last recorded states. It does not mark incomplete tasks complete, waive evidence or establish product/production acceptance.

## Archive inventory and last-known status

| Original plan | Archived snapshot | Status at archival boundary |
|---|---|---|
| Former execution index | [plans/README](plans/README.md) | Frozen old queue; superseded by UI Next |
| SAL — Direct Sales and moving average | [20261021-002](plans/20261021-002-direct-sales-and-moving-average-costing.md) | T00–T06 COMPLETE in recorded scopes; T07 IN PROGRESS / OPEN; T08 IN PROGRESS with bounded automated evidence and remaining acceptance OPEN |
| BCF — lookup and visual parity | [20261021-001](plans/20261021-001-business-compact-lookup-and-visual-parity.md) | T00–T06 COMPLETE / OWNER ACCEPTED in the signed bounded scope; original failures and exceptions retained |
| BCI — compact receipt inline editing | [20261020-001](plans/20261020-001-business-compact-inline-goods-receipt.md) | T00–T06 complete in the final signed UI scope; PR #40 merged |
| GUI-P5 — GUI acceptance | [20261019-001](plans/20261019-001-full-winforms-gui-p5-acceptance.md) | T00–T06 complete in approved scope; signed Warehouse125Dark Down NOT TESTED exception retained |
| RESP — fixture reuse | [20261018-002](plans/20261018-002-respawn-fixture-reuse.md) | T00–T05 complete; candidate-specific accepted reset/ownership contract retained |
| L2DB — net48 persistence | [20261018-001](plans/20261018-001-linq2db-net48-persistence-migration.md) | T00–T07 complete in plan scope; original correctness/package/limited Local smoke boundary retained |

The moved plan bodies are historical snapshots. Only archive notices and relative links are adjusted for relocation. Their original active/pending phrasing describes the old queue and has no current execution authority. The original bytes remain available at the [snapshot commit](https://github.com/chung6a8m/MyDmsVn/tree/7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639/docs/plans).

## SAL state — preserve the newer evidence

The earlier 2026-10-22 UI assessment inspected app `9d8a199` and described T08 as NOT STARTED and R4 as deferred. Those are not the latest archival states. The base now includes merged PR #43 and these later records:

- [Final source wave](../../evidence/sal/20261023-t08-final-source-fix-wave.md) records reviewed R3/R4 fixes at `f5039cb21bae3df4e9995150e42689635b8d307b`.
- [Frozen T08 evidence](../../evidence/sal/20261021-t08-integrated-acceptance.md) distinguishes that product/package revision from GUI test `4697744063eab8484aeb2df2f0a6d23f42e2ca12`. It records core, matched SQL, package and native100 runnable automated results and retains original failed trials.
- [SAL acceptance](../../DIRECT_SALES_ACCEPTANCE.md) still leaves full native125/T07, attended observations and dated Owner acceptance OPEN. The archive does not supply any missing result.

Source implementation and bounded automated success are not full SAL or operational release acceptance. Conversely, old entry pages saying T08 NOT STARTED do not erase the later evidence. Current navigation has been synchronized to this boundary; historical records retain their own dates and candidates.

## Explicit exclusion from the new queue

The UI Next roadmap does **not** inherit or depend on completing SAL-T07/T08, native125 capacity/old visual acceptance, attended SAL sign-off, the old frozen full-SQL/package acceptance programme, prior deferred Minor items, deleted REL/P6/.NET 8 tasks or [production release prerequisites](../../DEFERRED_WORK.md#release-prerequisites). These remain unscheduled historical/open obligations where their original records say so.

UI Next will test its own changed controls, input and lifecycle behavior with fresh acceptance IDs. Reusing a Sales fixture, native125 environment or package harness is a new regression check for that UI change; it cannot close or rename an archived SAL row. If a future Owner request resumes historical work, start from its original ledger and explicitly select its scope.

Implemented business behavior, architecture, security, precision, recovery and test-resource safeguards remain current constraints. Archiving an execution plan does not undo those contracts. Canonical specifications and evidence stay at their existing `docs/` and `docs/evidence/` locations; they are not duplicated or re-certified here.

## Preservation checks

Before committing this archival change, compare each moved body to the snapshot, allowing only the archive notice and relative-link rebasing; check inbound links, statuses and UTF-8/EOL rules. No source/test/package result is newly executed by the docs PR. The [new evidence protocol](../../ui-next/20261023-validation-and-handoff.md) records its separate documentation-verification boundary.
