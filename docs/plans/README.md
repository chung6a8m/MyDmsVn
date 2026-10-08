# Phase execution plans — Codex entry points

All phase statuses: **Not started** when authored on 2026-10-08. These are execution instructions, not implementation evidence.

| Phase | Plan |
|---|---|
| P0 | [Repository and solution foundation](20261008-002-phase-0-foundation.md) |
| P1 | [Persistence foundation](20261008-003-phase-1-persistence.md) |
| P2 | [Application contracts and pipeline](20261008-004-phase-2-application-pipeline.md) |
| P3 | [Security foundation](20261008-005-phase-3-security.md) |
| P4 | [WinForms desktop foundation](20261008-006-phase-4-desktop.md) |
| P5 | [Sales & Inventory Foundation](20261008-001-sales-inventory-foundation.md) |
| P6 | [Local v1 hardening](20261008-007-phase-6-local-hardening.md) |
| P7 | [ASP.NET Core v2 / HTTP parity](20261008-008-phase-7-remote-api.md) |

A phase begins only after preceding phase exit checks have evidence. `docs/DEVELOPMENT_PLAN.md` is the summary/source of phase status; task-specific detail is here. `AGENTS.md` is the required AI instructions entry point.

## How to instruct Codex

Example: "Read AGENTS.md and docs/plans/20261008-002-phase-0-foundation.md, implement P0 only, run available tests, report what passed and what requires Windows/SQL, and update checkboxes only for verified work."

When Codex finishes a task, demand:
1. Scope and changed files.
2. Actual build/test commands, result summaries and unexecuted gates.
3. Any deviation, compatibility issue or accepted ADR.
4. Next task ID and blockers.

Do not combine multiple phases into an unreviewable implementation change.
