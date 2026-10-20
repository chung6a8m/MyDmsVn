# Execution roadmap — v1a-pro

**Updated:** 2026-10-20. This index is the sole execution queue. The owner selected Business Compact and true inline Goods Receipt editing, and requested removal of every older unimplemented plan. The three completed plans below remain evidence, not active work.

## Active plan

| Order | Plan | State | Next action |
|---:|---|---|---|
| 1 | [BCI — Business Compact and inline Goods Receipt](20261020-001-business-compact-inline-goods-receipt.md) | **ACTIVE**; BCI-T00–T05 complete in recorded proof/scalar/adapter/control/accessibility/layout scopes; T06 planned | BCI-T06: frozen integrated UI/package acceptance; retain T00–T05 scoped evidence |

Read [BUSINESS_COMPACT_UI](../BUSINESS_COMPACT_UI.md) for the design and [BUSINESS_COMPACT_ACCEPTANCE](../BUSINESS_COMPACT_ACCEPTANCE.md) for required observable outcomes. Supporting specifications are not independent plans. Execute BCI-T00 → T01 → T02 → T03 → T04 → T05 → T06; preparation or parallel work must respect the interfaces and dependencies recorded in that plan.

The planning documents implement no product feature. T00 now supplies the scoped native editor proof recorded in the acceptance ledger; T01 supplies the scalar numeric parser; T02 supplies the stable grid/session foundation; T03 integrates lookup and commands into the actual receipt control. T04 supplies editable cell accessibility, real native editor reachability and a focused clean-package probe. T05 supplies compact layout and hosted native 100%/125% measurements; [evidence](../BUSINESS_COMPACT_ACCEPTANCE.md#86-bci-t05-business-compact-layout--2026-10-20). Complete frozen UI/package acceptance remains T06.

## Completed baselines — retain, do not restart

| Plan | Accepted scope | Evidence to preserve |
|---|---|---|
| [RESP — fixture reuse](20261018-002-respawn-fixture-reuse.md) | T00–T05 complete; PR #36 `1e24df4` | [Reset/seed/ownership evidence](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| [L2DB — net48 persistence](20261018-001-linq2db-net48-persistence-migration.md) | T00–T07 complete in plan scope; PR #37 `42d94ba` | [Correctness/package/limited Local smoke](../L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| [GUI-P5 — GUI acceptance](20261019-001-full-winforms-gui-p5-acceptance.md) | T00–T06 complete in Owner-approved scope; PR #38 `f37038b` | [Final frozen gates](../GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); T05 retains its signed Warehouse 125% Dark Down NOT TESTED exception |

GUI-P5 recorded product/package `d5207b2`, final Desktop test source `12e6a20`, Session build `cb86fbc`, Session 108/108, Desktop 329/329 at each 100%/125%, and a separate packaged 18/18. These are historical external-editor results, not BCI targets or new runs. Reconcile the actual new discovery and execution sets when implementing BCI.

## Retired unimplemented plans

The unexecuted REL/P6 plan and deferred .NET 8 reactivation procedure have been deleted from the working tree. The former P7/net8 checklist is also removed from the deferred-work page. [History](../HISTORY.md#2026-10-20--business-compact-roadmap-reset) preserves the original sources and deletion rationale.

[Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) remain unaccepted, unscheduled constraints. P7/HTTP, runtime reactivation and new business modules are outside BCI. Removing their plans neither executes their tasks nor waives production release requirements.

## Execution rules

1. Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), this index, the selected task, its spec and the sole [TEST_POLICY](../TEST_POLICY.md).
2. Keep active product/tests `net48`, Windows x86 packages, pinned vendor references and current business/security contracts unless a task explicitly identifies a narrow required change.
3. Use focused direct project tests for task work. Run the broader BCI GUI/package matrix only at its named integration milestone or to resolve a concrete regression.
4. Keep interactive Windows, serialized guarded STA, bounded owned processes, explicit non-production SQL and positive resource ownership. No global runner, approval-file or automatic prefix cleanup.
5. Record exact revisions, artifacts, commands actually run, discovered/executed outcomes, environment, resource disposal and limitations. Missing or skipped mandatory evidence is NOT RUN/BLOCKED.
6. Do not infer new UI or production acceptance from this documentation PR, previous test counts, or a successful build.
