# Codex instructions — MyDmsVn

Applies to the entire repository. This file is authoritative for AI-assisted changes.

## Mission and status

Develop MyDmsVn as a Windows desktop-first modular-monolith SME platform, starting with Sales & Inventory Foundation. **P0–P4 foundations and P5 implementation/automated gates exist; P5 authenticated interactive acceptance and P6 release gates remain open.** Consult `docs/DEVELOPMENT_PLAN.md` for the current evidence-based status. **P3.1 initial Administrator/continuous permission sync is implemented and verified (PR #18 merged 2026-10-14); P4.1 real Login/session remains planned**, despite historical P3/P4 completion. Implement in small, reviewable phase-scoped changes and do not claim later-phase capabilities exist.

## Read-order and precedence

1. This `AGENTS.md`.
2. `docs/PRD.md`, `docs/ARCHITECTURE.md`, `docs/TECH_STACK.md`.
3. `docs/DEVELOPMENT_PLAN.md` and the **specific phase plan**.
4. Applicable `docs/CONTRACTS.md`, `docs/PERSISTENCE.md`, `docs/SECURITY.md`, `docs/DATA_MODEL.md` and `docs/TEST_STRATEGY.md`.
5. Accepted decisions in `docs/adr/`.
6. `project-idea.md` is the raw idea; its examples may be incorrect and are **not implementation instructions**.

If documents contradict each other, flag the issue in the change summary; do not silently substitute a different architecture. Explicit user decisions override prior plans.

## Fixed technical decisions

- Keep `net48`, `net8.0` and `net8.0-windows` as appropriate. **Do not move to .NET 10.**
- RepoDb `1.16.0`; RepoDb.SqlServer `1.16.1`; RepoDb.SqlServer.BulkOperations `1.16.2`.
- Explicit UnitOfWork only: scope owns a single open connection and optional transaction; repositories and Dapper queries within that scope share them explicitly. No ambient/static AsyncLocal transaction.
- RepoDb for command-side storage, Dapper for query-side DTO projection; DbUp for versioned SQL migrations.
- Server.Application / Domain must not know HTTP, WinForms or database implementation.
- Desktop UI / ViewModels depend only on desktop abstractions and Contracts, never directly on Server.Infrastructure.
- v1 Local uses in-process Application dispatch; v2 HTTP uses the **same DTO/API result semantics**.
- Preserve legacy `PasswordHash` and `PasswordSalt` columns for migration; no new plaintext or reversible password storage.
- Authorization is checked on server-side application use cases; UI gating alone is insufficient. UserPermission explicit deny/grant overrides roles; default deny.
- New features must declare/register permission keys centrally and validate new-key lifecycle in tests. Provision/synchronize permissions under reviewed deployment/administrator privilege only; never seed users/roles or run synchronization from desktop Login/startup. See `docs/plans/20261013-001-initial-security-provisioning-permission-sync.md`.
- Do not add sales order, issue, returns, pricing, accounting or multi-warehouse receipt to P5 unless separately approved.

## Coding conventions

- Follow the **existing** `.editorconfig` and `.gitattributes`: C#/Markdown CRLF; UTF-8 with BOM where specified; Node/web assets LF if introduced.
- Prefer readable C# that compiles for **both targeted TFMs**. Do not use runtime APIs missing on `net48` simply because newer language syntax parses.
- Nullable annotations and target-specific code must be handled with explicit project settings. No suppressing warnings project-wide merely to get a green build.
- Use Microsoft DI; register repository implementations explicitly (no arbitrary first-type assembly scanning).
- Services return typed results. Keep cross-process DTOs independent from `ErrorOr`, MediatR, RepoDb and WinForms.
- Centralize domain validation, authorization, logging and error mapping; avoid duplicated rules in forms.
- SQL scripts are additive, repeatable in clean environments and never drop user data implicitly.
- No secrets, real credentials or production connection strings in commits.

## Work protocol

1. Inspect the repo and phase dependencies; choose the first unfinished phase/task. Do not invent completed code. Cross-phase SQLPERF test-hardening tasks may proceed independently in separate PRs; preserve full dual-TFM SQL gates.
2. State the scope and affected layers before implementation; avoid changing unrelated modules.
3. Make a minimal vertical change with automated tests.
4. Build both `net48` and `net8.0` / `net8.0-windows` configurations on an environment with the targeting packs.
5. Run applicable tests. SQL Server integration tests must target an isolated disposable test database, **not production**.
6. Summarize changed files, tests run and their actual results, remaining risks, and the next roadmap task. Only check off acceptance criteria supported by evidence.
7. Update plan checkboxes/status when tasks are genuinely complete; record significant deviations in an ADR.

## Testing safeguards

- WinForms tests that create controls must run on an STA thread and close/dispose controls deterministically.
- Ensure `Application.ThreadException`, `AppDomain.UnhandledException` and `DataGridView.DataError` do not trigger modal dialogs in headless tests. Capture/assert errors instead.
- Use timeouts for UI/integration tests, keep background tasks cancellable, and avoid blocked Codex sessions.
- Test transaction commit, rollback, parallel updates, duplicate posting, permissions and parity of Local/HTTP adapters as described in `docs/TEST_STRATEGY.md`.
- If Windows GUI, SQL Server or targeting packs are unavailable, report the limitation honestly; do not mark their tests as passed.

## Useful starting prompts

- "Implement P0 from `docs/plans/20261008-002-phase-0-foundation.md`; create solution skeleton, DI and build verification only."
- "Implement P1 from `docs/DEVELOPMENT_PLAN.md` and `docs/PERSISTENCE.md`, including SQL integration tests."
- "Implement P5.1 master data after P0–P4 quality gates; follow `docs/plans/20261008-001-sales-inventory-foundation.md`."
- "Optimize SQL integration test performance one task at a time from `docs/plans/20261014-001-sql-integration-test-performance-optimization.md`; start at SQLPERF-T01 instrumentation, preserve real SQL isolation and dual-target regression gates."
- "Implement P4.1 shared WinForms Login/session in `docs/plans/20261013-002-desktop-login-session.md` after P3.1, then complete authenticated P5/P6 release gates."

Do not automatically implement all phases in a single unreviewable change.
