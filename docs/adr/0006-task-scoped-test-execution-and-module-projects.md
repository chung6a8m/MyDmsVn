# ADR 0006 — Task-scoped test execution and module-scoped test projects

Status: **Proposed** · Date: 2026-10-16

## Context

MyDmsVn currently has progressive L1/L2/L3/L4 test guidance, but SQL integration cases remain concentrated in `MyDmsVn.Server.Infrastructure.IntegrationTests`. The accepted SQLPERF-T06 result reports ~506.6 s (`net48`) and ~498.6 s (`net8.0`) median per unfiltered pass. Its strict benchmark runner performs a warm-up and three measured passes on **each** TFM, although ordinary feature acceptance requires correctness, not performance benchmarking. The existing focused runner selects one SQL responsibility category at a time, not a module/task gate.

These facts create a risk that AI development repeatedly invokes expensive, unrelated SQL suites during a single feature implementation. Source-level categories and xUnit serialization are useful but do not themselves establish a task execution budget.

## Proposed decision (requires review/acceptance)

1. **Gate hierarchy:** Use L1 focused inner-loop, L2 explicit task gates, L3 one *planned* final acceptance invocation after the last gate-affecting revision, and L4 exceptional manual/performance/stress checks. Never schedule an unfiltered full SQL suite or solution-wide regression after each task. A failed L3 gate or subsequent gate-affecting change invalidates the relevant evidence and necessitates a repair and re-run; never re-use stale evidence to enforce an artificial exactly-once limit.
2. **Executable task contract:** Every new implementation plan declares affected projects/TFMs, named test targets, SQL module/category and required adversarial scenarios, prerequisites, gate commands (only if tooling exists), timeout/cleanup expectations, and pass/fail/blocked evidence for each task. New test cases must enter the proper gate inventory; newly affected cross-module dependencies expand the gate and must be documented.
3. **Separate responsibilities:** A `TestProject` defines build/discovery/execution ownership; a `Module` expresses a business boundary (Security, Catalog, Inventory, Migration, etc.); a `Category` describes the SQL behavior (`FastSQL`, `MigrationSchema`, `SecurityProvisioning`, `SqlConcurrency`). Categories do not grant parallelism and modules do not replace cross-module regression.
4. **Single-pass strict PR acceptance:** Add a dedicated functional PR mode that executes the **complete unfiltered current-commit SQL inventory once per TFM**, sequentially. Reuse the existing independent discovery/theory-case and classification checks, zero-mandatory-skip enforcement, operator-approved SQL identity and `master` preflight, bounded process-tree watchdog, durable ownership journal, live reconciliation/quarantine and sanitized TRX/report evidence. Keep benchmark warm-up/repeated sampling under a separate explicitly requested L4 mode. The proposed mode is **not implemented** by this ADR.
5. **Modular test projects:** Incrementally extract pure tests into non-SQL projects; partition SQL tests into module-focused assemblies and maintain cross-module integration/upgrade/concurrency regression where needed. Provide a single source-controlled, versioned **logical-case identity** inventory across all projects to prevent lost/duplicated cases, stale exemptions, hidden theory rows or missing TFMs. Define identity independently of project/assembly and VSTest/TRX-generated GUID (TFM + normalized method signature + stable Fact/Theory row key + reviewed classification), with old → new project mappings and explicit reviewed logical-key changes. Use one serialized SQL process/TFM at a time on the current approved capacity-constrained instance until safe cross-process coordination and measured capacity are proven.
6. **No safety tradeoffs:** Preserve ADR 0005's per-test disposable database isolation, exclusive GUI-interacting STA tests, resource-based concurrency policy, SQL migration/history coverage and current release assertions. A `dotnet test` exit code or category-filtered pass alone is never an L3 SQL acceptance certificate.
7. **Local-only rollout:** TESTGATE-T01–T05 use local Codex/developer runs for task and final acceptance. Do not create/modify CI test workflows or make CI repeat the full SQL suite already run locally. CI adoption is deferred until separately requested and reviewed.

## Rollout boundary

This ADR records a **proposed target**, not evidence that a new gate runner, single-pass SQL mode, split assemblies or module selectors exist. Until an implementation is reviewed and verified, existing scripts and the existing strict multi-pass acceptance protocol remain the only available accepted SQL paths. Documentation-only changes do not retroactively invalidate previously accepted functional evidence.

Implement in separately reviewable stages: [TESTGATE-T01–T03 execution policy, strict one-pass gate and orchestration](../plans/20261016-001-task-gates-and-modular-tests.md) and [TESTGATE-T04/T05 modular Desktop and SQL test projects](../plans/20261016-002-modular-test-projects.md). Do not edit production persistence, grant more SQL privileges, enable SQL parallelism, or move tests en masse in this policy PR.

## Alternatives considered

- **Run solution-wide/full SQL after each task:** rejected for developer iteration due to repeated unrelated SQL cost.
- **Run filtered tests only for PR acceptance:** rejected because tests/migrations outside filters could regress.
- **Replace strict SQL runner with plain `dotnet test`:** rejected because it cannot prove inventory/discovery, skip, watchdog and ownership gates.
- **Use the benchmark's repeated runs as a functional PR requirement:** rejected as a target architecture; retain repetition for performance experiments and stress mandates.
- **One shared migrated SQL database for every test:** rejected under SQLPERF-T04's current isolation evidence.
- **Immediately parallelize split SQL projects:** rejected pending independent process/instance resource orchestration and capacity evidence.

## Consequences and verification

- Normal feature tasks pay only task-scoped test cost; final correctness still covers the complete mandatory matrix.
- Stale-final-revision invalidation remains mandatory even if it requires another full run.
- Local Codex gate selection must be independently verified against source/discovery/theory-row inventory rather than trusting only the selected filtered results. Publish only a reviewed sanitized evidence summary; retain raw TRX, timing and ownership/approval journals locally. No CI rollout or duplicate CI full-suite gate is included.
- Migration to new assemblies must prove one-to-one **logical-case-key** source/discovery/TRX reconciliation independently on both SQL TFMs and explicit old → new mappings, not raw TRX IDs or count-only parity. Reject unstable row keys, unreviewed renames/categories, lost or duplicated theory rows; preserve all required coverage, failed/aborted-run reconciliation and no SQL/GUI concurrency cross-process regression. T03 defines the shared identity contract in the [core TESTGATE plan](../plans/20261016-001-task-gates-and-modular-tests.md); migration-specific task gates and evidence are in the [modularization plan](../plans/20261016-002-modular-test-projects.md).
- Do not claim a performance gain from project partitioning without separately measured evidence; cost measurement is not a mandatory implementation gate.

## Related decisions

[ADR 0005](0005-test-isolation-and-bounded-parallelism.md) continues to govern **isolation/parallel safety**. ADR 0006 would govern **when and what to execute**. If ADR 0006 is accepted, adjust its status and local execution policy consistently. CI remains out of scope until explicitly requested; do not rewrite the established performance benchmark results.
