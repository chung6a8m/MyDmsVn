# Phase execution plans — Codex entry points

These are execution instructions, not implementation evidence. Statuses belong to each plan and the roadmap; P3.1 initial Administrator provisioning/permission synchronization is **complete** after PR #18; P4.1 interactive Login/session is still pending.

| Phase | Plan |
|---|---|
| P0 | [Repository and solution foundation](20261008-002-phase-0-foundation.md) |
| P1 | [Persistence foundation](20261008-003-phase-1-persistence.md) |
| P2 | [Application contracts and pipeline](20261008-004-phase-2-application-pipeline.md) |
| P3 | [Security foundation](20261008-005-phase-3-security.md) |
| **P3.1** | **[Initial security provisioning and continuous permission synchronization](20261013-001-initial-security-provisioning-permission-sync.md)** — complete (2026-10-14) |
| P4 | [WinForms desktop foundation](20261008-006-phase-4-desktop.md) |
| **P4.1** | **[Desktop Login, session and logout](20261013-002-desktop-login-session.md)** — new, not started |
| P5 | [Sales & Inventory Foundation](20261008-001-sales-inventory-foundation.md) |
| P6 | [Local v1 hardening](20261008-007-phase-6-local-hardening.md) |
| P7 | [ASP.NET Core v2 / HTTP parity](20261008-008-phase-7-remote-api.md) |
| **SQLPERF** (cross-phase) | **[SQL integration test performance optimization](20261014-001-sql-integration-test-performance-optimization.md)** — planned (2026-10-14); can proceed separately from P4.1; do not weaken full SQL gates |

A phase begins only after preceding phase exit checks have evidence. **P3.1 is verified; P4.1 remains a prerequisite for authenticated P5 GUI acceptance and P6 release sign-off.** SQLPERF is an independent test-infrastructure workstream with serial full-suite gates. This is a targeted integration follow-up to completed P3/P4, not a rewrite of their historical evidence. `docs/DEVELOPMENT_PLAN.md` is the summary/source of phase status; task-specific detail is here. `AGENTS.md` is the required AI instructions entry point.

## How to instruct Codex

Example: "Read AGENTS.md and docs/plans/20261008-002-phase-0-foundation.md, implement P0 only, run available tests, report what passed and what requires Windows/SQL, and update checkboxes only for verified work."

When Codex finishes a task, demand:
1. Scope and changed files.
2. Actual build/test commands, result summaries and unexecuted gates.
3. Any deviation, compatibility issue or accepted ADR.
4. Next task ID and blockers.

P3.1 registry/sync/first-admin CLI is complete. Implement **P4.1 shared Login/session UI** as the next authenticated feature prerequisite. Execute **SQLPERF-T01** before modifying SQL test synchronization or database fixtures; separate performance PRs from P4.1 feature PRs. The permission lifecycle must continue on every later feature release (new keys -> registration -> approval -> sync -> authorization tests); neither task may add production defaults or run DbUp/provisioning during desktop Login.

Do not combine multiple phases into an unreviewable implementation change.
