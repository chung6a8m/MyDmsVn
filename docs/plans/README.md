# Execution roadmap — v1a-pro

**Updated:** 2026-10-21. This index is the sole execution queue. After merging PR #40, the owner selected lookup correctness first and visual parity with all three mockups second. Completed plans remain evidence; retired directions remain unscheduled.

## Active roadmap — T05 receipt implemented; native125 verification pending

| Order | Plan | State | Next action |
|---:|---|---|---|
| 1 | [BCF — lookup correctness and visual parity](20261021-001-business-compact-lookup-and-visual-parity.md) | **ACTIVE**; T00 COMPLETE / RED; T01 COMPLETE; T02 COMPLETE / lookup gate PASS; T03 IMPLEMENTED / verification partial; T04 Products is IMPLEMENTED / focused Windows verification PASS; see [T04 evidence](../evidence/bcf/20261021-t04-products-inspector.md). T05 is IMPLEMENTED / native125 verification pending ([T05 evidence](../evidence/bcf/20261021-t05-receipt-presentation.md)); T06 remains NOT STARTED | T05 receipt presentation is implemented; native100 301/301 plus 185/185 supplementary PASS, native125 pending; preserve [T03 wide/chrome limits](../evidence/bcf/20261021-t03-chrome-followup.md) and [T04 evidence/Minor](../evidence/bcf/20261021-t04-products-inspector.md) for T06 |

Read [BCF design](../BUSINESS_COMPACT_REFINEMENT.md), [BCF acceptance](../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [three retained references](../design-references/business-compact/README.md). These support the same plan and do not define separate queues. The planning baseline is `6040d7bcfca698fc9daba8268bb94e4454e8eacb`; T00–T02 results belong to their exact candidates and input scope. T02 does not establish visual or release acceptance.

| Phase | Order | Exit condition |
|---|---|---|
| 1 — correct input | T00 failing per-key proof → T01 binding repair → T02 focused Windows gate | All three targets retain text/caret/range and correct committed state; required native100/125 Light/Dark and attended cases pass |
| 2 — complete visual design | T03 shell/theme → T04 Products → T05 receipt detail → T06 frozen acceptance | All BCF-A01–A23 reconciled; reviewed actual comparisons against all three mockups and explicit Owner disposition |

**T02 PASS is mandatory before T03 implementation.** The targets are `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`; inline Product remains a regression/reference path. Phase 2 explicitly includes Products content and a guarded receipt List/Detail presentation, expanding the historical BCI visual scope.

## Completed baselines — retain, do not restart

| Plan | Accepted scope | Evidence to preserve |
|---|---|---|
| [BCI — Business Compact and inline Goods Receipt](20261020-001-business-compact-inline-goods-receipt.md) | T00–T06 complete in final signed UI scope; PR #40 merged at `6040d7b` | [Final signed acceptance](../BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21), exact candidate/test/package evidence and retained failed trials |
| [RESP — fixture reuse](20261018-002-respawn-fixture-reuse.md) | T00–T05 complete; PR #36 `1e24df4` | [Reset/seed/ownership evidence](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| [L2DB — net48 persistence](20261018-001-linq2db-net48-persistence-migration.md) | T00–T07 complete in plan scope; PR #37 `42d94ba` | [Correctness/package/limited Local smoke](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| [GUI-P5 — GUI acceptance](20261019-001-full-winforms-gui-p5-acceptance.md) | T00–T06 complete in Owner-approved scope; PR #38 `f37038b` | [Final frozen gates](../GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); T05 retains its signed Warehouse 125% Dark Down NOT TESTED exception |

Historical counts, product/test revision distinctions and exceptions stay in their canonical ledgers. None establishes a BCF PASS or waives new lookup/visual obligations. Reconcile the actual new discovery and execution sets at each named gate.

## Retired unimplemented plans

The unexecuted REL/P6 plan and deferred .NET 8 reactivation procedure remain deleted. The former P7/net8 checklist remains removed from the deferred-work page. [History](../HISTORY.md#2026-10-20--business-compact-roadmap-reset) preserves the original sources and deletion rationale.

[Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) remain unaccepted, unscheduled constraints. P7/HTTP, runtime reactivation and new business modules are outside BCF. Removing their plans neither executes their tasks nor waives production release requirements.

## Execution rules

1. Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), this index, the selected task, its spec and the sole [TEST_POLICY](../TEST_POLICY.md).
2. Keep active product/tests `net48`, Windows x86 packages, pinned vendor references and current business/security contracts. Any proven vendor seam needs narrow compatible implementation and explicit pin provenance.
3. Use focused direct project tests for task work. T02 is the lookup gate; T06 is the broader frozen GUI/package milestone. Do not repeat the entire SQL/GUI matrix for each binding or layout edit.
4. Keep interactive Windows, serialized guarded STA, bounded owned processes, explicit non-production SQL and positive resource ownership. No global runner, approval-file or automatic prefix cleanup.
5. Record exact revisions, artifacts, commands actually run, discovered/executed outcomes, environment, resource disposal and limitations. Missing or skipped mandatory evidence is NOT RUN/BLOCKED.
6. Do not infer new UI or production acceptance from the planning commit, previous test counts, or a successful build.
