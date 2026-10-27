# History and documentation reset

**Reset date:** 2026-10-18 · **Branch assessed:** `v1a` · **Baseline:** [c7ad32a4b0040f87417973eabd6c12757c54f7d5](https://github.com/chung6a8m/MyDmsVn/commit/c7ad32a4b0040f87417973eabd6c12757c54f7d5).

This page is historical provenance, not an execution queue or test policy. Current scheduling follows the [execution index](plans/README.md). Historical activation checkpoints below retain their original dates/statuses; [the archive index](archive/README.md) records the dated boundaries. Runtime behavior follows current source/specifications and execution follows [TEST_POLICY](TEST_POLICY.md).

## 2026-10-25 — local documentation authority

After the archive, the Owner requested removal of directions tied to the separate documentation repository. Current reading, decision ownership and work selection now use local [AGENTS](../AGENTS.md), [Architecture](ARCHITECTURE.md), [ADRs](adr/README.md) and the [execution index](plans/README.md), under explicit Owner authorization. External links in historical records remain provenance only; they add no required reading, synchronization, approval or publication dependency. The next roadmap remains unselected.

## 2026-10-25 — archive before the next roadmap

The Owner requested documentation archival on `v1a-pro-ui-next` after merged PR #48. The inspected HEAD is `6ee6951c58f6e4ad92f991521ff5c553d2d22526`. The [archive boundary](archive/20261025-pre-next-roadmap/README.md) preserves ten UI-NEXT records, the A0/A1 plan and former selector, with old-path compatibility anchors. The [execution index](plans/README.md) selects no active plan; the next roadmap has not been chosen.

Product PR #47 and consumer PR #48 are merged; the separate Docs PR #1 was still open at inspection. That dated external publication status is historical context, not a dependency or pending local backlog. Their exact candidate results, earlier full Desktop failures and NOT RUN scopes remain separate from merge status. Existing evidence, earlier archives, current technical runbooks and source behavior are preserved. Unfinished acceptance and release prerequisites are not completed, waived or automatically queued. This Markdown-only change supplies no runtime acceptance.

## 2026-10-23 — UI Next selected; prior plans archived

The Owner moved the UI assessment to `docs/ui-next` on `v1a-pro-ui-next` and requested analysis, an active roadmap, archival of old plans without carrying their unfinished work, and a PR into that branch. The inspected base is `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`, including merged PR #43 and the moved assessment.

The [new roadmap](archive/20261025-pre-next-roadmap/ui-next/20261023-active-roadmap.md), [design](archive/20261025-pre-next-roadmap/ui-next/20261023-design-and-control-policy.md), [implementation tasks](archive/20261025-pre-next-roadmap/ui-next/20261023-implementation-plan.md) and [validation](archive/20261025-pre-next-roadmap/ui-next/20261023-validation-and-handoff.md) select contextual feedback, field/cell validation, shared edit mechanics, lossless numeric entry, typed dates, close/busy/theme primitives, DGV numeric parity and bounded paste/measurement. Every new implementation task is NOT STARTED; no runtime outcome is supplied by this documentation change.

Six old plans plus their execution index moved to the [archive](archive/20261023-pre-ui-next/README.md). SAL-T07/T08 are archived incomplete: final product/package `f5039cb` and GUI tests `4697744` have bounded runnable automated evidence, while native125, attended and dated Owner acceptance remain OPEN. The archive preserves those results and earlier failures/exceptions. Unfinished old tasks and release prerequisites are excluded from the new queue, not completed or waived. Current business/security/recovery contracts and test-resource safeguards remain in force.

## 2026-10-21 — Direct Sales roadmap selected

After PR #41 merged at `35ba2e8a3f37fb07095701eb94f4b56f7941c491`, the Owner selected direct sales receipts, manual price, independent repeated Product lines, gifts, cost tracking, Product filtering, LINQ-first queries and keyset pagination. The Owner then chose moving weighted average and requested design/acceptance/plan documents, activation of that roadmap and a PR into `v1a-pro`.

[ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md), [the business design](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md), [acceptance](DIRECT_SALES_ACCEPTANCE.md) and [SAL-T00–T08](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) record this decision. T00 is READY; no product/schema changes or runtime passes are supplied by the documentation PR. Existing schema9 remains the source fact; schema10/bootstrap and receipt/sale/reconciliation changes are planned.

BCF remains COMPLETE / OWNER ACCEPTED in its original bounded scope, preserving exact candidate revisions, failed trials, NOT TESTED exceptions and Owner dispositions. Earlier completed baselines and retired REL/P6/.NET8 directions are not restarted. Payments, debt, returns and broader production release acceptance remain outside SAL.

## Established baseline

| Area | Historical status / qualification | Immutable evidence |
|---|---|---|
| Foundations | P0–P4 implementation and automated foundations exist | [Original phase archive](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/archive/README.md) |
| P3.1 | Provisioning/synchronization was historically implemented, then superseded by SEC-REBUILD | [Historical P3.1 record](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/P3_1_VERIFICATION.md) |
| P4.1 | Owner accepted Login/session on 2026-10-16; full packaged idle-expiry/relogin interval was not observed and remains a specific accepted exception, not PASS | [P4.1 record](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/P4_1_VERIFICATION.md) |
| P5 / P6 | Business functionality and automated work exist; P5 interactive and P6 operational/release acceptance remain open | [Unfinished-work source](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) |
| SQLPERF | Prior measurement/optimization completed; the then-rejected fixture reuse and retained fresh-database strategy describe the old baseline | [T04 decision](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/SQLPERF_T04_DECISION.md), [T06 measurements](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/SQLPERF_T06_VERIFICATION.md) |
| NET8-PAUSE | PR #32 merged at `8e310778`; product/SQL/Desktop net48 active. Duration comparison explicitly waived at `09d00bfa` because no matched baseline existed; no measured speedup follows from that waiver | [Pause verification](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/NET8_PAUSE_T05_VERIFICATION.md) |
| TESTGATE / modularization | Task/final tooling and modular test projects were implemented; modularization accepted at `973109f`. The framework was later removed; modular projects remain | [Core plan](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-001-task-gates-and-modular-tests.md), [Module extraction](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-002-modular-test-projects.md) |
| SEC-REBUILD | T00–T06 accepted in PR #34; automated, packaged GUI and owner walkthrough evidence recorded at `0303a911` | [Security rebuild verification](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/SEC_REBUILD_VERIFICATION.md) |
| Stabilization | PR #35 merged at `8cfa2535`; latest recorded reviewer-round-3 evidence is at `6d71b711`. Proposed STAB-T03/T04 manifest-v2 work was not implemented by that merge | [Stabilization source, including final review section](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261017-001-testgate-stabilization.md) |
| L2DB direction | Owner selected 1A + 2A + 3A with active net48; assessment/planning committed at the baseline above | [Original L2DB plan](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261018-001-linq2db-net48-persistence-migration.md) |

Historical totals describe their own revisions and scope. They are not new RESP/L2DB results. The owner's narrow P4.1 and duration-comparison exceptions do not waive other release, reset, SQL or runtime requirements.

## What was removed from working documentation

- Completed/superseded plans under `docs/plans`, the old `docs/plans/archive`, and duplicate implementation/spec narratives under `docs/superpowers`.
- The previous `docs/CODEX_TEST_EXECUTION_POLICY.md` and `docs/TEST_GATE_MATRIX.md`; their replacement is [TEST_POLICY.md](TEST_POLICY.md). Coverage and command references no longer define competing policies.
- Historical verification/benchmark narratives formerly alongside current root `docs` references. Their exact contents remain in the immutable baseline and Git history.
- The raw [project-idea.md](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/project-idea.md), whose outdated examples are superseded by the current PRD, architecture, contracts and ADRs.

At the 2026-10-18 reset, pending requirements were consolidated into DEFERRED_WORK.md with unchecked acceptance. The 2026-10-20 decision below removes those old executable checklists; [the current page](DEFERRED_WORK.md) now records scope boundaries only. Removal from the working tree does not delete Git history, waive unfinished work or revert implemented code. The first documentation reset retained executable test tooling. The owner's subsequent complete framework-removal decision below supersedes that retention; RESP now extends the remaining local helpers directly.

## Subsequent owner decision — complete SQL framework removal

On 2026-10-18, after [the documentation reset at a5ee124](https://github.com/chung6a8m/MyDmsVn/commit/a5ee124bed72f2994c4ee712e660b91d0dc2f953), the owner explicitly selected complete removal of:

- `tests/test-gates.v1.json`.
- `MyDmsVn.SqlTestHarness` and its framework tests.
- SQL runner, runner tests and all dependent command wrappers under `tests/tools/`.
- Framework-only invocation/exemption inventories and measured migration instrumentation.

Their [immutable pre-removal tests tree](https://github.com/chung6a8m/MyDmsVn/tree/a5ee124bed72f2994c4ee712e660b91d0dc2f953/tests) remains available for provenance. These files are removed from current execution, not relocated under another framework name. The unimplemented STAB-T03/T04 manifest-v2 work is cancelled by this decision.

Business SQL tests and their six module owners remain, alongside Application, Architecture and two Desktop owners: ten active net48 test projects. Direct dotnet test/Test Explorer execution uses a local fresh-database helper with live instance/allocation/marker checks before drop. No approval-file, global lease, external watchdog or automatic orphan recovery service is carried forward. See [current test commands](../tests/README.md).

**Historical state immediately after framework removal (2026-10-18):** two infrastructure plans were then pending, RESP and L2DB. They were implemented/accepted subsequently in PR #36/#37; framework removal **alone** did not implement them or grant release acceptance. GUI-P5 subsequently completed in PR #38; the 2026-10-20 decision below retires the still-unimplemented REL plan and selects BCI.

## 2026-10-20 — Business Compact roadmap reset

**Assessed branch:** `v1a-pro` at `f37038b250821a538b9cdc10a6d6d8b4bc1f386f`. The owner requested documents for the Business Compact and true inline Goods Receipt proposals, activation as the roadmap, removal of every older unimplemented plan, and a PR targeting `v1a-pro`.

| Disposition | Evidence and reason |
|---|---|
| Retain completed RESP | 29 checked / 0 unchecked task steps; PR #36 accepted scope remains in its plan and evidence |
| Retain completed L2DB | 48 checked / 0 unchecked task steps; PR #37 accepted scope remains in its plan and evidence |
| Retain completed GUI-P5 | 25 checked / 0 unchecked task steps; PR #38 final accepted scope and signed Down exception remain unchanged |
| Delete unimplemented REL/P6 plan | 0 checked / 32 unchecked task steps at the assessed source; [immutable retired plan](https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/docs/plans/20261019-002-local-v1-p6-release-acceptance.md) |
| Delete deferred .NET 8 reactivation procedure | No implemented runtime reactivation; [immutable retired procedure](https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/docs/REACTIVATE_NET8.md) |
| Remove old P7/net8 executable checklist | Eight unchecked items were future instructions, not completed work; [original scope page](https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/docs/DEFERRED_WORK.md) |
| Activate BCI | [Single implementation plan](archive/20261023-pre-ui-next/plans/20261020-001-business-compact-inline-goods-receipt.md), [design](BUSINESS_COMPACT_UI.md) and [acceptance matrix](BUSINESS_COMPACT_ACCEPTANCE.md); all new implementation/runtime work remains unexecuted |

Completed plans stay at their existing paths to preserve evidence backlinks; their original task handoffs are historical. Current context/index/policy references are refreshed so no old queue competes with BCI. The large historical checkpoints formerly duplicated in AI_CONTEXT remain in canonical GUI-P5 ledgers and Git history; the handoff now describes the current state concisely.

No product source, test implementation, schema or vendor pin is changed by this reset. Retiring REL/P6 does not approve production release: [release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain unaccepted/unscheduled. The modern host remains parked. This documentation action establishes no new runtime PASS.

## 2026-10-21 — PR #40 merged and BCF activated

The owner merged [PR #40](https://github.com/chung6a8m/MyDmsVn/pull/40) into `v1a-pro` at `6040d7bcfca698fc9daba8268bb94e4454e8eacb`, then requested an active roadmap committed to that branch. BCI remains complete in its [final signed UI scope](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21); its original evidence, failed trials and limitations are retained.

[BCF](archive/20261023-pre-ui-next/plans/20261021-001-business-compact-lookup-and-visual-parity.md) is the sole new active roadmap. T00–T02 first reproduce and repair manual per-key input in the list warehouse filter and header Warehouse/Employee lookups, including caret/selection, canceled-operation completion and committed-value boundaries. These binding paths predate PR #40. A mandatory native100/125 Light/Dark and attended lookup gate precedes visual implementation.

T03–T06 then complete the shared shell, Products content/inspector, detail-focused receipt presentation and Graphite consistency against [all three original designs](design-references/business-compact/README.md), followed by new frozen integrated evidence and Owner comparison. This deliberately expands BCI's catalog exclusion and always-visible receipt list while preserving the established business/security/runtime boundaries and English UI.

The reference PNGs are copied unchanged with original filenames, dimensions and hashes recorded in their index. [The new design](BUSINESS_COMPACT_REFINEMENT.md) and [BCF-A01–A23 ledger](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) separate proposed APIs/tests from the implemented baseline. All BCF implementation tasks are NOT STARTED; runtime obligations are NOT RUN. Entry documents now point to BCF, while completed plans remain historical.

This activation changes documentation and retained design assets only. It implements no lookup fix or visual behavior, runs no new GUI/SQL acceptance, changes no vendor pin/schema and grants no operational release approval.

## Recover a historical source

Use the immutable links above or `git show c7ad32a4b0040f87417973eabd6c12757c54f7d5:<repository-relative-path>`. The full [baseline documentation tree](https://github.com/chung6a8m/MyDmsVn/tree/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs) remains available. A shallow clone may need that commit fetched before using `git show`.

Read historical imperative text as the policy of its date. Current owner decisions, [AGENTS](../AGENTS.md), the current execution-plan index and TEST_POLICY control new work.

## 2026-10-21 — BCF-T06 Owner acceptance

The Owner explicitly accepted the final interface differences, bounded hardware/Minor exceptions and evidence after review of all three reference comparisons. [The final T06 record](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) freezes package9559107, unchanged product991530f and separate test revisions. Six strict wide125 rows remain NOT TESTED; native100 retains its failed broad run plus repair and composite attended/no-independent-no-write limitation. BCF-T00–T06 are complete in that accepted scope on the isolated branch. No push/merge, upstream merge, production release acceptance or new roadmap is implied.
