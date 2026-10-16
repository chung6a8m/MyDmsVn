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

### Codex test execution policy

**Task execution budget (mandatory for new implementation work):**

- Codex MUST NOT automatically run `dotnet test MyDmsVn.sln`, an unfiltered SQL suite, the strict SQL benchmark, or any full repository regression during L1 edits/fixes or after each L2 task. Run only the smallest correct affected project/class/method and real SQL categories actually required by that task, recording selection and coverage rationale.
- Every new implementation plan MUST provide an explicit **per-task L2 gate matrix**: affected module/dependencies, projects/TFMs, reviewed test selectors, needed SQL category and adversarial cases, actual runnable commands, timeout/approval/cleanup, and pass/fail/skip/blocked evidence. See [Test Gate Matrix](docs/TEST_GATE_MATRIX.md).
- Schedule **one planned L3 acceptance invocation** only after all task gates are green and gate-affecting edits are frozen. Preserve unfiltered dual-TFM SQL, discovery/TRX, watchdog, approval and ownership reconciliation. A failed L3 gate or later relevant edit invalidates prior evidence: repair via focused tests, then rerun the affected complete mandatory gate. Never treat stale evidence as green to force exactly one execution.
- **Current tooling:** `Invoke-TestGate.ps1` reads `tests/test-gates.v1.json` and provides manifest-owned `Task`/`Final` modes; Final reuses the strict single-pass `pr-acceptance` path (one unfiltered sequential pass per SQL TFM), requires a clean committed revision and approved SQL resources, and emits only sanitized aggregate evidence. The focused SQL wrapper still selects one category rather than module+category. Do not invent broader selectors or downgrade L3 to plain `dotnet test`; use `baseline` only for plan-triggered measurements.
- **Local-only acceptance for TESTGATE:** execute focused task checks and the final full dual-TFM SQL gate in the approved local environment; publish only a separately reviewed sanitized aggregate evidence report and retain raw TRX, timing and ownership/approval journals locally (never attach raw ZIPs); do not add CI jobs or require CI to rerun the same full suite. CI adoption requires a separate explicit user request. This does not weaken local L3 evidence or change existing unrelated CI behavior.
- Performance benchmarks/stress repetitions belong to plan-triggered L4, not ordinary feature L1/L2. [Proposed ADR 0006](docs/adr/0006-task-scoped-test-execution-and-module-projects.md) and the verified [TESTGATE T01–T03 plan](docs/plans/20261016-001-task-gates-and-modular-tests.md) define the current contract; [T04/T05 modularization](docs/plans/20261016-002-modular-test-projects.md) remains future work. Accepted ADR 0005 remains authoritative on isolation/parallelism.


- Before running tests for a change, choose the smallest relevant *non-SQL or SQL* test scope and record its rationale. Use progressive validation: focused inner-loop tests, affected-project/task tests, then final unfiltered PR acceptance. Do not rerun the entire SQL suite after each local fix.
- The authoritative level-by-level decision matrix, existing command capabilities, exceptions and evidence template are in `docs/CODEX_TEST_EXECUTION_POLICY.md`. Follow it alongside the existing isolation policy below and every explicit plan acceptance gate; a narrow inner-loop run never replaces a required final gate.
- For SQL-backed focused development runs, prefer the existing guarded `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` by a verified SQL responsibility category. Its category filter is **not** a full-suite acceptance result. Preserve fail-closed SQL instance approval, process watchdog, ownership reconciliation, theory-row inventory and zero mandatory SQL skips.
- For a code-bearing PR, perform final applicable Release tests on both supported TFMs, including unfiltered dual-TFM SQL integration when required by the project acceptance rules, **for the final gate-affecting revision** (including source, tests/theory rows, SQL/migrations, inventories/manifests, harness/runner, and build/CI inputs), not just the last application-behavior edit. If these inputs change after a green run, rerun the applicable complete L3 gates. A documentation-only PR may use documented static checks instead; never claim an unexecuted SQL/GUI gate passed. Missing Windows, SQL approval or test resources means `blocked/not run`, not `passed`.
- The strict SQL runner exposes separate `pr-acceptance` (one unfiltered sequential pass per TFM) and multi-pass `baseline` (one warm-up and three measured passes per TFM) protocols. Do not treat bare `dotnet test` as equivalent to the strict discovery/TRX/ownership gate. Performance A/B comparisons and specialized stress tests remain conditional on the relevant plan.

### Test parallelization and resource isolation

- Decide parallel safety from **resource ownership and observable isolation**, not merely from the test-project name, SQL test trait, or test duration. Test classification (for example `FastSQL`, `MigrationSchema`, `SecurityProvisioning`, `SqlConcurrency`) is separate from execution/isolation policy.
- For SQL integration tests, the existing `[assembly: CollectionBehavior(DisableTestParallelization = true)]` is a **transitional safety guard**, not the long-term architectural default. Aim for **bounded xUnit collection-level parallelism** between independently owned databases/resources only after adversarial isolation, cleanup, capacity, repeatability and both-TFM gates pass. Do not remove the current guard merely to improve a benchmark; retain a proven serial fallback.
- A migrated database fixture may be reused only with explicit ownership, complete reset/teardown and no overlapping writes or unsafe shared state. Tests sharing mutable fixture state must be coordinated by the same collection or a separately proven equivalent mechanism. SQL migration/history, schema mutation, security provisioning and multi-session race tests require isolation appropriate to their behavior.
- Continue to serialize Desktop tests that create/use WinForms controls, interact with global UI state, drive real mouse/keyboard/focus or rely on shared message-loop/process state. STA execution by itself does **not** make simulated GUI input parallel-safe. The present `Desktop.Tests` assembly-wide guard stays in place; pure ViewModel tests may be separated into independent parallel-compatible projects/collections only after proof.
- Unit/architecture/contract tests without shared mutable resources should retain normal xUnit collection parallelism. A collection guard applies only within its test assembly/process; runner/CI coordination is required for shared SQL/UI resources across processes and target frameworks. Do not confuse deliberate multi-session concurrency **inside one test** with runner-level parallelism.
- Keep SQLPERF acceptance measurements for `net48` and `net8.0` sequential unless the measurement protocol is explicitly revalidated. Preserve approval, ownership journaling, mandatory SQL-case inventory, watchdog, zero-skipped required tests and release gates. See `docs/adr/0005-test-isolation-and-bounded-parallelism.md` and `docs/TEST_STRATEGY.md`.

## Useful starting prompts

- "Implement P0 from `docs/plans/20261008-002-phase-0-foundation.md`; create solution skeleton, DI and build verification only."
- "Implement P1 from `docs/DEVELOPMENT_PLAN.md` and `docs/PERSISTENCE.md`, including SQL integration tests."
- "Implement P5.1 master data after P0–P4 quality gates; follow `docs/plans/20261008-001-sales-inventory-foundation.md`."
- "For future feature PRs, follow `docs/CODEX_TEST_EXECUTION_POLICY.md`: focused L1/L2 checks, then L3 evidence from the final gate-affecting revision. Do not restart completed SQLPERF-T01–T06 without a separately approved task."
- "Implement P4.1 shared WinForms Login/session in `docs/plans/20261013-002-desktop-login-session.md` after P3.1, then complete authenticated P5/P6 release gates."

Do not automatically implement all phases in a single unreviewable change.
