# Documentation index — v1a-pro-ui-next

**Updated:** 2026-10-25. The [execution index](plans/README.md) records **no active execution plan; the next roadmap has not been selected** after the [latest archive boundary](archive/20261025-pre-next-roadmap/README.md). This index routes readers by responsibility; historical results stay with their original candidates.

## Current work and Codex entry points

| Document | Owns |
|---|---|
| [AGENTS.md](../AGENTS.md) | Stable repository instructions and invariants |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Source context, historical boundaries and evidence distinctions |
| [Execution index](plans/README.md) | Current selection state; pointer to a plan only after new scope is selected |
| [Development-plan alias](DEVELOPMENT_PLAN.md) | Compatibility entry point to the index; no progress copy |
| [Test policy](TEST_POLICY.md) | Sole execution policy; focused scope and required safeguards |
| [Test coverage](TEST_STRATEGY.md) | Required behavior and failure boundaries |
| [Test commands](../tests/README.md) | Direct projects, SQL ownership and interactive GUI prerequisites |
| [Sales UI Lab runbook](development/UI_LAB.md) | Developer host, scenarios, reset and focused Lab commands |
| [MyDmsVn.Docs](https://github.com/chung6a8m/MyDmsVn.Docs) | Cross-repository assessments, organization proposals and scoped decisions; no duplicate task status |

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

The implemented Sales rules remain in [business design](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md) and [ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md). BCF's [design](BUSINESS_COMPACT_REFINEMENT.md) and [unchanged reference images](design-references/business-compact/README.md) retain the existing composition and input boundaries. These references do not create an alternate execution queue.

## Archived plans and evidence

[Archive index](archive/README.md) · [2026-10-25 boundary and retained A1/A3 limits](archive/20261025-pre-next-roadmap/README.md) · [Archived A0/A1 record](archive/20261025-pre-next-roadmap/plans/20261024-001-a0-a1-sales-ui-lab.md)

[UI-NEXT historical index](ui-next/README.md) · [DateInput candidate evidence](ui-next/evidence/20261024-dateinput-unified-compact-correction.md) · [T13 retained failures and acceptance](ui-next/evidence/20261024-uin-t13-integration.md)

[Archive boundary](archive/20261023-pre-ui-next/README.md) · [Frozen pre-UI-NEXT plan index](archive/20261023-pre-ui-next/plans/README.md) · [SAL plan](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) · [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) · [T08 integration evidence](evidence/sal/20261021-t08-integrated-acceptance.md)

SAL source and reviewed R3/R4 fixes are present. T08 records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS; native125, attended observations and dated Owner acceptance remain OPEN. Original failures and source/test/package distinctions remain authoritative. Old unfinished acceptance and full integration repeats are not automatically inherited by future work.

[BCF final scope](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) · [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [BCI final acceptance](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) · [RESP verification](RESPAWN_VERIFICATION.md) · [L2DB verification](L2DB_VERIFICATION.md) · [GUI-P5 final verification](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20)

Historical evidence retains its original dates, revisions, failures and signed exceptions. It does not establish acceptance of a later candidate or production release.

## Operational references and scope limits

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md)

Runbooks describe source behavior and safe procedures. [Release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED and excluded from the active queue. [Retired/out-of-scope directions](DEFERRED_WORK.md) contain no alternative task order; [History](HISTORY.md) records prior decisions without replacing their evidence.
