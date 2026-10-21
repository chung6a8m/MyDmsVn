# Documentation index — v1a-pro

**Updated:** 2026-10-21. Business Compact refinement (BCF) is COMPLETE / OWNER ACCEPTED in its [final bounded scope](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21); **SAL — Direct Sales and moving weighted average** is now the sole ACTIVE roadmap from PR #41 baseline `35ba2e8a3f37fb07095701eb94f4b56f7941c491`; SAL-T00 is READY and implementation/evidence are NOT RUN. BCI, RESP, L2DB and GUI-P5 are completed baselines; older unimplemented plans remain retired.

## Current work and Codex entry points

| Document | Owns |
|---|---|
| [AGENTS.md](../AGENTS.md) | Repository instructions, invariants and current work |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Current source/evidence handoff |
| [Execution index](plans/README.md) | Current execution state and completed/retired classification |
| [SAL implementation plan](plans/20261021-002-direct-sales-and-moving-average-costing.md) | Active T00–T08 queue, exact changes, dependencies and test obligations |
| [Direct Sales design](DIRECT_SALES.md) | Approved business scope, lifecycle, contracts and architecture |
| [Sales costing](DIRECT_SALES_COSTING.md) | Exact moving-average math, shared locks, schema10 and baseline bootstrap |
| [Sales query/UI](DIRECT_SALES_QUERY_AND_UI.md) | LINQ/keyset contract and seven-column desktop interaction |
| [Sales acceptance](DIRECT_SALES_ACCEPTANCE.md) | New SAL evidence obligations; all NOT RUN |
| [ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md) | Accepted direction and consequences; not implemented |
| [BCF implementation plan](plans/20261021-001-business-compact-lookup-and-visual-parity.md) | Completed lookup-first sequence, implemented/source distinctions and final bounded acceptance |
| [BCF design](BUSINESS_COMPACT_REFINEMENT.md) | Three lookup contracts, shell/catalog/receipt composition and guarded navigation |
| [BCF acceptance ledger](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) | BCF-A01–A23 reconciled; exact candidates, failed trials and Owner-accepted exceptions |
| [Design references](design-references/business-compact/README.md) | The three unchanged Owner mockups, provenance, hashes and interpretation |
| [Development status](DEVELOPMENT_PLAN.md) | Implemented baseline versus planned sales/valuation |
| [Test policy](TEST_POLICY.md) | Sole execution policy; focused work versus milestone scope |
| [Test coverage](TEST_STRATEGY.md) | Required behavior and failure boundaries |
| [Test commands](../tests/README.md) | Direct projects, SQL ownership and interactive GUI prerequisites |

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

## Completed evidence

[BCI design](BUSINESS_COMPACT_UI.md) · [BCI final acceptance](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) · [BCI walkthrough](BUSINESS_COMPACT_WALKTHROUGH.md) · [RESP verification](RESPAWN_VERIFICATION.md) · [RESP case map](RESPAWN_CASE_MAP.md) · [L2DB verification](L2DB_VERIFICATION.md) · [L2DB case migration](L2DB_CASE_MIGRATION.md) · [GUI-P5 final verification](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) · [GUI coverage](GUI_P5_COVERAGE_MATRIX.md) · [T04 walkthrough](GUI_P5_T04_WALKTHROUGH.md) · [T05 walkthrough](GUI_P5_T05_WALKTHROUGH.md)

Historical task handoffs and trial failures retain their original dates and revisions. They do not override the current execution index or establish acceptance of the new SAL obligations.

## Operational references and scope limits

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md)

Runbooks describe current behavior and safe procedures. [Release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain unaccepted and unscheduled. [Retired/out-of-scope directions](DEFERRED_WORK.md) contain no alternative task queue; [History](HISTORY.md) records roadmap decisions without replacing their original evidence.
