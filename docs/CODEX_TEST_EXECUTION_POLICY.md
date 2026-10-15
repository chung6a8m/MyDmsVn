# Codex Test Execution Policy — progressive verification

**Status:** Repository procedure (2026-10-15). Applies to future implementation tasks and pull requests for MyDmsVn, subject to more stringent requirements in the specific plan. Read with [TEST_STRATEGY.md](TEST_STRATEGY.md), [ADR 0005](adr/0005-test-isolation-and-bounded-parallelism.md) and [tests/README.md](../tests/README.md).

## Intent and hard boundaries

Choose **the smallest correct set of tests during development**, then provide **complete evidence at the required PR/release gate**. Test responsibility traits (such as `FastSQL`) select relevant checks; they do not authorize test parallelization or shared SQL fixtures. Keep the existing SQL and WinForms assembly serial guards, approved non-production SQL instances, positive database ownership proof, and existing dual-target/release acceptance rules. Do not suppress, skip, mock, or filter mandatory regression cases merely to reduce duration.

Before executing a test command, identify the change surface, affected projects/TFMs, needed infrastructure, applicable plan gates and the next test stage. Prefer `--no-build` or `--no-restore` only after a successful corresponding build/restore **for that exact revision, configuration and target**. A failed test should lead to a focused fix/re-run. **L3 evidence must match the final gate-affecting revision**, not merely the last application-behavior edit. After changes to source code, tests/theory rows, SQL scripts/migrations, independent test inventory/classification manifests, harness/runner, or build/CI configuration that can affect scope, discovery, execution, or results, rerun the applicable complete L3 gates (including strict unfiltered dual-TFM SQL when required). Record the verified commit SHA and fresh discovery/TRX/ownership evidence; do not reuse results from before these changes. The static-only exception below applies to strictly documentation-only PRs; policy/acceptance changes in a code-bearing PR still require checking whether additional gates were introduced.

## Mandatory task execution budget

**Do not run whole-solution or unfiltered SQL suites automatically while editing or after individual tasks.** Declare affected modules and cross-module dependencies, exact test projects/TFMs, reviewed selectors, SQL category (only if needed), task-specific real SQL/race scenarios, SQL approvals, timeout/cleanup and pass/fail/skip/blocked evidence **before** each L2 task gate. Use the [current gate matrix](TEST_GATE_MATRIX.md) and include such a table in every new implementation plan.

After all focused L2 gates are green and all gate-affecting edits are frozen, schedule **one planned final L3 invocation** with the complete mandatory regression and strict unfiltered SQL on both TFMs. If the gate fails or gate-affecting files later change, its evidence becomes stale and the relevant complete L3 checks MUST be rerun after focused repairs. An absolute single-execution cap must never weaken acceptance.

**Implemented-tool boundary:** Today's strict SQL runner performs one warm-up and three measured passes **per TFM**; it is NOT a strict single-pass PR acceptance command. The proposed `Invoke-TestGate.ps1`, module-scoped project runner and strict `pr-acceptance` mode do NOT exist. Never fabricate those flags; use existing strict tooling or report L3 blocked. L4 benchmark repetition is not routine feature validation. See [TESTGATE plan](plans/20261016-001-task-gates-and-modular-tests.md) and [proposed ADR 0006](adr/0006-task-scoped-test-execution-and-module-projects.md), which do not replace ADR 0005 isolation policy.

## Four levels

| Level / trigger | Minimum expectation | What does **not** suffice |
|---|---|---|
| **L1 — Inner loop**: while editing/fixing | Run the changed test(s)/class and the nearest pure unit/contract/VM checks for the affected behavior. Use no SQL when the assertion does not require it. If real SQL is needed, run an approved, bounded SQL category through the existing focused runner. | A green unrelated unit test; ad-hoc SQL against an unapproved instance; repeated full SQL suites after every local edit |
| **L2 — Task validation**: before declaring a task/vertical slice done | Build current affected projects/TFMs and run affected test projects, including SQL category/GUI STA tests and adversarial scenarios explicitly required by the task. Cross-layer security, migration, transaction and concurrency changes require their relevant real-SQL categories, not just pure unit tests. | Replacing an explicit task gate with the fastest category; assuming a `FastSQL` trait proves isolation; treating a missing Windows/SQL prerequisite as pass |
| **L3 — PR acceptance**: after the final gate-affecting change, before claiming ready to merge | Restore/build Release for all supported/affected targets; run the applicable full automated regression suites. For **code-bearing PRs**, include full **unfiltered** SQL integration suites sequentially on `net48` and `net8.0` with strict independent discovery/theory-row inventory, zero mandatory skips, TRX, watchdog and approved ownership reconciliation. Run the Desktop/architecture/application checks applicable to the change, including both Desktop TFMs where required. Document any genuine manual-only GUI acceptance separately. | Focused-only SQL success, direct solution `dotnet test` alone as SQL acceptance evidence, stale pre-change test results, or an unsupported environment reported as passing |
| **L4 — Specialized gates**: plan-mandated release, SQLPERF, security, migration or concurrency verification | Execute only the explicitly required extra stress repetitions, migration upgrade/recovery, install/packaged GUI smoke, dual-revision A/B benchmarking, manual walkthroughs or production-readiness checks with their own evidence. Keep SQLPERF TFM passes sequential. | Automatically running performance warm-ups/three measured passes or destructive recovery drills after every ordinary edit without a plan trigger |

**Exception:** A strictly documentation-only PR with no source, test harness, build, CI or execution-behavior modification may use diff/links/cross-reference/static checks in place of runtime suites. State the reason and list SQL/GUI tests as **not run**, not passed. Changes to test policy or acceptance documentation require checking for conflicts with `AGENTS.md`, `TEST_STRATEGY.md`, accepted ADRs and active plans. A code-bearing PR with a required L3 gate unavailable is **blocked/not verified** and its task/merge acceptance checkboxes remain open; never weaken the gate to get a green report.

## Selection matrix for development

| Change surface | Start with | Add at task/PR boundary |
|---|---|---|
| Pure Domain/Application rules, validations, contracts | Targeted non-SQL unit/contract tests | Affected test projects/TFMs, architecture and full PR regression |
| RepoDb/Dapper mappings, queries, CRUD or UnitOfWork | Direct behavior test plus relevant approved `FastSQL` category | All affected SQL/security/concurrency categories where required; unfiltered dual-TFM SQL gate at L3 |
| DbUp migrations, schema compatibility, permission synchronization, identity/session authorization | Targeted pure checks and appropriate `MigrationSchema` / `SecurityProvisioning` categories | Real SQL failure/upgrade/revocation tests dictated by the plan; full dual-TFM SQL at L3 |
| Lock ordering, stock posting/idempotency, transaction contention | Related focused tests and `SqlConcurrency` category with bounded sessions | Task-required adversarial/repeat probes, full dual-TFM SQL at L3; separate stress/benchmark at L4 only if specified |
| WinForms Login/controls, ViewModels and navigation | Deterministic ViewModel tests, isolated STA UI checks on supported Windows target(s) | Affected Desktop test projects on `net48`/`net8.0-windows`, any real SQL identity tests, full PR regression and plan-specific packaged/manual smoke |
| Docs only | Markdown diff, references, active-plan/ADR consistency | PR Test Execution Report showing runtime suites not run, with explanation |

## Use only tooling that exists

- Non-SQL targeted `dotnet test` filters and affected-project runs may be used for L1/L2. Match the project/TFM to the code under test; do not invent a blanket parallel switch. Protect GUI input with the existing Desktop assembly guard.
- For **SQL category** L1/L2 runs, use `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` (parameters and working command are in [tests/README.md](../tests/README.md)). It accepts `-Category` = `FastSQL`, `MigrationSchema`, `SecurityProvisioning` or `SqlConcurrency`, and `-Frameworks` = `net48`/`net8.0` (both by default). It verifies approval, selection and bounded owned-process-tree execution; still never counts as a full L3 gate. Use a fresh artifact directory for each invocation.
- For strict **unfiltered** SQL acceptance, the currently implemented `tests/tools/Invoke-SqlIntegrationBenchmark.ps1` calls the `baseline` mode: **one warm-up plus three measured full-suite passes per TFM**. This is an existing strict evidence path, but expensive. There is **no implemented one-pass strict PR gate**, so do not claim a filtered, bare `dotnet test` or a nonexistent `--single-pass` mode is equivalent. Until a separately reviewed single-pass strict acceptance runner exists, use the existing strict path when the gate is required, or accurately report L3 as blocked. A two-revision baseline-vs-candidate **performance comparison** belongs only to L4 when required; it is **not** an ordinary functional PR obligation.
- Both SQL environment variables are mandatory before any SQL DDL: `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` (effective database `master`) and `MYDMSVN_SQL_TEST_APPROVAL_FILE` (operator-maintained, instance-bound approval). A missing/expired approval, unavailable SQL or blocked DMV check is **blocked**, not an SQL success. Never create/reuse/drop production or unknown databases, print secrets, bypass ownership evidence or interpret a passed process exit code alone as the SQL gate.
- Neither xUnit collection serialization nor the current SQL/WinForms assembly guards coordinate separate processes/TFMs. Do not run multiple SQL full suites against the same constrained host simultaneously without a separately proven orchestration policy. The accepted SQLPERF benchmark sequence remains sequential.

A separate change may later add a **single-pass strict PR-validation mode** reusing the current discovery, TRX, approval, watchdog and ownership gates. This policy does not implement or assume that mode.

## PR Test Execution Report (required)

Include this concise section in each implementation PR, adjusting rows to the actual scope:

```markdown
### Test Execution Report

- Change / risk surface: ...
- L1 focused checks: <commands, project/TFM, pass/fail/skip, or not run>
- L2 task checks: <commands, project/TFM, pass/fail/skip, or not run>
- L3 final Release/SQL/Desktop gate: <commands, exact revision, pass/fail/skip, or BLOCKED with reason>
- SQL acceptance (if required): <unfiltered each TFM; inventory/discovery/TRX, watchdog, approval and ownership status; sanitized evidence references>
- L4 plan-specific gates: <executed results, not applicable, or blocked>
- Open risks / remaining manual or environment-dependent work: ...
- Readiness: READY only when every mandatory gate has evidence; otherwise NOT VERIFIED / BLOCKED.
```

Report actual commands and counts, without disclosing approval contents, instance/host identity, credentials, raw journals, theory values or paths that reveal sensitive data. Do not mark a plan checkbox `[x]`, approve a release, or describe a PR as fully verified while a mandatory case or platform gate remains skipped, missing, timed out or unexecuted.

## Example: P4.1 Desktop Login/session

During Login ViewModel changes, run small pure session/validation tests (L1). At task completion add Desktop STA and relevant SecurityProvisioning/real-SQL identity tests on affected TFMs (L2). After the **final gate-affecting change**, run full Release regression, unfiltered SQL on `net48`/`net8.0` and required dual-runtime Desktop tests (L3). Packaged interactive sign-in/logout/expiry walkthroughs and any explicitly prescribed race/failure repetition are distinct plan gates (L4). No step may bypass authorization by injecting a test Login into production.
