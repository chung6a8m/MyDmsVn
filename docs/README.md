# Documentation index — v1a-pro-ui-next

**Updated:** 2026-10-23. **UI-NEXT is the sole ACTIVE roadmap**, based on `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` (includes merged PR #43 and the migrated assessment). All implementation tasks are **NOT STARTED**; no new runtime PASS is claimed. The [archive boundary](archive/20261023-pre-ui-next/README.md) retires previous queues and excludes their unfinished work without completing or waiving it.

## Current work and Codex entry points

| Document | Owns |
|---|---|
| [AGENTS.md](../AGENTS.md) | Repository instructions, invariants and current work |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Current source, archive boundary and implementation handoff |
| [UI-NEXT active roadmap](ui-next/20261023-active-roadmap.md) | Sole execution queue, dependencies and task status |
| [Design and control policy](ui-next/20261023-design-and-control-policy.md) | Workspace behavior, existing/proposed controls and app/vendor ownership |
| [Implementation plan](ui-next/20261023-implementation-plan.md) | Task-owned source/test work and concrete implementation steps |
| [Validation and handoff](ui-next/20261023-validation-and-handoff.md) | Scoped future verification, evidence and completion boundaries |
| [Development status](DEVELOPMENT_PLAN.md) | Implemented baseline versus unstarted UI-NEXT work |
| [Test policy](TEST_POLICY.md) | Sole execution policy; focused scope and required safeguards |
| [Test coverage](TEST_STRATEGY.md) | Required behavior and failure boundaries |
| [Test commands](../tests/README.md) | Direct projects, SQL ownership and interactive GUI prerequisites |

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

The implemented Sales rules remain in [business design](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md) and [ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md). BCF's [design](BUSINESS_COMPACT_REFINEMENT.md) and [unchanged reference images](design-references/business-compact/README.md) retain the existing composition and input boundaries. These references do not create an alternate execution queue.

## Archived plans and evidence

[Archive boundary](archive/20261023-pre-ui-next/README.md) · [Former plan index](plans/README.md) · [SAL plan](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) · [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) · [T08 integration evidence](evidence/sal/20261021-t08-integrated-acceptance.md)

SAL source and reviewed R3/R4 fixes are present. T08 records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS; native125, attended observations and dated Owner acceptance remain OPEN. Original failures and source/test/package distinctions remain authoritative. Old unfinished acceptance and full integration repeats are excluded from UI-NEXT.

[BCF final scope](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) · [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [BCI final acceptance](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) · [RESP verification](RESPAWN_VERIFICATION.md) · [L2DB verification](L2DB_VERIFICATION.md) · [GUI-P5 final verification](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20)

Historical evidence retains its original dates, revisions, failures and signed exceptions. It does not establish acceptance of UI-NEXT or production release.

## Operational references and scope limits

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md)

Runbooks describe source behavior and safe procedures. [Release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED and excluded from the active queue. [Retired/out-of-scope directions](DEFERRED_WORK.md) contain no alternative task order; [History](HISTORY.md) records prior decisions without replacing their evidence.
