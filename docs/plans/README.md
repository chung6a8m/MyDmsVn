# Execution-plan index

**Updated:** 2026-10-28 · **Original source baseline:** [`main@8de4451573e8`](https://github.com/chung6a8m/MyDmsVn/commit/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9) · **Runtime/test handoff:** `8ec1ff71ef0a6a7cf4befa88bbd7bed2616827a0` · **Status:** SELECTED ROADMAP COMPLETE WITHIN BOUNDED SCOPE

## Current plan

**Selected:** [Business Compact conformance and independent UI Labs](20261027-001-business-compact-and-independent-ui-labs.md). The Owner approved this direction and requested the documentation PR/activation. That plan is the sole task/status owner for UIL-T00–UIL-T08: control inventory and repository UI workflow, shared product/Lab presentation, independent Sales/Security launchers, SEC native pilot and approval, remaining SEC conformance, final approval, focused product-host/shared regression and handoff.

UIL-T00–UIL-T08 are complete within the roadmap's recorded bounded scope. UIL-T04 and UIL-T06 have dated Owner decisions; UIL-T07 records SQL-free physical-100/125 product-host parity, the session/sidebar correction and focused shared/Sales regression. No further task, SQL, actual Login, package or release acceptance is selected. The durable [Business Compact policy](../design/BUSINESS_COMPACT.md) and [Lab specification](../design/INDEPENDENT_UI_LABS.md) own the selected contracts; they do not duplicate task status. Sidebar PR #50 and Security PR #51 remain archived with their original bounded evidence. Neither OPEN historical acceptance nor an archived handoff is an implicit additional task.

Use [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → this index → the selected dated plan and its design references → [TEST_POLICY](../TEST_POLICY.md). Current [Architecture](../ARCHITECTURE.md), [ADRs](../adr/README.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md) remain valid references.

## New-roadmap activation policy

1. Select the new scope explicitly with the Owner. Inspect the then-current `main` source, relevant gitlinks and actual tests; do not use an old branch name as the base of new context.
2. Create a **new dated plan** under `docs/plans/` that owns its status, concrete tasks, dependencies, UI Labs/design approval when applicable, source/runtime boundaries and focused acceptance evidence. Add the plan link here **only when selected**.
3. Use historical failures/limits as risk references, **not** inherited work authorizations or automatic PASS. Preserve the sole test policy, separate SQL/GUI gates, explicit evidence and release safeguards.
4. At the next archive, freeze completed bounded execution owners, leave compatibility anchors, refresh AGENTS/AI_CONTEXT and keep original evidence immutable.

## Historical plans and preserved limits

| Boundary | Records and limitations |
|---|---|
| [2026-10-27 main reset](../archive/20261027-main-roadmap-reset/README.md) | [Sidebar plan](../archive/20261027-main-roadmap-reset/plans/20261025-001-sidebar-navigation-horizontal-scrollbar.md) (hosted-shell only) and [Security plan](../archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md) (Owner-approved SEC-T05 plus bounded Local/SQL; incomplete SEC-T04/attended acceptance). |
| [2026-10-25 archive](../archive/20261025-pre-next-roadmap/README.md) | UI-NEXT and A0/A1, retained A3 consumer and candidate-specific limitations |
| [UI-NEXT historical index](../ui-next/README.md) | DateInput and T13 candidate failures, corrections and bounded results |
| [2026-10-23 archive](../archive/20261023-pre-ui-next/README.md) | Earlier plans including uncompleted SAL native125/attended/Owner acceptance |
| [Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) | UNACCEPTED / UNSCHEDULED — never inferred from a source merge |

Evidence must remain tied to its source/test/package hashes, environment and selected scope. Documentation-only archival or activation has no product acceptance or SQL/GUI pass of its own.
