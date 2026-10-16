# Codex instructions — MyDmsVn

Applies to the entire repository. This file is authoritative for AI-assisted changes.

## Mission and status

Develop MyDmsVn as a Windows desktop-first modular-monolith SME platform, starting with Sales & Inventory Foundation. **P0–P4 foundations and P5 implementation/automated gates exist; P5 authenticated interactive acceptance and P6 release gates remain open.** Consult `docs/DEVELOPMENT_PLAN.md` for the current evidence-based status. **P3.1 is historically verified; P4.1 is owner-accepted complete on 2026-10-16 (automated L3 + packaged manual UI evidence, with an explicit unobserved idle-expiry exception); remaining P5 GUI/P6 release gates are not waived**, despite historical P3/P4 completion. Implement in small, reviewable phase-scoped changes and do not claim later-phase capabilities exist.

## Read-order and precedence

1. This `AGENTS.md`.
2. `docs/PRD.md`, `docs/ARCHITECTURE.md`, `docs/TECH_STACK.md`.
3. `docs/DEVELOPMENT_PLAN.md`, [active plans](docs/plans/README.md) **in the recorded order**, and the specific active task; [archive](docs/plans/archive/README.md) is historical, not an execution queue.
4. Applicable `docs/CONTRACTS.md`, `docs/PERSISTENCE.md`, `docs/SECURITY.md`, `docs/DATA_MODEL.md` and `docs/TEST_STRATEGY.md`.
5. Accepted decisions in `docs/adr/`.
6. `project-idea.md` is the raw idea; its examples may be incorrect and are **not implementation instructions**.

If documents contradict each other, flag the issue in the change summary; do not silently substitute a different architecture. Explicit user decisions override prior plans.

## Fixed technical decisions

- **This NET8-PAUSE candidate checkout actively targets `net48` for application, SQL and Desktop projects.** Historical `net8.0` / `net8.0-windows` logical-case metadata is retained as `PARKED / NOT RUN`; `MyDmsVn.Desktop.AppCore` is parked outside the solution. The SQL runner and runner tests remain tool-only `net8.0` and do not count as application/SQL coverage. `main` and unrelated PRs keep their supported matrix until this migration is accepted and merged. See the [temporary .NET 8 pause plan](docs/plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md), ADR 0008 and [reactivation procedure](docs/REACTIVATE_NET8.md). No .NET 10 upgrade.
- RepoDb `1.16.0`; RepoDb.SqlServer `1.16.1`; RepoDb.SqlServer.BulkOperations `1.16.2`.
- Explicit UnitOfWork only: scope owns a single open connection and optional transaction; repositories and Dapper queries within that scope share them explicitly. No ambient/static AsyncLocal transaction.
- RepoDb for command-side storage, Dapper for query-side DTO projection; DbUp for versioned SQL migrations.
- Server.Application / Domain must not know HTTP, WinForms or database implementation.
- Desktop UI / ViewModels depend only on desktop abstractions and Contracts, never directly on Server.Infrastructure.
- v1 Local uses in-process Application dispatch; v2 HTTP uses the **same DTO/API result semantics**.
- Preserve legacy `PasswordHash` and `PasswordSalt` columns for migration; no new plaintext or reversible password storage.
- Authorization is checked on server-side application use cases; UI gating alone is insufficient. UserPermission explicit deny/grant overrides roles; default deny.
- New features must declare/register permission keys centrally and validate new-key lifecycle in tests. Provision/synchronize permissions under reviewed deployment/administrator privilege only; never seed users/roles or run synchronization from desktop Login/startup. Historical source: `docs/plans/archive/20261013-001-initial-security-provisioning-permission-sync.md`. The active [SEC-REBUILD plan](docs/plans/20261016-003-permission-system-rebuild.md) proposes replacing this implementation **after** the test-gate prerequisite.
- Do not add sales order, issue, returns, pricing, accounting or multi-warehouse receipt to P5 unless separately approved.

## Coding conventions

- Follow the **existing** `.editorconfig` and `.gitattributes`: C#/Markdown CRLF; UTF-8 with BOM where specified; Node/web assets LF if introduced.
- Prefer readable C# that compiles for **all current declared targets**, preserving restore-to-net8 compatibility when applicable. Do not use runtime APIs missing on `net48` simply because newer syntax parses.
- Nullable annotations and target-specific code must be handled with explicit project settings. No suppressing warnings project-wide merely to get a green build.
- Use Microsoft DI; register repository implementations explicitly (no arbitrary first-type assembly scanning).
- Services return typed results. Keep cross-process DTOs independent from `ErrorOr`, MediatR, RepoDb and WinForms.
- Centralize domain validation, authorization, logging and error mapping; avoid duplicated rules in forms.
- SQL scripts are additive, repeatable in clean environments and never drop user data implicitly.
- No secrets, real credentials or production connection strings in commits.

## Work protocol

1. Inspect the repo and [ordered active roadmap](docs/plans/README.md); do the first unimplemented prerequisite in sequence. TESTGATE T01–T03 remains **verified context**, not a task to redo. The ordinary dual-TFM SQL gate applies on `main`/unrelated PRs until a verified NET8-PAUSE code PR merges. **Only the specifically approved NET8-PAUSE implementation PR** may use its plan's Stage B candidate-only active-`net48` L3 transition after T01 owner-reviewed baseline/scope authorization, T02 actual TFM change and T03 validated runner/manifest. All other PRs retain their currently declared supported TFMs.
2. State the scope and affected layers before implementation; avoid changing unrelated modules.
3. Make a minimal vertical change with automated tests.
4. Build each **actually declared mandatory project × TFM** on Windows with its targeting packs. In this Stage B candidate that means active application/test `net48` plus verified tool-only runner `net8.0`; parked application/GUI TFMs are `NOT RUN`, never PASS. Other branches follow their checked-in declarations.
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
- Schedule **one planned L3 acceptance envelope** after focused task gates are green and inputs freeze. Preserve unfiltered SQL for all **applicable reviewed project × TFM** targets (dual-TFM for `main`/unrelated PRs until merge; active `net48` for the **Stage B candidate NET8-PAUSE implementation PR only** after its T01–T03 transition gates). Preserve independent source/discovery/TRX, GUI watchdog, non-production approval, cross-process serialization and owned-database cleanup. A failed L3 gate or later relevant edit invalidates prior evidence: repair via focused tests, then rerun the affected complete mandatory gate. Never treat stale evidence as green to force exactly one execution.
- **Current tooling:** `Invoke-TestGate.ps1` reads `tests/test-gates.v1.json` and provides manifest-owned `Task`/`Final` modes; Final reuses the strict single-pass `pr-acceptance` path (one unfiltered sequential pass per SQL TFM), requires a clean committed revision and approved SQL resources, and emits only sanitized aggregate evidence. The focused SQL wrapper still selects one category rather than module+category. Do not invent broader selectors or downgrade L3 to plain `dotnet test`; use `baseline` only for plan-triggered measurements.
- **Local-only TESTGATE acceptance:** execute focused Task checks and final **full unfiltered SQL on all currently mandatory TFMs** in the approved local environment (normally dual-target; only NET8-PAUSE's authorized Stage B candidate uses active `net48`). Publish only separately reviewed sanitized aggregate results; keep raw TRX, timing and ownership/approval journals local. Do not add CI jobs or duplicate the full SQL suite there without an explicit separate request.
- Performance benchmarks/stress repetitions belong to plan-triggered L4, not ordinary feature L1/L2. [Proposed ADR 0006](docs/adr/0006-task-scoped-test-execution-and-module-projects.md) and the verified [TESTGATE T01–T03 plan](docs/plans/20261016-001-task-gates-and-modular-tests.md) define the current contract; [T04/T05 modularization](docs/plans/20261016-002-modular-test-projects.md) remains future work. Accepted ADR 0005 remains authoritative on isolation/parallelism.


- Before running tests for a change, choose the smallest relevant *non-SQL or SQL* test scope and record its rationale. Use progressive validation: focused inner-loop tests, affected-project/task tests, then final unfiltered PR acceptance. Do not rerun the entire SQL suite after each local fix.
- The authoritative level-by-level decision matrix, existing command capabilities, exceptions and evidence template are in `docs/CODEX_TEST_EXECUTION_POLICY.md`. Follow it alongside the existing isolation policy below and every explicit plan acceptance gate; a narrow inner-loop run never replaces a required final gate.
- For SQL-backed focused development runs, prefer the existing guarded `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` by a verified SQL responsibility category. Its category filter is **not** a full-suite acceptance result. Preserve fail-closed SQL instance approval, process watchdog, ownership reconciliation, theory-row inventory and zero mandatory SQL skips.
- For a code-bearing PR, perform final applicable Release tests on all **actually required TFMs** (normally both, but for the explicitly approved NET8-PAUSE **Stage B candidate only**, the post-transition active `net48` matrix), including unfiltered SQL acceptance and separately guarded GUI tests, **for the final gate-affecting revision** (including source, tests/theory rows, SQL/migrations, inventories/manifests, harness/runner, and build/CI inputs), not just the last application-behavior edit. If these inputs change after a green run, rerun the applicable complete L3 gates. A documentation-only PR may use documented static checks instead; never claim an unexecuted SQL/GUI gate passed. Missing Windows, SQL approval or test resources means `blocked/not run`, not `passed`.
- The strict SQL runner exposes separate `pr-acceptance` (one unfiltered sequential pass per TFM) and multi-pass `baseline` (one warm-up and three measured passes per TFM) protocols. Do not treat bare `dotnet test` as equivalent to the strict discovery/TRX/ownership gate. Performance A/B comparisons and specialized stress tests remain conditional on the relevant plan.

### Test parallelization and resource isolation

- Decide parallel safety from **resource ownership and observable isolation**, not merely from the test-project name, SQL test trait, or test duration. Test classification (for example `FastSQL`, `MigrationSchema`, `SecurityProvisioning`, `SqlConcurrency`) is separate from execution/isolation policy.
- For SQL integration tests, the existing `[assembly: CollectionBehavior(DisableTestParallelization = true)]` is a **transitional safety guard**, not the long-term architectural default. Aim for **bounded xUnit collection-level parallelism** between independently owned databases/resources only after adversarial isolation, cleanup, capacity, repeatability and both-TFM gates pass. Do not remove the current guard merely to improve a benchmark; retain a proven serial fallback.
- A migrated database fixture may be reused only with explicit ownership, complete reset/teardown and no overlapping writes or unsafe shared state. Tests sharing mutable fixture state must be coordinated by the same collection or a separately proven equivalent mechanism. SQL migration/history, schema mutation, security provisioning and multi-session race tests require isolation appropriate to their behavior.
- Continue to serialize Desktop tests that create/use WinForms controls, interact with global UI state, drive real mouse/keyboard/focus or rely on shared message-loop/process state. STA execution by itself does **not** make simulated GUI input parallel-safe. The present `Desktop.Tests` assembly-wide guard stays in place; pure ViewModel tests may be separated into independent parallel-compatible projects/collections only after proof.
- Unit/architecture/contract tests without shared mutable resources should retain normal xUnit collection parallelism. A collection guard applies only within its test assembly/process; runner/CI coordination is required for shared SQL/UI resources across processes and target frameworks. Do not confuse deliberate multi-session concurrency **inside one test** with runner-level parallelism.
- Keep SQLPERF acceptance measurements for `net48` and `net8.0` sequential unless the measurement protocol is explicitly revalidated. Preserve approval, ownership journaling, mandatory SQL-case inventory, watchdog, zero-skipped required tests and release gates. See `docs/adr/0005-test-isolation-and-bounded-parallelism.md` and `docs/TEST_STRATEGY.md`.

## Useful starting prompts

- "Follow [the ordered active roadmap](docs/plans/README.md). NET8-PAUSE T01–T04 is implemented context in the candidate; complete its frozen T05 acceptance before modularization."
- "After NET8-PAUSE is actually implemented and accepted, implement [TESTGATE-T04/T05 modular test projects](docs/plans/20261016-002-modular-test-projects.md) using the actual target matrix."
- "After modularization, implement [SEC-REBUILD](docs/plans/20261016-003-permission-system-rebuild.md); then complete the [remaining P5 GUI/P6 operational gates](docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md), then P7."

Do not automatically implement all phases in a single unreviewable change.
