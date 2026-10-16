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
| **SQLPERF** (cross-phase) | **[SQL integration test performance optimization](20261014-001-sql-integration-test-performance-optimization.md)** — T01–T06 complete (2026-10-15); serial SQL safety and unfiltered release gates remain |
| **TESTGATE T01–T03** (cross-phase) | [Task-scoped execution and strict SQL gate orchestration](20261016-001-task-gates-and-modular-tests.md) — T01 policy/docs complete; T02–T03 proposed, not verified |
| **TESTGATE T04–T05** (cross-phase) | [Modular Desktop and SQL test projects](20261016-002-modular-test-projects.md) — proposed, not implemented; requires reviewed T02/T03 gates |
| **SEC-SIMPLE T01–T06** (post-TESTGATE) | [Single superuser and customer-managed permissions](20261016-003-superuser-and-manual-permission-management.md) — **proposed / not implemented**; starts only after verified T02/T03 and T04/T05 acceptance; [ADR 0007](../adr/0007-code-defined-permissions-and-single-superuser.md) is proposed |

A phase begins only after preceding phase exit checks have evidence. **P3.1 is verified; P4.1 remains a prerequisite for authenticated P5 GUI acceptance and P6 release sign-off.** SQLPERF is an independent test-infrastructure workstream with serial full-suite gates. This is a targeted integration follow-up to completed P3/P4, not a rewrite of their historical evidence. `docs/DEVELOPMENT_PLAN.md` is the summary/source of phase status; task-specific detail is here. `AGENTS.md` is the required AI instructions entry point.

## Progressive test policy for pending work (2026-10-15)

Apply [Codex Test Execution Policy](../CODEX_TEST_EXECUTION_POLICY.md) to **P4.1, the two pending P5 GUI walkthroughs, pending P6 verification/release tasks and future P7**. The seven fully completed plans (P0, P1, P2, P3, P3.1, P4 and SQLPERF) are **not modified**. For partially completed P5/P6 plans, guidance applies only to their unchecked items, without revisiting historical completed results or checkboxes.

## How to instruct Codex

Example: "Read AGENTS.md and docs/plans/20261008-002-phase-0-foundation.md, implement P0 only, run available tests, report what passed and what requires Windows/SQL, and update checkboxes only for verified work."

When Codex finishes a task, demand:
1. Scope and changed files.
2. Actual build/test commands, result summaries and unexecuted gates.
3. Any deviation, compatibility issue or accepted ADR.
4. Next task ID and blockers.

P3.1 registry/sync/first-admin CLI is complete. Implement **P4.1 shared Login/session UI** as the next authenticated feature prerequisite. SQLPERF-T01–T06 are complete; do **not** restart them without an approved new task. For future feature PRs follow the progressive Codex test policy; separate any new performance A/B work from routine PR regression. **Until SEC-SIMPLE is implemented and verified**, the currently deployed P3.1 permission lifecycle remains mandatory on feature releases (new keys -> registration -> approval -> sync -> authorization tests). The proposed [SEC-SIMPLE follow-up](20261016-003-superuser-and-manual-permission-management.md) would replace that *future* workflow only after its safe migration and verification gates; it must never be treated as current runtime behavior. Neither task may add production defaults or run DbUp/provisioning during desktop Login.

Do not combine multiple phases into an unreviewable implementation change.
