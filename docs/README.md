# Documentation index — v1a-pro

**Updated:** 2026-10-20. Business Compact and inline Goods Receipt editing (BCI) is the sole active roadmap. RESP, L2DB and GUI-P5 are completed baselines; older unimplemented plans were retired by the owner.

## Current work and Codex entry points

| Document | Owns |
|---|---|
| [AGENTS.md](../AGENTS.md) | Repository instructions, invariants and current work |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Concise source/evidence handoff |
| [Execution index](plans/README.md) | The single active roadmap and completed/retired classification |
| [BCI implementation plan](plans/20261020-001-business-compact-inline-goods-receipt.md) | Ordered tasks, files, interfaces and verification |
| [Business Compact design](BUSINESS_COMPACT_UI.md) | Visual layout, inline lifecycle, editor and accessibility decisions |
| [Business Compact acceptance](BUSINESS_COMPACT_ACCEPTANCE.md) | Observable requirements and new-candidate evidence; all new runtime cases NOT RUN |
| [Development status](DEVELOPMENT_PLAN.md) | Implemented baseline versus planned UI work |
| [Test policy](TEST_POLICY.md) | Sole execution policy; focused work versus milestone scope |
| [Test coverage](TEST_STRATEGY.md) | Required behavior and failure boundaries |
| [Test commands](../tests/README.md) | Direct projects, SQL ownership and interactive GUI prerequisites |

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

## Completed evidence

[RESP verification](RESPAWN_VERIFICATION.md) · [RESP case map](RESPAWN_CASE_MAP.md) · [L2DB verification](L2DB_VERIFICATION.md) · [L2DB case migration](L2DB_CASE_MIGRATION.md) · [GUI-P5 final verification](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) · [GUI coverage](GUI_P5_COVERAGE_MATRIX.md) · [T04 walkthrough](GUI_P5_T04_WALKTHROUGH.md) · [T05 walkthrough](GUI_P5_T05_WALKTHROUGH.md)

Historical task handoffs and trial failures retain their original dates and revisions. They do not override the current execution index or establish acceptance of the proposed inline UI.

## Operational references and scope limits

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md)

Runbooks describe current behavior and safe procedures. [Release prerequisites](DEFERRED_WORK.md#release-prerequisites) are unaccepted and unscheduled. [Retired/out-of-scope directions](DEFERRED_WORK.md) contain no alternative task queue; [History](HISTORY.md#2026-10-20--business-compact-roadmap-reset) records removal of unimplemented plans.
