# Execution-plan index

**Updated:** 2026-10-26 · **Base branch:** `v1a-pro-ui-next`

## Current plan

The Owner-requested Sidebar follow-up and the planned Security workspace are separate workstreams. Each linked plan is the sole owner of task state, dependencies and verification within its scope; this index contains no duplicate task ledger.

| Workstream | Scope and status owner | Design/review references |
|---|---|---|
| Sidebar navigation | [Sidebar Navigation Horizontal Scrollbar](20261025-001-sidebar-navigation-horizontal-scrollbar.md): complete within the Owner-selected SQL-free hosted-shell scope; the plan owns exact evidence and limitations. | [Reviewed zero-tab and Products-tab screenshots](../design-references/sidebar-horizontal-scrollbar/README.md) |
| Security workspace | [Security Administration — UI Lab First](20261025-002-security-administration-ui-lab-first.md): Owner-approved SQL-free candidate, completed bounded SEC-T06–SEC-T08 cutover/integration/handoff, with attended and release limits retained. | [UI and behavior specification](../design/SECURITY_ADMINISTRATION_UI.md) · [UI Labs review guide](../development/SECURITY_UI_LAB_REVIEW.md) |

The UI Lab now hosts Sales and the SQL-free Security candidate. The Owner approved the reviewed Security candidate at **SEC-T05** on 2026-10-26; **SEC-T06 Local cutover**, **SEC-T07 focused SQL integration** and **SEC-T08 bounded handoff** are recorded in the Security plan and [post-gate evidence](../evidence/security/20261025-security-local-sql.md). Its incomplete attended matrix and other stated limits remain open. Record any future task execution and gate decision only in the plan that owns that scope.

Use [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → this index → the plan for the authorized scope and [TEST_POLICY](../TEST_POLICY.md). [TEST_STRATEGY](../TEST_STRATEGY.md) owns coverage and [tests/README](../../tests/README.md) owns commands. Continue only work authorized by the Owner; archived tasks and prompts do not supply that authorization.

## Selecting the next roadmap

These are bounded, separately owned follow-ups after the [2026-10-25 archive boundary](../archive/20261025-pre-next-roadmap/README.md). The Sidebar plan retains its source-based diagnosis and focused Windows verification. The Security plan records the UI-Labs-first implementation and bounded handoff without replacing Sidebar tasks or activating a broader product/release roadmap.

Do not carry forward unfinished UI-NEXT/SAL acceptance, old integration repeats, release prerequisites or other proposed phases automatically. Any additional selected follow-up must cite the original evidence and define its own scope; archival neither completes nor waives it.

Future scope is selected explicitly by the Owner and recorded in a dated plan based on the then-current source. Point this index to one execution-status owner for each selected scope. Record decisions needed for this repository in its local [Architecture](../ARCHITECTURE.md), [ADRs](../adr/README.md) and authorized plan. Historical external proposals or publication status do not add required reading, synchronization, approval or implementation work.

## Historical plans and acceptance

- [2026-10-25 archive](../archive/20261025-pre-next-roadmap/README.md): UI-NEXT assessments/design/plans/prompts, A0/A1 plan and the former execution index. Old-path files retain compatibility anchors only.
- [UI-NEXT reference index](../ui-next/README.md): route to archived rationale and unchanged evidence, including DateInput and T13 bounded acceptance.
- [2026-10-23 archive](../archive/20261023-pre-ui-next/README.md): six earlier plans and their frozen index; incomplete SAL acceptance remains open in its original evidence.
- [Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED. Deleted REL/P6/runtime plans stay retired.

The archive and current source are not interchangeable: historical verification applies only to its exact candidate and scope. This documentation change supplies no new runtime or production acceptance.
