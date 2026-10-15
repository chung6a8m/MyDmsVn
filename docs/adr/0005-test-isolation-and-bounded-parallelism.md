# ADR 0005 — Test resource isolation and bounded parallelism

Status: **Accepted** · Date: 2026-10-15

## Context

At the MVP stage the SQL integration assembly uses `[assembly: CollectionBehavior(DisableTestParallelization = true)]` to avoid SQL overload and command timeouts when many disposable databases and migrations are started at once. `Desktop.Tests` separately disables assembly parallelization to protect WinForms/STA/UI tests from competing global interface state. SQLPERF-T04/T05 investigates migrated fixture reuse and bounded parallel execution; the current serial fallback must be preserved until those experiments meet their safety gates.

The user clarified that a preference for sequential tests refers specifically to Desktop tests involving real GUI state, including mouse/keyboard simulation, focus and STA controls. It is not a perpetual policy for every SQL integration test.

## Decision

1. Determine parallel eligibility from **resource ownership, isolation and actual test behavior**, not from the project name, traits or test duration. Test category/classification and execution/isolation policy are independent dimensions.
2. **SQL:** Treat assembly-level `DisableTestParallelization` as a transitional safety measure, not the target architecture. The future target is **bounded parallelism between independently isolated xUnit collections**, with fresh/dedicated databases or safe migrated-fixture boundaries as appropriate. Shared mutable fixtures are serialized within their ownership boundary; no unsafe parallel writes, shared ambient UoW or dirty-state reuse. Schema/migration, security provisioning and multi-session contention cases retain the isolation/exclusivity their behavior needs.
3. **Desktop:** Keep GUI-interacting WinForms tests serialized, including tests that create controls, rely on message-loop/focus/window state or drive mouse and keyboard interactions. STA alone does not make global UI side effects parallel-safe. The current Desktop assembly guard remains until a proven partition is introduced. Pure non-UI ViewModel tests may be separated into parallel-compatible projects/collections after evidence.
4. **Pure unit/architecture/contract tests:** Keep normal xUnit collection parallelism when no shared mutable resource is involved. Do not introduce a blanket serial policy for new test assemblies.
5. Parallelism **within a single test** (for example, a SQL locking/concurrency assertion using independent connections) is distinct from **runner-level test parallelism**. xUnit collection coordination applies inside one test assembly/process; shared SQL, test database lifecycle and GUI resources across processes or frameworks require separate runner/CI control.
6. Preserve existing SQLPERF safety gates. The accepted benchmark protocol runs `net48` and `net8.0` full suites sequentially for comparable A/B evidence. Do not enable SQL assembly parallelization as a quick performance optimization; require a bounded capacity experiment, adversarial cleanup/isolation evidence, dual-TFM verification and an operable serial fallback.

## Alternatives considered

- **Permanently serialize every test assembly:** rejected; imposes avoidable latency on isolated SQL and pure tests without improving their correctness.
- **Remove SQL assembly guard immediately:** rejected; fixture ownership, SQL capacity and failure cleanup must be demonstrated before doing so.
- **Turn off all Desktop serialization once STA exists:** rejected; simulated input, focus, dialogs and process-global UI behavior can interfere across STA threads.

## Consequences / tradeoffs

Parallel SQL execution is a **future capability requiring proof**, not a completed part of T04/T05 and not permission to weaken release gates. Isolation per database can cost more resources; migrated fixtures reduce setup but make ownership/reset correctness more demanding. Conservative exclusive collections may still be required. The policy does not mandate a specific concurrency limit, new framework, CI topology or unimplemented feature flag.

## Required tests / evidence for future implementation

- Explicit per-test/collection mapping of database/fixture ownership, reset strategy and resources affected across testhosts; ensure any shared mutable fixture is not accessed concurrently.
- Repeated overlapping execution under a documented bounded SQL load on both supported TFMs, including contention, failure injection, canceled/abandoned tasks, leaks, reset failure and teardown/recovery; record orphaned database/session checks.
- Independently reconciled SQL test inventory (including theory rows), full unfiltered suites with zero skipped mandatory cases, approved non-production SQL instance/ownership evidence and a tested serial rollback path.
- Repeatable baseline/candidate performance measurements, separating SQL test-level parallelism from the benchmark runner's sequential cross-TFM protocol.
- GUI-focused Desktop isolation tests before extracting any serial test group, including window/input ownership, no unexpected modal dialogs, cleanup and repeat-run flake assessment.

## Affected documents

`AGENTS.md`, `docs/TEST_STRATEGY.md`, `tests/README.md`, `docs/plans/20261014-001-sql-integration-test-performance-optimization.md`, and `docs/adr/README.md`.

This ADR documents the architectural direction and does **not** change runtime/test code or assert that current SQL assembly parallelization is safe.
