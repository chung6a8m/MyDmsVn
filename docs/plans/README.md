# Execution-plan index

**Updated:** 2026-10-27 · **Source baseline:** [`main@6eee394263a2`](https://github.com/chung6a8m/MyDmsVn/commit/6eee394263a28c33360877fcf3feb6e0dcdab8ad) · **Status:** NO ACTIVE EXECUTION PLAN

## Current plan

**None selected.** Sidebar PR #50 and Security PR #51 have both merged into `main` in PR #52. Their bounded implementation/verification plans have been archived. This index is deliberately empty until the Owner explicitly authorizes the next workstream. Neither OPEN historical acceptance nor an archived handoff is an implicit new task.

Use [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → this index → a newly authorized dated plan → [TEST_POLICY](../TEST_POLICY.md). Current [Architecture](../ARCHITECTURE.md), [ADRs](../adr/README.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md) remain valid references.

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

Evidence must remain tied to its source/test/package hashes, environment and selected scope. Documentation-only archiving has no product acceptance or SQL/GUI pass of its own.
