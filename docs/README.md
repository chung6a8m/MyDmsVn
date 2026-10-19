# Documentation index — v1a

**Updated:** 2026-10-19. RESP/L2DB implementation acceptance is complete in its scope; GUI-P5 and P6 release acceptance are the current OPEN milestones. Historical plans and verification results do not automatically grant current GUI/release PASS.

## Codex entry points

| Document | Owns |
|---|---|
| [AGENTS.md](../AGENTS.md) | Current repo rules and invariants |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Accurate context/state handoff |
| [Execution plans](plans/README.md) | RESP/L2DB complete → GUI-P5 → REL/P6 |
| [GUI-P5 plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md) | Full WinForms automated/packaged GUI and two P5 attended acceptance IDs |
| [REL/P6 plan](plans/20261019-002-local-v1-p6-release-acceptance.md) | Operational hardening, clean package/install, SQL recovery, G1–G5, owner sign-off |
| [Development plan](DEVELOPMENT_PLAN.md) | Milestones, dependencies, status |
| [Test policy](TEST_POLICY.md) | Sole test execution policy; affected scope vs milestone |
| [Test coverage](TEST_STRATEGY.md) | Required behavior / failure boundaries |
| [Test commands](../tests/README.md) | Direct `dotnet test`, SQL ownership and interactive GUI requirements |

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

## Operations / release prerequisites

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md).

Runbooks explain current behavior and safe procedure. The fixed schema-manifest discrepancy is not full P6 release acceptance. GUI-P5 and REL will produce fresh candidate-specific evidence; future verification/coverage matrix files are deliverables, not currently accepted documents.

## Historical and later work

[Deferred P7/net8/modules](DEFERRED_WORK.md) · [.NET 8 reactivation](REACTIVATE_NET8.md) · [History/removed docs](HISTORY.md). Use immutable older P5/P6 plans only as provenance; they contain obsolete two-host and SQL-runner instructions.
