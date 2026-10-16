# TESTGATE — Task-scoped execution, strict single-pass PR gate, and modular test projects

**Status:** T01 policy/docs implemented in this PR; T02–T05 proposed / not implemented · **Date:** 2026-10-16 · **Scope:** cross-phase test infrastructure and Codex workflow. This is an implementation plan, **not** evidence that the runner or project split exists or has passed. Read `AGENTS.md`, `docs/CODEX_TEST_EXECUTION_POLICY.md`, `docs/TEST_STRATEGY.md`, ADR 0005, proposed ADR 0006, `tests/README.md`, SQLPERF-T04/T05/T06 verification, and the affected phase plan.

## Problem and constraints

P4.1 work demonstrates the operational cost of indiscriminately rerunning SQL tests. On the verified SQLPERF-T06 candidate, an unfiltered median run was 506.604 s (`net48`) and 498.631 s (`net8.0`); the benchmark performs one warm-up plus three measured runs on **each** TFM. These measurements are not an independently established breakdown of the P4.1 session's elapsed time.

Current baseline:
- `tests/MyDmsVn.Server.Infrastructure.IntegrationTests` targets `net48;net8.0` and serializes the SQL assembly. `tests/MyDmsVn.Desktop.Tests` targets `net48;net8.0-windows` and serializes GUI-interacting tests.
- `Invoke-FocusedSqlIntegrationTests.ps1` supports one existing SQL category per invocation (`FastSQL`, `MigrationSchema`, `SecurityProvisioning`, `SqlConcurrency`); it is NOT the L3 acceptance runner.
- `Invoke-SqlIntegrationBenchmark.ps1` currently invokes the strict multi-pass `baseline` mode; there is NO existing `--single-pass` PR mode and NO `Invoke-TestGate.ps1`.
- SQLPERF-T04 rejected fixture reuse under the then-available safety evidence. Do not reopen that decision without new adversarial tests.
- P4.1's existing L3 evidence and remaining L4 activities must not be overwritten or falsely reported as having been rerun by this documentation-only plan.

Objectives: minimize repeated SQL work **during task implementation**; retain full correctness, security, migration, both-TFM coverage and real-SQL isolation **at merge**; make actual and missing gates observable. **Execution is local-only for this plan:** Codex runs L1/L2/L3 on the approved developer machine, not again on CI. No new CI workflow, CI test-gate enforcement, CI SQL runner or duplicate full-suite acceptance execution is authorized. Revisit CI only through a separately requested plan.

## Gate definitions

| Level | When | Allowed scope | Forbidden substitution |
|---|---|---|---|
| L1 | On edit/fix | Affected test method/class, pure unit/contract/VM tests; approved focused real SQL only when necessary | Full unfiltered SQL/solution test as an automatic inner-loop action |
| L2 | Once per task after affected tests are green | Explicit affected project/TFMs + affected module/category + mandatory adversarial scenarios | Unrelated full SQL suite, or dropping a required real-SQL assertion |
| L3 | One planned **local** final attempt after all gate-affecting edits and L2 gates are green | Full required Release/dual-TFM regression, including **strict unfiltered** real SQL | Filter-only SQL, stale revision, incomplete TRX or missing inventory/cleanup |
| L4 | Only a separately triggered plan-specific requirement | Manual packaged GUI, migration recovery, stress or SQLPERF A/B repeated benchmarks | Repeating L4 benchmark for ordinary feature development |

"One final run" means one planned **acceptance invocation** at a frozen revision. L3 must be rerun for the affected complete gate if it fails or a code/test/theory row, SQL/migration, inventory, runner/harness, build input relevant to the gate changes afterward. Never downgrade acceptance or mark blocked gates green.

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

## Logical test-case identity and migration reconciliation (T03–T05 contract)

Project/assembly name and framework-generated VSTest/TRX GUID are **physical execution metadata**, not durable test identities. Before any split, define and version a deterministic logical-case key for each target framework:

```text
(TFM, normalized declaring type + method signature, Fact or Theory row key, SQL category or reviewed non-SQL classification)
```

- Normalize the method identity consistently across old/new projects (including overload signatures and generic arity when needed). For a Fact use a fixed `Fact` row marker. For a Theory use a deterministic row key from canonical, type-tagged argument representation (for example a SHA-256 fingerprint computed locally), or an explicitly assigned stable, opaque row ID when argument-based discovery is nondeterministic. Freeze and test the canonicalization version before comparing inventories. Do not commit or publish raw theory arguments, and do not rely on display names, array position, raw TRX GUIDs, assembly names or count equality as identity.
- Record the **old → new** project/assembly location separately from the logical key. If a move also changes the method signature, theory row identity, TFM or category, require an explicit reviewer-approved old → new **logical-key mapping and coverage rationale**; unapproved reclassification, collisions, missing rows or duplicate ownership fail closed. Module remains an independently reviewed business label, not inferred solely from the destination assembly.
- On **each TFM independently**, snapshot the source method inventory and independently discovered expanded cases **before** moving files, then reconcile source → discovery → executed TRX logical keys both before and after. Require one-to-one coverage/multiplicity (including method-scoped exemptions, skipped/blocked cases and all mandatory theory rows) plus a bijective before → after mapping. Reject dropped/added-unreviewed, duplicated, ambiguous or nondeterministic cases even if aggregate counts match. Preserve original local TRX for audit while only publishing aggregate sanitized reconciliation evidence.
- Implement these checks in T03/T04/T05 runner/inventory work; this documentation does **not** claim that a logical-key collector, row canonicalizer or cross-project inventory verifier already exists.

## TESTGATE's own task-level L2 gate matrix (T02–T05)

**Status: prospective contract, not execution evidence.** This matrix applies to the **TESTGATE implementation tasks themselves**, unlike the P4.1 worked example above. Command IDs R/H/D/S below refer only to tools and projects verified in the current repository. Their selectors must be rechecked against the actual source/discovery **at the task's revision**; add tests as the corresponding functionality is implemented and fail the task gate if required negative coverage is absent. The proposed \`Invoke-TestGate.ps1\`, \`pr-acceptance\` mode and new module/desktop test projects are **not** runnable yet.

| Task | Affected projects, TFMs and L2 selection | Real SQL trigger, required adverse cases and gate | Exit / evidence |
|---|---|---|---|
| **T02 — single-pass strict runner** | **R**: entire existing \`MyDmsVn.SqlTestRunner.Tests\` (\`net8.0\`); **H** (\`net48\`, \`net8.0\`) only if shared harness is changed. Keep baseline coordinator, inventory, discovery/TRX and watchdog tests in scope. | No routine full SQL at L2. If a changed SQL guard/approval/ownership path needs live validation, use **S** with the *affected, reviewed category* on \`net48\` and \`net8.0\`. Add negative runner tests for wrong/missing approval, stale/duplicate/missing inventory or theory rows, skipped mandatory cases, partial TRX, timeout/child-process leak, incomplete journal and ambiguous ownership; every failure must fail closed. The **one-pass unfiltered real SQL acceptance on both TFMs** belongs to this task PR's **final L3**, not another L2 repetition. | R (and conditional H/S) green with non-empty current selection; independently review negative-case inventory. L3 once at frozen revision must reconcile both complete TFMs and existing strict-path expectations. No code-bearing task accepted solely on R/H or a filtered S result. |
| **T03 — manifest + orchestration** | **R** (\`net8.0\`) for parser, manifest, identity and runner logic; **H** if ownership/preflight harness changed. Run new task-gate parser/selection tests in R after they exist; inspect source-discovered cases rather than trusting a filter alone. | **S** on \`net48\` and \`net8.0\` only when a change exercises real SQL ownership, cleanup or cross-process locking. Negative cases: unknown/omitted task, unrecognized category, empty selector, missing TFM/project, duplicate/ambiguous logical keys/theory rows, unsafe simultaneous runners, stale revision, absent approval, cancellation/crash and orphan-reconciliation. Prove cross-process SQL serialization in a bounded approved test where relevant; never infer it from xUnit assembly guards. | Real manifest and new orchestrator are mandatory for exit; until implemented, their CLI invocations are **BLOCKED / NOT IMPLEMENTED**, not commands to paste. Record negative tests and per-TFM source/discovery/TRX reconciliation; final global SQL regression only at L3. |
| **T04 — Desktop pure/GUI split** | **D** on the existing \`MyDmsVn.Desktop.Tests\` for \`net48\` and \`net8.0-windows\` *before* extraction. After moving tests, run the **actual created** pure/GUI project(s) on their verified TFMs plus the remaining old project, not a nonexistent target project. Keep GUI-interacting cases serialized. | Normally **no SQL** for pure VM/STA file moves. If SQL-backed identity/adapters/harness are altered, use **S \`SecurityProvisioning\`** on both SQL TFMs and any additional category justified by the changed behavior. Adverse checks: STA cleanup, unexpected dialogs/DataError, cancellation/timeout, global focus/input isolation and duplicate/missing or reclassified Fact/Theory rows. | Existing and created project targets pass; independent per-TFM before/after logical-case bijection and reviewed old→new mapping required. Missing Windows/TFM, new project not yet created or uncovered GUI cases = **BLOCKED**, not green. |
| **T05 — module SQL test split** | **R** (\`net8.0\`) for runner/global inventory changes; **H** on both TFMs when shared harness changes. **S** on the *original* integration project only while it still owns the selected cases; after relocation use T03's **verified implemented** guarded task runner and verified new project paths/TFMs. | Category per actual module/behavior: Catalog/Foundation CRUD → \`FastSQL\`; Inventory posting/locks → \`FastSQL\` + \`SqlConcurrency\` when affected; Security/permissions → \`SecurityProvisioning\`; DbUp/schema/history → \`MigrationSchema\` (also \`SecurityProvisioning\` if bootstrap affected). Run each required category separately and sequentially on both TFMs; include failure/rollback/timeout, migrations and cross-module permission/posting scenarios as applicable. A moved project must **not** be run through bare \`dotnet test\` as a substitute for the guarded SQL gate. | For each move, independently reconcile all old/new method and expanded Theory cases, exemptions and classifications on each SQL TFM; reject missing/duplicate/ambiguous cases and incomplete ownership cleanup. If T03's guarded cross-project selection is missing or a category unexpectedly discovers zero required cases, **BLOCKED**. One full unfiltered global SQL gate at final L3. |

### Existing command catalog for the matrix

Run these commands **from the repository root on Windows** at the actual task revision. The existing test projects are a supported way to exercise current logic, **not proof that new negative cases or new projects have already been implemented**. Do not use \`--no-build\` for runner changes without a successful corresponding build.

**R — Current SQL test-runner unit/negative suite (\`net8.0\`, no SQL DDL):**

\`\`\`powershell
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0
\`\`\`

The project is intentionally small and test-runner-specific. Its current build also builds the SQL integration project as a \`net8.0\` inventory fixture; that **build is not a real SQL execution**. For each T02/T03 task, compare the expected newly added failure-mode tests with source and actual discovery; a green pre-existing suite does not fulfill unimplemented acceptance cases.

**H — Shared SQL test-harness unit tests (only when the harness changes; no SQL DDL):**

\`\`\`powershell
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net48
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0
\`\`\`

**D — Current focused Desktop session/GUI coverage before project extraction:**

\`\`\`powershell
$desktopTests = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$desktopFilter = 'FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginViewModelTests|FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginFormTests|FullyQualifiedName~MyDmsVn.Desktop.Tests.DesktopSessionExpiryTests|FullyQualifiedName~MyDmsVn.Desktop.Tests.UiDispatcherTests'
dotnet test $desktopTests -c Release -f net48 --filter $desktopFilter
dotnet test $desktopTests -c Release -f net8.0-windows --filter $desktopFilter
\`\`\`

These four reviewed **existing class selectors** form an initial affected subset, not a complete new-project gate. Expand the focused selectors after inventorying the actual moved and GUI-dependent classes; after extraction record and run the new *verified* project paths with their real TFMs (no placeholder project is an executable command). Both GUI TFMs and the complete before/after theory-case inventory must still pass for T04 acceptance.

**S — Existing guarded focused real-SQL category (only after operator approval and build):**

Prerequisites: supported Windows/.NET tooling, \`MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING\` targeting effective \`master\` on a reviewed **non-production** instance, a current operator-managed \`MYDMSVN_SQL_TEST_APPROVAL_FILE\`, and fresh artifact storage. Missing/expired approval, unavailable Windows/SQL, required DMV permissions or any cleanup failure = **BLOCKED/FAILED**, never a skipped mandatory case or reported pass.

\`\`\`powershell
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release
dotnet build tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release
$category = 'FastSQL'  # Replace with the reviewed affected category for this task.
$artifactRoot = '.sqlperf/testgate-l2-' + [guid]::NewGuid().ToString('N')
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 \`
  -RepositoryRoot (Get-Location) \`
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj \`
  -ArtifactRoot $artifactRoot \`
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE \`
  -Category $category \`
  -Frameworks @('net48', 'net8.0')
\`\`\`

The existing wrapper accepts **one category**, not \`module + category\`; repeat **S** sequentially with a *new* artifact root for each genuinely affected category. This command is only valid while the source project still contains that category. Once tests move, the new guarded cross-project runner must be implemented/verified **before** using it; until then, mark that task gate blocked. Never invoke direct \`dotnet test\` for new SQL assemblies to bypass approval, process watchdog or ownership reconciliation.

**Gate accounting for each T02–T05 task:** Before L2, record reviewed source/module/dependency changes, selected class/category and why the scope is complete, project/TFM and exact actual command; after L2, record revision, discovered/executed/passed/failed/skipped counts (mandatory skips = 0), timeout/watchdog and ownership cleanup outcomes where applicable, source/inventory reconciliation, local artifact location (not public), and \`PASS / FAIL / BLOCKED / NOT RUN\` with reasons. New runners/tests cannot be claimed as executed until present and independently discovered. If a required negative test, new project, SQL approval or supported platform is absent, keep the task gate open; do not silently narrow coverage.

**Separate final L3 boundary:** Do **not** perform full unfiltered SQL after each T02–T05 task. For each code-bearing implementation PR, after all affected L2 tests and gate-affecting edits are frozen, invoke the **applicable complete** Release/dual-TFM regression with strict inventory/discovery/TRX, zero mandatory skips, approval, watchdog and positively owned database reconciliation. T02 must demonstrate the new one-pass mode on real SQL at this boundary; until that mode has separately passed review, use the existing strict multi-pass path or mark L3 blocked. At T05 include **all** old/new SQL test projects, not just migrated categories. A failure or later relevant edit invalidates the affected L3 evidence and requires a fresh applicable complete gate. Raw TRX/journals/timings remain local; publish only reviewed sanitized aggregate evidence. No TESTGATE CI rollout is authorized.

## Tasks / reviewable PR slices

### TESTGATE-T01 — Immediate Codex discipline and task gate contracts (documentation PR)

- [x] Update `AGENTS.md`, policy, strategy and tests README to mandate L1/L2 focused execution, an explicit per-task matrix, no full SQL/solution suite during normal inner loops, and a frozen-revision L3 boundary.
- [x] Attach an actual task-level selection matrix to P4.1 without changing any previously recorded test evidence/status. For new plans, require the same contract at plan creation.
- [x] Propose ADR 0006 and record what scripts/projects **are not yet implemented**. Preserve ADR 0005. Add command examples using only existing runner flags.
- [x] Static verify links, filenames and policy/ADR/plan consistency. As docs-only change, record SQL/GUI as **not run**.

**T01 evidence:** Repository paths and current P4.1 verified/partial status were inspected; documentation links and actual existing test project names were cross-checked. No runtime SQL/GUI suite was executed for this documentation-only change.

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
- [ ] Implement the versioned logical-case key and independently reconcile source/discovery/TRX globally on each TFM across all eligible assemblies, including overlapping categories, theory rows, method-scoped exemptions and explicit old/new project mappings. Reject ambiguous row identities and duplicate/missing cases; do not equate raw TRX GUIDs with durable identity.
- [ ] Capture sanitized per-task evidence (revision/project/TFM/filter/discovered/executed/passed/failed/skipped/time), final gate reuse invalidation and bounded-run diagnostics.
- [ ] Add CLI/parser/manifest negative tests for omitted task, category typo, duplicated identities across projects, missing-case/project, unsafe simultaneous resource use and absent approval.
- [ ] Independently validate the local manifest selection against source/discovery/expanded theory rows; never trust only the local runner's filtered test list. Do not add CI orchestration or CI test execution.

**Exit:** a reproducible, independently checked **local-only** task/final contract; no silent test omissions, concurrent SQL overload or duplicate CI full-suite run.

### TESTGATE-T04 — Separate pure session/VM and GUI test projects (code PR)

- [ ] Inventory every existing Desktop method/theory case per TFM; classify pure/no-SQL vs STA/control/global-state and capture normalized logical identities and old → new assembly mappings, not only before/after counts.
- [ ] Extract eligible pure tests without bringing `UseWindowsForms` or SQL initialization into the new project when avoidable; preserve both supported target runtimes and test behavior.
- [ ] Keep GUI-interacting tests serialized; retain STA, disposal, unhandled exception/DataError protection, modal dialog suppression and bounded worker cleanup.
- [ ] Update solution/project references and scripts, guard against duplicate execution, validate both `net48` and `net8.0-windows`.
- [ ] Require per-TFM source/discovery/TRX one-to-one logical-case reconciliation (including expanded theory rows, categories and reviewed renames); fail on duplicate/missing/ambiguous identities. Only narrow Desktop assembly serialization if independently proven safe; retain an explicit serial fallback.

**Exit:** unchanged assertions + passing dual-runtime GUI and pure tests, no SQL dependency added to pure tests.

### TESTGATE-T05 — Incrementally partition SQL test assemblies (one or more code PRs)

- [ ] Snapshot original SQL + reviewed non-SQL logical method/expanded-case identities, old assembly locations, categories, migration audit call sites and exemption fingerprints on BOTH TFMs using the versioned key defined above.
- [ ] Create shared harness without bypassing the existing approval and ownership guards. Move one coherent module at a time; preserve test-only embedded SQL scripts/resources/assembly metadata.
- [ ] Rebuild classification/exemption manifests per moved assembly and independent global aggregation; unknown cases/default SQL requirements fail closed.
- [ ] Include full scenarios for cross-module inventory and permission checks, migrations/drift, aborted/failed upgrades, transactions, race conditions and cleanup.
- [ ] Serialize SQL processes against the approved instance until cross-process/TFM resource coordination and bounded capacity have independent acceptance evidence.
- [ ] Require full **before/after** per-TFM source → discovery → TRX logical-case reconciliation and an explicit bijective old → new location/key mapping; negative-test missing/duplicate/ambiguous assemblies and theory rows, renames, categorization drift and non-deterministic row identities. Never accept raw TRX ID or aggregate-count parity alone.
- [ ] Run focused new-project gates, then final two-TFM strict acceptance; record execution and resource/time delta without promising a speedup from project splitting alone.

**Exit:** every required old behavior is still executed exactly as intended and mandatory SQL coverage is complete across the new projects.

## Scope safeguards and blockers

- **No CI rollout in TESTGATE-T01–T05.** Do not add/modify CI workflows or require CI to repeat local full SQL/desktop acceptance. Existing unrelated CI configuration is outside this plan; any future CI adoption requires a separate explicit user decision.

- Production SQL schema, DbUp migration runner, RepoDb/Dapper behavior, permissions and authentication semantics are out of scope.
- No parallel SQL experiments or migrated fixture reuse without separate safety evidence and approved ADR 0005 gates.
- Never run destructive SQL against an unapproved instance. Keep `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` (`master`) and `MYDMSVN_SQL_TEST_APPROVAL_FILE` operator-managed; do not commit raw values.
- Work cannot be claimed as `passed` until supported Windows TFMs and approved SQL resource gates were actually executed locally. Docs-only T01 must not mark implementation T02–T05 complete.
- Existing P4.1 completed acceptance/checkmarks are preserved; future changes must establish whether historical evidence remains applicable to the final code revision.

## Future PR acceptance report template

```markdown
### Test Execution Report
- Revision / gate-affecting inputs:
- L1 edit-loop tests, project/TFM/filter/results:
- L2 task gates (task ID, module/category, project, pass/fail/skip, blockers):
- L3 one planned final acceptance attempt (or blocked; no fabricated single-pass mode):
- SQL independently reconciled inventory/discovery/theory rows/TRX (sanitized aggregate, originals retained locally):
- Watchdog, approved-instance preflight result, journal/cleanup reconciliation (no raw identifiers):
- Public evidence sanitization review (no raw TRX, approval, ownership/timing journals or unreviewed ZIP):
- L4 explicit specialized/manual requirements:
- Exact SQL full-suite invocations and why repeats (if any) were necessary:
- Ready / NOT VERIFIED / BLOCKED:
```
