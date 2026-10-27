# Documentation index — main

**Updated:** 2026-10-27. The [execution index](plans/README.md) selects [Business Compact conformance and independent UI Labs](plans/20261027-001-business-compact-and-independent-ui-labs.md). Implementation is NOT STARTED and native gates are NOT APPROVED. The [2026-10-27 archive](archive/20261027-main-roadmap-reset/README.md) preserves the previous Sidebar and Security execution queues after they reached main through PR #52. Archived tests and approvals remain tied to their original candidates.

## Current work and Codex entry points

| Document | Responsibility |
|---|---|
| [AGENTS.md](../AGENTS.md) | Current repository rules, safety and scope-selection discipline |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Current source context, candidate evidence distinctions and acceptance limits |
| [Execution index](plans/README.md) · [active UIL roadmap](plans/20261027-001-business-compact-and-independent-ui-labs.md) | Single plan selector and sole task/status owner for the selected workstream |
| [Development-plan alias](DEVELOPMENT_PLAN.md) | Stable compatibility entry point to that index |
| [Test policy](TEST_POLICY.md) | Sole source-owned execution policy |
| [Test strategy](TEST_STRATEGY.md) · [test commands](../tests/README.md) | Coverage and direct scoped test commands |
| [UI Lab runbook](development/UI_LAB.md) | SQL-free Sales and Security scenario host, reset and usage |
| [Business Compact policy](design/BUSINESS_COMPACT.md) | Durable visual/control contract, toolkit mapping, justified exceptions and native evidence |
| [Independent UI Labs](design/INDEPENDENT_UI_LABS.md) | Selected direct Sales/Security launchers and shared product-presentation contract; implementation target |
| [Security UI specification](design/SECURITY_ADMINISTRATION_UI.md) · [Security Lab review](development/SECURITY_UI_LAB_REVIEW.md) | Implemented UI contracts and repeatable review exercises, not active task checklists |
| [Architecture](ARCHITECTURE.md) · [ADRs](adr/README.md) | Current principles, decisions and historical source distinctions |

The Owner-approved Security SQL-free candidate gate SEC-T05 was followed by bounded SEC-T06–SEC-T08 Local/SQL work. [Plan in archive](archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md), [Lab evidence](evidence/security/20261025-security-ui-lab.md) and [Local/SQL evidence](evidence/security/20261025-security-local-sql.md) retain remaining incomplete attended/native and release limits. Sidebar's [bounded evidence](evidence/sidebar/20261025-sidebar-horizontal-scrollbar.md) does not imply production-package acceptance.

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

The implemented Sales rules remain in [business design](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md) and [ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md). The current [Business Compact policy](design/BUSINESS_COMPACT.md) carries the durable visual/control contract; BCF's [design](BUSINESS_COMPACT_REFINEMENT.md) and [unchanged reference images](design-references/business-compact/README.md) retain original composition and input references. These references do not create an alternate execution queue.

## Archived plans and evidence

[2026-10-27 main archive: Sidebar and Security](archive/20261027-main-roadmap-reset/README.md) · [Retired plan index](archive/20261027-main-roadmap-reset/plans/README.md)

[Archive index](archive/README.md) · [2026-10-25 boundary and retained A1/A3 limits](archive/20261025-pre-next-roadmap/README.md) · [Archived A0/A1 record](archive/20261025-pre-next-roadmap/plans/20261024-001-a0-a1-sales-ui-lab.md)

[UI-NEXT historical index](ui-next/README.md) · [DateInput candidate evidence](ui-next/evidence/20261024-dateinput-unified-compact-correction.md) · [T13 retained failures and acceptance](ui-next/evidence/20261024-uin-t13-integration.md)

[Archive boundary](archive/20261023-pre-ui-next/README.md) · [Frozen pre-UI-NEXT plan index](archive/20261023-pre-ui-next/plans/README.md) · [SAL plan](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) · [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) · [T08 integration evidence](evidence/sal/20261021-t08-integrated-acceptance.md)

SAL source and reviewed R3/R4 fixes are present. T08 records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS; native125, attended observations and dated Owner acceptance remain OPEN. Original failures and source/test/package distinctions remain authoritative. Old unfinished acceptance and full integration repeats are not automatically inherited by future work.

[BCF final scope](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) · [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [BCI final acceptance](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) · [RESP verification](RESPAWN_VERIFICATION.md) · [L2DB verification](L2DB_VERIFICATION.md) · [GUI-P5 final verification](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20)

Historical evidence retains its original dates, revisions, failures and signed exceptions. It does not establish acceptance of a later candidate or production release.

## Operational references and scope limits

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md)

Runbooks describe source behavior and safe procedures. [Release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED and excluded from the active queue. [Retired/out-of-scope directions](DEFERRED_WORK.md) contain no alternative task order; [History](HISTORY.md) records prior decisions without replacing their evidence.
