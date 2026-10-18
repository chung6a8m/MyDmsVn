# Documentation index — v1a

**Updated:** 2026-10-18. Start with the current context and the two active plans. Historical documents are available through Git links in [History](HISTORY.md); they are not an execution queue.

## Codex entry points

| Document | Owns |
|---|---|
| [AGENTS.md](../AGENTS.md) | Repository instructions and invariants |
| [AI_CONTEXT.md](../AI_CONTEXT.md) | Short, accurate current-state handoff |
| [Active plans](plans/README.md) | RESP then L2DB; exactly two active plans |
| [Development plan](DEVELOPMENT_PLAN.md) | Scope, dependencies and completion meaning |
| [Test policy](TEST_POLICY.md) | When and what to test; sole execution policy |
| [Test coverage](TEST_STRATEGY.md) | Required behavior and failure cases |
| [Test commands](../tests/README.md) | Available tooling and transition constraints |

## Product and implementation references

[PRD](PRD.md) · [Architecture](ARCHITECTURE.md) · [Project structure](PROJECT_STRUCTURE.md) · [Tech stack](TECH_STACK.md) · [Contracts](CONTRACTS.md) · [Persistence](PERSISTENCE.md) · [Data model](DATA_MODEL.md) · [Security](SECURITY.md) · [ADRs](adr/README.md)

## Operations

[SQL configuration/security](CONFIGURATION_AND_SQL_SECURITY.md) · [Schema/migration runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security administration](SECURITY_ADMINISTRATION_RUNBOOK.md) · [Diagnostics](LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md) · [Release packaging](LOCAL_RELEASE_PACKAGING.md)

Operational instructions describe the checked-in product; they are not proof of completed P6 release acceptance. The release packaging document explicitly records the schema-manifest discrepancy.

## Deferred and historical context

[Deferred work](DEFERRED_WORK.md) · [.NET 8 reactivation](REACTIVATE_NET8.md) · [History and removed documents](HISTORY.md)

Read these when the task needs them. P5/P6/P7 and runtime changes are not extra active roadmap plans.
