# TESTGATE — Task-scoped execution, strict single-pass PR gate, and modular test projects

**Status:** Proposed / not implemented · **Date:** 2026-10-16 · **Scope:** cross-phase test infrastructure and Codex workflow. This is an implementation plan, **not** evidence that the runner, project split, or CI features below exist or have passed. Read `AGENTS.md`, `docs/CODEX_TEST_EXECUTION_POLICY.md`, `docs/TEST_STRATEGY.md`, ADR 0005, proposed ADR 0006, `tests/README.md`, SQLPERF-T04/T05/T06 verification, and the affected phase plan.

## Problem and constraints

P4.1 work demonstrates the operational cost of indiscriminately rerunning SQL tests. On the verified SQLPERF-T06 candidate, an unfiltered median run was 506.604 s (`net48`) and 498.631 s (`net8.0`); the benchmark performs one warm-up plus three measured runs on **each** TFM. These measurements are not an independently established breakdown of the P4.1 session's elapsed time.

Current baseline:
- `tests/MyDmsVn.Server.Infrastructure.IntegrationTests` targets `net48;net8.0` and serializes the SQL assembly. `tests/MyDmsVn.Desktop.Tests` targets `net48;net8.0-windows` and serializes GUI-interacting tests.
- `Invoke-FocusedSqlIntegrationTests.ps1` supports one existing SQL category per invocation (`FastSQL`, `MigrationSchema`, `SecurityProvisioning`, `SqlConcurrency`); it is NOT the L3 acceptance runner.
- `Invoke-SqlIntegrationBenchmark.ps1` currently invokes the strict multi-pass `baseline` mode; there is NO existing `--single-pass` PR mode and NO `Invoke-TestGate.ps1`.
- SQLPERF-T04 rejected fixture reuse under the then-available safety evidence. Do not reopen that decision without new adversarial tests.
- P4.1's existing L3 evidence and remaining L4 activities must not be overwritten or falsely reported as having been rerun by this documentation-only plan.

Objectives: minimize repeated SQL work **during task implementation**; retain full correctness, security, migration, both-TFM coverage and real-SQL isolation **at merge**; make actual and missing gates observable.

## Gate definitions

| Level | When | Allowed scope | Forbidden substitution |
|---|---|---|---|
| L1 | On edit/fix | Affected test method/class, pure unit/contract/VM tests; approved focused real SQL only when necessary | Full unfiltered SQL/solution test as an automatic inner-loop action |
| L2 | Once per task after affected tests are green | Explicit affected project/TFMs + affected module/category + mandatory adversarial scenarios | Unrelated full SQL suite, or dropping a required real-SQL assertion |
| L3 | One planned final attempt after all gate-affecting edits and L2 gates are green | Full required Release/dual-TFM regression, including **strict unfiltered** real SQL | Filter-only SQL, stale revision, incomplete TRX or missing inventory/cleanup |
| L4 | Only a separately triggered plan-specific requirement | Manual packaged GUI, migration recovery, stress or SQLPERF A/B repeated benchmarks | Repeating L4 benchmark for ordinary feature development |

"One final run" means one planned **acceptance invocation** at a frozen revision. L3 must be rerun for the affected complete gate if it fails or a code/test/theory row, SQL/migration, inventory, runner/harness, build or CI input relevant to the gate changes afterward. Never downgrade acceptance or mark blocked gates green.

Each task gate must declare: project and TFM, test selection rule and why it is complete for the task, real-SQL dependency and category, cross-module impacts, prerequisite build, exact existing executable command, timeout/watchdog, observed pass/fail/skip, failure cleanup, evidence artifact and outstanding manual check. No automatic fallback from absent SQL approval to skipped mandatory tests.

## P4.1 worked mapping (future edits; existing task status stays unchanged)

| Task | L1/L2 minimum scope | SQL when needed | L3? |
|---|---|---|---|
| P4.1-T01 Session/actor snapshot | Application + session/dispatcher focused unit tests for both relevant TFMs | Focused `SecurityProvisioning` only if SQL-backed validator/identity boundary changes | No |
| P4.1-T02 LoginForm/ViewModel | Focused VM tests, GUI STA tests `net48`/`net8.0-windows` | None for pure UI behavior | No |
| P4.1-T03 Startup/auth gate | Focused host/VM/STA tests, fail-closed startup | Focused `SecurityProvisioning` + additional affected category for SQL identity/account validation | No |
| P4.1-T04 Logout/expiry/races | Fake clock/session/dispatcher race tests + relevant STA | Focused real SQL for account revocation/permission semantics; concurrency category if real multi-session behavior is affected | No |
| P4.1-T05 Verification/exit | Run missing focused/packaged/manual requirements and report exact evidence | Strict unfiltered SQL on both TFMs only at final L3 boundary; benchmark repetition only if separately required | **Final only** |

These are selection rules, not a claim that every named test exists. Do not rerun P4.1's already verified historical L3 solely for this docs PR. Continue unresolved real-user expiry/relogin L4 acceptance separately.

## Proposed target assemblies (subject to source inventory)

- `MyDmsVn.Server.Application.Tests`, `MyDmsVn.Architecture.Tests`: keep existing pure tests and normal safe parallelism.
- `MyDmsVn.Desktop.Session.Tests`: pure session/dispatcher/ViewModel logic, no UI handles or SQL.
- `MyDmsVn.Desktop.WinForms.Tests`: GUI-interacting STA tests, serialized, dual Windows TFMs.
- `MyDmsVn.SqlTests.Foundation`: DAL/UoW/SQL harness integration.
- `MyDmsVn.SqlTests.Security`: login, identity, grants/denies, provisioning.
- `MyDmsVn.SqlTests.Catalog`: master data SQL CRUD/mapping.
- `MyDmsVn.SqlTests.Inventory`: receipt, stock, posting, real concurrency.
- `MyDmsVn.SqlTests.Migrations`: DbUp, schema drift, migration history/upgrade and failure recovery.
- `MyDmsVn.SqlTestHarness` and `MyDmsVn.SqlTestRunner`: shared ownership/approval/runner infrastructure rather than duplicating SQL lifecycle code.

This is an **illustrative target structure**, not a request to create empty shell assemblies. Boundaries follow the actual test inventory, and cross-module tests may remain in a specifically documented integration project. A test project is a build/runtime boundary; module and category remain orthogonal metadata. Do not use an assembly name as proof that its tests do not touch SQL.

## Tasks / reviewable PR slices

### TESTGATE-T01 — Immediate Codex discipline and task gate contracts (documentation PR)

- [ ] Update `AGENTS.md`, policy, strategy and tests README to mandate L1/L2 focused execution, an explicit per-task matrix, no full SQL/solution suite during normal inner loops, and a frozen-revision L3 boundary.
- [ ] Attach an actual task-level selection matrix to P4.1 without changing any previously recorded test evidence/status. For new plans, require the same contract at plan creation.
- [ ] Propose ADR 0006 and record what scripts/projects **are not yet implemented**. Preserve ADR 0005. Add command examples using only existing runner flags.
- [ ] Static verify links, filenames and policy/ADR/plan consistency. As docs-only change, record SQL/GUI as **not run**.

**Exit:** a Codex agent can determine which existing commands to run after each task without inventing tools or repeating unrelated full SQL; no reduced release acceptance.

### TESTGATE-T02 — Strict one-pass SQL acceptance runner (code PR; highest priority)

- [ ] Design `pr-acceptance` runner mode (name subject to implementation review), distinct from `baseline` measurements. Ensure one sequential full unfiltered SQL pass per required TFM; no warm-up or measured repetitions.
- [ ] Reuse, not copy/bypass, existing source + compiled classification/exemption verification, independent discovery and theory rows, source-method/case/TRX matching, mandatory zero skips, minimum/expected case reconciliation, real process exit code and per-TFM artifacts.
- [ ] Before any test DDL, validate the actual approved non-production SQL instance, effective `master`, attestation/expiration/DMV permissions. Maintain test harness revalidation before create/drop even when called outside the runner.
- [ ] Retain owned process-tree watchdog, bounded cancellation/drain, durable create-intent/marker journal, live instance/ownership reconciliation, quarantining ambiguous DBs rather than dropping, and sanitized machine-readable report per commit/TFM.
- [ ] Fail closed for missing/duplicate/unrecognized source/cases, unexpectedly skipped SQL tests, stale exemption/classification, partial TRX, wrong server/approval, timeout, child process leftovers, incomplete journal or leaked DBs.
- [ ] Add runner unit/negative tests for all above and an approved disposable-instance two-TFM real SQL acceptance run. Independently compare manifest/source/discovery/execution totals and pass/fail/skip with the existing strict path.
- [ ] Prove `baseline` behavior has not changed, and no performance benchmark is run automatically. Document actual invocation **only after implemented**.

**Exit:** demonstrably equivalent L3 safety at one pass per TFM; no claim of acceptance if SQL approval or Windows is unavailable.

### TESTGATE-T03 — Version-controlled test-gate manifest and orchestration (code PR)

- [ ] Design a reviewed schema containing plan ID, task ID, project/TFM, module/category, exact case selection (or reviewed pattern), build prerequisites, required SQL/GUI resources, strict mode, evidence and timeout.
- [ ] Implement `Invoke-TestGate.ps1` (name proposed), reading a checked-in manifest, with `Task` and `Final` modes; fail closed on unknown IDs, empty/unexpected selection, missing TFMs, stale source inventory or unsupported options. Never translate a task gate into a full solution run implicitly.
- [ ] Make runner enforce one SQL process/TFM at a time across separate test projects, not merely per-assembly xUnit serialization. Define cancellation, lock lifetime, recovered crash handling and ownership semantics.
- [ ] Reconcile source/discovery/TRX globally across all eligible assemblies, including overlapping categories, theory rows and method-scoped exemptions.
- [ ] Capture sanitized per-task evidence (revision/project/TFM/filter/discovered/executed/passed/failed/skipped/time), final gate reuse invalidation and bounded-run diagnostics.
- [ ] Add CLI/parser/manifest negative tests for omitted task, category typo, duplicated identities across projects, missing-case/project, unsafe simultaneous resource use and absent approval.
- [ ] Have CI consume the **same** selection manifest and independently check it, not just trust what the local runner chose.

**Exit:** identical contract locally and in CI; no silent test omissions or concurrent SQL overload.

### TESTGATE-T04 — Separate pure session/VM and GUI test projects (code PR)

- [ ] Inventory every existing Desktop method/theory case; classify pure/no-SQL vs STA/control/global-state and maintain exact before/after identities/counts.
- [ ] Extract eligible pure tests without bringing `UseWindowsForms` or SQL initialization into the new project when avoidable; preserve both supported target runtimes and test behavior.
- [ ] Keep GUI-interacting tests serialized; retain STA, disposal, unhandled exception/DataError protection, modal dialog suppression and bounded worker cleanup.
- [ ] Update solution/project references and scripts, guard against duplicate execution, validate both `net48` and `net8.0-windows`.
- [ ] Only narrow Desktop assembly serialization if independently proven safe; retain an explicit serial fallback.

**Exit:** unchanged assertions + passing dual-runtime GUI and pure tests, no SQL dependency added to pure tests.

### TESTGATE-T05 — Incrementally partition SQL test assemblies (one or more code PRs)

- [ ] Snapshot all original SQL + reviewed non-SQL method identities, expanded theory cases, categories, migration audit call sites and existing exemption fingerprints on BOTH TFMs.
- [ ] Create shared harness without bypassing the existing approval and ownership guards. Move one coherent module at a time; preserve test-only embedded SQL scripts/resources/assembly metadata.
- [ ] Rebuild classification/exemption manifests per moved assembly and independent global aggregation; unknown cases/default SQL requirements fail closed.
- [ ] Include full scenarios for cross-module inventory and permission checks, migrations/drift, aborted/failed upgrades, transactions, race conditions and cleanup.
- [ ] Serialize SQL processes against the approved instance until cross-process/TFM resource coordination and bounded capacity have independent acceptance evidence.
- [ ] Require full **before/after** method + expanded-case reconciliation with no orphaned or duplicated test; test missing/duplicate assemblies and theory-row drift.
- [ ] Run focused new-project gates, then final two-TFM strict acceptance; record execution and resource/time delta without promising a speedup from project splitting alone.

**Exit:** every required old behavior is still executed exactly as intended and mandatory SQL coverage is complete across the new projects.

### TESTGATE-T06 — CI rollout, regressions and cost verification

- [ ] Enforce L2 manifest gates and L3 required status/checks without auto-invoking SQLPERF benchmarks on ordinary PRs; preserve dual-TFM/GUI/manual L4 requirements.
- [ ] Pin artifact identity to final gate-affecting revision (commit SHA plus inputs), invalidate stale evidence on relevant edits, permit explicit targeted repairs and complete gate re-runs when invalidated.
- [ ] Distinguish blocked, skipped, failed and not run. Ensure no green status from absence of SQL approval, headless interactive Login, missing Windows or partial gate.
- [ ] Capture inner-loop task cost and end-of-PR cost separately (elapsed time, SQL invocations, owned DB count, case coverage), compare against pre-change baseline without mixing benchmark medians with developer elapsed time.
- [ ] Update all future plan templates/guidance so each new feature has an executable task gate mapping and at most one *planned* final full acceptance boundary.

**Exit:** quality gate CI proves completeness and optimized developer cadence; no regression in existing SQLPERF safety evidence.

## Scope safeguards and blockers

- Production SQL schema, DbUp migration runner, RepoDb/Dapper behavior, permissions and authentication semantics are out of scope.
- No parallel SQL experiments or migrated fixture reuse without separate safety evidence and approved ADR 0005 gates.
- Never run destructive SQL against an unapproved instance. Keep `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` (`master`) and `MYDMSVN_SQL_TEST_APPROVAL_FILE` operator-managed; do not commit raw values.
- Work cannot be claimed as `passed` until supported Windows TFMs and approved SQL resource gates were actually executed. Docs-only T01 must not mark implementation T02–T06 complete.
- Existing P4.1 completed acceptance/checkmarks are preserved; future changes must establish whether historical evidence remains applicable to the final code revision.

## Future PR acceptance report template

```markdown
### Test Execution Report
- Revision / gate-affecting inputs:
- L1 edit-loop tests, project/TFM/filter/results:
- L2 task gates (task ID, module/category, project, pass/fail/skip, blockers):
- L3 one planned final acceptance attempt (or blocked; no fabricated single-pass mode):
- SQL independently reconciled inventory/discovery/theory rows/TRX:
- Watchdog, approval, journal/cleanup reconciliation:
- L4 explicit specialized/manual requirements:
- Exact SQL full-suite invocations and why repeats (if any) were necessary:
- Ready / NOT VERIFIED / BLOCKED:
```
