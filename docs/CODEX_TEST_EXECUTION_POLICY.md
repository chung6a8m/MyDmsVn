# Codex Test Execution Policy — progressive verification

**Status:** Repository procedure (2026-10-15). Applies to future implementation tasks and pull requests for MyDmsVn, subject to more stringent requirements in the specific plan. Read with [TEST_STRATEGY.md](TEST_STRATEGY.md), [ADR 0005](adr/0005-test-isolation-and-bounded-parallelism.md) and [tests/README.md](../tests/README.md).

**Ordered roadmap/TFM note (2026-10-16):** [TESTGATE-T01–T03](plans/20261016-001-task-gates-and-modular-tests.md) is verified context. Next is the proposed [NET8-PAUSE](plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md), then T04/T05 modularization, SEC-REBUILD, remaining P5/P6 and P7. **Not implemented yet:** on `main` and all unrelated PRs, existing dual-TFM Release/SQL/WinForms checks remain mandatory until NET8-PAUSE passes and merges. The **NET8-PAUSE implementation PR alone** has an owner-approved, **T01-audited candidate-only Stage B exception**: after T02 removes the second TFM and T03 validates the parameterized strict runner/manifest, test its frozen candidate revision using the full reviewed **active `net48` Final + bounded Desktop GUI D2 and Release/tool gates**, never an impossible removed net8 target. Preserve net8 logical cases as historical `PARKED / NOT RUN`, not PASS. After merge apply the active TFM list to later PRs; restoration requires full dual-target validation. No mandatory active gate is waived.

## Intent and hard boundaries

Choose **the smallest correct set of tests during development**, then provide **complete evidence at the required PR/release gate**. Test responsibility traits (such as `FastSQL`) select relevant checks; they do not authorize test parallelization or shared SQL fixtures. Keep the existing SQL and WinForms assembly serial guards, approved non-production SQL instances, positive database ownership proof, and the **currently approved active-target/release acceptance** rules; the narrow NET8-PAUSE Stage B transition is the only pre-merge target-scope exception. Do not suppress, skip, mock, or filter mandatory regression cases merely to reduce duration.

Before executing a test command, identify the change surface, affected projects/TFMs, needed infrastructure, applicable plan gates and the next test stage. Prefer `--no-build` or `--no-restore` only after a successful corresponding build/restore **for that exact revision, configuration and target**. A failed test should lead to a focused fix/re-run. **L3 evidence must match the final gate-affecting revision**, not merely the last application-behavior edit. After changes to source code, tests/theory rows, SQL scripts/migrations, independent test inventory/classification manifests, harness/runner, or build/CI configuration that can affect scope, discovery, execution, or results, rerun the applicable complete L3 gates (including strict unfiltered dual-TFM SQL when required). Record the verified commit SHA and fresh discovery/TRX/ownership evidence; do not reuse results from before these changes. The static-only exception below applies to strictly documentation-only PRs; policy/acceptance changes in a code-bearing PR still require checking whether additional gates were introduced.

## Mandatory task execution budget

**Do not run whole-solution or unfiltered SQL suites automatically while editing or after individual tasks.** Declare affected modules and cross-module dependencies, exact test projects/TFMs, reviewed selectors, SQL category (only if needed), task-specific real SQL/race scenarios, SQL approvals, timeout/cleanup and pass/fail/skip/blocked evidence **before** each L2 task gate. Use the [current gate matrix](TEST_GATE_MATRIX.md) and include such a table in every new implementation plan.

After all focused L2 gates are green and all gate-affecting edits are frozen, schedule **one planned final L3 acceptance envelope** with complete active project/TFM regression and strict unfiltered SQL (dual-TFM on `main`/unrelated PRs before merge; **active `net48` only on authorized NET8-PAUSE candidate Stage B** after the reviewed transition). If the gate fails or gate-affecting files later change, its evidence becomes stale and the relevant complete L3 checks MUST be rerun after focused repairs. An absolute single-execution cap must never weaken acceptance.

**Current rollout is local-only:** For TESTGATE-T01–T05, perform L1/L2 and mandatory final L3 on the approved local developer/Codex environment. Publish only a separately prepared, reviewed, sanitized evidence summary in the PR; keep raw test artifacts local. Do not add CI triggers, CI test-gate enforcement or a second CI full SQL suite. Existing unrelated CI configuration is outside this plan; any CI rollout must be separately requested. Local L3 is still mandatory for code-bearing PRs and cannot be replaced by focused tests.

**Implemented-tool boundary:** `Invoke-TestGate.ps1` now reads `tests/test-gates.v1.json`. `Task` executes only the exact manifest-owned project/TFM/selection; `Final` requires a clean exact revision plus approved SQL and reuses the strict single-pass `pr-acceptance` path on `net48` then `net8.0`. Exit `0` means complete, `2` means resource/preflight denial, `3` means a valid but incomplete gate, and `64` means invalid CLI usage. Raw TRX, timing, ownership and lease data stay local; only the sanitized aggregate report is reviewable. The separate `baseline` remains one warm-up plus three measured passes per TFM, and no module+category selector or T04/T05 split project exists. Never fabricate broader commands or downgrade L3 to plain `dotnet test`.

## Evidence publication boundary (mandatory)

- **Local verification:** retain original TRX, timing JSONL, approval/ownership journals, discovery files and other runner artifacts in the approved local environment for audit/reconciliation. Do not attach raw artifact directories, ZIP archives, raw TRX or unreviewed generated reports to a PR, issue, CI log or public link. Treat review/approval identifiers, ownership tokens, live SQL fingerprints, database/host identifiers, local paths, credentials and theory arguments as potentially sensitive even if they are not passwords.
- **PR/shareable evidence:** create a separate human-reviewed sanitized Test Execution Report containing the tested commit, project/TFM, selected gate, independently reconciled method/theory-case totals, passed/failed/skipped/blocked counts, watchdog outcome, approved-instance preflight outcome (without identifying the instance), ownership/disposal/reconciliation result and any outstanding gate. Include only intentionally sanitized evidence references or non-sensitive checksums if useful; a checksum does not make the underlying artifact publishable. Remove absolute paths and raw test values.
- **Review before publication:** inspect the *actual* exported text and any attachment for secrets, machine/database/approval identifiers, raw journals, theory values and local paths. Do not assume that a runner output or a ZIP labeled `shareable` was automatically sanitized. If a safe exporter is not available, prepare a manual aggregate summary and keep the source files local. Report evidence that could not be safely shared as locally retained, not as publicly attached or independently reproduced.

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
- For strict **unfiltered** SQL acceptance, use `tests/tools/Invoke-SqlPrAcceptance.ps1` from a clean committed revision. It runs exactly one full pass on `net48`, then one on `net8.0`, and fails closed unless classification, discovery/theory rows, TRX, mandatory zero skips, process exit, timeout, approval, timing and ownership reconciliation all complete. It writes sanitized aggregate JSON per TFM while raw artifacts remain local. `tests/tools/Invoke-SqlIntegrationBenchmark.ps1` remains the multi-pass performance path (one warm-up plus three measured passes per TFM). A two-revision baseline-vs-candidate **performance comparison** belongs only to L4 when required; it is **not** an ordinary functional PR obligation.
- Both SQL environment variables are mandatory before any SQL DDL: `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` (effective database `master`) and `MYDMSVN_SQL_TEST_APPROVAL_FILE` (operator-maintained, instance-bound approval). A missing/expired approval, unavailable SQL or blocked DMV check is **blocked**, not an SQL success. Never create/reuse/drop production or unknown databases, print secrets, bypass ownership evidence or interpret a passed process exit code alone as the SQL gate.
- Neither xUnit collection serialization nor the current SQL/WinForms assembly guards coordinate separate processes/TFMs. Do not run multiple SQL full suites against the same constrained host simultaneously without a separately proven orchestration policy. The accepted SQLPERF benchmark sequence remains sequential.

The manifest runner implements exact task/final orchestration and durable logical-case reconciliation for the current projects. Module-specific split projects and a module+category selector remain T04/T05 work.

## PR Test Execution Report (required)

Include this concise section in each implementation PR, adjusting rows to the actual scope:

```markdown
### Test Execution Report

- Change / risk surface: ...
- L1 focused checks: <commands, project/TFM, pass/fail/skip, or not run>
- L2 task checks: <commands, project/TFM, pass/fail/skip, or not run>
- L3 final Release/SQL/Desktop gate: <commands, exact revision, pass/fail/skip, or BLOCKED with reason>
- SQL acceptance (if required): <unfiltered each TFM; inventory/discovery/TRX, watchdog, approval and ownership status; aggregate sanitized evidence only; raw files local>
- L4 plan-specific gates: <executed results, not applicable, or blocked>
- Public evidence review: <sanitized report reviewed; raw TRX/journals/timings/approval material retained locally; no raw ZIP attached>
- Open risks / remaining manual or environment-dependent work: ...
- Readiness: READY only when every mandatory gate has evidence; otherwise NOT VERIFIED / BLOCKED.
```

Report actual commands and counts, without disclosing approval contents, instance/host identity, credentials, raw journals, theory values or paths that reveal sensitive data. Do not mark a plan checkbox `[x]`, approve a release, or describe a PR as fully verified while a mandatory case or platform gate remains skipped, missing, timed out or unexecuted.

## Example: P4.1 Desktop Login/session

During Login ViewModel changes, run small pure session/validation tests (L1). At task completion add Desktop STA and relevant SecurityProvisioning/real-SQL identity tests on affected TFMs (L2). After the **final gate-affecting change**, run full Release regression, unfiltered SQL on `net48`/`net8.0` and required dual-runtime Desktop tests (L3). Packaged interactive sign-in/logout/expiry walkthroughs and any explicitly prescribed race/failure repetition are distinct plan gates (L4). No step may bypass authorization by injecting a test Login into production.
