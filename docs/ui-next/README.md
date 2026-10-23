# UI Next — active SME desktop roadmap

**Updated:** 2026-10-23
**Base branch:** `v1a-pro-ui-next` at `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`
**Status:** ACTIVE ROADMAP; implementation tasks NOT STARTED; runtime evidence NOT RUN

The Owner selected the UI foundation proposals for an active roadmap and requested a documentation PR into `v1a-pro-ui-next`. The work preserves the current compact visual direction and develops contextual feedback, complete validation, efficient inline entry and reusable workspace controls for the SME application.

## Read for current work

Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md) and the sole [TEST_POLICY](../TEST_POLICY.md), then the selected task and its contracts below. There is one execution queue, not one per index.

| Document | Responsibility |
|---|---|
| [Active roadmap](20261023-active-roadmap.md) | Selected scope, refreshed baseline, task order/dependencies, current status and exclusions |
| [Design and control policy](20261023-design-and-control-policy.md) | Control selection, ownership, numeric/date/editing behavior and workspace lifecycle |
| [Implementation plan](20261023-implementation-plan.md) | Bounded task steps, exact source/test owners, proposed interfaces and review focus |
| [Validation and handoff](20261023-validation-and-handoff.md) | New UI Next acceptance cases, focused execution and cross-session evidence protocol |
| [Pre-UI-Next archive](../archive/20261023-pre-ui-next/README.md) | Old plan inventory, last-known completion/open states and explicit no-carryover decision |

The [general plan index](../plans/README.md) routes here. Old plans are archived, including the incomplete SAL plan. SAL-T07/T08 open evidence, historical exceptions and operational release prerequisites are **not tasks or prerequisites of this roadmap**. Their status remains recorded; archiving does not complete or waive them. Existing business, security and recovery behavior remains a regression constraint.

## Assessment references

The following retain the 2026-10-22 analysis and source permalinks. Their candidate/authority statements describe that date and are superseded for scheduling by the active roadmap.

| Historical assessment | Use |
|---|---|
| [UI foundation assessment](20261022-ui-foundation-assessment.md) | Findings F01–F09 and original source evidence |
| [Input and inline-editing contracts](20261022-input-and-inline-editing-contracts.md) | Detailed reasoning and existing-versus-proposed distinctions |
| [Candidate backlog and validation](20261022-candidate-backlog-and-validation.md) | Original UIN-01–UIN-10 candidates and alternatives; selected disposition is in the active roadmap |

## Exact inspected baseline

The original app baseline was `9d8a199a27bc505f58b5997fd611ce249c26c00d`. The new base includes later SAL source/evidence changes and the migrated assessment. The three consumed UI pins are unchanged; the [refresh table](20261023-active-roadmap.md#refreshed-source-baseline) records what was rechecked. The [original baseline table](https://github.com/chung6a8m/MyDmsVn/blob/7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639/docs/ui-next/README.md#exact-inspected-baseline) remains available at its frozen commit. Do not copy the old assessment's T08 NOT STARTED or R4 deferred status into current handoffs.

## Resume without re-planning

1. Resolve the current app head and three nested pins; inspect relevant changes since the recorded baseline.
2. Select the first ready task in the active queue, with its design and acceptance IDs. Update only its state as work actually advances.
3. Keep new public API names marked proposed until their upstream commit is available. Land narrowly scoped upstream changes before explicitly consuming pins.
4. Record actual source/test/package revisions, selected tests, outcomes and limitations in this folder's evidence ledger. Planning is not runtime proof.
5. Do not resume an archived task from a stale link or treat a missing old acceptance result as a new UI Next task.

This activation changes documentation only. It does not implement proposed controls, change submodule pins, execute Windows/SQL verification or establish product/release acceptance.
